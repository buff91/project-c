#!/usr/bin/env python3
"""Slack Socket Mode review UI for the Project-C art pipeline."""

from __future__ import annotations

import argparse
import json
import os
import re
import signal
import sys
import threading
import time
import traceback
import uuid
from pathlib import Path
from typing import Any, Callable

from art_review import (
    BatchRegistry,
    DEFAULT_BATCH_DIR,
    DEFAULT_DB_PATH,
    DEFAULT_OUTPUT_ROOT,
    DEFAULT_RECIPE_DIR,
    Recipe,
    RecipeRegistry,
    ReviewError,
    ReviewStore,
    project_path,
    recipe_from_job,
)
from art_runner import (
    DEFAULT_COMFY_URL,
    approve_candidate,
    decide_candidate_shot,
    load_shot_manifest,
    reject_candidate,
    resolve_batch_jobs,
    work_once,
)


REACTION_LABELS = {
    "+1": "style-fit",
    "thumbsup": "style-fit",
    "-1": "reject",
    "thumbsdown": "reject",
    "art": "palette",
    "bone": "anatomy",
    "soap": "cleanup",
    "triangular_ruler": "scale-pivot",
    "arrows_counterclockwise": "variation",
}
CANDIDATE_PATTERN = re.compile(r"\bC\d{2}\b", re.IGNORECASE)
OUTBOX_MAX_ATTEMPTS = 5
STOP_EVENT = threading.Event()


def log_error(context: str) -> None:
    print(f"error: {context}", file=sys.stderr)
    traceback.print_exc()


ART_CATEGORY_LABELS = {
    "actor": "캐릭터",
    "effect": "전투 이펙트",
    "environment": "환경",
    "item": "아이템",
    "prop": "소품",
    "ui": "UI",
}

JOB_STATE_LABELS = {
    "queued": "대기 중",
    "running": "생성 중",
    "awaiting_review": "검토 대기",
    "failed": "실패",
    "complete": "완료",
    "cancelled": "취소됨",
}

CANDIDATE_STATE_VIEW = {
    "generated": (
        "🟡",
        "검토 대기",
        "이미지를 보고 채택·제외·비슷한 변형 중 하나를 선택하세요.",
    ),
    "approved": (
        "✅",
        "채택·보관됨",
        "원본 스냅샷이 보관되었습니다. 게임 반영은 Spark에 별도로 요청하세요.",
    ),
    "rejected": (
        "⚫",
        "제외됨",
        "이 후보는 더 이상 진행하지 않습니다.",
    ),
    "preparing": (
        "⏳",
        "Aseprite 준비 중",
        "배경 제거·캔버스·팔레트를 정리하고 있습니다.",
    ),
    "prepared": (
        "🧹",
        "Aseprite 준비 완료",
        "스레드의 마감 미리보기를 확인하세요.",
    ),
    "publishing": (
        "⏳",
        "정식 반영 중",
        "승인된 Aseprite 원본을 프로젝트 슬롯에 저장하고 있습니다.",
    ),
    "published": (
        "🚀",
        "프로젝트 반영 완료",
        "Unity 동기화와 플레이 화면 검증을 진행하세요.",
    ),
    "failed": (
        "❌",
        "작업 실패",
        "스레드의 오류 내용을 확인한 뒤 다시 요청하세요.",
    ),
}


def category_label(recipe: Recipe) -> str:
    category = str(recipe.purpose.get("category", "art"))
    return ART_CATEGORY_LABELS.get(category, category)


def job_state_label(state: str) -> str:
    return JOB_STATE_LABELS.get(state, state)


def candidate_state_view(state: str) -> tuple[str, str, str]:
    return CANDIDATE_STATE_VIEW.get(
        state,
        ("ℹ️", state, "상태를 확인하세요."),
    )


def slack_help_text() -> str:
    return (
        "*생성·조회*\n"
        "• `/art new` — 생성 폼 열기\n"
        "• `/art recipes` · `/art recipe <recipe-id>`\n"
        "• `/art run <recipe-id> [count]` — 레시피 전체 생성\n"
        "• `/art shot <recipe-id> <shot-id> [count]` — 한 샷만 시험 생성\n"
        "• `/art batches` · `/art batch <batch-id>` — 다용도 묶음 조회·실행\n"
        "• `/art queue` · `/art cancel <job-id>` · `/art retry <job-id>`\n\n"
        "*후보 전체*\n"
        "• `/art approve <candidate-id>` · `/art reject <candidate-id>`\n"
        "• `/art variation <candidate-id> [count]`\n"
        "• `/art prepare <candidate-id>` — Aseprite 마감본 준비\n"
        "• `/art animation <candidate-id> [timing-scale]`\n"
        "• `/art apply <candidate-id> confirm` — Spark 게임 반영 요청\n"
        "• `/art applies` — 반영 요청 상태\n\n"
        "*멀티샷의 한 샷*\n"
        "• `/art shot-approve <candidate-id> <shot-id>`\n"
        "• `/art shot-reject <candidate-id> <shot-id>`\n"
        "• `/art shot-variation <candidate-id> <shot-id> [count]`\n\n"
        "후보/샷 카드의 버튼으로도 같은 작업을 실행할 수 있습니다. "
        "자세한 순서는 `docs/art-direction/ART_REVIEW_AUTOMATION.md`를 보세요."
    )


def parse_bounded_int(
    value: str,
    *,
    name: str,
    minimum: int,
    maximum: int,
) -> int:
    try:
        parsed = int(value)
    except ValueError as exc:
        raise ReviewError(f"{name}은 정수여야 합니다.") from exc
    if not minimum <= parsed <= maximum:
        raise ReviewError(
            f"{name}은 {minimum}~{maximum} 사이여야 합니다."
        )
    return parsed


def load_env_file(path: Path | None) -> None:
    if path is None or not path.is_file():
        return
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        name, value = line.split("=", 1)
        name = name.strip()
        if name and name not in os.environ:
            os.environ[name] = value.strip().strip('"').strip("'")


