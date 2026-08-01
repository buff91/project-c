#!/usr/bin/env python3
"""Observable client for Project-C's local ComfyUI REST workflow.

The input workflow must be ComfyUI's API format (Save/Export API Format), not
the editor/canvas format. Every ``*.api.json`` must have a sibling
``*.workflow.json`` canvas file. The canvas graph is embedded in generated
media and can be loaded into ComfyUI's frontend bridge for live node progress.
"""

from __future__ import annotations

import argparse
import atexit
import fcntl
import hashlib
import json
import mimetypes
import os
import socket
import struct
import sys
import tempfile
import time
import uuid
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Callable
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode, urlparse, urlunparse
from urllib.request import Request, urlopen


DEFAULT_URL = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")
BRIDGE_ROOT = "/project-c/live"
DEFAULT_WORKFLOW_DIR = (
    Path(__file__).resolve().parents[2] / "docs/art-direction/comfyui"
)
DEFAULT_MODEL_NODES = (
    "CheckpointLoaderSimple",
    "LoraLoader",
    "ControlNetLoader",
    "VAELoader",
    "CLIPVisionLoader",
)


class ComfyError(RuntimeError):
    pass


ProgressCallback = Callable[[dict[str, Any]], None]


def utc_now() -> str:
    return datetime.now(UTC).isoformat()


def canonical_json(value: Any) -> str:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def json_digest(value: Any) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def ui_workflow_path(api_path: Path) -> Path:
    """Resolve the required canvas sibling for an API prompt."""
    suffix = ".api.json"
    if not api_path.name.endswith(suffix):
        raise ComfyError(
            f"API workflow must end with {suffix}: {api_path}"
        )
    return api_path.with_name(
        api_path.name.removesuffix(suffix) + ".workflow.json"
    )


def load_ui_workflow(api_path: Path) -> tuple[Path, dict[str, Any]]:
    path = ui_workflow_path(api_path)
    if not path.is_file():
        raise ComfyError(
            f"ComfyUI canvas workflow is missing for {api_path.name}: {path}"
        )
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document, dict) or not isinstance(
        document.get("nodes"), list
    ):
        raise ComfyError(f"Invalid ComfyUI canvas workflow: {path}")
    return path, document


def paired_node_contract(
    prompt: dict[str, Any],
    workflow: dict[str, Any],
) -> list[str]:
    """Return human-readable API/canvas node contract violations."""
    errors: list[str] = []
    canvas_nodes = {
        str(node.get("id")): node
        for node in workflow.get("nodes", [])
        if isinstance(node, dict) and node.get("id") is not None
    }
    canvas_links: dict[tuple[str, str], tuple[str, int]] = {}
    for link in workflow.get("links", []):
        if not isinstance(link, list) or len(link) < 5:
            continue
        _, source_id, source_slot, target_id, target_slot = link[:5]
        target = canvas_nodes.get(str(target_id), {})
        inputs = target.get("inputs", [])
        if (
            isinstance(inputs, list)
            and isinstance(target_slot, int)
            and 0 <= target_slot < len(inputs)
            and isinstance(inputs[target_slot], dict)
        ):
            input_name = str(inputs[target_slot].get("name", ""))
            canvas_links[(str(target_id), input_name)] = (
                str(source_id),
                int(source_slot),
            )
    for node_id, api_node in prompt.items():
        canvas_node = canvas_nodes.get(str(node_id))
        if canvas_node is None:
            errors.append(f"canvas is missing node {node_id}")
            continue
        api_type = str(api_node.get("class_type", ""))
        canvas_type = str(canvas_node.get("type", ""))
        if api_type != canvas_type:
            errors.append(
                f"node {node_id} type differs: API={api_type}, "
                f"canvas={canvas_type}"
            )
        for input_name, value in api_node.get("inputs", {}).items():
            if not (
                isinstance(value, list)
                and len(value) == 2
                and isinstance(value[1], int)
            ):
                continue
            expected = (str(value[0]), int(value[1]))
            actual = canvas_links.get((str(node_id), str(input_name)))
            if actual != expected:
                errors.append(
                    f"node {node_id}.{input_name} link differs: "
                    f"API={expected}, canvas={actual}"
                )
    extra = sorted(set(canvas_nodes) - {str(value) for value in prompt})
    for node_id in extra:
        errors.append(f"canvas has extra node {node_id}")
    return errors


def validate_workflow_pair(
    api_path: Path,
) -> tuple[dict[str, Any], Path, dict[str, Any]]:
    prompt = load_prompt(api_path)
    canvas_path, workflow = load_ui_workflow(api_path)
    errors = paired_node_contract(prompt, workflow)
    if errors:
        raise ComfyError(
            f"ComfyUI workflow pair is out of sync for {api_path}: "
            + "; ".join(errors)
        )
    return prompt, canvas_path, workflow


