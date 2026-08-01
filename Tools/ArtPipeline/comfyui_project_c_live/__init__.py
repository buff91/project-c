"""ComfyUI frontend bridge for observable Project-C API runs."""

from __future__ import annotations

import time
from typing import Any

from aiohttp import web
from server import PromptServer


WEB_DIRECTORY = "./web"
NODE_CLASS_MAPPINGS: dict[str, Any] = {}
NODE_DISPLAY_NAME_MAPPINGS: dict[str, str] = {}

SESSION_TTL_SECONDS = 15.0
RUN_TTL_SECONDS = 3600.0
_sessions: dict[str, float] = {}
_runs: dict[str, dict[str, Any]] = {}
routes = PromptServer.instance.routes


def _clean() -> None:
    now = time.monotonic()
    for client_id, seen_at in list(_sessions.items()):
        if now - seen_at > SESSION_TTL_SECONDS:
            _sessions.pop(client_id, None)
    for prompt_id, run in list(_runs.items()):
        if now - float(run["created_at"]) > RUN_TTL_SECONDS:
            _runs.pop(prompt_id, None)


@routes.post("/project-c/live/session")
async def register_session(request: web.Request) -> web.Response:
    body = await request.json()
    client_id = str(body.get("client_id", "")).strip()
    if not client_id:
        raise web.HTTPBadRequest(text="client_id is required")
    _clean()
    _sessions[client_id] = time.monotonic()
    return web.json_response({"registered": True, "client_id": client_id})


@routes.get("/project-c/live/session")
async def current_session(_request: web.Request) -> web.Response:
    _clean()
    if not _sessions:
        raise web.HTTPNotFound(text="no active Project-C frontend")
    client_id = max(_sessions, key=_sessions.get)
    return web.json_response({"client_id": client_id})


@routes.post("/project-c/live/run")
async def create_run(request: web.Request) -> web.Response:
    body = await request.json()
    prompt_id = str(body.get("prompt_id", "")).strip()
    client_id = str(body.get("client_id", "")).strip()
    workflow = body.get("workflow")
    if not prompt_id or not client_id or not isinstance(workflow, dict):
        raise web.HTTPBadRequest(
            text="prompt_id, client_id and workflow are required"
        )
    _clean()
    if client_id not in _sessions:
        raise web.HTTPConflict(text="frontend session is stale")
    _runs[prompt_id] = {
        "prompt_id": prompt_id,
        "client_id": client_id,
        "workflow": workflow,
        "loaded": False,
        "created_at": time.monotonic(),
        "events": [],
        "next_sequence": 1,
    }
    return web.json_response({"accepted": True, "prompt_id": prompt_id})


@routes.get("/project-c/live/run/next")
async def next_run(request: web.Request) -> web.Response:
    client_id = str(request.query.get("client_id", "")).strip()
    _clean()
    pending = [
        run
        for run in _runs.values()
        if run["client_id"] == client_id and not run["loaded"]
    ]
    if not pending:
        raise web.HTTPNotFound(text="no pending Project-C run")
    run = min(pending, key=lambda value: value["created_at"])
    return web.json_response(
        {
            "prompt_id": run["prompt_id"],
            "workflow": run["workflow"],
        }
    )


@routes.get("/project-c/live/run/{prompt_id}")
async def get_run(request: web.Request) -> web.Response:
    _clean()
    prompt_id = request.match_info["prompt_id"]
    run = _runs.get(prompt_id)
    if run is None:
        raise web.HTTPNotFound(text="unknown Project-C run")
    return web.json_response(
        {
            "prompt_id": prompt_id,
            "client_id": run["client_id"],
            "loaded": run["loaded"],
            "event_count": len(run["events"]),
        }
    )


@routes.post("/project-c/live/run/{prompt_id}/loaded")
async def mark_loaded(request: web.Request) -> web.Response:
    prompt_id = request.match_info["prompt_id"]
    run = _runs.get(prompt_id)
    if run is None:
        raise web.HTTPNotFound(text="unknown Project-C run")
    run["loaded"] = True
    return web.json_response({"loaded": True, "prompt_id": prompt_id})


@routes.post("/project-c/live/event")
async def record_event(request: web.Request) -> web.Response:
    body = await request.json()
    prompt_id = str(body.get("prompt_id", "")).strip()
    event_type = str(body.get("type", "")).strip()
    data = body.get("data", {})
    run = _runs.get(prompt_id)
    if run is None:
        raise web.HTTPNotFound(text="unknown Project-C run")
    sequence = int(run["next_sequence"])
    run["next_sequence"] = sequence + 1
    run["events"].append(
        {
            "sequence": sequence,
            "type": event_type,
            "data": data if isinstance(data, dict) else {},
        }
    )
    return web.json_response({"recorded": True, "sequence": sequence})


@routes.get("/project-c/live/run/{prompt_id}/events")
async def get_events(request: web.Request) -> web.Response:
    prompt_id = request.match_info["prompt_id"]
    run = _runs.get(prompt_id)
    if run is None:
        raise web.HTTPNotFound(text="unknown Project-C run")
    try:
        after = int(request.query.get("after", "0"))
    except ValueError as exc:
        raise web.HTTPBadRequest(text="after must be an integer") from exc
    return web.json_response(
        {
            "events": [
                event
                for event in run["events"]
                if int(event["sequence"]) > after
            ]
        }
    )


__all__ = [
    "NODE_CLASS_MAPPINGS",
    "NODE_DISPLAY_NAME_MAPPINGS",
    "WEB_DIRECTORY",
]