def require_env(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise ReviewError(f"Missing environment variable {name}")
    return value


def allowed_user(user_id: str) -> bool:
    configured = {
        value.strip()
        for value in os.environ.get(
            "SLACK_ART_ALLOWED_USERS",
            "",
        ).split(",")
        if value.strip()
    }
    return bool(user_id) and user_id in configured


def require_allowed(user_id: str) -> None:
    if not allowed_user(user_id):
        raise ReviewError(
            f"Slack user {user_id} is not allowed "
            "(SLACK_ART_ALLOWED_USERS)"
        )


def truncate(text: str, limit: int) -> str:
    return text if len(text) <= limit else f"{text[:limit - 1]}…"


def lora_summary(recipe: Recipe) -> str:
    values = []
    for lora in recipe.document.get("loras", []):
        values.append(
            f"`{lora['name']}` "
            f"M {lora['model_strength']} / C {lora['clip_strength']}"
        )
    return "\n".join(values) if values else "없음"


def supports_animation_draft(recipe: Recipe) -> bool:
    category = recipe.purpose.get("category")
    return recipe.is_multi_shot and (
        category == "effect"
        or (
            category == "actor"
            and bool(recipe.animation.get("draft", {}).get("clips"))
        )
    )


def candidate_blocks(
    recipe: Recipe,
    candidate: Any,
    *,
    status: str | None = None,
    approved: bool = False,
) -> list[dict[str, Any]]:
    generation = recipe.generation
    state = status or candidate["status"]
    state_icon, state_label, next_action = candidate_state_view(state)
    shot_summary = (
        f" · 샷 {len(recipe.shots)}개"
        if recipe.is_multi_shot
        else ""
    )
    buttons: list[dict[str, Any]] = [
        {
            "type": "button",
            "text": {"type": "plain_text", "text": "✅ 채택"},
            "style": "primary",
            "action_id": "art_candidate_approve",
            "value": candidate["id"],
        },
        {
            "type": "button",
            "text": {"type": "plain_text", "text": "❌ 제외"},
            "action_id": "art_candidate_reject",
            "value": candidate["id"],
        },
        {
            "type": "button",
            "text": {"type": "plain_text", "text": "🔁 비슷하게 4개"},
            "action_id": "art_candidate_variation",
            "value": candidate["id"],
        },
        {
            "type": "button",
            "text": {
                "type": "plain_text",
                "text": (
                    "🧹 Aseprite 소스"
                    if recipe.is_multi_shot
                    else "🧹 Aseprite 준비"
                ),
            },
            "action_id": "art_candidate_prepare",
            "value": candidate["id"],
        },
    ]
    if supports_animation_draft(recipe):
        buttons.append(
            {
                "type": "button",
                "text": {"type": "plain_text", "text": "🎞 애니 초안"},
                "action_id": "art_candidate_animation",
                "value": candidate["id"],
            }
        )
    if (
        approved
        and state in {"approved", "prepared"}
    ):
        buttons.append(
            {
                "type": "button",
                "text": {"type": "plain_text", "text": "🚀 게임 반영 요청"},
                "style": "danger",
                "action_id": "art_candidate_apply",
                "value": candidate["id"],
                "confirm": {
                    "title": {
                        "type": "plain_text",
                        "text": "Spark에 게임 반영을 요청할까요?",
                    },
                    "text": {
                        "type": "mrkdwn",
                        "text": (
                            "지금 파일을 덮지 않습니다. Codex Spark가 실제 Unity 참조를 "
                            "조사해 교체 대상을 정하고, 모호하면 스레드로 다시 묻습니다."
                        ),
                    },
                    "confirm": {"type": "plain_text", "text": "요청"},
                    "deny": {"type": "plain_text", "text": "취소"},
                    "style": "danger",
                },
            }
        )
    blocks = [
        {
            "type": "header",
            "text": {
                "type": "plain_text",
                "text": truncate(
                    f"{state_icon} {state_label} · {recipe.name}",
                    150,
                ),
            },
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": (
                    f"*대상*  {category_label(recipe)} · `{recipe.slot}`"
                    f"{shot_summary}\n"
                    f"*후보*  `{candidate['id']}` · seed `{candidate['seed']}`\n\n"
                    f"*지금 할 일*\n{next_action}"
                ),
            },
        },
        {
            "type": "context",
            "elements": [
                {
                    "type": "mrkdwn",
                    "text": (
                        f"`{recipe.id}` · {generation['steps']} steps · "
                        f"CFG {generation['cfg']} · denoise "
                        f"{generation.get('denoise')} · "
                        "상세 설정은 `/art recipe "
                        f"{recipe.id}`"
                    ),
                }
            ],
        },
        {
            "type": "context",
            "elements": [
                {
                    "type": "mrkdwn",
                    "text": (
                        "빠른 평가  👍 좋음 · 👎 제외 · 🎨 색 · "
                        "🦴 형태 · 🧼 정리 · 📐 크기/피벗"
                    ),
                }
            ],
        },
    ]
    for index in range(0, len(buttons), 5):
        blocks.append(
            {
                "type": "actions",
                "block_id": (
                    f"candidate-{candidate['id']}-{state}-{index // 5}"
                ),
                "elements": buttons[index:index + 5],
            }
        )
    return blocks


def shot_action_value(candidate_id: str, shot_id: str) -> str:
    return json.dumps(
        {"candidate_id": candidate_id, "shot_id": shot_id},
        separators=(",", ":"),
    )


def parse_shot_action(action: dict[str, Any]) -> tuple[str, str]:
    try:
        payload = json.loads(action["value"])
        candidate_id = str(payload["candidate_id"])
        shot_id = str(payload["shot_id"])
    except (KeyError, TypeError, json.JSONDecodeError) as exc:
        raise ReviewError("Invalid Slack shot action payload") from exc
    if not candidate_id or not shot_id:
        raise ReviewError("Slack shot action target is empty")
    return candidate_id, shot_id


def animation_action_value(
    candidate_id: str,
    timing_scale: float,
) -> str:
    return json.dumps(
        {
            "candidate_id": candidate_id,
            "timing_scale": timing_scale,
        },
        separators=(",", ":"),
    )


def parse_animation_action(
    action: dict[str, Any],
) -> tuple[str, float]:
    try:
        payload = json.loads(action["value"])
        candidate_id = str(payload["candidate_id"])
        timing_scale = float(payload["timing_scale"])
    except (KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
        raise ReviewError("Invalid Slack animation action payload") from exc
    if not candidate_id or not 0.5 <= timing_scale <= 2.0:
        raise ReviewError("Slack animation action target is invalid")
    return candidate_id, timing_scale


def animation_timing_blocks(
    candidate_id: str,
    timing_scale: float,
) -> list[dict[str, Any]]:
    return [
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": (
                    f"*🎞 애니메이션 초안 준비 완료*\n"
                    f"현재 속도 `×{timing_scale:.2f}` · GIF를 재생해 보고 "
                    "아래에서 속도를 고르세요.\n"
                    "자세한 수정은 `[walk] 발이 뜬다`처럼 답장하면 됩니다."
                ),
            },
        },
        {
            "type": "actions",
            "block_id": f"animation-{candidate_id}-{timing_scale:.2f}",
            "elements": [
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "⏩ 빠르게"},
                    "action_id": "art_animation_timing_fast",
                    "value": animation_action_value(candidate_id, 0.85),
                },
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "▶️ 기본 속도"},
                    "style": "primary",
                    "action_id": "art_animation_timing_normal",
                    "value": animation_action_value(candidate_id, 1.0),
                },
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "🐢 느리게"},
                    "action_id": "art_animation_timing_slow",
                    "value": animation_action_value(candidate_id, 1.15),
                },
            ],
        },
    ]