def publish_workflows(
    base_url: str,
    workflow_dir: Path,
    *,
    folder: str = "Project-C",
) -> list[str]:
    """Publish Project-C canvas files to ComfyUI's Workflows sidebar."""
    api_paths = sorted(workflow_dir.glob("*.api.json"))
    if not api_paths:
        raise ComfyError(
            f"No ComfyUI API workflows found in {workflow_dir}"
        )

    published: list[str] = []
    for api_path in api_paths:
        _, canvas_path, workflow = validate_workflow_pair(api_path)
        workflow_name = (
            api_path.name.removesuffix(".api.json") + ".json"
        )
        user_path = f"workflows/{folder.strip('/')}/{workflow_name}"
        request_bytes(
            base_url,
            "/userdata/"
            + quote(user_path, safe="")
            + "?"
            + urlencode({"overwrite": "true"}),
            method="POST",
            body=(
                json.dumps(workflow, ensure_ascii=False, indent=2) + "\n"
            ).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        published.append(user_path)
        print(user_path)

    return published


def request_bytes(
    base_url: str,
    path: str,
    *,
    method: str = "GET",
    body: bytes | None = None,
    headers: dict[str, str] | None = None,
    timeout: float = 30.0,
) -> bytes:
    url = f"{base_url.rstrip('/')}/{path.lstrip('/')}"
    request = Request(url, data=body, method=method, headers=headers or {})
    try:
        with urlopen(request, timeout=timeout) as response:
            return response.read()
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise ComfyError(f"{method} {url} failed ({exc.code}): {detail}") from exc
    except URLError as exc:
        raise ComfyError(
            f"Cannot reach ComfyUI at {base_url}. Start Desktop and enable its "
            f"HTTP server first: {exc.reason}"
        ) from exc


def request_json(
    base_url: str,
    path: str,
    *,
    method: str = "GET",
    payload: Any | None = None,
    timeout: float = 30.0,
) -> Any:
    body = None
    headers: dict[str, str] = {}
    if payload is not None:
        body = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    raw = request_bytes(
        base_url,
        path,
        method=method,
        body=body,
        headers=headers,
        timeout=timeout,
    )
    return json.loads(raw.decode("utf-8"))


def websocket_url(base_url: str, client_id: str) -> str:
    parsed = urlparse(base_url)
    if parsed.scheme not in {"http", "https"}:
        raise ComfyError(f"Unsupported ComfyUI URL: {base_url}")
    return urlunparse(
        (
            "wss" if parsed.scheme == "https" else "ws",
            parsed.netloc,
            f"{parsed.path.rstrip('/')}/ws",
            "",
            urlencode({"clientId": client_id}),
            "",
        )
    )


def open_progress_socket(
    base_url: str,
    client_id: str,
    *,
    timeout: float = 10.0,
) -> tuple[Any, Any] | None:
    """Open ComfyUI's progress socket when websocket-client is installed."""
    try:
        import websocket
    except ImportError:
        print(
            "warning: websocket-client is not installed; install "
            "Tools/ArtPipeline/requirements-art-review.txt for live progress",
            file=sys.stderr,
        )
        return None
    try:
        connection = websocket.create_connection(
            websocket_url(base_url, client_id),
            timeout=timeout,
        )
        connection.settimeout(1.0)
        return websocket, connection
    except (OSError, websocket.WebSocketException) as exc:
        print(
            f"warning: ComfyUI WebSocket unavailable ({exc}); "
            "falling back to history polling",
            file=sys.stderr,
        )
        return None


def _bridge_request(
    base_url: str,
    path: str,
    *,
    method: str = "GET",
    payload: Any | None = None,
    timeout: float = 2.0,
) -> Any | None:
    try:
        return request_json(
            base_url,
            f"{BRIDGE_ROOT}/{path.lstrip('/')}",
            method=method,
            payload=payload,
            timeout=timeout,
        )
    except (ComfyError, OSError, ValueError, json.JSONDecodeError):
        return None


def prepare_frontend_run(
    base_url: str,
    *,
    prompt_id: str,
    workflow: dict[str, Any],
    timeout: float = 6.0,
) -> str | None:
    """Ask the optional Project-C frontend bridge to load the canvas graph."""
    session = _bridge_request(base_url, "session")
    if not isinstance(session, dict) or not session.get("client_id"):
        return None
    client_id = str(session["client_id"])
    response = _bridge_request(
        base_url,
        "run",
        method="POST",
        payload={
            "prompt_id": prompt_id,
            "client_id": client_id,
            "workflow": workflow,
        },
    )
    if not isinstance(response, dict) or not response.get("accepted"):
        return None
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = _bridge_request(base_url, f"run/{prompt_id}")
        if isinstance(state, dict) and state.get("loaded") is True:
            return client_id
        time.sleep(0.2)
    print(
        "warning: ComfyUI frontend bridge did not load the workflow in time; "
        "using the standalone WebSocket monitor",
        file=sys.stderr,
    )
    return None


def bridge_events(
    base_url: str,
    prompt_id: str,
    *,
    after: int,
) -> tuple[int, list[dict[str, Any]]]:
    response = _bridge_request(
        base_url,
        f"run/{prompt_id}/events?{urlencode({'after': after})}",
    )
    if not isinstance(response, dict):
        return after, []
    events = response.get("events", [])
    if not isinstance(events, list):
        return after, []
    normalized = [
        event for event in events if isinstance(event, dict)
    ]
    latest = max(
        [after]
        + [
            int(event.get("sequence", after))
            for event in normalized
            if str(event.get("sequence", "")).isdigit()
        ]
    )
    return latest, normalized


def command_status(args: argparse.Namespace) -> None:
    stats = request_json(args.url, "/system_stats")
    queue = request_json(args.url, "/queue")
    print(json.dumps({"system_stats": stats, "queue": queue}, indent=2))


def choice_values(node_info: dict[str, Any]) -> dict[str, list[Any]]:
    result: dict[str, list[Any]] = {}
    inputs = node_info.get("input", {})
    for section in ("required", "optional"):
        for input_name, spec in inputs.get(section, {}).items():
            if (
                isinstance(spec, list)
                and spec
                and isinstance(spec[0], list)
            ):
                result[input_name] = spec[0]
    return result


def command_models(args: argparse.Namespace) -> None:
    object_info = request_json(args.url, "/object_info")
    selected = args.node or DEFAULT_MODEL_NODES
    result: dict[str, dict[str, list[Any]]] = {}
    for node_name in selected:
        node_info = object_info.get(node_name)
        if node_info:
            result[node_name] = choice_values(node_info)
    print(json.dumps(result, indent=2))


def multipart_body(
    fields: dict[str, str],
    file_field: str,
    source: Path,
) -> tuple[bytes, str]:
    def disposition_value(value: str) -> str:
        if "\r" in value or "\n" in value:
            raise ComfyError("Multipart names cannot contain CR or LF")
        return value.replace("\\", "\\\\").replace('"', '\\"')

    boundary = f"project-c-{uuid.uuid4().hex}"
    chunks: list[bytes] = []
    for name, value in fields.items():
        chunks.extend(
            [
                f"--{boundary}\r\n".encode(),
                (
                    "Content-Disposition: form-data; "
                    f'name="{disposition_value(name)}"\r\n\r\n'
                ).encode(),
                value.encode(),
                b"\r\n",
            ]
        )

    content_type = mimetypes.guess_type(source.name)[0] or "application/octet-stream"
    chunks.extend(
        [
            f"--{boundary}\r\n".encode(),
            (
                "Content-Disposition: form-data; "
                f'name="{disposition_value(file_field)}"; '
                f'filename="{disposition_value(source.name)}"\r\n'
            ).encode(),
            f"Content-Type: {content_type}\r\n\r\n".encode(),
            source.read_bytes(),
            b"\r\n",
            f"--{boundary}--\r\n".encode(),
        ]
    )
    return b"".join(chunks), boundary


def upload_image(base_url: str, source: Path) -> str:
    if not source.is_file():
        raise ComfyError(f"Upload source does not exist: {source}")
    body, boundary = multipart_body(
        {"type": "input", "overwrite": "true"},
        "image",
        source,
    )
    raw = request_bytes(
        base_url,
        "/upload/image",
        method="POST",
        body=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        timeout=120.0,
    )
    response = json.loads(raw.decode("utf-8"))
    name = response["name"]
    subfolder = response.get("subfolder", "")
    return f"{subfolder}/{name}".lstrip("/")


def parse_assignment(text: str) -> tuple[str, str, str]:
    target, separator, raw_value = text.partition("=")
    if not separator or "." not in target:
        raise ComfyError(
            f"Invalid assignment {text!r}; expected NODE.INPUT=VALUE"
        )
    node_id, input_name = target.split(".", 1)
    if not node_id or not input_name:
        raise ComfyError(
            f"Invalid assignment {text!r}; expected NODE.INPUT=VALUE"
        )
    return node_id, input_name, raw_value


def patch_input(
    prompt: dict[str, Any],
    node_id: str,
    input_name: str,
    value: Any,
) -> None:
    node = prompt.get(node_id)
    if not isinstance(node, dict):
        raise ComfyError(f"Workflow has no node {node_id!r}")
    inputs = node.setdefault("inputs", {})
    if not isinstance(inputs, dict):
        raise ComfyError(f"Workflow node {node_id!r} has invalid inputs")
    inputs[input_name] = value


def load_prompt(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    prompt = document.get("prompt", document)
    if not isinstance(prompt, dict) or not prompt:
        raise ComfyError(f"Workflow is empty or invalid: {path}")
    for node_id, node in prompt.items():
        if not isinstance(node, dict) or "class_type" not in node:
            raise ComfyError(
                f"{path} is not ComfyUI API format; node {node_id!r} has no "
                "'class_type'. In ComfyUI use Save/Export (API Format)."
            )
    return prompt


def command_validate(args: argparse.Namespace) -> None:
    prompt, canvas_path, workflow = validate_workflow_pair(args.workflow)
    print(
        json.dumps(
            {
                "api": str(args.workflow),
                "canvas": str(canvas_path),
                "nodes": len(prompt),
                "api_sha256": json_digest(prompt),
                "canvas_sha256": json_digest(workflow),
            },
            ensure_ascii=False,
            indent=2,
        )
    )


def command_sync_workflows(args: argparse.Namespace) -> None:
    publish_workflows(
        args.url,
        args.workflow_dir,
        folder=args.folder,
    )


def apply_overrides(
    prompt: dict[str, Any],
    assignments: list[str],
) -> None:
    for assignment in assignments:
        node_id, input_name, raw_value = parse_assignment(assignment)
        try:
            value = json.loads(raw_value)
        except json.JSONDecodeError:
            value = raw_value
        patch_input(prompt, node_id, input_name, value)


def apply_uploads(
    base_url: str,
    prompt: dict[str, Any],
    assignments: list[str],
) -> None:
    for assignment in assignments:
        node_id, input_name, source_text = parse_assignment(assignment)
        remote_name = upload_image(base_url, Path(source_text).expanduser().resolve())
        patch_input(prompt, node_id, input_name, remote_name)
        print(f"uploaded {source_text} -> {remote_name}", file=sys.stderr)


def wait_for_history(
    base_url: str,
    prompt_id: str,
    *,
    timeout: float,
    poll_interval: float,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        history = request_json(base_url, f"/history/{prompt_id}")
        if prompt_id in history:
            record = history[prompt_id]
            status = record.get("status", {})
            status_name = str(status.get("status_str", "")).lower()
            if status_name in {
                "error",
                "interrupted",
                "cancelled",
                "canceled",
            }:
                raise ComfyError(
                    f"ComfyUI job {prompt_id} ended as {status_name}: "
                    f"{json.dumps(status, ensure_ascii=False)}"
                )
            if status.get("completed") is True or record.get("outputs"):
                return record
        time.sleep(poll_interval)
    raise ComfyError(f"Timed out waiting for ComfyUI job {prompt_id}")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


class RunObserver:
    """Persist one exact submitted graph and its live execution state."""

    def __init__(
        self,
        root: Path,
        *,
        prompt_id: str,
        client_id: str,
        prompt: dict[str, Any],
        workflow: dict[str, Any],
        api_path: Path | None,
        canvas_path: Path | None,
        mode: str,
        callback: ProgressCallback | None,
    ):
        self.directory = root / prompt_id
        self.manifest_path = self.directory / "run-manifest.json"
        self.events_path = self.directory / "events.ndjson"
        self.preview_path: Path | None = None
        self.callback = callback
        self.manifest: dict[str, Any] = {
            "schema_version": 1,
            "prompt_id": prompt_id,
            "client_id": client_id,
            "status": "submitting",
            "observation_mode": mode,
            "created_at": utc_now(),
            "updated_at": utc_now(),
            "api_path": str(api_path) if api_path else None,
            "canvas_path": str(canvas_path) if canvas_path else None,
            "api_sha256": json_digest(prompt),
            "canvas_sha256": json_digest(workflow),
            "current_node": None,
            "current_node_title": None,
            "progress": None,
            "preview_path": None,
            "error": None,
        }
        self.node_titles = {
            str(node_id): str(
                node.get("_meta", {}).get("title")
                or node.get("class_type")
                or node_id
            )
            for node_id, node in prompt.items()
        }
        self.directory.mkdir(parents=True, exist_ok=True)
        write_json(self.directory / "prompt.api.json", prompt)
        write_json(self.directory / "workflow.json", workflow)
        write_json(self.manifest_path, self.manifest)

    def submitted(self) -> None:
        self.manifest["status"] = "queued"
        self._flush()

    def emit(self, event: dict[str, Any]) -> None:
        event_type = str(event.get("type", "unknown"))
        data = event.get("data", {})
        if not isinstance(data, dict):
            data = {}
        enriched = {
            "observed_at": utc_now(),
            "type": event_type,
            "data": data,
        }
        node_id = data.get("node") or data.get("display_node")
        if node_id is not None:
            enriched["node_title"] = self.node_titles.get(
                str(node_id), str(node_id)
            )
        with self.events_path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(enriched, ensure_ascii=False) + "\n")

        if event_type == "execution_start":
            self.manifest["status"] = "running"
        elif event_type == "executing":
            if node_id is None:
                self.manifest["status"] = "finalizing"
            else:
                self.manifest["status"] = "running"
                self.manifest["current_node"] = str(node_id)
                self.manifest["current_node_title"] = enriched["node_title"]
                self.manifest["progress"] = None
        elif event_type == "progress":
            value = data.get("value")
            maximum = data.get("max")
            self.manifest["progress"] = {
                "value": value,
                "max": maximum,
                "fraction": (
                    float(value) / float(maximum)
                    if isinstance(value, (int, float))
                    and isinstance(maximum, (int, float))
                    and maximum
                    else None
                ),
            }
        elif event_type == "execution_success":
            self.manifest["status"] = "completed"
        elif event_type in {
            "execution_error",
            "execution_interrupted",
        }:
            self.manifest["status"] = "failed"
            self.manifest["error"] = (
                data.get("exception_message")
                or data.get("exception_type")
                or event_type
            )
        self._flush()
        if self.callback:
            self.callback(enriched)

    def save_preview(self, data: bytes, suffix: str) -> None:
        path = self.directory / f"preview{suffix}"
        path.write_bytes(data)
        self.preview_path = path
        self.manifest["preview_path"] = str(path)
        self._flush()

    def complete(self, outputs: list[Path]) -> None:
        self.manifest["status"] = "completed"
        self.manifest["completed_at"] = utc_now()
        self.manifest["outputs"] = [str(path) for path in outputs]
        self._flush()

    def fail(self, error: BaseException) -> None:
        self.manifest["status"] = "failed"
        self.manifest["completed_at"] = utc_now()
        self.manifest["error"] = str(error)
        self._flush()

    def _flush(self) -> None:
        self.manifest["updated_at"] = utc_now()
        write_json(self.manifest_path, self.manifest)


def console_progress(event: dict[str, Any]) -> None:
    event_type = event["type"]
    data = event.get("data", {})
    title = event.get("node_title")
    if event_type == "executing" and title:
        print(f"ComfyUI node: {title}", file=sys.stderr)
    elif event_type == "progress":
        value = data.get("value")
        maximum = data.get("max")
        node = title or data.get("node")
        print(
            f"ComfyUI progress: {node or 'sampler'} {value}/{maximum}",
            file=sys.stderr,
        )
    elif event_type == "execution_cached":
        print("ComfyUI reused cached nodes", file=sys.stderr)
    elif event_type == "execution_success":
        print("ComfyUI execution completed", file=sys.stderr)


def _binary_preview(frame: bytes) -> tuple[bytes, str] | None:
    if len(frame) < 8:
        return None
    event_type = struct.unpack(">I", frame[:4])[0]
    if event_type == 1:
        image_type = struct.unpack(">I", frame[4:8])[0]
        payload = frame[8:]
    elif event_type == 4:
        metadata_length = struct.unpack(">I", frame[4:8])[0]
        start = 8 + metadata_length
        if len(frame) <= start:
            return None
        payload = frame[start:]
        image_type = 2 if payload.startswith(b"\x89PNG") else 1
    else:
        return None
    return payload, ".png" if image_type == 2 else ".jpg"


def _event_error(event: dict[str, Any]) -> ComfyError | None:
    event_type = str(event.get("type", ""))
    if event_type not in {"execution_error", "execution_interrupted"}:
        return None
    data = event.get("data", {})
    if not isinstance(data, dict):
        data = {}
    detail = (
        data.get("exception_message")
        or data.get("exception_type")
        or event_type
    )
    node_id = data.get("node_id")
    location = f" at node {node_id}" if node_id is not None else ""
    return ComfyError(f"ComfyUI {event_type}{location}: {detail}")


def wait_for_websocket(
    base_url: str,
    prompt_id: str,
    *,
    websocket_module: Any,
    connection: Any,
    observer: RunObserver,
    timeout: float,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    try:
        while time.monotonic() < deadline:
            try:
                message = connection.recv()
            except (
                websocket_module.WebSocketTimeoutException,
                socket.timeout,
            ):
                continue
            if isinstance(message, bytes):
                preview = _binary_preview(message)
                if preview:
                    observer.save_preview(*preview)
                continue
            try:
                event = json.loads(message)
            except (TypeError, json.JSONDecodeError):
                continue
            if not isinstance(event, dict):
                continue
            data = event.get("data", {})
            if isinstance(data, dict):
                event_prompt_id = data.get("prompt_id")
                if event_prompt_id and event_prompt_id != prompt_id:
                    continue
            observer.emit(event)
            error = _event_error(event)
            if error:
                raise error
            event_type = str(event.get("type", ""))
            if event_type == "execution_success" or (
                event_type == "executing"
                and isinstance(data, dict)
                and data.get("node") is None
                and data.get("prompt_id") == prompt_id
            ):
                return wait_for_history(
                    base_url,
                    prompt_id,
                    timeout=min(30.0, max(1.0, deadline - time.monotonic())),
                    poll_interval=0.2,
                )
    finally:
        connection.close()
    raise ComfyError(f"Timed out waiting for ComfyUI job {prompt_id}")


def wait_for_frontend(
    base_url: str,
    prompt_id: str,
    *,
    observer: RunObserver,
    timeout: float,
    poll_interval: float,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    cursor = 0
    while time.monotonic() < deadline:
        cursor, events = bridge_events(
            base_url,
            prompt_id,
            after=cursor,
        )
        for event in events:
            observer.emit(event)
            error = _event_error(event)
            if error:
                raise error
            if event.get("type") == "execution_success":
                return wait_for_history(
                    base_url,
                    prompt_id,
                    timeout=min(
                        30.0,
                        max(1.0, deadline - time.monotonic()),
                    ),
                    poll_interval=0.2,
                )
        history = request_json(base_url, f"/history/{prompt_id}")
        if prompt_id in history:
            record = history[prompt_id]
            status = record.get("status", {})
            if status.get("completed") is True or record.get("outputs"):
                return record
        time.sleep(poll_interval)
    raise ComfyError(f"Timed out waiting for ComfyUI job {prompt_id}")


def final_output_node_ids(prompt: dict[str, Any]) -> tuple[str, ...]:
    node_ids = tuple(
        sorted(
            (
                str(node_id)
                for node_id, node in prompt.items()
                if node.get("class_type") in {"SaveImage", "SaveAnimatedWEBP"}
            ),
            key=lambda value: (not value.isdigit(), int(value) if value.isdigit() else value),
        )
    )
    if not node_ids:
        raise ComfyError("Workflow has no final SaveImage/SaveAnimatedWEBP node")
    return node_ids


def output_descriptors(
    record: dict[str, Any],
    *,
    node_ids: tuple[str, ...] | None = None,
) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    outputs = record.get("outputs", {})
    selected = node_ids or tuple(
        sorted(outputs, key=lambda value: (not str(value).isdigit(), str(value)))
    )
    for node_id in selected:
        node_output = outputs.get(str(node_id), {})
        for collection_name in ("images", "gifs", "audio"):
            for item in node_output.get(collection_name, []):
                if isinstance(item, dict) and "filename" in item:
                    result.append(item)
    return result


def download_outputs(
    base_url: str,
    record: dict[str, Any],
    output_dir: Path,
    *,
    node_ids: tuple[str, ...] | None = None,
) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for item in output_descriptors(record, node_ids=node_ids):
        query = urlencode(
            {
                "filename": item["filename"],
                "subfolder": item.get("subfolder", ""),
                "type": item.get("type", "output"),
            }
        )
        data = request_bytes(base_url, f"/view?{query}", timeout=120.0)
        destination = output_dir / Path(item["filename"]).name
        if destination.exists():
            destination = destination.with_name(
                f"{destination.stem}-{uuid.uuid4().hex[:8]}{destination.suffix}"
            )
        destination.write_bytes(data)
        written.append(destination)
    return written


DEFAULT_LEASE_PATH = Path(
    os.environ.get(
        "COMFY_LEASE_PATH",
        Path(tempfile.gettempdir()) / "project-c-comfy-checkpoint.lease",
    )
)
# chunk 가 클수록 재로드는 줄지만(160/chunk 초) 다른 드라이버의 대기가 길어진다.
# 4 면 낭비의 75% 를 걷어내면서 최대 대기가 잡 4개(약 15분)로 묶인다.
DEFAULT_LEASE_CHUNK = 4


def lease_chunk() -> int:
    """호출 시점에 읽는다 — 테스트·CI 가 ``COMFY_LEASE_CHUNK=0`` 으로 끌 수 있게."""
    try:
        return int(os.environ.get("COMFY_LEASE_CHUNK", str(DEFAULT_LEASE_CHUNK)))
    except ValueError:
        return DEFAULT_LEASE_CHUNK


def prompt_checkpoint(prompt: dict[str, Any]) -> str | None:
    """API 프롬프트가 로드하는 체크포인트 이름 (없으면 None)."""
    for node in prompt.values():
        if not isinstance(node, dict):
            continue
        if node.get("class_type") == "CheckpointLoaderSimple":
            name = node.get("inputs", {}).get("ckpt_name")
            if isinstance(name, str):
                return name
    return None


class CheckpointLease:
    """배치 드라이버 여러 개가 한 ComfyUI 큐를 공유할 때 체크포인트 재로드를 줄인다.

    드라이버들은 서로를 모른 채 같은 큐에 발주한다. 체크포인트가 다른 두 배치가
    1:1로 교대하면 ComfyUI 는 매 장마다 SDXL 체크포인트를 다시 읽는다 — 2026-07-31
    로컬 실측에서 장당 221초가 381초로 늘었다(+160초, 58잡 중 21잡이 이 비용을 냄).

    ``chunk`` 개의 잡을 연속으로 점유한 뒤 락을 놓아 다른 드라이버에 차례를 준다.
    재로드는 chunk 분의 1로 줄고, 긴 배치가 다른 워크트리를 무한정 막지도 않는다.
    락을 잡지 못하는 환경(권한·플랫폼)에서는 경고만 남기고 그냥 진행한다 — 발주를
    막는 것보다 느린 편이 낫다.
    """

    def __init__(
        self,
        checkpoint: str | None,
        *,
        chunk: int = DEFAULT_LEASE_CHUNK,
        path: Path | None = None,
    ) -> None:
        if chunk < 1:
            raise ValueError("chunk must be >= 1")
        self.checkpoint = checkpoint
        self.chunk = chunk
        self.path = path or DEFAULT_LEASE_PATH
        self._handle: Any = None
        self._used = 0

    def _acquire(self) -> None:
        if self._handle is not None or self.checkpoint is None:
            return
        try:
            handle = self.path.open("a+")
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
        except OSError as exc:
            print(
                f"comfy lease unavailable ({exc}); 직렬화 없이 진행한다",
                file=sys.stderr,
            )
            self.checkpoint = None
            return
        self._handle = handle
        self._used = 0

    def _release(self) -> None:
        if self._handle is None:
            return
        try:
            fcntl.flock(self._handle.fileno(), fcntl.LOCK_UN)
        finally:
            self._handle.close()
            self._handle = None

    def tick(self) -> None:
        """잡 하나를 제출하기 직전에 부른다. chunk 를 다 쓰면 차례를 넘긴다."""
        if self.checkpoint is None:
            return
        if self._handle is not None and self._used >= self.chunk:
            self._release()
        self._acquire()
        self._used += 1

    def __enter__(self) -> CheckpointLease:
        return self

    def __exit__(self, *exc_info: Any) -> None:
        self._release()


_process_lease: CheckpointLease | None = None


def hold_checkpoint_lease(checkpoint: str | None) -> None:
    """이 프로세스가 발주하는 동안 큐를 체크포인트 단위로 점유한다.

    배치 드라이버는 ``execute_prompt`` 만 부르고 리스의 존재를 몰라도 된다 —
    드라이버가 워크트리마다 복제돼 있어 호출부를 일괄 수정할 수 없기 때문이다.
    ``COMFY_LEASE_CHUNK=0`` 으로 끌 수 있다.
    """
    global _process_lease
    chunk = lease_chunk()
    if checkpoint is None or chunk < 1:
        return
    if _process_lease is None or _process_lease.checkpoint != checkpoint:
        if _process_lease is not None:
            _process_lease._release()
        _process_lease = CheckpointLease(checkpoint, chunk=chunk)
        atexit.register(_process_lease._release)
    _process_lease.tick()


def execute_prompt(
    base_url: str,
    prompt: dict[str, Any],
    output_dir: Path,
    *,
    timeout: float,
    poll_interval: float = 1.0,
    workflow_path: Path | None = None,
    prefer_frontend: bool = True,
    progress: ProgressCallback | None = console_progress,
) -> tuple[str, list[Path]]:
    node_ids = final_output_node_ids(prompt)
    if workflow_path is None:
        raise ComfyError(
            "execute_prompt requires the source *.api.json path so its "
            "paired canvas workflow can be embedded"
        )
    canvas_path, workflow = load_ui_workflow(workflow_path)
    contract_errors = paired_node_contract(prompt, workflow)
    if contract_errors:
        raise ComfyError(
            f"ComfyUI workflow pair is out of sync for {workflow_path}: "
            + "; ".join(contract_errors)
        )

    # 큐를 체크포인트 단위로 점유한다 — 다른 배치와 1:1 교대하면 매 장마다
    # 체크포인트를 다시 읽어 장당 ~160초를 버린다. CheckpointLease 주석 참고.
    hold_checkpoint_lease(prompt_checkpoint(prompt))

    prompt_id = str(uuid.uuid4())
    client_id: str | None = None
    observation_mode = "websocket"
    progress_socket: tuple[Any, Any] | None = None
    if prefer_frontend:
        client_id = prepare_frontend_run(
            base_url,
            prompt_id=prompt_id,
            workflow=workflow,
        )
        if client_id:
            observation_mode = "comfyui-frontend"
    if client_id is None:
        client_id = uuid.uuid4().hex
        progress_socket = open_progress_socket(base_url, client_id)
        if progress_socket is None:
            observation_mode = "history-poll"

    observer = RunObserver(
        output_dir / "_runs",
        prompt_id=prompt_id,
        client_id=client_id,
        prompt=prompt,
        workflow=workflow,
        api_path=workflow_path,
        canvas_path=canvas_path,
        mode=observation_mode,
        callback=progress,
    )
    payload = {
        "prompt": prompt,
        "client_id": client_id,
        "prompt_id": prompt_id,
        "extra_data": {
            "extra_pnginfo": {"workflow": workflow},
            "project_c": {
                "api_path": str(workflow_path),
                "canvas_path": str(canvas_path),
                "api_sha256": json_digest(prompt),
                "canvas_sha256": json_digest(workflow),
            },
        },
    }
    try:
        response = request_json(
            base_url,
            "/prompt",
            method="POST",
            payload=payload,
            timeout=120.0,
        )
        returned_prompt_id = response.get("prompt_id")
        if not returned_prompt_id:
            raise ComfyError(
                f"ComfyUI did not return a prompt_id: {response}"
            )
        if str(returned_prompt_id) != prompt_id:
            raise ComfyError(
                "ComfyUI returned a different prompt_id than requested: "
                f"{returned_prompt_id} != {prompt_id}"
            )
        observer.submitted()
        if observation_mode == "comfyui-frontend":
            record = wait_for_frontend(
                base_url,
                prompt_id,
                observer=observer,
                timeout=timeout,
                poll_interval=poll_interval,
            )
        elif progress_socket is not None:
            websocket_module, connection = progress_socket
            record = wait_for_websocket(
                base_url,
                prompt_id,
                websocket_module=websocket_module,
                connection=connection,
                observer=observer,
                timeout=timeout,
            )
        else:
            record = wait_for_history(
                base_url,
                prompt_id,
                timeout=timeout,
                poll_interval=poll_interval,
            )
        outputs = download_outputs(
            base_url,
            record,
            output_dir,
            node_ids=node_ids,
        )
        observer.complete(outputs)
        return prompt_id, outputs
    except Exception as exc:
        if progress_socket is not None:
            try:
                progress_socket[1].close()
            except Exception:
                pass
        observer.fail(exc)
        raise


def command_run(args: argparse.Namespace) -> None:
    prompt, _, workflow = validate_workflow_pair(args.workflow)
    apply_overrides(prompt, args.set)
    apply_uploads(args.url, prompt, args.upload)

    if args.no_wait:
        prompt_id = str(uuid.uuid4())
        client_id = (
            prepare_frontend_run(
                args.url,
                prompt_id=prompt_id,
                workflow=workflow,
            )
            if not args.no_frontend
            else None
        )
        observation_mode = (
            "comfyui-frontend" if client_id else "detached"
        )
        client_id = client_id or uuid.uuid4().hex
        observer = RunObserver(
            args.output_dir / "_runs",
            prompt_id=prompt_id,
            client_id=client_id,
            prompt=prompt,
            workflow=workflow,
            api_path=args.workflow,
            canvas_path=ui_workflow_path(args.workflow),
            mode=observation_mode,
            callback=None,
        )
        try:
            response = request_json(
                args.url,
                "/prompt",
                method="POST",
                payload={
                    "prompt": prompt,
                    "client_id": client_id,
                    "prompt_id": prompt_id,
                    "extra_data": {
                        "extra_pnginfo": {"workflow": workflow},
                        "project_c": {
                            "api_path": str(args.workflow),
                            "canvas_path": str(
                                ui_workflow_path(args.workflow)
                            ),
                        },
                    },
                },
                timeout=120.0,
            )
            returned_prompt_id = response.get("prompt_id")
            if not returned_prompt_id:
                raise ComfyError(
                    f"ComfyUI did not return a prompt_id: {response}"
                )
            observer.submitted()
        except Exception as exc:
            observer.fail(exc)
            raise
        print(prompt_id)
        return
    prompt_id, written = execute_prompt(
        args.url,
        prompt,
        args.output_dir,
        timeout=args.timeout,
        poll_interval=args.poll_interval,
        workflow_path=args.workflow,
        prefer_frontend=not args.no_frontend,
    )
    print(prompt_id)
    if not written:
        raise ComfyError(f"Job {prompt_id} completed without downloadable outputs")
    for path in written:
        print(path)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default=DEFAULT_URL, help="ComfyUI base URL")
    subparsers = parser.add_subparsers(dest="command", required=True)

    status = subparsers.add_parser("status", help="Show server and queue status")
    status.set_defaults(handler=command_status)

    models = subparsers.add_parser(
        "models",
        help="List model choices exposed by loader nodes",
    )
    models.add_argument(
        "--node",
        action="append",
        default=[],
        help="Node class to inspect; may be repeated",
    )
    models.set_defaults(handler=command_models)

    validate = subparsers.add_parser(
        "validate",
        help="Validate a paired API and canvas workflow",
    )
    validate.add_argument("workflow", type=Path)
    validate.set_defaults(handler=command_validate)

    sync_workflows = subparsers.add_parser(
        "sync-workflows",
        help="Publish canvas files to ComfyUI's Workflows sidebar",
    )
    sync_workflows.add_argument(
        "--workflow-dir",
        type=Path,
        default=DEFAULT_WORKFLOW_DIR,
        help="Directory containing paired *.api.json/*.workflow.json files",
    )
    sync_workflows.add_argument(
        "--folder",
        default="Project-C",
        help="ComfyUI Workflows sidebar folder",
    )
    sync_workflows.set_defaults(handler=command_sync_workflows)

    run = subparsers.add_parser("run", help="Submit an API-format workflow")
    run.add_argument("workflow", type=Path)
    run.add_argument(
        "--set",
        action="append",
        default=[],
        metavar="NODE.INPUT=JSON",
        help="Override a node input; plain strings need no JSON quotes",
    )
    run.add_argument(
        "--upload",
        action="append",
        default=[],
        metavar="NODE.INPUT=PATH",
        help="Upload an input image and patch the receiving LoadImage input",
    )
    run.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help="Directory where completed outputs are copied",
    )
    run.add_argument("--no-wait", action="store_true")
    run.add_argument(
        "--no-frontend",
        action="store_true",
        help="Do not auto-load the workflow in the ComfyUI frontend bridge",
    )
    run.add_argument("--timeout", type=float, default=1800.0)
    run.add_argument("--poll-interval", type=float, default=1.0)
    run.set_defaults(handler=command_run)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        args.handler(args)
    except (ComfyError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
