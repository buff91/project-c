#!/usr/bin/env python3
"""로컬 후보 리뷰 뷰어.

Slack 카드를 열거나 `output/review/.../raw.png`를 Finder로 찾아가지 않고,
브라우저 한 화면에서 최근 후보를 훑고 채택·거절·변형까지 끝낸다. 판정은
CLI와 같은 함수를 호출하므로(`ViewerActions`) 상태 머신이 둘로 갈라지지 않는다.
"""

from __future__ import annotations

import html
import json
import mimetypes
import re
import sqlite3
import threading
import webbrowser
from dataclasses import dataclass
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Callable
from urllib.parse import unquote, urlparse

from art_review import (
    ReviewError,
    ReviewStore,
    project_path,
)


DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 8787
DEFAULT_LIMIT = 24
MAX_IMAGE_BYTES = 32 * 1024 * 1024
LOCAL_HOSTNAMES = frozenset({"127.0.0.1", "localhost", "::1"})
CANDIDATE_ACTIONS = frozenset(
    {
        "approve",
        "reject",
        "variation",
        "prepare",
        "animation_draft",
    }
)
SHOT_ACTIONS = frozenset({"shot_approve", "shot_reject", "shot_variation"})
STATUS_LABELS = {
    "generated": "생성됨",
    "approved": "채택",
    "rejected": "거절",
    "prepared": "마감 준비",
    "published": "반영됨",
    "failed": "실패",
}
IMAGE_ROUTE = re.compile(
    r"^/image/(?P<candidate>[^/]+)"
    r"(?:/shot/(?P<shot>[^/]+))?"
    r"/(?P<kind>raw|preview)$"
)


class ViewerError(ReviewError):
    """뷰어 요청이 거절된 이유 — HTTP 400으로 나간다."""


@dataclass(frozen=True)
class ViewerActions:
    """판정 경로를 CLI와 공유하기 위한 어댑터.

    뷰어가 art_runner를 직접 import 하면 순환이 되고, 판정 로직을 복제하면
    Slack·CLI·뷰어가 서로 다른 상태를 남긴다. 호출자가 주입한다.
    """

    approve: Callable[[str], None]
    reject: Callable[[str], None]
    shot_decision: Callable[[str, str, str], None]
    enqueue: Callable[[str, str, dict[str, Any]], str]


@dataclass(frozen=True)
class ShotView:
    id: str
    label: str
    decision: str | None
    has_preview: bool


@dataclass(frozen=True)
class CandidateView:
    index: int
    id: str
    status: str
    seed: int
    recipe_id: str
    job_id: str
    job_notes: str
    version: str
    has_image: bool
    shots: tuple[ShotView, ...]

    @property
    def status_label(self) -> str:
        return STATUS_LABELS.get(self.status, self.status)


def candidate_dir(store: ReviewStore, candidate_id: str) -> Path:
    candidate = store.get_candidate(candidate_id)
    return project_path(candidate["raw_path"]).parent


def load_shot_views(
    store: ReviewStore,
    candidate: sqlite3.Row,
) -> tuple[ShotView, ...]:
    """멀티샷 후보의 샷 목록. 매니페스트가 없으면 단일 컷으로 다룬다."""
    manifest_path = (
        project_path(candidate["raw_path"]).parent / "shot-manifest.json"
    )
    if not manifest_path.is_file():
        return ()
    try:
        document = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return ()
    shots = document.get("shots")
    if not isinstance(shots, list):
        return ()
    views: list[ShotView] = []
    for shot in shots:
        if not isinstance(shot, dict) or "id" not in shot:
            continue
        shot_id = str(shot["id"])
        views.append(
            ShotView(
                id=shot_id,
                label=str(shot.get("label") or shot_id),
                decision=store.shot_decision(candidate["id"], shot_id),
                has_preview=bool(shot.get("game_preview_path")),
            )
        )
    return tuple(views)


