#!/usr/bin/env python3
"""Small, dependency-free client for Project-C's local ComfyUI REST workflow.

The input workflow must be ComfyUI's API format (Save/Export API Format), not
the editor/canvas format. This client deliberately does not invent or rewrite
graphs: it patches declared node inputs, submits the graph, waits, and copies
the generated files into the repository.
"""

from __future__ import annotations

import argparse
import json
import mimetypes
import os
import sys
import time
import uuid
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


DEFAULT_URL = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")
DEFAULT_MODEL_NODES = (
    "CheckpointLoaderSimple",
    "LoraLoader",
    "ControlNetLoader",
    "VAELoader",
    "CLIPVisionLoader",
)


class ComfyError(RuntimeError):
    pass


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
    boundary = f"project-c-{uuid.uuid4().hex}"
    chunks: list[bytes] = []
    for name, value in fields.items():
        chunks.extend(
            [
                f"--{boundary}\r\n".encode(),
                (
                    f'Content-Disposition: form-data; name="{name}"\r\n\r\n'
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
                f'Content-Disposition: form-data; name="{file_field}"; '
                f'filename="{source.name}"\r\n'
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
            if status.get("status_str") == "error":
                raise ComfyError(
                    f"ComfyUI job {prompt_id} failed: "
                    f"{json.dumps(status, ensure_ascii=False)}"
                )
            return record
        time.sleep(poll_interval)
    raise ComfyError(f"Timed out waiting for ComfyUI job {prompt_id}")


def output_descriptors(record: dict[str, Any]) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    for node_output in record.get("outputs", {}).values():
        for collection_name in ("images", "gifs", "audio"):
            for item in node_output.get(collection_name, []):
                if isinstance(item, dict) and "filename" in item:
                    result.append(item)
    return result


def download_outputs(
    base_url: str,
    record: dict[str, Any],
    output_dir: Path,
) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for item in output_descriptors(record):
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


def command_run(args: argparse.Namespace) -> None:
    prompt = load_prompt(args.workflow)
    apply_overrides(prompt, args.set)
    apply_uploads(args.url, prompt, args.upload)

    response = request_json(
        args.url,
        "/prompt",
        method="POST",
        payload={"prompt": prompt, "client_id": uuid.uuid4().hex},
        timeout=120.0,
    )
    prompt_id = response.get("prompt_id")
    if not prompt_id:
        raise ComfyError(f"ComfyUI did not return a prompt_id: {response}")
    print(prompt_id)
    if args.no_wait:
        return

    record = wait_for_history(
        args.url,
        prompt_id,
        timeout=args.timeout,
        poll_interval=args.poll_interval,
    )
    written = download_outputs(args.url, record, args.output_dir)
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