def shot_blocks(
    recipe: Recipe,
    candidate: Any,
    shot: Any,
    *,
    decision: str | None = None,
) -> list[dict[str, Any]]:
    state = {
        "approve": "✅ 채택됨",
        "reject": "⚫ 제외됨",
    }.get(decision, "🟡 검토 대기")
    next_action = {
        "approve": "선택을 마쳤습니다. 다른 샷을 계속 검토하세요.",
        "reject": "필요하면 이 샷만 새 변형을 요청하세요.",
    }.get(
        decision,
        "이미지를 보고 채택·제외·이 샷만 변형 중 하나를 선택하세요.",
    )
    value = shot_action_value(candidate["id"], shot.id)
    return [
        {
            "type": "header",
            "text": {
                "type": "plain_text",
                "text": truncate(f"{state} · {shot.label}", 150),
            },
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": (
                    f"*샷*  `{shot.id}` · 슬롯 `{shot.slot or recipe.slot}`\n"
                    f"*규격*  "
                    f"`{(shot.output_canvas or recipe.canvas)[0]}×"
                    f"{(shot.output_canvas or recipe.canvas)[1]}`\n"
                    f"*지금 할 일*\n{next_action}\n\n"
                    f"수정 의견은 `[{shot.id}] 바꿀 내용`으로 답장하세요."
                ),
            },
        },
        {
            "type": "actions",
            "block_id": (
                f"shot-{candidate['id']}-{shot.id}-{decision or 'pending'}"
            ),
            "elements": [
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "✅ 채택"},
                    "style": "primary",
                    "action_id": "art_shot_approve",
                    "value": value,
                },
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "❌ 제외"},
                    "action_id": "art_shot_reject",
                    "value": value,
                },
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "🔁 이 샷만 2개"},
                    "action_id": "art_shot_variation",
                    "value": value,
                },
            ],
        },
    ]


def recipe_blocks(recipe: Recipe) -> list[dict[str, Any]]:
    generation = recipe.generation
    shot_summary = (
        "\n*포함 샷*  "
        + ", ".join(f"`{shot.id}`" for shot in recipe.shots)
        if recipe.is_multi_shot
        else ""
    )
    return [
        {
            "type": "header",
            "text": {
                "type": "plain_text",
                "text": truncate(f"🧪 레시피 · {recipe.name}", 150),
            },
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": (
                    f"*대상*  {category_label(recipe)} · `{recipe.slot}`\n"
                    f"*목표*  {recipe.purpose.get('readability_goal')}\n"
                    f"*출력*  {generation['width']}×{generation['height']} · "
                    f"{generation['steps']} steps · CFG {generation['cfg']} · "
                    f"denoise {generation.get('denoise')}"
                    f"{shot_summary}"
                ),
            },
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": truncate(
                    f"*긍정 프롬프트*\n{recipe.prompt['positive']}\n\n"
                    f"*제외 프롬프트*\n"
                    f"{recipe.prompt.get('negative', '')}",
                    2400,
                ),
            },
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": truncate(
                    f"*모델*  `{recipe.pipeline.get('checkpoint')}`\n"
                    f"*LoRA*\n{lora_summary(recipe)}",
                    1800,
                ),
            },
        },
        {
            "type": "context",
            "elements": [
                {
                    "type": "mrkdwn",
                    "text": truncate(
                        f"`{recipe.id}` · "
                        f"{recipe.pipeline.get('type')} · "
                        f"{generation.get('sampler')}/"
                        f"{generation.get('scheduler')}",
                        1900,
                    ),
                }
            ],
        },
        {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": "*다음 단계*\n설정을 확인한 뒤 기본 후보 묶음을 생성하세요.",
            },
        },
        {
            "type": "actions",
            "elements": [
                {
                    "type": "button",
                    "text": {"type": "plain_text", "text": "▶️ 기본 후보 생성"},
                    "style": "primary",
                    "action_id": "art_recipe_run",
                    "value": recipe.id,
                }
            ],
        },
    ]


def modal_view(registry: RecipeRegistry) -> dict[str, Any]:
    options = [
        {
            "text": {
                "type": "plain_text",
                "text": truncate(recipe.name, 75),
            },
            "value": recipe.id,
        }
        for recipe in registry.load_all().values()
    ][:100]
    return {
        "type": "modal",
        "callback_id": "art_new_job_modal",
        "title": {"type": "plain_text", "text": "아트 생성"},
        "submit": {"type": "plain_text", "text": "큐에 추가"},
        "close": {"type": "plain_text", "text": "취소"},
        "blocks": [
            {
                "type": "input",
                "block_id": "recipe",
                "label": {"type": "plain_text", "text": "레시피"},
                "element": {
                    "type": "static_select",
                    "action_id": "value",
                    "options": options,
                },
            },
            {
                "type": "input",
                "block_id": "count",
                "optional": True,
                "label": {"type": "plain_text", "text": "후보 수"},
                "element": {
                    "type": "plain_text_input",
                    "action_id": "value",
                    "initial_value": "4",
                },
            },
            {
                "type": "input",
                "block_id": "notes",
                "optional": True,
                "label": {"type": "plain_text", "text": "이번 배치 메모"},
                "element": {
                    "type": "plain_text_input",
                    "action_id": "value",
                    "multiline": True,
                    "placeholder": {
                        "type": "plain_text",
                        "text": "예: 팔은 유지하고 슬링 길이만 짧게",
                    },
                },
            },
        ],
    }


def find_candidate_from_text(
    store: ReviewStore,
    root_ts: str,
    text: str,
    *,
    mapping: Any | None = None,
) -> tuple[str | None, str | None]:
    if mapping is None:
        mapping = store.find_slack_message(root_ts)
    if mapping and mapping["candidate_id"]:
        return mapping["job_id"], mapping["candidate_id"]
    match = CANDIDATE_PATTERN.search(text)
    if not match:
        return (
            mapping["job_id"] if mapping else None,
            None,
        )
    suffix = match.group(0).upper()
    if mapping and mapping["job_id"]:
        candidate_id = f"{mapping['job_id']}-{suffix}"
        try:
            store.get_candidate(candidate_id)
            return mapping["job_id"], candidate_id
        except ReviewError:
            pass
    return (mapping["job_id"] if mapping else None, None)


def find_feedback_target(
    store: ReviewStore,
    root_ts: str,
    text: str,
) -> tuple[str | None, str | None, str | None]:
    mapping = store.find_slack_message(root_ts)
    job_id, candidate_id = find_candidate_from_text(
        store,
        root_ts,
        text,
        mapping=mapping,
    )
    if mapping and str(mapping["kind"]).startswith("shot:"):
        return job_id, candidate_id, str(mapping["kind"]).split(":", 1)[1]
    if not job_id:
        return job_id, candidate_id, None
    recipe = recipe_from_job(store.get_job(job_id))
    lowered = text.lower()
    for shot in sorted(recipe.shots, key=lambda value: -len(value.id)):
        if shot.is_default:
            continue
        if (
            f"[{shot.id.lower()}]" in lowered
            or re.search(
                rf"(?<![a-z0-9-]){re.escape(shot.id.lower())}"
                rf"(?![a-z0-9-])",
                lowered,
            )
        ):
            return job_id, candidate_id, shot.id
    return job_id, candidate_id, None


