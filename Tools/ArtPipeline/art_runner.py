#!/usr/bin/env python3
"""Queue and execute reproducible Project-C art recipes.

The runner is deterministic infrastructure. It does not interpret natural
language feedback; Codex Scheduled converts that feedback into recipe edits or
explicit runner commands.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import signal
import subprocess
import sys
import time
import traceback
import uuid
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw

import art_asset
import comfy_batch
from art_review import (
    ASSET_TYPES,
    BatchRegistry,
    DEFAULT_BATCH_DIR,
    DEFAULT_DB_PATH,
    DEFAULT_OUTPUT_ROOT,
    DEFAULT_RECIPE_DIR,
    PROJECT_ROOT,
    Recipe,
    RecipeRegistry,
    ReviewError,
    ReviewStore,
    ShotSpec,
    SlotCatalog,
    UNITY_SLOT_SOURCE,
    VALID_ASSET_TYPES,
    WorkflowTypeRegistry,
    enforce_color_area_limits,
    image_metrics,
    make_id,
    project_path,
    relative_project_path,
    recipe_from_job,
    row_dict,
    utc_now,
)
from art_compose import MethodRegistry, definition_for_target, resolve_by_id


DEFAULT_COMFY_URL = os.environ.get(
    "COMFYUI_URL",
    comfy_batch.DEFAULT_URL,
)
ASEPRITE_ANIMATION_SCRIPT = (
    Path(__file__).resolve().with_name("aseprite_build_animation.lua")
)
STALE_RUNNING_SECONDS = float(
    os.environ.get("PROJECTC_ART_STALE_RUNNING_SECONDS", 3600.0)
)
STOP_REQUESTED = False


def handle_stop(_signum: int, _frame: Any) -> None:
    global STOP_REQUESTED
    STOP_REQUESTED = True


def json_print(value: Any) -> None:
    print(json.dumps(value, ensure_ascii=False, indent=2))


def submit_prompt(
    recipe: Recipe,
    *,
    seed: int,
    shot: ShotSpec | None,
    output_dir: Path,
    comfy_url: str,
    timeout: float,
) -> list[Path]:
    prompt = comfy_batch.load_prompt(recipe.workflow_path)
    comfy_batch.apply_overrides(prompt, recipe.assignments(seed, shot))
    comfy_batch.apply_uploads(comfy_url, prompt, recipe.uploads(shot))
    prompt_id, outputs = comfy_batch.execute_prompt(
        comfy_url,
        prompt,
        output_dir,
        timeout=timeout,
        poll_interval=1.0,
    )
    image_outputs = [
        path for path in outputs
        if path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"}
    ]
    if not image_outputs:
        raise ReviewError(
            f"ComfyUI job {prompt_id} returned no raster image for {recipe.id}"
        )
    return image_outputs


def review_sheet(
    images: list[tuple[str, Path]],
    destination: Path,
    *,
    columns: int = 3,
    cell_size: tuple[int, int] = (384, 416),
) -> Path:
    """Build one labelled Slack-friendly sheet without altering source shots."""
    if not images:
        raise ReviewError("Cannot build an empty review sheet")
    columns = max(1, min(columns, len(images)))
    rows = (len(images) + columns - 1) // columns
    cell_width, cell_height = cell_size
    label_height = 32
    sheet = Image.new(
        "RGBA",
        (columns * cell_width, rows * cell_height),
        (12, 18, 24, 255),
    )
    draw = ImageDraw.Draw(sheet)
    for index, (label, path) in enumerate(images):
        source = Image.open(path).convert("RGBA")
        available = (cell_width - 16, cell_height - label_height - 16)
        source.thumbnail(available, Image.Resampling.LANCZOS)
        column = index % columns
        row = index // columns
        origin_x = column * cell_width
        origin_y = row * cell_height
        image_x = origin_x + (cell_width - source.width) // 2
        image_y = origin_y + label_height + (
            cell_height - label_height - source.height
        ) // 2
        sheet.alpha_composite(source, (image_x, image_y))
        draw.text(
            (origin_x + 8, origin_y + 8),
            label,
            fill=(230, 235, 238, 255),
        )
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(destination)
    return destination


def write_shot_manifest(
    destination: Path,
    *,
    recipe: Recipe,
    seed: int,
    shots: list[dict[str, Any]],
) -> Path:
    document = {
        "schema_version": 1,
        "recipe_id": recipe.id,
        "recipe_hash": recipe.digest,
        "candidate_seed": seed,
        "shots": shots,
    }
    destination.write_text(
        json.dumps(document, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return destination


def load_shot_manifest(candidate_dir: Path) -> dict[str, Any]:
    path = candidate_dir / "shot-manifest.json"
    if not path.is_file():
        raise ReviewError(f"Multi-shot manifest is missing: {path}")
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ReviewError(f"Cannot load shot manifest {path}: {exc}") from exc
    if not isinstance(document.get("shots"), list):
        raise ReviewError(f"Shot manifest has no shots list: {path}")
    return document


def process_job(
    store: ReviewStore,
    job: Any,
    *,
    comfy_url: str,
    output_root: Path,
    timeout: float,
) -> None:
    try:
        recipe = recipe_from_job(job)
        recipe.validate_files()
        output_dir = output_root / job["id"]
        output_dir.mkdir(parents=True, exist_ok=True)
        comfy_batch.request_json(comfy_url, "/system_stats", timeout=10.0)
        for index in range(job["candidate_count"]):
            ordinal = index + 1
            seed = int(job["base_seed"]) + index
            candidate_dir = output_dir / f"C{ordinal:02d}"
            destination = candidate_dir / "raw.png"
            if recipe.is_multi_shot:
                generated: list[tuple[str, Path]] = []
                manifest_shots: list[dict[str, Any]] = []
                for shot in recipe.shots:
                    shot_dir = candidate_dir / "shots" / shot.id
                    outputs = submit_prompt(
                        recipe,
                        seed=seed,
                        shot=shot,
                        output_dir=shot_dir,
                        comfy_url=comfy_url,
                        timeout=timeout,
                    )
                    source = outputs[0]
                    raw_shot = shot_dir / "raw.png"
                    if source.resolve() != raw_shot.resolve():
                        shutil.copy2(source, raw_shot)
                    generated.append((shot.label, raw_shot))
                    shot_canvas = shot.output_canvas or recipe.canvas
                    manifest_shots.append(
                        {
                            "id": shot.id,
                            "label": shot.label,
                            "slot": shot.slot or recipe.slot,
                            "seed": seed + shot.seed_offset,
                            "canvas": list(shot_canvas),
                            "raw_path": relative_project_path(raw_shot),
                        }
                    )
                review_sheet(generated, destination)
                write_shot_manifest(
                    candidate_dir / "shot-manifest.json",
                    recipe=recipe,
                    seed=seed,
                    shots=manifest_shots,
                )
            else:
                outputs = submit_prompt(
                    recipe,
                    seed=seed,
                    shot=None,
                    output_dir=candidate_dir,
                    comfy_url=comfy_url,
                    timeout=timeout,
                )
                source = outputs[0]
                if source.resolve() != destination.resolve():
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(source, destination)
            if recipe.is_multi_shot:
                metrics = {
                    "shot_count": len(manifest_shots),
                    "shots": {
                        item["id"]: image_metrics(
                            project_path(item["raw_path"])
                        )
                        for item in manifest_shots
                    },
                }
            else:
                metrics = image_metrics(destination)
            store.add_candidate(
                job_id=job["id"],
                ordinal=ordinal,
                seed=seed,
                raw_path=destination,
                metrics=metrics,
            )
        store.set_job_status(job["id"], "awaiting_review")
        store.enqueue_outbox("job_ready", {"job_id": job["id"]})
    except Exception as exc:
        message = str(exc)
        store.set_job_status(job["id"], "failed", error=message)
        store.enqueue_outbox(
            "job_failed",
            {"job_id": job["id"], "error": message},
        )
        raise


def aseprite_binary() -> Path:
    try:
        return art_asset.aseprite_binary()
    except art_asset.AssetError as exc:
        raise ReviewError(str(exc)) from exc


def export_aseprite(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            str(aseprite_binary()),
            "--batch",
            str(source),
            "--save-as",
            str(destination),
        ],
        cwd=PROJECT_ROOT,
        check=True,
    )
    if not destination.is_file():
        raise ReviewError(f"Aseprite did not export {destination}")


def build_aseprite_animation(manifest_path: Path) -> None:
    if not ASEPRITE_ANIMATION_SCRIPT.is_file():
        raise ReviewError(
            f"Aseprite animation script is missing: "
            f"{ASEPRITE_ANIMATION_SCRIPT}"
        )
    subprocess.run(
        [
            str(aseprite_binary()),
            "--batch",
            "--script-param",
            f"manifest={manifest_path.resolve()}",
            "--script",
            str(ASEPRITE_ANIMATION_SCRIPT),
        ],
        cwd=PROJECT_ROOT,
        check=True,
    )


def export_animation_gifs(
    source: Path,
    tags: list[str],
    output_dir: Path,
) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    previews: list[Path] = []
    for tag in tags:
        for scale in (1, 8):
            destination = output_dir / f"{tag}-{scale}x.gif"
            subprocess.run(
                [
                    str(aseprite_binary()),
                    "--batch",
                    str(source),
                    "--tag",
                    tag,
                    "--scale",
                    str(scale),
                    "--save-as",
                    str(destination),
                ],
                cwd=PROJECT_ROOT,
                check=True,
            )
            if not destination.is_file():
                raise ReviewError(
                    f"Aseprite did not export animation preview {destination}"
                )
            previews.append(destination)
    return previews


def render_draft_frame(
    source: Path,
    destination: Path,
    *,
    scale: float,
    opacity: float,
) -> Path:
    """Render one deterministic nearest-neighbour FX draft frame."""
    with Image.open(source) as opened:
        image = opened.convert("RGBA")
    width, height = image.size
    target_width = max(1, round(width * scale))
    target_height = max(1, round(height * scale))
    scaled = image.resize(
        (target_width, target_height),
        Image.Resampling.NEAREST,
    )
    if opacity < 1.0:
        alpha = scaled.getchannel("A").point(
            lambda value: round(value * max(0.0, opacity))
        )
        scaled.putalpha(alpha)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.alpha_composite(
        scaled,
        ((width - target_width) // 2, (height - target_height) // 2),
    )
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination)
    return destination


def write_animation_manifest(
    destination: Path,
    *,
    canvas: tuple[int, int],
    palette: Path,
    output: Path,
    clips: list[dict[str, Any]],
) -> Path:
    document = {
        "schema_version": 1,
        "canvas": list(canvas),
        "palette": str(palette.resolve()),
        "output": str(output.resolve()),
        "clips": clips,
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(
        json.dumps(document, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return destination


def _handoff_shot_sources(
    candidate_dir: Path,
    handoff: dict[str, Any],
) -> dict[str, Path]:
    sources: dict[str, Path] = {}
    for item in handoff["shots"]:
        shot_id = str(item["id"])
        conformed_value = item.get("conformed_path")
        if conformed_value:
            conformed = project_path(conformed_value)
        else:
            aseprite_source = project_path(item["aseprite_path"])
            conformed = (
                candidate_dir / "conformed-shots" / f"{shot_id}.png"
            )
            export_aseprite(aseprite_source, conformed)
        if not conformed.is_file():
            raise ReviewError(
                f"Conformed animation shot is missing: {conformed}"
            )
        sources[shot_id] = conformed
    return sources


def build_animation_draft(
    store: ReviewStore,
    candidate_id: str,
    *,
    timing_scale: float = 1.0,
) -> Path:
    """Build editable Aseprite timelines and animated review previews."""
    if not 0.5 <= timing_scale <= 2.0:
        raise ReviewError("Animation timing_scale must be in 0.5..2.0")
    candidate = store.get_candidate(candidate_id)
    job = store.get_job(candidate["job_id"])
    recipe = recipe_from_job(job)
    if not recipe.is_multi_shot:
        raise ReviewError(
            f"Recipe {recipe.id} has no multi-shot animation source set"
        )
    if not candidate["aseprite_path"]:
        prepare_candidate(store, candidate_id)
        candidate = store.get_candidate(candidate_id)
    handoff_path = project_path(candidate["aseprite_path"])
    if handoff_path.name != "aseprite-handoff.json":
        raise ReviewError(
            f"Candidate {candidate_id} has no Aseprite shot handoff"
        )
    handoff = json.loads(handoff_path.read_text(encoding="utf-8"))
    candidate_dir = project_path(candidate["raw_path"]).parent
    sources = _handoff_shot_sources(candidate_dir, handoff)
    palette = project_path(recipe.output["palette"])
    animation_dir = candidate_dir / "animation"
    outputs: list[dict[str, Any]] = []

    if recipe.purpose["category"] in ("actor", "environment"):
        declared_clips = recipe.animation.get("draft", {}).get("clips", [])
        if not declared_clips:
            raise ReviewError(
                f"{recipe.purpose['category'].title()} recipe {recipe.id} "
                "has no animation.draft.clips"
            )
        clips: list[dict[str, Any]] = []
        for clip in declared_clips:
            duration = max(
                1,
                round(1000.0 / int(clip["fps"]) * timing_scale),
            )
            clips.append(
                {
                    "tag": str(clip["tag"]),
                    "loop": bool(clip.get("loop", False)),
                    "frames": [
                        {
                            "source": str(sources[str(shot_id)].resolve()),
                            "duration_ms": duration,
                        }
                        for shot_id in clip["frames"]
                    ],
                }
            )
        source_path = animation_dir / f"{recipe.slot}.aseprite"
        manifest_path = write_animation_manifest(
            animation_dir / f"{recipe.slot}.animation.json",
            canvas=recipe.canvas,
            palette=palette,
            output=source_path,
            clips=clips,
        )
        build_aseprite_animation(manifest_path)
        previews = export_animation_gifs(
            source_path,
            [str(clip["tag"]) for clip in clips],
            animation_dir / "previews",
        )
        outputs.append(
            {
                "slot": recipe.slot,
                "aseprite_path": relative_project_path(source_path),
                "manifest_path": relative_project_path(manifest_path),
                "tags": [str(clip["tag"]) for clip in clips],
                "preview_paths": [
                    relative_project_path(path) for path in previews
                ],
            }
        )
    elif recipe.purpose["category"] == "effect":
        shots_by_id = {shot.id: shot for shot in recipe.shots}
        for shot_id, source in sources.items():
            shot = shots_by_id[shot_id]
            is_status = shot_id.startswith("fx-status-")
            if is_status:
                scales = (0.92, 0.96, 1.0, 1.04, 1.0, 0.96, 0.92, 0.90)
                opacities = (0.82, 0.90, 1.0, 0.92, 1.0, 0.90, 0.82, 0.78)
                tag = "idle-loop"
                fps = 12
                loop = True
            else:
                scales = (0.45, 0.72, 1.0, 1.08, 1.0, 0.82)
                opacities = (0.85, 1.0, 1.0, 0.82, 0.48, 0.16)
                tag = "burst"
                fps = 30
                loop = False
            frame_dir = animation_dir / "draft-frames" / shot_id
            frame_paths = [
                render_draft_frame(
                    source,
                    frame_dir / f"{index:02d}.png",
                    scale=scale,
                    opacity=opacity,
                )
                for index, (scale, opacity) in enumerate(
                    zip(scales, opacities, strict=True),
                    start=1,
                )
            ]
            duration = max(1, round(1000.0 / fps * timing_scale))
            clip = {
                "tag": tag,
                "loop": loop,
                "frames": [
                    {
                        "source": str(path.resolve()),
                        "duration_ms": duration,
                    }
                    for path in frame_paths
                ],
            }
            source_path = animation_dir / f"{shot_id}.aseprite"
            manifest_path = write_animation_manifest(
                animation_dir / f"{shot_id}.animation.json",
                canvas=shot.output_canvas or recipe.canvas,
                palette=palette,
                output=source_path,
                clips=[clip],
            )
            build_aseprite_animation(manifest_path)
            previews = export_animation_gifs(
                source_path,
                [tag],
                animation_dir / "previews" / shot_id,
            )
            outputs.append(
                {
                    "slot": shot.slot or shot_id,
                    "aseprite_path": relative_project_path(source_path),
                    "manifest_path": relative_project_path(manifest_path),
                    "tags": [tag],
                    "preview_paths": [
                        relative_project_path(path) for path in previews
                    ],
                }
            )
    else:
        raise ReviewError(
            f"Recipe category {recipe.purpose['category']!r} has no "
            "animation draft strategy"
        )

    aggregate_path = animation_dir / "animation-draft.json"
    aggregate = {
        "schema_version": 1,
        "candidate_id": candidate_id,
        "recipe_id": recipe.id,
        "recipe_hash": recipe.digest,
        "timing_scale": timing_scale,
        "outputs": outputs,
        "note": (
            "Deterministic review draft only. Fix foot baseline, silhouette, "
            "timing, and in-betweens in Aseprite before Unity promotion."
        ),
    }
    aggregate_path.write_text(
        json.dumps(aggregate, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    store.enqueue_outbox(
        "candidate_animation_ready",
        {
            "candidate_id": candidate_id,
            "manifest_path": relative_project_path(aggregate_path),
            "timing_scale": timing_scale,
        },
    )
    return aggregate_path


def prepare_candidate(
    store: ReviewStore,
    candidate_id: str,
) -> tuple[Path, Path, Path]:
    candidate = store.get_candidate(candidate_id)
    job = store.get_job(candidate["job_id"])
    recipe = recipe_from_job(job)
    raw_path = project_path(candidate["raw_path"])
    candidate_dir = raw_path.parent
    prepared_path = candidate_dir / "prepared.png"
    aseprite_path = candidate_dir / "candidate.aseprite"
    preview_path = candidate_dir / "conformed.png"
    width, height = recipe.canvas
    output = recipe.output
    key_color = output.get("key_color")

    store.set_candidate_status(candidate_id, "preparing")
    if recipe.is_multi_shot:
        manifest = load_shot_manifest(candidate_dir)
        shots_by_id = {shot.id: shot for shot in recipe.shots}
        prepared_images: list[tuple[str, Path]] = []
        prepared_shots: list[dict[str, Any]] = []
        aseprite_dir = candidate_dir / "aseprite"
        for item in manifest["shots"]:
            shot_id = str(item["id"])
            if shot_id not in shots_by_id:
                raise ReviewError(
                    f"Manifest shot {shot_id!r} is absent from recipe {recipe.id}"
                )
            shot = shots_by_id[shot_id]
            shot_width, shot_height = shot.output_canvas or recipe.canvas
            shot_raw = project_path(item["raw_path"])
            shot_prepared = candidate_dir / "prepared-shots" / f"{shot_id}.png"
            shot_aseprite = aseprite_dir / f"{shot_id}.aseprite"
            shot_conformed = (
                candidate_dir / "conformed-shots" / f"{shot_id}.png"
            )
            if str(key_color).lower() == "auto":
                with Image.open(shot_raw) as source_image:
                    shot_key = art_asset.detect_border_color(source_image)
            elif key_color:
                shot_key = art_asset.parse_hex_color(str(key_color))
            else:
                shot_key = None
            art_asset.prepare_image(
                shot_raw,
                shot_prepared,
                width=shot_width,
                height=shot_height,
                fit=str(output.get("fit", "contain")),
                anchor=str(output.get("anchor", "bottom")),
                padding=int(output.get("padding", 2)),
                alpha_cutoff=int(output.get("alpha_cutoff", 80)),
                key_color=shot_key,
                key_tolerance=int(output.get("key_tolerance", 8)),
                trim_detached=bool(output.get("trim_detached", False)),
            )
            art_asset.conform_to_aseprite(
                shot_prepared,
                shot_aseprite,
                width=shot_width,
                height=shot_height,
                force=True,
            )
            export_aseprite(shot_aseprite, shot_conformed)
            enforce_color_area_limits(
                shot_conformed,
                recipe.document.get("quality_gates", {}),
                alpha_cutoff=int(output.get("alpha_cutoff", 80)),
            )
            prepared_images.append((shot.label, shot_conformed))
            prepared_shots.append(
                {
                    **item,
                    "prepared_path": relative_project_path(shot_prepared),
                    "aseprite_path": relative_project_path(shot_aseprite),
                    "conformed_path": relative_project_path(shot_conformed),
                }
            )
        review_sheet(prepared_images, preview_path)
        handoff_manifest = write_shot_manifest(
            candidate_dir / "aseprite-handoff.json",
            recipe=recipe,
            seed=int(manifest["candidate_seed"]),
            shots=prepared_shots,
        )
        store.set_candidate_status(
            candidate_id,
            "prepared",
            prepared_path=preview_path,
            aseprite_path=handoff_manifest,
        )
        store.enqueue_outbox(
            "candidate_prepared",
            {
                "candidate_id": candidate_id,
                "preview_path": str(preview_path),
                "handoff_manifest": str(handoff_manifest),
            },
        )
        return preview_path, handoff_manifest, preview_path

    if str(key_color).lower() == "auto":
        with Image.open(raw_path) as source_image:
            parsed_key = art_asset.detect_border_color(source_image)
    elif key_color:
        parsed_key = art_asset.parse_hex_color(str(key_color))
    else:
        parsed_key = None
    art_asset.prepare_image(
        raw_path,
        prepared_path,
        width=width,
        height=height,
        fit=str(output.get("fit", "contain")),
        anchor=str(output.get("anchor", "bottom")),
        padding=int(output.get("padding", 2)),
        alpha_cutoff=int(output.get("alpha_cutoff", 80)),
        key_color=parsed_key,
        key_tolerance=int(output.get("key_tolerance", 8)),
        trim_detached=bool(output.get("trim_detached", False)),
    )
    art_asset.conform_to_aseprite(
        prepared_path,
        aseprite_path,
        width=width,
        height=height,
        force=True,
    )
    export_aseprite(aseprite_path, preview_path)
    enforce_color_area_limits(
        preview_path,
        recipe.document.get("quality_gates", {}),
        alpha_cutoff=int(output.get("alpha_cutoff", 80)),
    )
    store.set_candidate_status(
        candidate_id,
        "prepared",
        prepared_path=preview_path,
        aseprite_path=aseprite_path,
    )
    store.enqueue_outbox(
        "candidate_prepared",
        {
            "candidate_id": candidate_id,
            "preview_path": str(preview_path),
        },
    )
    return prepared_path, aseprite_path, preview_path


def publish_candidate(
    store: ReviewStore,
    candidate_id: str,
    *,
    target_slot: str | None = None,
    apply_request_id: str | None = None,
) -> Path:
    candidate = store.get_candidate(candidate_id)
    if not store.candidate_is_approved(candidate_id):
        raise ReviewError(
            f"{candidate_id} has no current explicit approval"
        )
    if candidate["status"] not in {"approved", "prepared"}:
        raise ReviewError(
            f"{candidate_id} must be approved or prepared before publishing"
        )
    job = store.get_job(candidate["job_id"])
    recipe = recipe_from_job(job)
    if recipe.output.get("promotion", "aseprite") != "aseprite":
        raise ReviewError(
            f"Recipe {recipe.id} uses promotion "
            f"{recipe.output.get('promotion')!r}; it cannot publish directly "
            "to an Aseprite slot."
        )
    if not candidate["aseprite_path"]:
        prepare_candidate(store, candidate_id)
        candidate = store.get_candidate(candidate_id)
    source = project_path(candidate["aseprite_path"])
    slot = target_slot or recipe.slot
    try:
        art_asset.validate_slot(slot)
    except art_asset.AssetError as exc:
        raise ReviewError(str(exc)) from exc
    # 정규식은 이름의 모양만 본다. 실제로 Unity 가 읽는 슬롯인지는 발급
    # 목록만 안다 — target_slot 으로 Spark 가 넘긴 값도 여기서 걸린다.
    if not SlotCatalog().is_registered(slot):
        raise ReviewError(
            f"Slot {slot!r} is not registered in "
            f"{UNITY_SLOT_SOURCE.name} (CatalogSlots); publishing there "
            "would create an .aseprite file Unity never reads."
        )
    if not source.is_file() or source.suffix.lower() not in {
        ".aseprite",
        ".ase",
    }:
        raise ReviewError(
            f"{candidate_id} has no directly publishable Aseprite source: "
            f"{source}. Multi-shot handoffs must be finalized first."
        )
    destination = art_asset.official_output(slot)
    allow_replace = bool(recipe.output.get("allow_replace", False))
    if destination.exists() and not allow_replace:
        raise ReviewError(
            f"Official slot already exists: {destination}. Set "
            "output.allow_replace: true in a reviewed recipe version to replace it."
        )
    store.set_candidate_status(candidate_id, "publishing")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and allow_replace:
        timestamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
        request_suffix = (
            f"-{apply_request_id}" if apply_request_id else ""
        )
        backup = (
            source.parent
            / "backups"
            / (
                f"{destination.stem}-previous-{timestamp}"
                f"{request_suffix}{destination.suffix}"
            )
        )
        backup.parent.mkdir(parents=True, exist_ok=True)
        if backup.exists():
            backup = backup.with_name(
                f"{backup.stem}-{uuid.uuid4().hex[:8]}{backup.suffix}"
            )
        shutil.copy2(destination, backup)
    shutil.copy2(source, destination)
    store.set_candidate_status(
        candidate_id,
        "published",
        aseprite_path=destination,
    )
    store.enqueue_outbox(
        "candidate_published",
        {
            "candidate_id": candidate_id,
            "slot": slot,
            "path": str(destination),
            "unity_sync": "pending",
        },
    )
    return destination


def approve_candidate(
    store: ReviewStore,
    candidate_id: str,
    *,
    user_id: str,
    event_key: str,
) -> None:
    candidate = store.get_candidate(candidate_id)
    if candidate["status"] in {"failed", "published"}:
        raise ReviewError(
            f"Cannot approve {candidate_id} from {candidate['status']}"
        )
    raw_path = project_path(candidate["raw_path"])
    shot_manifest = raw_path.parent / "shot-manifest.json"
    snapshot_manifest: dict[str, Any] | None = None
    if shot_manifest.is_file():
        snapshot_manifest = json.loads(
            shot_manifest.read_text(encoding="utf-8")
        )
        shots = raw_path.parent / "shots"
        if not shots.is_dir():
            raise ReviewError(
                f"Shot manifest exists but shot sources are missing: {shots}"
            )
        for shot in snapshot_manifest.get("shots", []):
            original = project_path(shot["raw_path"])
            if not original.is_file():
                raise ReviewError(
                    f"Approval shot source is missing: {original}"
                )
    snapshot = raw_path.parent / "approvals" / make_id("APPROVAL")
    snapshot.mkdir(parents=True, exist_ok=True)
    shutil.copy2(raw_path, snapshot / "raw.png")
    if snapshot_manifest is not None:
        for shot in snapshot_manifest.get("shots", []):
            shot["raw_path"] = relative_project_path(
                snapshot / "shots" / str(shot["id"]) / "raw.png"
            )
        (snapshot / shot_manifest.name).write_text(
            json.dumps(snapshot_manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
    shots = raw_path.parent / "shots"
    if shots.is_dir():
        shutil.copytree(shots, snapshot / "shots", dirs_exist_ok=True)
    job = store.get_job(candidate["job_id"])
    (snapshot / "recipe.json").write_text(
        json.dumps(
            json.loads(job["recipe_json"]),
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (snapshot / "approval.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "candidate_id": candidate_id,
                "approved_by": user_id,
                "approved_at": utc_now(),
                "source_path": relative_project_path(raw_path),
                "recipe_id": job["recipe_id"],
                "recipe_hash": job["recipe_hash"],
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    store.set_approved_snapshot(candidate_id, snapshot)
    store.set_candidate_status(candidate_id, "approved")
    store.add_feedback(
        event_key=event_key,
        user_id=user_id,
        source="button",
        label="approve",
        candidate_id=candidate_id,
    )
    store.resolve_feedback_by_event(event_key, "deterministic approve")
    store.enqueue_outbox(
        "candidate_status",
        {"candidate_id": candidate_id, "status": "approved"},
    )


def reject_candidate(
    store: ReviewStore,
    candidate_id: str,
    *,
    user_id: str,
    event_key: str,
) -> None:
    candidate = store.get_candidate(candidate_id)
    if candidate["status"] == "published":
        raise ReviewError(f"Cannot reject published candidate {candidate_id}")
    store.set_candidate_status(candidate_id, "rejected")
    store.cancel_apply_requests_for_candidate(
        candidate_id,
        reason="Candidate was rejected after apply was requested",
    )
    store.add_feedback(
        event_key=event_key,
        user_id=user_id,
        source="button",
        label="reject",
        candidate_id=candidate_id,
    )
    store.resolve_feedback_by_event(event_key, "deterministic reject")
    store.enqueue_outbox(
        "candidate_status",
        {"candidate_id": candidate_id, "status": "rejected"},
    )


def decide_candidate_shot(
    store: ReviewStore,
    candidate_id: str,
    shot_id: str,
    decision: str,
    *,
    user_id: str,
    event_key: str,
    source: str = "shot-command",
) -> None:
    if decision not in {"approve", "reject"}:
        raise ReviewError(f"Unknown shot decision {decision!r}")
    candidate = store.get_candidate(candidate_id)
    recipe = recipe_from_job(store.get_job(candidate["job_id"]))
    if shot_id not in {shot.id for shot in recipe.shots}:
        raise ReviewError(
            f"Candidate {candidate_id} has no shot {shot_id!r}"
        )
    store.add_feedback(
        event_key=event_key,
        user_id=user_id,
        source=source,
        label=f"shot:{shot_id}:{decision}",
        candidate_id=candidate_id,
    )
    store.resolve_feedback_by_event(
        event_key,
        f"deterministic shot {decision}",
    )
    store.enqueue_outbox(
        "shot_status",
        {
            "candidate_id": candidate_id,
            "shot_id": shot_id,
            "status": decision,
        },
    )


def process_action(
    store: ReviewStore,
    action: Any,
) -> None:
    payload = json.loads(action["payload_json"])
    try:
        if action["kind"] == "prepare":
            prepare_candidate(store, action["candidate_id"])
        elif action["kind"] == "publish":
            publish_candidate(store, action["candidate_id"])
        elif action["kind"] in {"variation", "shot_variation"}:
            candidate = store.get_candidate(action["candidate_id"])
            parent_job = store.get_job(candidate["job_id"])
            recipe = recipe_from_job(parent_job)
            shot_id = str(payload.get("shot_id", "")).strip()
            if action["kind"] == "shot_variation":
                if not shot_id:
                    raise ReviewError("shot_variation requires shot_id")
                recipe = recipe.only_shot(shot_id)
            count = int(payload.get("count", recipe.candidate_count))
            job_id = store.create_job(
                recipe,
                requested_by=action["requested_by"],
                candidate_count=count,
                notes=(
                    payload.get("notes")
                    or (
                        f"Variation of {shot_id} from {candidate['id']}"
                        if shot_id
                        else f"Variation from {candidate['id']}"
                    )
                ),
                parent_candidate_id=candidate["id"],
            )
            store.enqueue_outbox(
                "job_queued",
                {
                    "job_id": job_id,
                    "parent_candidate_id": candidate["id"],
                    "shot_id": shot_id or None,
                },
            )
        elif action["kind"] == "animation_draft":
            build_animation_draft(
                store,
                action["candidate_id"],
                timing_scale=float(payload.get("timing_scale", 1.0)),
            )
        else:
            raise ReviewError(f"Unknown action kind {action['kind']!r}")
        store.finish_action(action["id"])
    except Exception as exc:
        store.finish_action(action["id"], error=str(exc))
        store.enqueue_outbox(
            "action_failed",
            {
                "action_id": action["id"],
                "candidate_id": action["candidate_id"],
                "error": str(exc),
            },
        )
        raise


def work_once(
    store: ReviewStore,
    *,
    comfy_url: str,
    output_root: Path,
    timeout: float,
) -> bool:
    action = store.claim_action()
    if action is not None:
        process_action(store, action)
        return True
    job = store.claim_job()
    if job is not None:
        process_job(
            store,
            job,
            comfy_url=comfy_url,
            output_root=output_root,
            timeout=timeout,
        )
        return True
    return False


def command_init(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    recipes = RecipeRegistry(args.recipe_dir).load_all()
    for recipe in recipes.values():
        recipe.validate_files()
    json_print(
        {
            "database": str(store.path),
            "recipes": [recipe.summary() for recipe in recipes.values()],
        }
    )


def command_recipes(args: argparse.Namespace) -> None:
    recipes = RecipeRegistry(args.recipe_dir).load_all()
    if args.recipe_id:
        recipe = recipes.get(args.recipe_id)
        if recipe is None:
            raise ReviewError(f"Unknown recipe {args.recipe_id}")
        json_print(recipe.summary())
        return
    asset_type = getattr(args, "asset_type", None)
    if asset_type and asset_type not in VALID_ASSET_TYPES:
        known = ", ".join(sorted(VALID_ASSET_TYPES))
        raise ReviewError(
            f"Unknown asset type {asset_type!r}; expected one of: {known}"
        )
    # Slack 목록과 같은 순서로 낸다 — 같은 축을 두 UI가 다르게 정렬하면
    # "3번째 캐릭터 레시피"라는 말이 통하지 않는다.
    order = {type_id: index for index, (type_id, _) in enumerate(ASSET_TYPES)}
    selected = [
        recipe
        for recipe in recipes.values()
        if asset_type is None or recipe.asset_type == asset_type
    ]
    selected.sort(
        key=lambda recipe: (
            order.get(recipe.asset_type, len(order)),
            recipe.id,
        )
    )
    json_print([recipe.summary() for recipe in selected])


def command_slots(args: argparse.Namespace) -> None:
    """Unity 슬롯 발급 목록 + 각 슬롯을 실제로 채우는 것이 무엇인지."""
    slots = SlotCatalog().load_all()
    recipes = RecipeRegistry(args.recipe_dir).load_all().values()
    by_slot: dict[str, list[str]] = {}
    for recipe in recipes:
        for slot in recipe.target_slots:
            by_slot.setdefault(slot, []).append(recipe.id)

    catalog = SlotCatalog()
    rows = []
    for slot, field in sorted(slots.items()):
        if args.prefix and not slot.startswith(args.prefix):
            continue
        covering = sorted(by_slot.get(slot, []))
        display_name, description = catalog.describe(slot)
        rows.append(
            {
                "slot": slot,
                "display_name": display_name,
                "description": description,
                "unity_field": field,
                "aseprite_source": (
                    relative_project_path(art_asset.official_output(slot))
                    if art_asset.official_output(slot).is_file()
                    else None
                ),
                "recipes": covering,
            }
        )
    if args.uncovered:
        rows = [row for row in rows if not row["recipes"]]
    json_print(rows)


def command_workflow_types(args: argparse.Namespace) -> None:
    types = WorkflowTypeRegistry().load_all()
    if args.type_id:
        json_print(WorkflowTypeRegistry().get(args.type_id).summary())
        return
    json_print([entry.summary() for entry in types.values()])


def resolve_batch_jobs(
    store: ReviewStore,
    batch_id: str,
    *,
    batch_dir: Path,
    recipe_dir: Path,
) -> tuple[Any, list[tuple[str, Recipe, int, str | None, str]]]:
    plan = BatchRegistry(batch_dir).get(batch_id)
    recipes = RecipeRegistry(recipe_dir)
    plan.validate_recipes(recipes)
    jobs: list[tuple[str, Recipe, int, str | None, str]] = []
    for item in plan.items:
        recipe = recipes.get(item.recipe_id)
        shot_id = item.shot or store.next_batch_shot(
            plan.id,
            item.id,
            item.shot_cycle,
        )
        if shot_id:
            recipe = recipe.only_shot(shot_id)
        recipe.validate_files()
        note_parts = [item.notes]
        if shot_id:
            note_parts.append(f"rotating shot: {shot_id}")
        jobs.append(
            (
                item.id,
                recipe,
                item.candidate_count,
                shot_id,
                " · ".join(part for part in note_parts if part),
            )
        )
    return plan, jobs


def command_batches(args: argparse.Namespace) -> None:
    batches = BatchRegistry(args.batch_dir).load_all()
    recipes = RecipeRegistry(args.recipe_dir)
    if args.batch_id:
        plan = batches.get(args.batch_id)
        if plan is None:
            raise ReviewError(f"Unknown batch {args.batch_id}")
        plan.validate_recipes(recipes)
        json_print(plan.summary())
        return
    for plan in batches.values():
        plan.validate_recipes(recipes)
    json_print([plan.summary() for plan in batches.values()])


def command_batch_submit(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    plan, jobs = resolve_batch_jobs(
        store,
        args.batch_id,
        batch_dir=args.batch_dir,
        recipe_dir=args.recipe_dir,
    )
    run_id, job_ids = store.create_batch_run(
        plan,
        requested_by=args.requested_by,
        jobs=jobs,
        notes=args.notes,
    )
    store.enqueue_outbox(
        "batch_queued",
        {
            "batch_id": run_id,
            "plan_id": plan.id,
            "job_ids": job_ids,
        },
    )
    json_print({"batch_id": run_id, "job_ids": job_ids})


def command_batch_runs(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    if args.batch_id:
        json_print(store.get_batch_run(args.batch_id))
        return
    json_print(store.list_batch_runs(limit=args.limit))


def command_submit(args: argparse.Namespace) -> None:
    recipe = RecipeRegistry(args.recipe_dir).get(args.recipe_id)
    if args.shot:
        recipe = recipe.only_shot(args.shot)
    recipe.validate_files()
    job_id = ReviewStore(args.db).create_job(
        recipe,
        requested_by=args.requested_by,
        candidate_count=args.count,
        base_seed=args.seed,
        notes=args.notes,
        parent_candidate_id=args.parent_candidate,
    )
    print(job_id)


def command_compose_submit(args: argparse.Namespace) -> None:
    """화풍×세계관×대상×방법을 Slack 폼과 같은 계약으로 큐에 넣는다."""
    store = ReviewStore(args.db)
    method = MethodRegistry().get(args.method_id)
    if method.requires_source_candidate and not args.source_candidate:
        raise ReviewError(
            f"Method {method.id} requires --source-candidate"
        )
    recipe = resolve_by_id(
        args.method_id,
        args.target_id,
        style_id=args.style,
        world_id=args.world,
    )
    if args.source_candidate:
        recipe = recipe.with_source_image(
            store.approved_candidate_source(args.source_candidate)
        )
    positive = args.positive
    if positive is None:
        positive = str(recipe.prompt["positive"])
    if args.positive_suffix:
        positive = ", ".join(
            part.strip().strip(",")
            for part in (positive, args.positive_suffix)
            if part.strip()
        )
    default_definition = definition_for_target(args.target_id)
    if (
        args.target_definition
        and args.target_definition != default_definition
    ):
        if default_definition and default_definition in positive:
            positive = positive.replace(
                default_definition,
                args.target_definition,
                1,
            )
        elif args.target_definition not in positive:
            positive = f"{positive.strip().strip(',')}, {args.target_definition}"
    recipe = recipe.with_overrides(
        checkpoint=args.checkpoint,
        positive=positive,
        negative=args.negative,
        steps=args.steps,
        cfg=args.cfg,
        denoise=args.denoise,
    )
    recipe.validate_files()
    notes = args.notes
    if args.positive_suffix:
        notes = (
            f"생성 내용: {args.positive_suffix}\n{notes}".strip()
        )
    if (
        args.target_definition
        and args.target_definition != default_definition
    ):
        notes = (
            f"대상 정의: {args.target_definition}\n{notes}".strip()
        )
    job_id = store.create_job(
        recipe,
        requested_by=args.requested_by,
        candidate_count=args.count,
        base_seed=args.seed,
        notes=notes,
        parent_candidate_id=args.source_candidate,
    )
    print(job_id)


def command_jobs(args: argparse.Namespace) -> None:
    rows = ReviewStore(args.db).list_jobs(
        status=args.status,
        limit=args.limit,
    )
    json_print([row_dict(row) for row in rows])


def command_job(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    job = row_dict(store.get_job(args.job_id))
    job["recipe_json"] = json.loads(job["recipe_json"])
    job["candidates"] = [
        {
            **row_dict(candidate),
            "metrics_json": json.loads(candidate["metrics_json"]),
        }
        for candidate in store.list_candidates(args.job_id)
    ]
    json_print(job)


def command_queue(args: argparse.Namespace) -> None:
    jobs = ReviewStore(args.db).list_jobs(limit=args.limit)
    visible = (
        jobs
        if args.all
        else [
            job
            for job in jobs
            if job["status"] in {"queued", "running", "failed"}
        ]
    )
    json_print([row_dict(job) for job in visible])


def command_job_control(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    if args.control == "cancel":
        store.cancel_job(args.job_id)
        kind = "job_cancelled"
    else:
        store.retry_job(args.job_id)
        kind = "job_queued"
    store.enqueue_outbox(kind, {"job_id": args.job_id})
    print(args.job_id)


def command_apply_request(args: argparse.Namespace) -> None:
    request_id = ReviewStore(args.db).create_apply_request(
        args.candidate_id,
        requested_by=args.requested_by,
        intent=args.intent,
    )
    print(request_id)


def command_apply_requests(args: argparse.Namespace) -> None:
    rows = ReviewStore(args.db).list_apply_requests(
        status=args.status,
        limit=args.limit,
    )
    json_print([row_dict(row) for row in rows])


def command_claim_apply(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    request = store.claim_apply_request(args.request_id)
    if request is None:
        json_print({})
        return
    json_print(store.apply_context(request["id"]))


def load_optional_json(
    path: Path | None,
    raw: str | None,
) -> dict[str, Any] | None:
    if path is not None and raw is not None:
        raise ReviewError("Use either a JSON file or inline JSON, not both")
    if path is None and raw is None:
        return None
    try:
        value = json.loads(
            path.read_text(encoding="utf-8") if path is not None else raw
        )
    except (OSError, json.JSONDecodeError) as exc:
        raise ReviewError(f"Cannot load JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise ReviewError("Apply metadata must be a JSON object")
    return value


def command_apply_status(args: argparse.Namespace) -> None:
    ReviewStore(args.db).set_apply_request_status(
        args.request_id,
        args.status,
        plan=load_optional_json(args.plan_file, args.plan_json),
        result=load_optional_json(args.result_file, args.result_json),
        error=args.error,
    )


def command_publish(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    request = store.get_apply_request(args.apply_request)
    if request["candidate_id"] != args.candidate_id:
        raise ReviewError(
            f"Apply request {request['id']} belongs to "
            f"{request['candidate_id']}, not {args.candidate_id}"
        )
    if request["status"] not in {"planning", "applying"}:
        raise ReviewError(
            f"Apply request {request['id']} cannot publish from "
            f"{request['status']}"
        )
    store.set_apply_request_status(
        request["id"],
        "applying",
        expected_statuses=("planning", "applying"),
    )
    try:
        destination = publish_candidate(
            store,
            args.candidate_id,
            target_slot=args.target_slot,
            apply_request_id=request["id"],
        )
    except Exception as exc:
        store.set_apply_request_status(
            request["id"],
            "failed",
            error=str(exc),
        )
        raise
    print(destination)


def command_work(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    if args.job_id:
        job = store.claim_job(args.job_id)
        if job is None:
            raise ReviewError(
                f"Job {args.job_id} is not queued or does not exist"
            )
        process_job(
            store,
            job,
            comfy_url=args.comfy_url,
            output_root=args.output_root,
            timeout=args.timeout,
        )
        return
    if args.once:
        work_once(
            store,
            comfy_url=args.comfy_url,
            output_root=args.output_root,
            timeout=args.timeout,
        )
        return
    next_recovery = 0.0
    while not STOP_REQUESTED:
        if time.monotonic() >= next_recovery:
            store.recover_stale_running(
                older_than_seconds=STALE_RUNNING_SECONDS
            )
            next_recovery = time.monotonic() + 60.0
        try:
            worked = work_once(
                store,
                comfy_url=args.comfy_url,
                output_root=args.output_root,
                timeout=args.timeout,
            )
        except Exception:
            traceback.print_exc()
            worked = False
        if not worked:
            time.sleep(args.poll_interval)


def command_candidate_action(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    event_key = f"cli:{args.action}:{args.candidate_id}:{uuid.uuid4().hex}"
    if args.action == "approve":
        approve_candidate(
            store,
            args.candidate_id,
            user_id=args.requested_by,
            event_key=event_key,
        )
    elif args.action == "reject":
        reject_candidate(
            store,
            args.candidate_id,
            user_id=args.requested_by,
            event_key=event_key,
        )
    else:
        payload: dict[str, Any] = {}
        if args.action == "variation":
            payload["count"] = args.count
            payload["notes"] = args.notes
        elif args.action == "shot_variation":
            payload["count"] = args.count
            payload["notes"] = args.notes
            payload["shot_id"] = args.shot_id
        elif args.action == "animation_draft":
            payload["timing_scale"] = args.timing_scale
        action_id = store.enqueue_action(
            args.action,
            requested_by=args.requested_by,
            candidate_id=args.candidate_id,
            payload=payload,
        )
        print(action_id)


def command_shot_decision(args: argparse.Namespace) -> None:
    decide_candidate_shot(
        ReviewStore(args.db),
        args.candidate_id,
        args.shot_id,
        args.decision,
        user_id=args.requested_by,
        event_key=(
            f"cli:shot:{args.decision}:{args.candidate_id}:"
            f"{args.shot_id}:{uuid.uuid4().hex}"
        ),
    )


def command_feedback(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    inserted = store.add_feedback(
        event_key=args.event_key or f"cli:{uuid.uuid4().hex}",
        user_id=args.user,
        source=args.source,
        label=args.label,
        text=args.text,
        job_id=args.job_id,
        candidate_id=args.candidate_id,
    )
    print("inserted" if inserted else "duplicate")


def command_pending_feedback(args: argparse.Namespace) -> None:
    rows = ReviewStore(args.db).pending_feedback(args.limit)
    json_print([row_dict(row) for row in rows])


def command_feedback_context(args: argparse.Namespace) -> None:
    store = ReviewStore(args.db)
    result = []
    for feedback in store.pending_feedback(args.limit):
        item = row_dict(feedback)
        job = store.get_job(feedback["job_id"])
        recipe = recipe_from_job(job)
        item["job"] = {
            "id": job["id"],
            "status": job["status"],
            "notes": job["notes"],
            "recipe_id": job["recipe_id"],
            "recipe_path": job["recipe_path"],
            "recipe_hash": job["recipe_hash"],
        }
        item["recipe"] = recipe.summary()
        if feedback["candidate_id"]:
            candidate = store.get_candidate(feedback["candidate_id"])
            item["candidate"] = {
                **row_dict(candidate),
                "metrics_json": json.loads(candidate["metrics_json"]),
                "raw_path": str(project_path(candidate["raw_path"])),
                "prepared_path": (
                    str(project_path(candidate["prepared_path"]))
                    if candidate["prepared_path"]
                    else None
                ),
            }
        result.append(item)
    json_print(result)


def command_feedback_progress(args: argparse.Namespace) -> None:
    started = ReviewStore(args.db).start_feedback(
        args.feedback_id,
        args.detail,
    )
    print("started" if started else "already-processing")


def command_resolve_feedback(args: argparse.Namespace) -> None:
    ReviewStore(args.db).resolve_feedback(args.feedback_id, args.resolution)


def candidate_count_arg(value: str) -> int:
    try:
        count = int(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(
            "candidate count must be an integer"
        ) from exc
    if not 1 <= count <= 12:
        raise argparse.ArgumentTypeError(
            "candidate count must be in 1..12"
        )
    return count


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH)
    parser.add_argument(
        "--recipe-dir",
        type=Path,
        default=DEFAULT_RECIPE_DIR,
    )
    parser.add_argument(
        "--batch-dir",
        type=Path,
        default=DEFAULT_BATCH_DIR,
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    init = subparsers.add_parser("init")
    init.set_defaults(handler=command_init)

    recipes = subparsers.add_parser("recipes")
    recipes.add_argument("recipe_id", nargs="?")
    recipes.add_argument(
        "--asset-type",
        dest="asset_type",
        help="에셋 타입으로 거른다: " + ", ".join(
            type_id for type_id, _ in ASSET_TYPES
        ),
    )
    recipes.set_defaults(handler=command_recipes)

    slots = subparsers.add_parser("slots")
    slots.add_argument(
        "prefix",
        nargs="?",
        help="슬롯 접두사로 거른다. 예: actor-",
    )
    slots.add_argument(
        "--uncovered",
        action="store_true",
        help="아직 레시피가 없는 슬롯만 보여준다",
    )
    slots.set_defaults(handler=command_slots)

    workflow_types = subparsers.add_parser("workflow-types")
    workflow_types.add_argument("type_id", nargs="?")
    workflow_types.set_defaults(handler=command_workflow_types)

    batches = subparsers.add_parser("batches")
    batches.add_argument("batch_id", nargs="?")
    batches.set_defaults(handler=command_batches)

    batch_submit = subparsers.add_parser("batch-submit")
    batch_submit.add_argument("batch_id")
    batch_submit.add_argument("--notes", default="")
    batch_submit.add_argument("--requested-by", default="cli")
    batch_submit.set_defaults(handler=command_batch_submit)

    batch_runs = subparsers.add_parser("batch-runs")
    batch_runs.add_argument("batch_id", nargs="?")
    batch_runs.add_argument("--limit", type=int, default=20)
    batch_runs.set_defaults(handler=command_batch_runs)

    submit = subparsers.add_parser("submit")
    submit.add_argument("recipe_id")
    submit.add_argument("--count", type=candidate_count_arg)
    submit.add_argument("--seed", type=int)
    submit.add_argument("--notes", default="")
    submit.add_argument("--parent-candidate")
    submit.add_argument(
        "--shot",
        help="Submit one declared multi-shot key pose for a cheap test run",
    )
    submit.add_argument("--requested-by", default="cli")
    submit.set_defaults(handler=command_submit)

    compose_submit = subparsers.add_parser(
        "compose-submit",
        help="Queue a style × world × subject × method job",
    )
    compose_submit.add_argument("target_id")
    compose_submit.add_argument("method_id")
    compose_submit.add_argument("--style", required=True)
    compose_submit.add_argument("--world", required=True)
    compose_submit.add_argument("--count", type=candidate_count_arg)
    compose_submit.add_argument("--seed", type=int)
    compose_submit.add_argument("--steps", type=int)
    compose_submit.add_argument("--cfg", type=float)
    compose_submit.add_argument("--denoise", type=float)
    compose_submit.add_argument("--checkpoint")
    compose_submit.add_argument("--positive")
    compose_submit.add_argument("--positive-suffix")
    compose_submit.add_argument("--target-definition")
    compose_submit.add_argument("--negative")
    compose_submit.add_argument("--source-candidate")
    compose_submit.add_argument("--notes", default="")
    compose_submit.add_argument("--requested-by", default="manual")
    compose_submit.set_defaults(handler=command_compose_submit)

    jobs = subparsers.add_parser("jobs")
    jobs.add_argument("--status")
    jobs.add_argument("--limit", type=int, default=50)
    jobs.set_defaults(handler=command_jobs)

    job = subparsers.add_parser("job")
    job.add_argument("job_id")
    job.set_defaults(handler=command_job)

    queue = subparsers.add_parser("queue")
    queue.add_argument("--limit", type=int, default=50)
    queue.add_argument("--all", action="store_true")
    queue.set_defaults(handler=command_queue)

    for command_name in ("cancel", "retry"):
        control = subparsers.add_parser(command_name)
        control.add_argument("job_id")
        control.set_defaults(
            handler=command_job_control,
            control=command_name,
        )

    apply_request = subparsers.add_parser("apply-request")
    apply_request.add_argument("candidate_id")
    apply_request.add_argument("--intent", default="")
    apply_request.add_argument("--requested-by", default="cli")
    apply_request.set_defaults(handler=command_apply_request)

    apply_requests = subparsers.add_parser("apply-requests")
    apply_requests.add_argument("--status")
    apply_requests.add_argument("--limit", type=int, default=50)
    apply_requests.set_defaults(handler=command_apply_requests)

    claim_apply = subparsers.add_parser("claim-apply")
    claim_apply.add_argument("request_id", nargs="?")
    claim_apply.set_defaults(handler=command_claim_apply)

    apply_status = subparsers.add_parser("apply-status")
    apply_status.add_argument("request_id")
    apply_status.add_argument(
        "status",
        choices=(
            "queued",
            "planning",
            "applying",
            "needs_input",
            "complete",
            "failed",
            "cancelled",
        ),
    )
    apply_status.add_argument("--plan-file", type=Path)
    apply_status.add_argument("--plan-json")
    apply_status.add_argument("--result-file", type=Path)
    apply_status.add_argument("--result-json")
    apply_status.add_argument("--error")
    apply_status.set_defaults(handler=command_apply_status)

    work = subparsers.add_parser("work")
    work.add_argument("--job-id")
    work.add_argument("--once", action="store_true")
    work.add_argument("--comfy-url", default=DEFAULT_COMFY_URL)
    work.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    work.add_argument("--timeout", type=float, default=1800.0)
    work.add_argument("--poll-interval", type=float, default=5.0)
    work.set_defaults(handler=command_work)

    for action_name in (
        "approve",
        "reject",
        "prepare",
        "animation",
    ):
        action = subparsers.add_parser(action_name)
        action.add_argument("candidate_id")
        action.add_argument("--requested-by", default="cli")
        if action_name == "animation":
            action.add_argument("--timing-scale", type=float, default=1.0)
        action.set_defaults(
            handler=command_candidate_action,
            action=(
                "animation_draft"
                if action_name == "animation"
                else action_name
            ),
        )

    publish = subparsers.add_parser("publish")
    publish.add_argument("candidate_id")
    publish.add_argument("--apply-request", required=True)
    publish.add_argument("--target-slot")
    publish.set_defaults(handler=command_publish)

    variation = subparsers.add_parser("variation")
    variation.add_argument("candidate_id")
    variation.add_argument("--count", type=candidate_count_arg, default=4)
    variation.add_argument("--notes", default="")
    variation.add_argument("--requested-by", default="cli")
    variation.set_defaults(
        handler=command_candidate_action,
        action="variation",
    )

    shot_variation = subparsers.add_parser("shot-variation")
    shot_variation.add_argument("candidate_id")
    shot_variation.add_argument("shot_id")
    shot_variation.add_argument(
        "--count",
        type=candidate_count_arg,
        default=2,
    )
    shot_variation.add_argument("--notes", default="")
    shot_variation.add_argument("--requested-by", default="cli")
    shot_variation.set_defaults(
        handler=command_candidate_action,
        action="shot_variation",
    )

    for command_name, decision in (
        ("shot-approve", "approve"),
        ("shot-reject", "reject"),
    ):
        shot_decision = subparsers.add_parser(command_name)
        shot_decision.add_argument("candidate_id")
        shot_decision.add_argument("shot_id")
        shot_decision.add_argument("--requested-by", default="cli")
        shot_decision.set_defaults(
            handler=command_shot_decision,
            decision=decision,
        )

    feedback = subparsers.add_parser("feedback")
    feedback.add_argument("--job-id")
    feedback.add_argument("--candidate-id")
    feedback.add_argument("--user", default="cli")
    feedback.add_argument("--source", default="cli")
    feedback.add_argument("--label", default="")
    feedback.add_argument("--text", default="")
    feedback.add_argument("--event-key")
    feedback.set_defaults(handler=command_feedback)

    pending = subparsers.add_parser("pending-feedback")
    pending.add_argument("--limit", type=int, default=100)
    pending.set_defaults(handler=command_pending_feedback)

    context = subparsers.add_parser("feedback-context")
    context.add_argument("--limit", type=int, default=100)
    context.set_defaults(handler=command_feedback_context)

    progress = subparsers.add_parser("feedback-progress")
    progress.add_argument("feedback_id", type=int)
    progress.add_argument(
        "detail",
        nargs="?",
        default="이미지와 실행 설정을 함께 확인하고 있습니다.",
    )
    progress.set_defaults(handler=command_feedback_progress)

    resolve = subparsers.add_parser("resolve-feedback")
    resolve.add_argument("feedback_id", type=int)
    resolve.add_argument("resolution")
    resolve.set_defaults(handler=command_resolve_feedback)
    return parser


def main() -> int:
    signal.signal(signal.SIGTERM, handle_stop)
    signal.signal(signal.SIGINT, handle_stop)
    parser = build_parser()
    args = parser.parse_args()
    try:
        args.db = args.db.expanduser().resolve()
        args.recipe_dir = args.recipe_dir.expanduser().resolve()
        args.batch_dir = args.batch_dir.expanduser().resolve()
        if hasattr(args, "output_root"):
            args.output_root = args.output_root.expanduser().resolve()
        args.handler(args)
    except (
        ReviewError,
        comfy_batch.ComfyError,
        art_asset.AssetError,
        OSError,
        ValueError,
        json.JSONDecodeError,
        subprocess.CalledProcessError,
    ) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