def build_index(
    store: ReviewStore,
    *,
    limit: int = DEFAULT_LIMIT,
    recipe_id: str | None = None,
    status: str | None = None,
) -> tuple[CandidateView, ...]:
    rows = store.list_recent_candidates(
        limit,
        recipe_id=recipe_id,
        status=status,
    )
    views: list[CandidateView] = []
    for index, row in enumerate(rows, start=1):
        raw_path = project_path(row["raw_path"])
        views.append(
            CandidateView(
                index=index,
                id=str(row["id"]),
                status=str(row["status"]),
                seed=int(row["seed"]),
                recipe_id=str(row["recipe_id"]),
                job_id=str(row["job_id"]),
                job_notes=str(row["job_notes"] or ""),
                version=str(row["updated_at"]),
                has_image=raw_path.is_file(),
                shots=load_shot_views(store, row),
            )
        )
    return tuple(views)


def image_path(
    store: ReviewStore,
    candidate_id: str,
    kind: str,
    shot_id: str | None = None,
) -> Path:
    """요청 경로가 아니라 DB·매니페스트에서 파일을 되찾는다.

    브라우저는 후보 ID와 샷 ID만 넘기고 파일명은 서버가 정한다. 그래도 샷 ID가
    경로 조각이므로, 결과가 그 후보 폴더 밖을 가리키면 거절한다.
    """
    directory = candidate_dir(store, candidate_id)
    if shot_id is None:
        if kind != "raw":
            raise ViewerError(f"Unknown image kind {kind!r}")
        target = directory / "raw.png"
    else:
        if "/" in shot_id or "\\" in shot_id or shot_id in {"", ".", ".."}:
            raise ViewerError(f"Unsafe shot id {shot_id!r}")
        name = "raw.png" if kind == "raw" else "game-preview.png"
        target = directory / "shots" / shot_id / name
    resolved = target.resolve()
    if not resolved.is_relative_to(directory.resolve()):
        raise ViewerError(f"Image escapes the candidate folder: {resolved}")
    if not resolved.is_file():
        raise ViewerError(f"Image is missing: {resolved}")
    return resolved


def _count_argument(payload: dict[str, Any], default: int) -> int:
    try:
        count = int(payload.get("count", default))
    except (TypeError, ValueError) as exc:
        raise ViewerError("count must be an integer") from exc
    if not 1 <= count <= 12:
        raise ViewerError("count must be in 1..12")
    return count


def _timing_scale(payload: dict[str, Any]) -> float:
    try:
        scale = float(payload.get("timing_scale", 1.0))
    except (TypeError, ValueError) as exc:
        raise ViewerError("timing_scale must be a number") from exc
    if not 0.5 <= scale <= 2.0:
        raise ViewerError("timing_scale must be in 0.5..2.0")
    return scale


def dispatch_action(
    store: ReviewStore,
    actions: ViewerActions,
    payload: dict[str, Any],
) -> dict[str, Any]:
    """버튼 한 번 = CLI 명령 한 번. 새 판정 규칙을 여기서 만들지 않는다."""
    action = str(payload.get("action") or "")
    candidate_id = str(payload.get("candidate_id") or "").strip()
    if not candidate_id:
        raise ViewerError("candidate_id is required")
    if action not in CANDIDATE_ACTIONS | SHOT_ACTIONS:
        raise ViewerError(f"Unknown action {action!r}")
    store.get_candidate(candidate_id)

    queued: str | None = None
    if action == "approve":
        actions.approve(candidate_id)
    elif action == "reject":
        actions.reject(candidate_id)
    elif action == "variation":
        queued = actions.enqueue(
            "variation",
            candidate_id,
            {"count": _count_argument(payload, 4), "notes": "viewer"},
        )
    elif action == "prepare":
        queued = actions.enqueue("prepare", candidate_id, {})
    elif action == "animation_draft":
        queued = actions.enqueue(
            "animation_draft",
            candidate_id,
            {"timing_scale": _timing_scale(payload)},
        )
    else:
        shot_id = str(payload.get("shot_id") or "").strip()
        if not shot_id:
            raise ViewerError("shot_id is required")
        if action == "shot_variation":
            queued = actions.enqueue(
                "shot_variation",
                candidate_id,
                {
                    "count": _count_argument(payload, 2),
                    "notes": "viewer",
                    "shot_id": shot_id,
                },
            )
        else:
            decision = "approve" if action == "shot_approve" else "reject"
            actions.shot_decision(candidate_id, shot_id, decision)

    candidate = store.get_candidate(candidate_id)
    return {
        "candidate_id": candidate_id,
        "action": action,
        "status": candidate["status"],
        "status_label": STATUS_LABELS.get(
            candidate["status"],
            candidate["status"],
        ),
        "queued_action_id": queued,
        "shot_id": payload.get("shot_id"),
    }