class SlackReviewService:
    def __init__(
        self,
        *,
        store: ReviewStore,
        registry: RecipeRegistry,
        channel_id: str,
        comfy_url: str,
        output_root: Path,
        batch_dir: Path = DEFAULT_BATCH_DIR,
        work_timeout: float,
        poll_interval: float,
        run_worker: bool,
    ):
        self.store = store
        self.registry = registry
        self.channel_id = channel_id
        self.comfy_url = comfy_url
        self.output_root = output_root
        self.batch_dir = batch_dir
        self.work_timeout = work_timeout
        self.poll_interval = poll_interval
        self.run_worker = run_worker

    def post_candidate(self, client: Any, candidate_id: str) -> None:
        candidate = self.store.get_candidate(candidate_id)
        job = self.store.get_job(candidate["job_id"])
        recipe = recipe_from_job(job)
        response = client.chat_postMessage(
            channel=self.channel_id,
            text=f"검토 대기 · {recipe.name} · {candidate_id}",
            blocks=candidate_blocks(
                recipe,
                candidate,
                approved=self.store.candidate_is_approved(candidate_id),
            ),
        )
        root_ts = response["ts"]
        self.store.map_slack_message(
            message_ts=root_ts,
            channel_id=self.channel_id,
            kind="candidate-root",
            job_id=job["id"],
            candidate_id=candidate_id,
        )
        raw_path = project_path(candidate["raw_path"])
        client.files_upload_v2(
            channel=self.channel_id,
            thread_ts=root_ts,
            file=str(raw_path),
            title=f"생성 원본 · {candidate_id}",
            initial_comment=(
                f"🖼️ *생성 원본* · seed `{candidate['seed']}`\n"
                "수정 의견은 이 메시지에 답장하세요. "
                "예: `팔은 유지하고 무기만 짧게`"
            ),
        )
        if recipe.is_multi_shot:
            manifest = load_shot_manifest(raw_path.parent)
            manifest_by_id = {
                str(item["id"]): item for item in manifest["shots"]
            }
            for shot in recipe.shots:
                item = manifest_by_id.get(shot.id)
                if item is None:
                    continue
                shot_path = project_path(item["raw_path"])
                client.files_upload_v2(
                    channel=self.channel_id,
                    thread_ts=root_ts,
                    file=str(shot_path),
                    title=f"{shot.label} · {shot.id}",
                    initial_comment=(
                        f"🖼️ *샷 원본* · `{shot.id}`\n"
                        f"수정 의견: `[{shot.id}] 바꿀 내용`"
                    ),
                )
                shot_response = client.chat_postMessage(
                    channel=self.channel_id,
                    thread_ts=root_ts,
                    text=f"샷 검토 대기 · {shot.label} · {shot.id}",
                    blocks=shot_blocks(recipe, candidate, shot),
                )
                self.store.map_slack_message(
                    message_ts=shot_response["ts"],
                    channel_id=self.channel_id,
                    kind=f"shot:{shot.id}",
                    job_id=job["id"],
                    candidate_id=candidate_id,
                )

    def update_candidate(self, client: Any, candidate_id: str) -> None:
        mapping = self.store.find_candidate_slack_message(candidate_id)
        if mapping is None:
            return
        candidate = self.store.get_candidate(candidate_id)
        job = self.store.get_job(candidate["job_id"])
        recipe = recipe_from_job(job)
        client.chat_update(
            channel=mapping["channel_id"],
            ts=mapping["message_ts"],
            text=(
                f"{candidate_state_view(candidate['status'])[1]} · "
                f"{recipe.name} · {candidate_id}"
            ),
            blocks=candidate_blocks(
                recipe,
                candidate,
                approved=self.store.candidate_is_approved(candidate_id),
            ),
        )

    def update_shot(
        self,
        client: Any,
        candidate_id: str,
        shot_id: str,
    ) -> None:
        mapping = self.store.find_candidate_shot_slack_message(
            candidate_id,
            shot_id,
        )
        if mapping is None:
            return
        candidate = self.store.get_candidate(candidate_id)
        recipe = recipe_from_job(
            self.store.get_job(candidate["job_id"])
        )
        shot = next(
            (value for value in recipe.shots if value.id == shot_id),
            None,
        )
        if shot is None:
            return
        decision = self.store.shot_decision(candidate_id, shot_id)
        decision_label = {
            "approve": "채택",
            "reject": "제외",
        }.get(decision, "검토 대기")
        client.chat_update(
            channel=mapping["channel_id"],
            ts=mapping["message_ts"],
            text=f"샷 {decision_label} · {shot.label} · {shot_id}",
            blocks=shot_blocks(
                recipe,
                candidate,
                shot,
                decision=decision,
            ),
        )

    def post_thread(
        self,
        client: Any,
        candidate_id: str,
        text: str,
    ) -> None:
        mapping = self.store.find_candidate_slack_message(candidate_id)
        if mapping is None:
            client.chat_postMessage(channel=self.channel_id, text=text)
            return
        client.chat_postMessage(
            channel=mapping["channel_id"],
            thread_ts=mapping["message_ts"],
            text=text,
        )

    def dispatch_outbox(self, client: Any, row: Any) -> None:
        payload = json.loads(row["payload_json"])
        kind = row["kind"]
        if kind == "job_ready":
            job_id = payload["job_id"]
            job = self.store.get_job(job_id)
            recipe = recipe_from_job(job)
            client.chat_postMessage(
                channel=self.channel_id,
                text=f"생성 완료 · {recipe.name} · 후보 {job['candidate_count']}개",
                blocks=[
                    {
                        "type": "header",
                        "text": {
                            "type": "plain_text",
                            "text": "✅ 아트 후보 생성 완료",
                        },
                    },
                    {
                        "type": "section",
                        "text": {
                            "type": "mrkdwn",
                            "text": (
                                f"*대상*  {category_label(recipe)} · `{recipe.slot}`\n"
                                f"*레시피*  {recipe.name}\n"
                                f"*결과*  후보 {job['candidate_count']}개"
                            ),
                        },
                    },
                    {
                        "type": "section",
                        "text": {
                            "type": "mrkdwn",
                            "text": (
                                "*다음 단계*\n"
                                "아래 후보 카드를 보고 채택·제외·변형을 선택하세요."
                            ),
                        },
                    },
                    {
                        "type": "context",
                        "elements": [
                            {
                                "type": "mrkdwn",
                                "text": f"작업 `{job_id}`",
                            }
                        ],
                    },
                ],
            )
            for candidate in self.store.list_candidates(job_id):
                self.post_candidate(client, candidate["id"])
        elif kind == "job_failed":
            client.chat_postMessage(
                channel=self.channel_id,
                text=f"생성 실패 · {payload['job_id']}",
                blocks=[
                    {
                        "type": "header",
                        "text": {
                            "type": "plain_text",
                            "text": "❌ 아트 생성 실패",
                        },
                    },
                    {
                        "type": "section",
                        "text": {
                            "type": "mrkdwn",
                            "text": (
                                "*지금 할 일*\n"
                                "ComfyUI가 실행 중인지 확인한 뒤 다시 요청하세요."
                            ),
                        },
                    },
                    {
                        "type": "section",
                        "text": {
                            "type": "mrkdwn",
                            "text": (
                                f"*오류 정보*\n"
                                f"```{truncate(payload['error'], 1700)}```"
                            ),
                        },
                    },
                    {
                        "type": "context",
                        "elements": [
                            {
                                "type": "mrkdwn",
                                "text": f"작업 `{payload['job_id']}`",
                            }
                        ],
                    },
                ],
            )
        elif kind == "job_queued":
            parent = payload.get("parent_candidate_id")
            if parent:
                shot_id = payload.get("shot_id")
                self.post_thread(
                    client,
                    parent,
                    (
                        f"🔁 *샷 변형을 시작했습니다*\n"
                        f"`{shot_id}`만 새 후보로 만듭니다. "
                        f"작업 `{payload['job_id']}`"
                        if shot_id
                        else (
                            f"🔁 *비슷한 후보 생성을 시작했습니다*\n"
                            f"작업 `{payload['job_id']}`"
                        )
                    ),
                )
        elif kind == "batch_queued":
            client.chat_postMessage(
                channel=self.channel_id,
                text=(
                    f"배치 대기열 등록 · {payload['plan_id']} · "
                    f"{len(payload['job_ids'])}개 작업"
                ),
                blocks=[
                    {
                        "type": "header",
                        "text": {
                            "type": "plain_text",
                            "text": "📦 다용도 아트 배치를 시작했습니다",
                        },
                    },
                    {
                        "type": "section",
                        "text": {
                            "type": "mrkdwn",
                            "text": (
                                f"*배치* `{payload['plan_id']}`\n"
                                f"*생성 작업* {len(payload['job_ids'])}개\n"
                                "각 결과가 준비되는 순서대로 검토 카드가 올라옵니다."
                            ),
                        },
                    },
                    {
                        "type": "context",
                        "elements": [
                            {
                                "type": "mrkdwn",
                                "text": f"배치 실행 `{payload['batch_id']}`",
                            }
                        ],
                    },
                ],
            )
        elif kind == "job_cancelled":
            client.chat_postMessage(
                channel=self.channel_id,
                text=f"🛑 대기 작업을 취소했습니다 · `{payload['job_id']}`",
            )
        elif kind == "candidate_status":
            self.update_candidate(client, payload["candidate_id"])
        elif kind == "shot_status":
            self.update_shot(
                client,
                payload["candidate_id"],
                payload["shot_id"],
            )
        elif kind == "candidate_prepared":
            candidate_id = payload["candidate_id"]
            mapping = self.store.find_candidate_slack_message(candidate_id)
            if mapping:
                client.files_upload_v2(
                    channel=mapping["channel_id"],
                    thread_ts=mapping["message_ts"],
                    file=str(project_path(payload["preview_path"])),
                    title=f"Aseprite 준비 결과 · {candidate_id}",
                    initial_comment=(
                        "🧹 *Aseprite 준비 완료*\n"
                        "배경 제거·캔버스·Torchstone 팔레트를 적용했습니다. "
                        "이미지를 확인한 뒤 필요한 픽셀을 Aseprite에서 마감하세요."
                    ),
                )
                self.update_candidate(client, candidate_id)
        elif kind == "candidate_animation_ready":
            candidate_id = payload["candidate_id"]
            mapping = self.store.find_candidate_slack_message(candidate_id)
            if mapping:
                manifest_path = project_path(payload["manifest_path"])
                manifest = json.loads(
                    manifest_path.read_text(encoding="utf-8")
                )
                for output in manifest["outputs"]:
                    for preview_value in output["preview_paths"]:
                        preview_path = project_path(preview_value)
                        if not preview_path.name.endswith("-8x.gif"):
                            continue
                        client.files_upload_v2(
                            channel=mapping["channel_id"],
                            thread_ts=mapping["message_ts"],
                            file=str(preview_path),
                            title=(
                                f"애니메이션 미리보기 · {output['slot']}"
                            ),
                            initial_comment=(
                                f"🎞️ *애니메이션 미리보기* · `{output['slot']}`\n"
                                f"포함 동작: `{', '.join(output['tags'])}`"
                            ),
                        )
                client.chat_postMessage(
                    channel=mapping["channel_id"],
                    thread_ts=mapping["message_ts"],
                    text=f"애니메이션 초안 준비 완료 · {candidate_id}",
                    blocks=animation_timing_blocks(
                        candidate_id,
                        float(payload["timing_scale"]),
                    ),
                )
        elif kind == "candidate_published":
            self.post_thread(
                client,
                payload["candidate_id"],
                (
                    f"🚀 *프로젝트 원본 저장 완료* · `{payload['slot']}`\n"
                    f"저장 위치: `{payload['path']}`\n"
                    "*다음 단계*: Unity 카탈로그 동기화와 플레이 화면 검증"
                ),
            )
            self.update_candidate(client, payload["candidate_id"])
        elif kind == "apply_queued":
            self.post_thread(
                client,
                payload["candidate_id"],
                (
                    "🤖 *Codex Spark 게임 반영 요청을 등록했습니다.*\n"
                    "후보는 그대로 보관됩니다. Spark가 Unity 참조와 기존 에셋을 "
                    "조사해 대상을 정하며, 모호하면 이 스레드로 질문합니다.\n"
                    f"요청 `{payload['apply_request_id']}`"
                ),
            )
        elif kind == "apply_status":
            status = payload["status"]
            labels = {
                "planning": "대상 분석 중",
                "applying": "게임 반영 중",
                "needs_input": "사용자 선택 필요",
                "complete": "게임 반영 완료",
                "failed": "게임 반영 실패",
                "cancelled": "반영 요청 취소",
            }
            request = self.store.get_apply_request(
                payload["apply_request_id"]
            )
            detail = ""
            if payload.get("error"):
                detail = f"\n```{truncate(payload['error'], 1400)}```"
            elif payload.get("result"):
                detail = (
                    "\n"
                    + truncate(
                        json.dumps(
                            payload["result"],
                            ensure_ascii=False,
                            indent=2,
                        ),
                        1400,
                    )
                )
            self.post_thread(
                client,
                request["candidate_id"],
                (
                    f"🤖 *Spark 반영 상태: {labels.get(status, status)}*"
                    f"{detail}\n요청 `{request['id']}`"
                ),
            )
        elif kind in {"action_failed"}:
            candidate_id = payload.get("candidate_id")
            text = (
                f"❌ *후처리 작업 실패*\n"
                "*지금 할 일*: 아래 오류를 확인한 뒤 같은 작업을 다시 요청하세요.\n"
                f"```{truncate(payload['error'], 1700)}```\n"
                f"작업 `{payload.get('action_id')}`"
            )
            if candidate_id:
                self.post_thread(client, candidate_id, text)
            else:
                client.chat_postMessage(channel=self.channel_id, text=text)

    def outbox_loop(self, client: Any) -> None:
        while not STOP_EVENT.is_set():
            try:
                rows = self.store.claim_outbox()
            except Exception:
                log_error("outbox claim failed")
                STOP_EVENT.wait(self.poll_interval)
                continue
            if not rows:
                STOP_EVENT.wait(self.poll_interval)
                continue
            failed = False
            for row in rows:
                if STOP_EVENT.is_set():
                    return
                try:
                    self.dispatch_outbox(client, row)
                    self.store.finish_outbox(row["id"])
                except Exception as exc:
                    failed = True
                    retry = row["attempts"] + 1 < OUTBOX_MAX_ATTEMPTS
                    log_error(
                        f"outbox {row['id']} ({row['kind']}) "
                        f"{'retrying' if retry else 'failed permanently'}"
                    )
                    self.store.finish_outbox(
                        row["id"],
                        error=str(exc),
                        retry=retry,
                    )
            if failed:
                STOP_EVENT.wait(self.poll_interval)

    def worker_loop(self) -> None:
        while not STOP_EVENT.is_set():
            try:
                worked = work_once(
                    self.store,
                    comfy_url=self.comfy_url,
                    output_root=self.output_root,
                    timeout=self.work_timeout,
                )
            except Exception:
                log_error("worker iteration failed")
                worked = False
            if not worked:
                STOP_EVENT.wait(self.poll_interval)