def host_is_local(host_header: str | None, port: int) -> bool:
    """DNS 리바인딩으로 남의 페이지가 이 서버를 조작하지 못하게 막는다."""
    if not host_header:
        return False
    host = host_header.strip()
    if host.startswith("["):
        hostname, _, tail = host[1:].partition("]")
        port_text = tail[1:] if tail.startswith(":") else ""
    else:
        head, separator, tail = host.rpartition(":")
        if separator and tail.isdigit():
            hostname, port_text = head, tail
        else:
            hostname, port_text = host, ""
    if hostname.lower() not in LOCAL_HOSTNAMES:
        return False
    return not port_text or port_text == str(port)


def render_shot(candidate: CandidateView, shot: ShotView) -> str:
    decision = {"approve": "채택", "reject": "거절"}.get(shot.decision or "")
    mark = f"<em>{html.escape(decision)}</em>" if decision else ""
    base = f"/image/{html.escape(candidate.id)}/shot/{html.escape(shot.id)}"
    preview = (
        f'<img loading="lazy" src="{base}/preview?v='
        f'{html.escape(candidate.version)}" alt="게임 스케일 프리뷰">'
        if shot.has_preview
        else ""
    )
    buttons = "".join(
        f'<button data-action="{action}" data-shot="{html.escape(shot.id)}">'
        f"{label}</button>"
        for action, label in (
            ("shot_approve", "샷 채택"),
            ("shot_reject", "샷 거절"),
            ("shot_variation", "변형 2장"),
        )
    )
    return (
        f'<li class="shot"><div class="shot-head">'
        f"<span>{html.escape(shot.label)}</span>{mark}</div>"
        f'<div class="shot-images">'
        f'<img loading="lazy" src="{base}/raw?v='
        f'{html.escape(candidate.version)}" alt="{html.escape(shot.label)}">'
        f"{preview}</div>"
        f'<div class="row">{buttons}</div></li>'
    )


def render_card(candidate: CandidateView) -> str:
    image = (
        f'<img loading="lazy" src="/image/{html.escape(candidate.id)}/raw?v='
        f'{html.escape(candidate.version)}" alt="{html.escape(candidate.id)}">'
        if candidate.has_image
        else '<p class="missing">원본 PNG가 없다</p>'
    )
    shots = (
        f'<ul class="shots">'
        + "".join(render_shot(candidate, shot) for shot in candidate.shots)
        + "</ul>"
        if candidate.shots
        else ""
    )
    notes = (
        f'<p class="notes">{html.escape(candidate.job_notes)}</p>'
        if candidate.job_notes
        else ""
    )
    buttons = "".join(
        f'<button data-action="{action}">{label}</button>'
        for action, label in (
            ("approve", "채택"),
            ("reject", "거절"),
            ("variation", "변형 4장"),
            ("prepare", "Aseprite 준비"),
            ("animation_draft", "애니 초안"),
        )
    )
    return (
        f'<article class="card" data-candidate="{html.escape(candidate.id)}"'
        f' data-status="{html.escape(candidate.status)}">'
        f'<header><span class="ordinal">^{candidate.index}</span>'
        f'<code>{html.escape(candidate.id)}</code>'
        f'<span class="pill">{html.escape(candidate.status_label)}</span>'
        f"</header>"
        f'<p class="meta">{html.escape(candidate.recipe_id)} · seed '
        f"{candidate.seed}</p>"
        f"{notes}"
        f'<div class="hero">{image}</div>'
        f'<div class="row">{buttons}</div>'
        f"{shots}"
        f'<p class="result"></p></article>'
    )


def render_page(candidates: tuple[CandidateView, ...]) -> str:
    cards = "".join(render_card(candidate) for candidate in candidates)
    if not cards:
        cards = '<p class="empty">아직 리뷰할 후보가 없다.</p>'
    return PAGE_TEMPLATE.replace("__COUNT__", str(len(candidates))).replace(
        "__CARDS__", cards
    )


PAGE_TEMPLATE = """<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<title>Project-C 아트 리뷰</title>
<style>
:root { color-scheme: dark; }
body { margin: 0; padding: 24px; background: #0c1218; color: #e6ebee;
  font: 14px/1.5 -apple-system, "Apple SD Gothic Neo", sans-serif; }
h1 { font-size: 18px; margin: 0 0 4px; }
.hint { color: #8fa3b0; margin: 0 0 20px; }
.hint code { color: #cfe3f0; }
.grid { display: grid; gap: 16px;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr)); }
.card { background: #141d26; border: 1px solid #223243; border-radius: 10px;
  padding: 12px; }
.card[data-status="approved"] { border-color: #2f7d4f; }
.card[data-status="rejected"] { opacity: 0.55; }
header { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.ordinal { color: #7fd1ff; font-weight: 700; }
code { font-size: 12px; color: #cfe3f0; }
.pill { margin-left: auto; background: #223243; border-radius: 999px;
  padding: 2px 10px; font-size: 12px; }
.meta, .notes { color: #8fa3b0; font-size: 12px; margin: 6px 0 0; }
.hero { margin: 10px 0; background: #0a0f14; border-radius: 6px;
  display: flex; justify-content: center; }
.hero img { max-width: 100%; height: auto; image-rendering: pixelated; }
.row { display: flex; gap: 6px; flex-wrap: wrap; }
button { background: #1d2a37; color: #e6ebee; border: 1px solid #2c3e50;
  border-radius: 6px; padding: 6px 10px; font-size: 12px; cursor: pointer; }
button:hover { background: #26364a; }
button[disabled] { opacity: 0.5; cursor: progress; }
.shots { list-style: none; margin: 12px 0 0; padding: 0;
  display: grid; gap: 10px; }
.shot { border-top: 1px solid #223243; padding-top: 10px; }
.shot-head { display: flex; gap: 8px; font-size: 12px; color: #b9c8d2; }
.shot-head em { color: #7fd1ff; font-style: normal; }
.shot-images { display: flex; gap: 8px; margin: 6px 0; }
.shot-images img { max-width: 45%; height: auto; image-rendering: pixelated; }
.result { min-height: 16px; margin: 8px 0 0; font-size: 12px;
  color: #7fd1ff; }
.result.error { color: #ff8f8f; }
.missing, .empty { color: #8fa3b0; }
</style>
</head>
<body>
<h1>Project-C 아트 리뷰 — 최근 후보 __COUNT__건</h1>
<p class="hint">카드의 <code>^N</code>은 CLI 별칭과 같은 번호다 —
<code>art_runner.py approve ^2</code>처럼 쓴다. 판정은 즉시 같은 DB에 남고,
변형·준비·애니 초안은 워커 큐로 들어간다.</p>
<div class="grid">__CARDS__</div>
<script>
document.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) return;
  const card = button.closest(".card");
  const result = card.querySelector(".result");
  const body = {
    candidate_id: card.dataset.candidate,
    action: button.dataset.action,
  };
  if (button.dataset.shot) body.shot_id = button.dataset.shot;
  button.disabled = true;
  result.classList.remove("error");
  result.textContent = "처리 중…";
  try {
    const response = await fetch("/action", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || response.statusText);
    card.dataset.status = data.status;
    card.querySelector(".pill").textContent = data.status_label;
    result.textContent = data.queued_action_id
      ? `큐 등록: ${data.queued_action_id}`
      : `${button.textContent} 완료`;
  } catch (error) {
    result.classList.add("error");
    result.textContent = String(error.message || error);
  } finally {
    button.disabled = false;
  }
});
</script>
</body>
</html>
"""