def register_handlers(
    app: Any,
    service: SlackReviewService,
) -> None:
    store = service.store
    registry = service.registry

    def safe_action(
        handler: Callable[[dict[str, Any], str], None],
    ) -> Callable[..., None]:
        def wrapped(
            ack: Callable[[], None],
            body: dict[str, Any],
            client: Any,
            respond: Callable[..., None],
            **_kwargs: Any,
        ) -> None:
            ack()
            user_id = body["user"]["id"]
            try:
                require_allowed(user_id)
                action = body["actions"][0]
                handler(action, user_id)
                respond(
                    response_type="ephemeral",
                    text="요청을 받았습니다. 완료되면 카드와 스레드가 갱신됩니다.",
                    replace_original=False,
                )
            except Exception as exc:
                respond(
                    response_type="ephemeral",
                    text=f"처리하지 못했습니다: {exc}",
                    replace_original=False,
                )
        return wrapped

    @app.action("art_candidate_approve")
    @safe_action
    def approve(action: dict[str, Any], user_id: str) -> None:
        approve_candidate(
            store,
            action["value"],
            user_id=user_id,
            event_key=f"slack-button:{action['action_ts']}:{user_id}",
        )

    @app.action("art_candidate_reject")
    @safe_action
    def reject(action: dict[str, Any], user_id: str) -> None:
        reject_candidate(
            store,
            action["value"],
            user_id=user_id,
            event_key=f"slack-button:{action['action_ts']}:{user_id}",
        )

    def queue_action(kind: str, action: dict[str, Any], user_id: str) -> None:
        payload = {"count": 4} if kind == "variation" else {}
        store.enqueue_action(
            kind,
            requested_by=f"slack:{user_id}",
            candidate_id=action["value"],
            payload=payload,
        )

    @app.action("art_candidate_variation")
    @safe_action
    def variation(action: dict[str, Any], user_id: str) -> None:
        queue_action("variation", action, user_id)

    @app.action("art_candidate_prepare")
    @safe_action
    def prepare(action: dict[str, Any], user_id: str) -> None:
        queue_action("prepare", action, user_id)

    @app.action("art_candidate_animation")
    @safe_action
    def animation(action: dict[str, Any], user_id: str) -> None:
        store.enqueue_action(
            "animation_draft",
            requested_by=f"slack:{user_id}",
            candidate_id=action["value"],
            payload={"timing_scale": 1.0},
        )

    @app.action(re.compile(r"^art_animation_timing_(fast|normal|slow)$"))
    @safe_action
    def animation_timing(
        action: dict[str, Any],
        user_id: str,
    ) -> None:
        candidate_id, timing_scale = parse_animation_action(action)
        store.enqueue_action(
            "animation_draft",
            requested_by=f"slack:{user_id}",
            candidate_id=candidate_id,
            payload={"timing_scale": timing_scale},
        )

    @app.action("art_candidate_apply")
    @safe_action
    def apply_candidate(action: dict[str, Any], user_id: str) -> None:
        store.create_apply_request(
            action["value"],
            requested_by=f"slack:{user_id}",
        )

    def decide_shot(
        decision: str,
        action: dict[str, Any],
        user_id: str,
    ) -> None:
        candidate_id, shot_id = parse_shot_action(action)
        event_key = (
            f"slack-shot-button:{action['action_ts']}:{user_id}:{shot_id}"
        )
        decide_candidate_shot(
            store,
            candidate_id,
            shot_id,
            decision,
            user_id=user_id,
            event_key=event_key,
            source="shot-button",
        )

    @app.action("art_shot_approve")
    @safe_action
    def approve_shot(action: dict[str, Any], user_id: str) -> None:
        decide_shot("approve", action, user_id)

    @app.action("art_shot_reject")
    @safe_action
    def reject_shot(action: dict[str, Any], user_id: str) -> None:
        decide_shot("reject", action, user_id)

    @app.action("art_shot_variation")
    @safe_action
    def vary_shot(action: dict[str, Any], user_id: str) -> None:
        candidate_id, shot_id = parse_shot_action(action)
        store.enqueue_action(
            "shot_variation",
            requested_by=f"slack:{user_id}",
            candidate_id=candidate_id,
            payload={"count": 2, "shot_id": shot_id},
        )

    @app.action("art_recipe_run")
    @safe_action
    def run_recipe(action: dict[str, Any], user_id: str) -> None:
        recipe = registry.get(action["value"])
        job_id = store.create_job(
            recipe,
            requested_by=f"slack:{user_id}",
        )
        store.enqueue_outbox("job_queued", {"job_id": job_id})

    @app.event("reaction_added")
    def reaction_added(event: dict[str, Any], body: dict[str, Any]) -> None:
        user_id = event.get("user", "")
        if not allowed_user(user_id):
            return
        item = event.get("item", {})
        if item.get("type") != "message":
            return
        mapping = store.find_slack_message(item.get("ts", ""))
        if mapping is None:
            return
        reaction = event.get("reaction", "")
        base_label = REACTION_LABELS.get(reaction, f"emoji:{reaction}")
        kind = str(mapping["kind"])
        shot_id = kind.split(":", 1)[1] if kind.startswith("shot:") else None
        label = (
            f"shot:{shot_id}:{base_label}"
            if shot_id
            else base_label
        )
        store.add_feedback(
            event_key=f"{body.get('event_id')}:{reaction}:added",
            user_id=user_id,
            source="shot-reaction" if shot_id else "reaction",
            label=label,
            job_id=mapping["job_id"],
            candidate_id=mapping["candidate_id"],
        )

    @app.event("reaction_removed")
    def reaction_removed(event: dict[str, Any], body: dict[str, Any]) -> None:
        user_id = event.get("user", "")
        if not allowed_user(user_id):
            return
        item = event.get("item", {})
        mapping = store.find_slack_message(item.get("ts", ""))
        if mapping is None:
            return
        reaction = event.get("reaction", "")
        base_label = REACTION_LABELS.get(reaction, f"emoji:{reaction}")
        kind = str(mapping["kind"])
        shot_id = kind.split(":", 1)[1] if kind.startswith("shot:") else None
        store.add_feedback(
            event_key=f"{body.get('event_id')}:{reaction}:removed",
            user_id=user_id,
            source=(
                "shot-reaction-removed"
                if shot_id
                else "reaction-removed"
            ),
            label=(
                f"shot:{shot_id}:{base_label}"
                if shot_id
                else base_label
            ),
            job_id=mapping["job_id"],
            candidate_id=mapping["candidate_id"],
        )

    def record_message_feedback(
        event: dict[str, Any],
        body: dict[str, Any],
    ) -> None:
        if event.get("bot_id") or event.get("subtype"):
            return
        user_id = event.get("user", "")
        if not allowed_user(user_id):
            return
        root_ts = event.get("thread_ts")
        if not root_ts:
            return
        text = re.sub(r"<@[A-Z0-9]+>", "", event.get("text", "")).strip()
        if not text:
            return
        job_id, candidate_id, shot_id = find_feedback_target(
            store,
            root_ts,
            text,
        )
        if not job_id and not candidate_id:
            return
        animation_tag = None
        if job_id and not shot_id:
            recipe = recipe_from_job(store.get_job(job_id))
            lowered = text.lower()
            for clip in recipe.animation.get("draft", {}).get("clips", []):
                tag = str(clip.get("tag", "")).lower()
                if tag and f"[{tag}]" in lowered:
                    animation_tag = tag
                    break
        store.add_feedback(
            event_key=(
                f"slack-message:{event.get('channel')}:{event.get('ts')}"
            ),
            user_id=user_id,
            source=(
                "shot-thread"
                if shot_id
                else "animation-thread"
                if animation_tag
                else "thread"
            ),
            label=(
                f"shot:{shot_id}"
                if shot_id
                else f"animation:{animation_tag}"
                if animation_tag
                else ""
            ),
            text=text,
            job_id=job_id,
            candidate_id=candidate_id,
        )

    @app.event("message")
    def message_feedback(
        event: dict[str, Any],
        body: dict[str, Any],
    ) -> None:
        record_message_feedback(event, body)

    @app.event("app_mention")
    def mention_feedback(
        event: dict[str, Any],
        body: dict[str, Any],
    ) -> None:
        record_message_feedback(event, body)

    @app.command("/art")
    def art_command(
        ack: Callable[[], None],
        command: dict[str, Any],
        client: Any,
        respond: Callable[..., None],
    ) -> None:
        ack()
        user_id = command["user_id"]
        try:
            require_allowed(user_id)
            words = command.get("text", "").strip().split()
            verb = words[0].lower() if words else "help"

            def candidate_action(
                kind: str,
                candidate_id: str,
                payload: dict[str, Any] | None = None,
            ) -> None:
                action_id = store.enqueue_action(
                    kind,
                    requested_by=f"slack:{user_id}",
                    candidate_id=candidate_id,
                    payload=payload or {},
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        "요청을 받았습니다. 완료되면 후보 스레드가 갱신됩니다.\n"
                        f"작업 `{action_id}`"
                    ),
                )

            if verb == "new":
                client.views_open(
                    trigger_id=command["trigger_id"],
                    view=modal_view(registry),
                )
            elif verb == "recipes":
                values = registry.load_all().values()
                respond(
                    response_type="ephemeral",
                    text="\n".join(
                        f"• *{category_label(recipe)}* · `{recipe.id}`\n"
                        f"  {recipe.name}"
                        for recipe in values
                    ),
                )
            elif verb == "recipe" and len(words) >= 2:
                recipe = registry.get(words[1])
                respond(
                    response_type="ephemeral",
                    text=recipe.name,
                    blocks=recipe_blocks(recipe),
                )
            elif verb == "batches":
                plans = BatchRegistry(service.batch_dir).load_all().values()
                respond(
                    response_type="ephemeral",
                    text="\n".join(
                        f"• *{plan.name}* · `/art batch {plan.id}`\n"
                        f"  {plan.description or '설명 없음'}"
                        for plan in plans
                    ) or "등록된 배치가 없습니다.",
                )
            elif verb == "batch" and len(words) >= 2:
                plan, batch_jobs = resolve_batch_jobs(
                    store,
                    words[1],
                    batch_dir=service.batch_dir,
                    recipe_dir=registry.directory,
                )
                batch_id, job_ids = store.create_batch_run(
                    plan,
                    requested_by=f"slack:{user_id}",
                    jobs=batch_jobs,
                )
                store.enqueue_outbox(
                    "batch_queued",
                    {
                        "batch_id": batch_id,
                        "plan_id": plan.id,
                        "job_ids": job_ids,
                    },
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        f"📦 *{plan.name}* 배치를 대기열에 넣었습니다.\n"
                        f"생성 작업 {len(job_ids)}개 · 배치 `{batch_id}`"
                    ),
                )
            elif verb == "run" and len(words) >= 2:
                recipe = registry.get(words[1])
                count = (
                    parse_bounded_int(
                        words[2],
                        name="후보 수",
                        minimum=1,
                        maximum=12,
                    )
                    if len(words) >= 3
                    else None
                )
                job_id = store.create_job(
                    recipe,
                    requested_by=f"slack:{user_id}",
                    candidate_count=count,
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        f"🎨 *{recipe.name}* 생성을 시작했습니다.\n"
                        f"후보가 준비되면 채널에 검토 카드가 올라옵니다. 작업 `{job_id}`"
                    ),
                )
            elif verb == "shot" and len(words) >= 3:
                recipe = registry.get(words[1]).only_shot(words[2])
                count = (
                    parse_bounded_int(
                        words[3],
                        name="후보 수",
                        minimum=1,
                        maximum=12,
                    )
                    if len(words) >= 4
                    else None
                )
                job_id = store.create_job(
                    recipe,
                    requested_by=f"slack:{user_id}",
                    candidate_count=count,
                    notes=f"Single-shot trial: {words[2]}",
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        f"🎯 `{words[2]}` 샷만 시험 생성합니다.\n"
                        f"완료되면 채널에 검토 카드가 올라옵니다. 작업 `{job_id}`"
                    ),
                )
            elif verb in {"status", "queue"}:
                jobs = store.list_jobs(limit=10)
                if verb == "queue":
                    jobs = [
                        job
                        for job in jobs
                        if job["status"] in {"queued", "running", "failed"}
                    ]
                respond(
                    response_type="ephemeral",
                    text="\n".join(
                        f"• *{job_state_label(job['status'])}* · "
                        f"`{job['recipe_id']}`\n  `{job['id']}`"
                        for job in jobs
                    ) or "작업이 없습니다.",
                )
            elif verb in {"cancel", "retry"} and len(words) >= 2:
                if verb == "cancel":
                    store.cancel_job(words[1])
                    store.enqueue_outbox(
                        "job_cancelled",
                        {"job_id": words[1]},
                    )
                    message = "대기 작업을 취소했습니다."
                else:
                    store.retry_job(words[1])
                    store.enqueue_outbox(
                        "job_queued",
                        {"job_id": words[1]},
                    )
                    message = "실패 작업을 다시 대기열에 넣었습니다."
                respond(
                    response_type="ephemeral",
                    text=f"{message}\n작업 `{words[1]}`",
                )
            elif verb in {"approve", "reject"} and len(words) >= 2:
                handler = (
                    approve_candidate
                    if verb == "approve"
                    else reject_candidate
                )
                handler(
                    store,
                    words[1],
                    user_id=user_id,
                    event_key=(
                        f"slack-command:{verb}:{words[1]}:"
                        f"{user_id}:{uuid.uuid4().hex}"
                    ),
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        f"{'✅ 채택' if verb == 'approve' else '⚫ 제외'} "
                        f"결정을 기록했습니다.\n후보 `{words[1]}`"
                    ),
                )
            elif verb == "variation" and len(words) >= 2:
                count = (
                    parse_bounded_int(
                        words[2],
                        name="후보 수",
                        minimum=1,
                        maximum=12,
                    )
                    if len(words) >= 3
                    else 4
                )
                candidate_action(
                    "variation",
                    words[1],
                    {"count": count},
                )
            elif verb == "prepare" and len(words) >= 2:
                candidate_action("prepare", words[1])
            elif verb == "animation" and len(words) >= 2:
                try:
                    timing_scale = (
                        float(words[2]) if len(words) >= 3 else 1.0
                    )
                except ValueError as exc:
                    raise ReviewError(
                        "timing-scale은 숫자여야 합니다."
                    ) from exc
                if not 0.5 <= timing_scale <= 2.0:
                    raise ReviewError(
                        "timing-scale은 0.5~2.0 사이여야 합니다."
                    )
                candidate_action(
                    "animation_draft",
                    words[1],
                    {"timing_scale": timing_scale},
                )
            elif (
                verb == "apply"
                and len(words) >= 3
                and words[2].lower() == "confirm"
            ):
                request_id = store.create_apply_request(
                    words[1],
                    requested_by=f"slack:{user_id}",
                    intent=" ".join(words[3:]),
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        "🤖 Codex Spark 게임 반영 요청을 등록했습니다.\n"
                        f"요청 `{request_id}`"
                    ),
                )
            elif verb == "applies":
                requests = store.list_apply_requests(limit=10)
                respond(
                    response_type="ephemeral",
                    text="\n".join(
                        f"• *{request['status']}* · "
                        f"`{request['candidate_id']}`\n"
                        f"  요청 `{request['id']}`"
                        for request in requests
                    ) or "게임 반영 요청이 없습니다.",
                )
            elif verb in {"shot-approve", "shot-reject"} and len(words) >= 3:
                decision = verb.removeprefix("shot-")
                decide_candidate_shot(
                    store,
                    words[1],
                    words[2],
                    decision,
                    user_id=user_id,
                    event_key=(
                        f"slack-command:{verb}:{words[1]}:{words[2]}:"
                        f"{user_id}:{uuid.uuid4().hex}"
                    ),
                    source="shot-command",
                )
                respond(
                    response_type="ephemeral",
                    text=(
                        f"{'✅ 채택' if decision == 'approve' else '⚫ 제외'} "
                        f"결정을 기록했습니다.\n"
                        f"샷 `{words[2]}` · 후보 `{words[1]}`"
                    ),
                )
            elif verb == "shot-variation" and len(words) >= 3:
                count = (
                    parse_bounded_int(
                        words[3],
                        name="후보 수",
                        minimum=1,
                        maximum=12,
                    )
                    if len(words) >= 4
                    else 2
                )
                candidate_action(
                    "shot_variation",
                    words[1],
                    {"shot_id": words[2], "count": count},
                )
            else:
                respond(
                    response_type="ephemeral",
                    text=slack_help_text(),
                )
        except Exception as exc:
            respond(response_type="ephemeral", text=f"처리하지 못했습니다: {exc}")

    @app.shortcut("art_new_job")
    def art_shortcut(
        ack: Callable[[], None],
        shortcut: dict[str, Any],
        client: Any,
    ) -> None:
        ack()
        user_id = shortcut["user"]["id"]
        try:
            require_allowed(user_id)
            client.views_open(
                trigger_id=shortcut["trigger_id"],
                view=modal_view(registry),
            )
        except Exception as exc:
            client.chat_postMessage(
                channel=user_id,
                text=f"처리하지 못했습니다: {exc}",
            )

    @app.view("art_new_job_modal")
    def art_new_job_modal(
        ack: Callable[..., None],
        body: dict[str, Any],
        view: dict[str, Any],
        client: Any,
    ) -> None:
        user_id = body["user"]["id"]
        try:
            require_allowed(user_id)
            values = view["state"]["values"]
            recipe_id = values["recipe"]["value"]["selected_option"]["value"]
            count_text = values["count"]["value"].get("value") or ""
            try:
                count = int(count_text) if count_text else None
            except ValueError:
                ack(
                    response_action="errors",
                    errors={"count": "후보 수는 정수여야 합니다."},
                )
                return
            if count is not None and not 1 <= count <= 12:
                ack(
                    response_action="errors",
                    errors={"count": "후보 수는 1~12 사이여야 합니다."},
                )
                return
            notes = values["notes"]["value"].get("value") or ""
            recipe = registry.get(recipe_id)
        except Exception as exc:
            ack(
                response_action="errors",
                errors={"notes": f"작업을 만들 수 없습니다: {exc}"},
            )
            return
        ack()
        try:
            store.create_job(
                recipe,
                requested_by=f"slack:{user_id}",
                candidate_count=count,
                notes=notes,
            )
        except Exception as exc:
            log_error("modal job creation failed")
            client.chat_postMessage(
                channel=user_id,
                text=f"작업을 만들 수 없습니다: {exc}",
            )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH)
    parser.add_argument("--recipe-dir", type=Path, default=DEFAULT_RECIPE_DIR)
    parser.add_argument("--batch-dir", type=Path, default=DEFAULT_BATCH_DIR)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--comfy-url", default=DEFAULT_COMFY_URL)
    parser.add_argument("--work-timeout", type=float, default=1800.0)
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument("--no-worker", action="store_true")
    parser.add_argument(
        "--env-file",
        type=Path,
        default=Path(
            os.environ.get(
                "PROJECTC_ART_REVIEW_ENV",
                DEFAULT_DB_PATH.parent / "env",
            )
        ),
    )
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        load_env_file(args.env_file.expanduser())
        bot_token = require_env("SLACK_BOT_TOKEN")
        app_token = require_env("SLACK_APP_TOKEN")
        channel_id = require_env("SLACK_ART_CHANNEL_ID")
        require_env("SLACK_ART_ALLOWED_USERS")
        try:
            from slack_bolt import App
            from slack_bolt.adapter.socket_mode import SocketModeHandler
        except ImportError as exc:
            raise ReviewError(
                "Slack dependencies are missing. Install "
                "Tools/ArtPipeline/requirements-art-review.txt"
            ) from exc

        store = ReviewStore(args.db)
        store.recover_stale_running()
        registry = RecipeRegistry(args.recipe_dir)
        service = SlackReviewService(
            store=store,
            registry=registry,
            channel_id=channel_id,
            comfy_url=args.comfy_url,
            output_root=args.output_root,
            batch_dir=args.batch_dir,
            work_timeout=args.work_timeout,
            poll_interval=args.poll_interval,
            run_worker=not args.no_worker,
        )
        app = App(token=bot_token)
        register_handlers(app, service)
        threads = [
            threading.Thread(
                target=service.outbox_loop,
                args=(app.client,),
                daemon=True,
                name="art-review-outbox",
            )
        ]
        if service.run_worker:
            threads.append(
                threading.Thread(
                    target=service.worker_loop,
                    daemon=True,
                    name="art-review-worker",
                )
            )
        for thread in threads:
            thread.start()

        socket_handler = SocketModeHandler(app, app_token)

        def stop(_signum: int, _frame: Any) -> None:
            STOP_EVENT.set()
            socket_handler.close()

        signal.signal(signal.SIGTERM, stop)
        signal.signal(signal.SIGINT, stop)
        socket_handler.start()
    except (ReviewError, OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    finally:
        STOP_EVENT.set()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