def make_handler(
    store: ReviewStore,
    actions: ViewerActions,
    *,
    limit: int,
    port: int,
) -> type[BaseHTTPRequestHandler]:
    lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        server_version = "ProjectCArtViewer/1"

        def log_message(self, *_args: Any) -> None:  # noqa: D102
            return

        def _guard(self) -> bool:
            if host_is_local(self.headers.get("Host"), port):
                return True
            self._send_json(
                HTTPStatus.FORBIDDEN,
                {"error": "This viewer only answers localhost"},
            )
            return False

        def _send_json(self, status: HTTPStatus, payload: Any) -> None:
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _send_bytes(
            self,
            body: bytes,
            content_type: str,
            *,
            status: HTTPStatus = HTTPStatus.OK,
        ) -> None:
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self) -> None:  # noqa: N802
            if not self._guard():
                return
            path = urlparse(self.path).path
            if path in {"/", "/index.html"}:
                with lock:
                    page = render_page(build_index(store, limit=limit))
                self._send_bytes(
                    page.encode("utf-8"),
                    "text/html; charset=utf-8",
                )
                return
            match = IMAGE_ROUTE.match(path)
            if match is None:
                self._send_json(HTTPStatus.NOT_FOUND, {"error": "Not found"})
                return
            try:
                target = image_path(
                    store,
                    unquote(match.group("candidate")),
                    match.group("kind"),
                    (
                        unquote(match.group("shot"))
                        if match.group("shot")
                        else None
                    ),
                )
                if target.stat().st_size > MAX_IMAGE_BYTES:
                    raise ViewerError(f"Image is too large: {target}")
                body = target.read_bytes()
            except (ReviewError, OSError) as exc:
                self._send_json(HTTPStatus.NOT_FOUND, {"error": str(exc)})
                return
            content_type = (
                mimetypes.guess_type(target.name)[0]
                or "application/octet-stream"
            )
            self._send_bytes(body, content_type)

        def do_POST(self) -> None:  # noqa: N802
            if not self._guard():
                return
            if urlparse(self.path).path != "/action":
                self._send_json(HTTPStatus.NOT_FOUND, {"error": "Not found"})
                return
            try:
                length = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                length = 0
            if length <= 0 or length > 64 * 1024:
                self._send_json(
                    HTTPStatus.BAD_REQUEST,
                    {"error": "Action body is missing or too large"},
                )
                return
            try:
                payload = json.loads(self.rfile.read(length).decode("utf-8"))
                if not isinstance(payload, dict):
                    raise ViewerError("Action body must be an object")
                with lock:
                    result = dispatch_action(store, actions, payload)
            except (ReviewError, ValueError, OSError) as exc:
                self._send_json(HTTPStatus.BAD_REQUEST, {"error": str(exc)})
                return
            self._send_json(HTTPStatus.OK, result)

    return Handler


def serve(
    store: ReviewStore,
    actions: ViewerActions,
    *,
    host: str = DEFAULT_HOST,
    port: int = DEFAULT_PORT,
    limit: int = DEFAULT_LIMIT,
    open_browser: bool = True,
) -> None:
    handler = make_handler(store, actions, limit=limit, port=port)
    with ThreadingHTTPServer((host, port), handler) as httpd:
        url = f"http://{host}:{httpd.server_address[1]}/"
        print(f"아트 리뷰 뷰어: {url}  (Ctrl+C 로 종료)")
        if open_browser:
            webbrowser.open(url)
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print()
