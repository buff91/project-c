#!/usr/bin/env python3
"""Shared recipe, queue, and review state for the Project-C art pipeline."""

from __future__ import annotations

import copy
import hashlib
import json
import os
import re
import secrets
import sqlite3
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterator

import yaml
from PIL import Image


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_RECIPE_DIR = (
    PROJECT_ROOT / "docs/art-direction/comfyui/recipes"
)
DEFAULT_STATE_DIR = Path(
    os.environ.get(
        "PROJECTC_ART_REVIEW_STATE",
        PROJECT_ROOT / "Tools/ArtPipeline/.art-review",
    )
)
DEFAULT_DB_PATH = DEFAULT_STATE_DIR / "art-review.sqlite3"
DEFAULT_OUTPUT_ROOT = (
    PROJECT_ROOT / "docs/art-direction/comfyui/output/review"
)
VALID_JOB_STATES = {
    "queued",
    "running",
    "awaiting_review",
    "failed",
    "complete",
}
VALID_CANDIDATE_STATES = {
    "generated",
    "approved",
    "rejected",
    "preparing",
    "prepared",
    "publishing",
    "published",
    "failed",
}


class ReviewError(RuntimeError):
    """Raised for invalid recipes or state transitions."""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def make_id(prefix: str) -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    return f"{prefix}-{stamp}-{secrets.token_hex(3)}"


def project_path(value: str | Path) -> Path:
    path = Path(value).expanduser()
    if not path.is_absolute():
        path = PROJECT_ROOT / path
    return path.resolve()


def relative_project_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return str(resolved)


def canonical_json(value: Any) -> str:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def recipe_digest(document: dict[str, Any]) -> str:
    return hashlib.sha256(canonical_json(document).encode("utf-8")).hexdigest()


def _mapping(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ReviewError(f"{label} must be a mapping")
    return value


def _positive_int(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ReviewError(f"{label} must be a positive integer")
    return value


SHOT_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")


@dataclass(frozen=True)
class ShotSpec:
    """One ComfyUI submission inside a multi-shot review candidate."""

    id: str
    label: str
    slot: str | None = None
    prompt_suffix: str = ""
    negative_suffix: str = ""
    seed_offset: int = 0
    output_canvas: tuple[int, int] | None = None
    overrides: dict[str, Any] | None = None
    uploads: dict[str, str] | None = None

    @property
    def is_default(self) -> bool:
        return self.id == "default"


@dataclass(frozen=True)
class Recipe:
    """Validated, immutable view of one YAML art recipe."""

    path: Path
    document: dict[str, Any]
    digest: str

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def name(self) -> str:
        return str(self.document["name"])

    @property
    def purpose(self) -> dict[str, Any]:
        return _mapping(self.document["purpose"], "purpose")

    @property
    def output(self) -> dict[str, Any]:
        return _mapping(self.document["output"], "output")

    @property
    def pipeline(self) -> dict[str, Any]:
        return _mapping(self.document["pipeline"], "pipeline")

    @property
    def generation(self) -> dict[str, Any]:
        return _mapping(self.document["generation"], "generation")

    @property
    def prompt(self) -> dict[str, Any]:
        return _mapping(self.document["prompt"], "prompt")

    @property
    def review(self) -> dict[str, Any]:
        return _mapping(self.document.get("review", {}), "review")

    @property
    def animation(self) -> dict[str, Any]:
        return _mapping(self.document.get("animation", {}), "animation")

    @property
    def workflow_path(self) -> Path:
        return project_path(self.pipeline["workflow"])

    @property
    def candidate_count(self) -> int:
        return int(self.generation.get("candidates", 4))

    @property
    def slot(self) -> str:
        return str(self.purpose["slot"])

    @property
    def canvas(self) -> tuple[int, int]:
        width, height = self.output["canvas"]
        return int(width), int(height)

    @property
    def shots(self) -> tuple[ShotSpec, ...]:
        """Expand a recipe into independently submitted ComfyUI shots."""
        declared = self.pipeline.get("shots")
        if declared is None:
            effect_variants = self.document.get("effect_variants", {})
            if isinstance(effect_variants, dict):
                declared = effect_variants.get("variants")
        if not declared:
            return (ShotSpec(id="default", label=self.name),)

        result: list[ShotSpec] = []
        for index, raw_shot in enumerate(declared):
            shot = _mapping(raw_shot, f"shots[{index}]")
            shot_id = str(shot.get("id", "")).strip()
            canvas = shot.get("output_canvas")
            parsed_canvas = (
                (int(canvas[0]), int(canvas[1]))
                if canvas is not None
                else None
            )
            result.append(
                ShotSpec(
                    id=shot_id,
                    label=str(shot.get("label") or shot_id),
                    slot=(
                        str(shot["slot"])
                        if shot.get("slot")
                        else None
                    ),
                    prompt_suffix=str(shot.get("prompt_suffix", "")),
                    negative_suffix=str(shot.get("negative_suffix", "")),
                    seed_offset=int(shot.get("seed_offset", 0)),
                    output_canvas=parsed_canvas,
                    overrides=dict(shot.get("overrides", {})),
                    uploads={
                        str(target): str(source)
                        for target, source in shot.get("uploads", {}).items()
                    },
                )
            )
        return tuple(result)

    @property
    def is_multi_shot(self) -> bool:
        return len(self.shots) > 1 or not self.shots[0].is_default

    def only_shot(self, shot_id: str) -> "Recipe":
        """Return an immutable job snapshot containing one declared shot."""
        matching = [shot for shot in self.shots if shot.id == shot_id]
        if not matching:
            raise ReviewError(
                f"Recipe {self.id} has no shot {shot_id!r}"
            )
        if matching[0].is_default:
            return self

        document = copy.deepcopy(self.document)
        if document["pipeline"].get("shots"):
            document["pipeline"]["shots"] = [
                shot
                for shot in document["pipeline"]["shots"]
                if str(shot.get("id")) == shot_id
            ]
        else:
            variants = document.get("effect_variants", {}).get("variants")
            if not isinstance(variants, list):
                raise ReviewError(
                    f"Recipe {self.id} cannot isolate shot {shot_id!r}"
                )
            document["effect_variants"]["variants"] = [
                shot
                for shot in variants
                if str(shot.get("id")) == shot_id
            ]
        selected = matching[0]
        document["name"] = f"{self.name} · {selected.label}"
        document["purpose"]["slot"] = selected.slot or self.slot
        document["purpose"]["shot"] = selected.id
        # A one-shot variation is a new key-pose candidate, not a complete
        # animation set. Keeping the parent draft contract would leave clips
        # that reference shots deliberately removed above.
        document.pop("animation", None)
        return Recipe.from_document(document, path=self.path)

    @classmethod
    def load(cls, path: Path) -> "Recipe":
        resolved = path.resolve()
        try:
            document = yaml.safe_load(resolved.read_text(encoding="utf-8"))
        except (OSError, yaml.YAMLError) as exc:
            raise ReviewError(f"Cannot load recipe {resolved}: {exc}") from exc
        return cls.from_document(document, path=resolved)

    @classmethod
    def from_document(
        cls,
        document: Any,
        *,
        path: Path,
    ) -> "Recipe":
        root = _mapping(document, "recipe")
        for key in (
            "schema_version",
            "id",
            "name",
            "purpose",
            "output",
            "pipeline",
            "generation",
            "prompt",
        ):
            if key not in root:
                raise ReviewError(f"Recipe {path} is missing {key!r}")
        if root["schema_version"] != 1:
            raise ReviewError(
                f"Recipe {path} has unsupported schema_version "
                f"{root['schema_version']!r}"
            )
        if not str(root["id"]).strip():
            raise ReviewError(f"Recipe {path} has an empty id")

        purpose = _mapping(root["purpose"], "purpose")
        for key in ("category", "slot", "use"):
            if not purpose.get(key):
                raise ReviewError(f"Recipe {path} purpose.{key} is required")

        output = _mapping(root["output"], "output")
        canvas = output.get("canvas")
        if (
            not isinstance(canvas, list)
            or len(canvas) != 2
            or any(
                isinstance(item, bool)
                or not isinstance(item, int)
                or item <= 0
                for item in canvas
            )
        ):
            raise ReviewError(
                f"Recipe {path} output.canvas must be [positive width, height]"
            )

        pipeline = _mapping(root["pipeline"], "pipeline")
        if not pipeline.get("workflow"):
            raise ReviewError(f"Recipe {path} pipeline.workflow is required")
        bindings = _mapping(pipeline.get("bindings", {}), "pipeline.bindings")
        for name, target in bindings.items():
            if not isinstance(target, str) or "." not in target:
                raise ReviewError(
                    f"Recipe {path} binding {name!r} must be NODE.INPUT"
                )
        overrides = _mapping(
            pipeline.get("overrides", {}),
            "pipeline.overrides",
        )
        for target in overrides:
            if "." not in str(target):
                raise ReviewError(
                    f"Recipe {path} override {target!r} must be NODE.INPUT"
                )
        uploads = _mapping(pipeline.get("uploads", {}), "pipeline.uploads")
        for target in uploads:
            if "." not in str(target):
                raise ReviewError(
                    f"Recipe {path} upload {target!r} must be NODE.INPUT"
                )
        shots = pipeline.get("shots")
        if shots is None:
            effect_variants = root.get("effect_variants", {})
            if isinstance(effect_variants, dict):
                shots = effect_variants.get("variants")
        if shots is not None:
            if not isinstance(shots, list) or not shots:
                raise ReviewError(
                    f"Recipe {path} shots/variants must be a non-empty list"
                )
            shot_ids: set[str] = set()
            for index, raw_shot in enumerate(shots):
                shot = _mapping(raw_shot, f"shots[{index}]")
                shot_id = str(shot.get("id", "")).strip()
                if not SHOT_ID_PATTERN.fullmatch(shot_id):
                    raise ReviewError(
                        f"Recipe {path} shot id {shot_id!r} must be kebab-case"
                    )
                if shot_id in shot_ids:
                    raise ReviewError(
                        f"Recipe {path} has duplicate shot id {shot_id!r}"
                    )
                shot_ids.add(shot_id)
                canvas = shot.get("output_canvas")
                if canvas is not None and (
                    not isinstance(canvas, list)
                    or len(canvas) != 2
                    or any(
                        isinstance(item, bool)
                        or not isinstance(item, int)
                        or item <= 0
                        for item in canvas
                    )
                ):
                    raise ReviewError(
                        f"Recipe {path} shot {shot_id!r} output_canvas must "
                        "be [positive width, height]"
                    )
                if not isinstance(shot.get("seed_offset", 0), int):
                    raise ReviewError(
                        f"Recipe {path} shot {shot_id!r} seed_offset "
                        "must be an integer"
                    )
                shot_overrides = _mapping(
                    shot.get("overrides", {}),
                    f"shots[{index}].overrides",
                )
                shot_uploads = _mapping(
                    shot.get("uploads", {}),
                    f"shots[{index}].uploads",
                )
                for target in (*shot_overrides, *shot_uploads):
                    if "." not in str(target):
                        raise ReviewError(
                            f"Recipe {path} shot target {target!r} "
                            "must be NODE.INPUT"
                        )

        animation = root.get("animation", {})
        if animation:
            animation = _mapping(animation, "animation")
            draft = animation.get("draft")
            if draft is not None:
                draft = _mapping(draft, "animation.draft")
                clips = draft.get("clips")
                if not isinstance(clips, list) or not clips:
                    raise ReviewError(
                        f"Recipe {path} animation.draft.clips must be "
                        "a non-empty list"
                    )
                declared_shots = set(shot_ids) if shots is not None else {"default"}
                clip_tags: set[str] = set()
                for index, raw_clip in enumerate(clips):
                    clip = _mapping(
                        raw_clip,
                        f"animation.draft.clips[{index}]",
                    )
                    tag = str(clip.get("tag", "")).strip()
                    if not SHOT_ID_PATTERN.fullmatch(tag):
                        raise ReviewError(
                            f"Recipe {path} animation tag {tag!r} must be "
                            "kebab-case"
                        )
                    if tag in clip_tags:
                        raise ReviewError(
                            f"Recipe {path} has duplicate animation tag {tag!r}"
                        )
                    clip_tags.add(tag)
                    _positive_int(
                        clip.get("fps"),
                        f"animation.draft.clips[{index}].fps",
                    )
                    frames = clip.get("frames")
                    if not isinstance(frames, list) or not frames:
                        raise ReviewError(
                            f"Recipe {path} animation clip {tag!r} frames "
                            "must be a non-empty list"
                        )
                    unknown = [
                        str(frame)
                        for frame in frames
                        if str(frame) not in declared_shots
                    ]
                    if unknown:
                        raise ReviewError(
                            f"Recipe {path} animation clip {tag!r} references "
                            f"unknown shots: {', '.join(unknown)}"
                        )
                    if not isinstance(clip.get("loop", False), bool):
                        raise ReviewError(
                            f"Recipe {path} animation clip {tag!r} loop "
                            "must be boolean"
                        )

        generation = _mapping(root["generation"], "generation")
        for key in ("width", "height", "steps", "candidates"):
            _positive_int(generation.get(key), f"generation.{key}")
        if not isinstance(generation.get("cfg"), (int, float)):
            raise ReviewError(f"Recipe {path} generation.cfg must be numeric")
        if not 0 <= float(generation.get("denoise", 1.0)) <= 1:
            raise ReviewError(
                f"Recipe {path} generation.denoise must be in 0..1"
            )

        prompt = _mapping(root["prompt"], "prompt")
        if not str(prompt.get("positive", "")).strip():
            raise ReviewError(f"Recipe {path} prompt.positive is required")
        if "negative" not in prompt:
            raise ReviewError(f"Recipe {path} prompt.negative is required")

        return cls(
            path=path.resolve(),
            document=root,
            digest=recipe_digest(root),
        )

    def validate_files(self) -> None:
        if not self.workflow_path.is_file():
            raise ReviewError(
                f"Recipe {self.id} workflow is missing: {self.workflow_path}"
            )
        for target, source in self.pipeline.get("uploads", {}).items():
            path = project_path(str(source))
            if not path.is_file():
                raise ReviewError(
                    f"Recipe {self.id} upload {target} is missing: {path}"
                )
        for shot in self.shots:
            for target, source in (shot.uploads or {}).items():
                path = project_path(source)
                if not path.is_file():
                    raise ReviewError(
                        f"Recipe {self.id} shot {shot.id} upload {target} "
                        f"is missing: {path}"
                    )

    def assignments(
        self,
        seed: int,
        shot: ShotSpec | None = None,
    ) -> list[str]:
        """Return ComfyUI NODE.INPUT=JSON assignments for one candidate."""
        generation = self.generation
        prompt = self.prompt
        pipeline = self.pipeline
        selected = shot or self.shots[0]
        bindings = pipeline.get("bindings", {})
        values: dict[str, Any] = dict(pipeline.get("overrides", {}))
        values.update(selected.overrides or {})

        logical_values: dict[str, Any] = {
            "seed": seed + selected.seed_offset,
            "positive": (
                f"{prompt['positive']}{selected.prompt_suffix}"
            ),
            "negative": (
                f"{prompt.get('negative', '')}{selected.negative_suffix}"
            ),
            "checkpoint": pipeline.get("checkpoint"),
            "width": generation["width"],
            "height": generation["height"],
            "steps": generation["steps"],
            "cfg": generation["cfg"],
            "denoise": generation.get("denoise", 1.0),
            "sampler": generation.get("sampler"),
            "scheduler": generation.get("scheduler"),
        }
        for logical_name, target in bindings.items():
            if logical_name in logical_values:
                value = logical_values[logical_name]
                if value is not None:
                    values[target] = value

        for lora in self.document.get("loras", []):
            entry = _mapping(lora, "loras[]")
            node = str(entry.get("node", "")).strip()
            if not node:
                continue
            values[f"{node}.lora_name"] = entry["name"]
            values[f"{node}.strength_model"] = entry["model_strength"]
            values[f"{node}.strength_clip"] = entry["clip_strength"]

        for control in self.document.get("controlnets", []):
            entry = _mapping(control, "controlnets[]")
            loader_node = str(entry.get("loader_node", "")).strip()
            apply_node = str(entry.get("apply_node", "")).strip()
            if loader_node and entry.get("model"):
                values[f"{loader_node}.control_net_name"] = entry["model"]
            if apply_node and entry.get("strength") is not None:
                values[f"{apply_node}.strength"] = entry["strength"]

        return [
            f"{target}={canonical_json(value)}"
            for target, value in sorted(values.items())
        ]

    def uploads(self, shot: ShotSpec | None = None) -> list[str]:
        selected = shot or self.shots[0]
        uploads = dict(self.pipeline.get("uploads", {}))
        uploads.update(selected.uploads or {})
        return [
            f"{target}={project_path(str(source))}"
            for target, source in sorted(
                uploads.items()
            )
        ]

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "digest": self.digest,
            "purpose": self.purpose,
            "output": self.output,
            "pipeline": {
                "type": self.pipeline.get("type"),
                "workflow": relative_project_path(self.workflow_path),
                "checkpoint": self.pipeline.get("checkpoint"),
            },
            "loras": self.document.get("loras", []),
            "controlnets": self.document.get("controlnets", []),
            "generation": self.generation,
            "prompt": self.prompt,
            "quality_gates": self.document.get("quality_gates", {}),
            "review": self.review,
            "shots": [
                {
                    "id": shot.id,
                    "label": shot.label,
                    "slot": shot.slot or self.slot,
                    "prompt_suffix": shot.prompt_suffix,
                    "seed_offset": shot.seed_offset,
                    "output_canvas": shot.output_canvas or self.canvas,
                }
                for shot in self.shots
            ],
        }


class RecipeRegistry:
    def __init__(self, directory: Path = DEFAULT_RECIPE_DIR):
        self.directory = directory.resolve()

    def load_all(self) -> dict[str, Recipe]:
        recipes: dict[str, Recipe] = {}
        if not self.directory.is_dir():
            return recipes
        for path in sorted(self.directory.glob("*.yaml")):
            recipe = Recipe.load(path)
            if recipe.id in recipes:
                raise ReviewError(f"Duplicate recipe id {recipe.id!r}")
            recipes[recipe.id] = recipe
        return recipes

    def get(self, recipe_id: str) -> Recipe:
        recipes = self.load_all()
        try:
            return recipes[recipe_id]
        except KeyError as exc:
            available = ", ".join(sorted(recipes)) or "(none)"
            raise ReviewError(
                f"Unknown recipe {recipe_id!r}; available: {available}"
            ) from exc


SCHEMA = """
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS jobs (
    id TEXT PRIMARY KEY,
    recipe_id TEXT NOT NULL,
    recipe_path TEXT NOT NULL,
    recipe_hash TEXT NOT NULL,
    recipe_json TEXT NOT NULL,
    status TEXT NOT NULL,
    requested_by TEXT NOT NULL,
    candidate_count INTEGER NOT NULL,
    base_seed INTEGER NOT NULL,
    notes TEXT NOT NULL DEFAULT '',
    parent_candidate_id TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    error TEXT
);

CREATE TABLE IF NOT EXISTS candidates (
    id TEXT PRIMARY KEY,
    job_id TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
    ordinal INTEGER NOT NULL,
    seed INTEGER NOT NULL,
    raw_path TEXT NOT NULL,
    prepared_path TEXT,
    aseprite_path TEXT,
    status TEXT NOT NULL,
    metrics_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    error TEXT,
    UNIQUE(job_id, ordinal)
);

CREATE TABLE IF NOT EXISTS feedback (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_key TEXT NOT NULL UNIQUE,
    job_id TEXT REFERENCES jobs(id) ON DELETE CASCADE,
    candidate_id TEXT REFERENCES candidates(id) ON DELETE CASCADE,
    user_id TEXT NOT NULL,
    source TEXT NOT NULL,
    label TEXT NOT NULL DEFAULT '',
    text TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'pending',
    created_at TEXT NOT NULL,
    resolved_at TEXT,
    resolution TEXT
);

CREATE TABLE IF NOT EXISTS actions (
    id TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    job_id TEXT REFERENCES jobs(id) ON DELETE CASCADE,
    candidate_id TEXT REFERENCES candidates(id) ON DELETE CASCADE,
    requested_by TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'queued',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    error TEXT
);

CREATE TABLE IF NOT EXISTS outbox (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    kind TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    error TEXT
);

CREATE TABLE IF NOT EXISTS slack_messages (
    message_ts TEXT PRIMARY KEY,
    channel_id TEXT NOT NULL,
    kind TEXT NOT NULL,
    job_id TEXT REFERENCES jobs(id) ON DELETE CASCADE,
    candidate_id TEXT REFERENCES candidates(id) ON DELETE CASCADE,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_jobs_status ON jobs(status, created_at);
CREATE INDEX IF NOT EXISTS idx_candidates_job ON candidates(job_id, ordinal);
CREATE INDEX IF NOT EXISTS idx_feedback_status ON feedback(status, created_at);
CREATE INDEX IF NOT EXISTS idx_actions_status ON actions(status, created_at);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox(status, created_at);
"""


class ReviewStore:
    def __init__(self, path: Path = DEFAULT_DB_PATH):
        self.path = path.resolve()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.initialize()

    @contextmanager
    def connect(self) -> Iterator[sqlite3.Connection]:
        connection = sqlite3.connect(self.path, timeout=30)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA foreign_keys = ON")
        connection.execute("PRAGMA journal_mode = WAL")
        try:
            yield connection
            connection.commit()
        except Exception:
            connection.rollback()
            raise
        finally:
            connection.close()

    def initialize(self) -> None:
        with self.connect() as connection:
            connection.executescript(SCHEMA)
            columns = {
                row["name"]
                for row in connection.execute(
                    "PRAGMA table_info(outbox)"
                ).fetchall()
            }
            if "attempts" not in columns:
                connection.execute(
                    "ALTER TABLE outbox "
                    "ADD COLUMN attempts INTEGER NOT NULL DEFAULT 0"
                )

    def recover_stale_running(
        self,
        *,
        older_than_seconds: float = 3600.0,
    ) -> int:
        """Requeue jobs/actions/outbox rows abandoned by a dead worker."""
        cutoff = (
            datetime.now(timezone.utc)
            - timedelta(seconds=older_than_seconds)
        ).isoformat(timespec="seconds")
        now = utc_now()
        recovered = 0
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            recovered += connection.execute(
                """
                UPDATE jobs
                SET status = 'queued', updated_at = ?
                WHERE status = 'running' AND updated_at < ?
                """,
                (now, cutoff),
            ).rowcount
            recovered += connection.execute(
                """
                UPDATE actions
                SET status = 'queued', updated_at = ?
                WHERE status = 'running' AND updated_at < ?
                """,
                (now, cutoff),
            ).rowcount
            recovered += connection.execute(
                """
                UPDATE outbox
                SET status = 'pending', updated_at = ?
                WHERE status = 'sending' AND updated_at < ?
                """,
                (now, cutoff),
            ).rowcount
        return recovered

    def create_job(
        self,
        recipe: Recipe,
        *,
        requested_by: str,
        candidate_count: int | None = None,
        base_seed: int | None = None,
        notes: str = "",
        parent_candidate_id: str | None = None,
    ) -> str:
        count = candidate_count or recipe.candidate_count
        _positive_int(count, "candidate_count")
        seed = base_seed if base_seed is not None else secrets.randbelow(2**31)
        if seed < 0:
            raise ReviewError("base_seed cannot be negative")
        job_id = make_id("ART")
        now = utc_now()
        with self.connect() as connection:
            connection.execute(
                """
                INSERT INTO jobs (
                    id, recipe_id, recipe_path, recipe_hash, recipe_json,
                    status, requested_by, candidate_count, base_seed, notes,
                    parent_candidate_id, created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, 'queued', ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    job_id,
                    recipe.id,
                    relative_project_path(recipe.path),
                    recipe.digest,
                    canonical_json(recipe.document),
                    requested_by,
                    count,
                    seed,
                    notes,
                    parent_candidate_id,
                    now,
                    now,
                ),
            )
        return job_id

    def get_job(self, job_id: str) -> sqlite3.Row:
        with self.connect() as connection:
            row = connection.execute(
                "SELECT * FROM jobs WHERE id = ?",
                (job_id,),
            ).fetchone()
        if row is None:
            raise ReviewError(f"Unknown job {job_id}")
        return row

    def list_jobs(
        self,
        *,
        status: str | None = None,
        limit: int = 50,
    ) -> list[sqlite3.Row]:
        with self.connect() as connection:
            if status:
                return connection.execute(
                    """
                    SELECT * FROM jobs
                    WHERE status = ?
                    ORDER BY created_at DESC
                    LIMIT ?
                    """,
                    (status, limit),
                ).fetchall()
            return connection.execute(
                """
                SELECT * FROM jobs
                ORDER BY created_at DESC
                LIMIT ?
                """,
                (limit,),
            ).fetchall()

    def claim_job(self, job_id: str | None = None) -> sqlite3.Row | None:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            if job_id:
                row = connection.execute(
                    "SELECT * FROM jobs WHERE id = ? AND status = 'queued'",
                    (job_id,),
                ).fetchone()
            else:
                row = connection.execute(
                    """
                    SELECT * FROM jobs
                    WHERE status = 'queued'
                    ORDER BY created_at
                    LIMIT 1
                    """
                ).fetchone()
            if row is None:
                return None
            now = utc_now()
            connection.execute(
                """
                UPDATE jobs
                SET status = 'running', updated_at = ?, error = NULL
                WHERE id = ? AND status = 'queued'
                """,
                (now, row["id"]),
            )
            return connection.execute(
                "SELECT * FROM jobs WHERE id = ?",
                (row["id"],),
            ).fetchone()

    def set_job_status(
        self,
        job_id: str,
        status: str,
        *,
        error: str | None = None,
    ) -> None:
        if status not in VALID_JOB_STATES:
            raise ReviewError(f"Invalid job status {status!r}")
        with self.connect() as connection:
            connection.execute(
                """
                UPDATE jobs
                SET status = ?, updated_at = ?, error = ?
                WHERE id = ?
                """,
                (status, utc_now(), error, job_id),
            )

    def add_candidate(
        self,
        *,
        job_id: str,
        ordinal: int,
        seed: int,
        raw_path: Path,
        metrics: dict[str, Any],
    ) -> str:
        candidate_id = f"{job_id}-C{ordinal:02d}"
        now = utc_now()
        with self.connect() as connection:
            connection.execute(
                """
                INSERT INTO candidates (
                    id, job_id, ordinal, seed, raw_path, status,
                    metrics_json, created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, 'generated', ?, ?, ?)
                ON CONFLICT(id) DO UPDATE SET
                    seed = excluded.seed,
                    raw_path = excluded.raw_path,
                    status = 'generated',
                    metrics_json = excluded.metrics_json,
                    updated_at = excluded.updated_at,
                    error = NULL
                """,
                (
                    candidate_id,
                    job_id,
                    ordinal,
                    seed,
                    relative_project_path(raw_path),
                    canonical_json(metrics),
                    now,
                    now,
                ),
            )
        return candidate_id

    def get_candidate(self, candidate_id: str) -> sqlite3.Row:
        with self.connect() as connection:
            row = connection.execute(
                "SELECT * FROM candidates WHERE id = ?",
                (candidate_id,),
            ).fetchone()
        if row is None:
            raise ReviewError(f"Unknown candidate {candidate_id}")
        return row

    def candidate_is_approved(self, candidate_id: str) -> bool:
        with self.connect() as connection:
            row = connection.execute(
                """
                SELECT label FROM feedback
                WHERE candidate_id = ?
                  AND source = 'button'
                  AND label IN ('approve', 'reject')
                ORDER BY id DESC
                LIMIT 1
                """,
                (candidate_id,),
            ).fetchone()
        return row is not None and row["label"] == "approve"

    def shot_decision(
        self,
        candidate_id: str,
        shot_id: str,
    ) -> str | None:
        labels = (
            f"shot:{shot_id}:approve",
            f"shot:{shot_id}:reject",
        )
        with self.connect() as connection:
            row = connection.execute(
                """
                SELECT label FROM feedback
                WHERE candidate_id = ?
                  AND source IN ('shot-button', 'shot-command')
                  AND label IN (?, ?)
                ORDER BY id DESC
                LIMIT 1
                """,
                (candidate_id, *labels),
            ).fetchone()
        if row is None:
            return None
        return row["label"].rsplit(":", 1)[-1]

    def list_candidates(self, job_id: str) -> list[sqlite3.Row]:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM candidates
                WHERE job_id = ?
                ORDER BY ordinal
                """,
                (job_id,),
            ).fetchall()

    def set_candidate_status(
        self,
        candidate_id: str,
        status: str,
        *,
        error: str | None = None,
        prepared_path: Path | None = None,
        aseprite_path: Path | None = None,
    ) -> None:
        if status not in VALID_CANDIDATE_STATES:
            raise ReviewError(f"Invalid candidate status {status!r}")
        with self.connect() as connection:
            row = connection.execute(
                "SELECT * FROM candidates WHERE id = ?",
                (candidate_id,),
            ).fetchone()
            if row is None:
                raise ReviewError(f"Unknown candidate {candidate_id}")
            connection.execute(
                """
                UPDATE candidates
                SET status = ?, error = ?,
                    prepared_path = COALESCE(?, prepared_path),
                    aseprite_path = COALESCE(?, aseprite_path),
                    updated_at = ?
                WHERE id = ?
                """,
                (
                    status,
                    error,
                    relative_project_path(prepared_path)
                    if prepared_path
                    else None,
                    relative_project_path(aseprite_path)
                    if aseprite_path
                    else None,
                    utc_now(),
                    candidate_id,
                ),
            )

    def add_feedback(
        self,
        *,
        event_key: str,
        user_id: str,
        source: str,
        label: str = "",
        text: str = "",
        job_id: str | None = None,
        candidate_id: str | None = None,
    ) -> bool:
        if not job_id and not candidate_id:
            raise ReviewError("Feedback needs a job_id or candidate_id")
        if candidate_id and not job_id:
            job_id = self.get_candidate(candidate_id)["job_id"]
        with self.connect() as connection:
            cursor = connection.execute(
                """
                INSERT OR IGNORE INTO feedback (
                    event_key, job_id, candidate_id, user_id, source,
                    label, text, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    event_key,
                    job_id,
                    candidate_id,
                    user_id,
                    source,
                    label,
                    text,
                    utc_now(),
                ),
            )
            return cursor.rowcount > 0

    def pending_feedback(self, limit: int = 100) -> list[sqlite3.Row]:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM feedback
                WHERE status = 'pending'
                ORDER BY created_at
                LIMIT ?
                """,
                (limit,),
            ).fetchall()

    def resolve_feedback(self, feedback_id: int, resolution: str) -> None:
        with self.connect() as connection:
            cursor = connection.execute(
                """
                UPDATE feedback
                SET status = 'resolved', resolved_at = ?, resolution = ?
                WHERE id = ? AND status = 'pending'
                """,
                (utc_now(), resolution, feedback_id),
            )
            if cursor.rowcount == 0:
                raise ReviewError(
                    f"Pending feedback {feedback_id} does not exist"
                )

    def resolve_feedback_by_event(
        self,
        event_key: str,
        resolution: str,
    ) -> None:
        with self.connect() as connection:
            connection.execute(
                """
                UPDATE feedback
                SET status = 'resolved', resolved_at = ?, resolution = ?
                WHERE event_key = ? AND status = 'pending'
                """,
                (utc_now(), resolution, event_key),
            )

    def enqueue_action(
        self,
        kind: str,
        *,
        requested_by: str,
        job_id: str | None = None,
        candidate_id: str | None = None,
        payload: dict[str, Any] | None = None,
    ) -> str:
        if not job_id and not candidate_id:
            raise ReviewError("Action needs a job_id or candidate_id")
        if candidate_id and not job_id:
            job_id = self.get_candidate(candidate_id)["job_id"]
        action_id = make_id("ACT")
        now = utc_now()
        with self.connect() as connection:
            connection.execute(
                """
                INSERT INTO actions (
                    id, kind, job_id, candidate_id, requested_by,
                    payload_json, status, created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, ?, 'queued', ?, ?)
                """,
                (
                    action_id,
                    kind,
                    job_id,
                    candidate_id,
                    requested_by,
                    canonical_json(payload or {}),
                    now,
                    now,
                ),
            )
        return action_id

    def claim_action(self) -> sqlite3.Row | None:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            row = connection.execute(
                """
                SELECT * FROM actions
                WHERE status = 'queued'
                ORDER BY created_at
                LIMIT 1
                """
            ).fetchone()
            if row is None:
                return None
            connection.execute(
                """
                UPDATE actions
                SET status = 'running', updated_at = ?, error = NULL
                WHERE id = ? AND status = 'queued'
                """,
                (utc_now(), row["id"]),
            )
            return connection.execute(
                "SELECT * FROM actions WHERE id = ?",
                (row["id"],),
            ).fetchone()

    def finish_action(
        self,
        action_id: str,
        *,
        error: str | None = None,
    ) -> None:
        status = "failed" if error else "complete"
        with self.connect() as connection:
            connection.execute(
                """
                UPDATE actions
                SET status = ?, updated_at = ?, error = ?
                WHERE id = ?
                """,
                (status, utc_now(), error, action_id),
            )

    def enqueue_outbox(self, kind: str, payload: dict[str, Any]) -> int:
        now = utc_now()
        with self.connect() as connection:
            cursor = connection.execute(
                """
                INSERT INTO outbox (
                    kind, payload_json, status, created_at, updated_at
                ) VALUES (?, ?, 'pending', ?, ?)
                """,
                (kind, canonical_json(payload), now, now),
            )
            return int(cursor.lastrowid)

    def pending_outbox(self, limit: int = 20) -> list[sqlite3.Row]:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM outbox
                WHERE status = 'pending'
                ORDER BY id
                LIMIT ?
                """,
                (limit,),
            ).fetchall()

    def claim_outbox(self, limit: int = 20) -> list[sqlite3.Row]:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            rows = connection.execute(
                """
                SELECT * FROM outbox
                WHERE status = 'pending'
                ORDER BY id
                LIMIT ?
                """,
                (limit,),
            ).fetchall()
            now = utc_now()
            for row in rows:
                connection.execute(
                    """
                    UPDATE outbox
                    SET status = 'sending', updated_at = ?
                    WHERE id = ? AND status = 'pending'
                    """,
                    (now, row["id"]),
                )
            return rows

    def finish_outbox(
        self,
        outbox_id: int,
        *,
        error: str | None = None,
        retry: bool = False,
    ) -> None:
        status = "pending" if retry else ("failed" if error else "complete")
        with self.connect() as connection:
            connection.execute(
                """
                UPDATE outbox
                SET status = ?, updated_at = ?, error = ?,
                    attempts = attempts + ?
                WHERE id = ?
                """,
                (
                    status,
                    utc_now(),
                    error,
                    1 if error or retry else 0,
                    outbox_id,
                ),
            )

    def map_slack_message(
        self,
        *,
        message_ts: str,
        channel_id: str,
        kind: str,
        job_id: str | None = None,
        candidate_id: str | None = None,
    ) -> None:
        with self.connect() as connection:
            connection.execute(
                """
                INSERT INTO slack_messages (
                    message_ts, channel_id, kind, job_id, candidate_id,
                    created_at
                ) VALUES (?, ?, ?, ?, ?, ?)
                ON CONFLICT(message_ts) DO UPDATE SET
                    channel_id = excluded.channel_id,
                    kind = excluded.kind,
                    job_id = excluded.job_id,
                    candidate_id = excluded.candidate_id
                """,
                (
                    message_ts,
                    channel_id,
                    kind,
                    job_id,
                    candidate_id,
                    utc_now(),
                ),
            )

    def find_slack_message(self, message_ts: str) -> sqlite3.Row | None:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM slack_messages
                WHERE message_ts = ?
                """,
                (message_ts,),
            ).fetchone()

    def find_candidate_slack_message(
        self,
        candidate_id: str,
    ) -> sqlite3.Row | None:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM slack_messages
                WHERE candidate_id = ? AND kind = 'candidate-root'
                ORDER BY created_at DESC
                LIMIT 1
                """,
                (candidate_id,),
            ).fetchone()

    def find_candidate_shot_slack_message(
        self,
        candidate_id: str,
        shot_id: str,
    ) -> sqlite3.Row | None:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM slack_messages
                WHERE candidate_id = ? AND kind = ?
                ORDER BY created_at DESC
                LIMIT 1
                """,
                (candidate_id, f"shot:{shot_id}"),
            ).fetchone()


def image_metrics(path: Path, alpha_cutoff: int = 80) -> dict[str, Any]:
    with Image.open(path) as source:
        rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= alpha_cutoff else 0)
    bounds = mask.getbbox()
    alpha_data = (
        alpha.get_flattened_data()
        if hasattr(alpha, "get_flattened_data")
        else alpha.getdata()
    )
    rgba_data = (
        rgba.get_flattened_data()
        if hasattr(rgba, "get_flattened_data")
        else rgba.getdata()
    )
    visible_pixels = sum(
        1 for value in alpha_data if value >= alpha_cutoff
    )
    visible_colors = {
        (red, green, blue)
        for red, green, blue, alpha_value in rgba_data
        if alpha_value >= alpha_cutoff
    }
    return {
        "width": rgba.width,
        "height": rgba.height,
        "visible_pixels": visible_pixels,
        "unique_visible_colors": len(visible_colors),
        "alpha_bounds": list(bounds) if bounds else None,
    }


def recipe_from_job(row: sqlite3.Row) -> Recipe:
    document = json.loads(row["recipe_json"])
    return Recipe.from_document(
        document,
        path=project_path(row["recipe_path"]),
    )


def row_dict(row: sqlite3.Row) -> dict[str, Any]:
    return {key: row[key] for key in row.keys()}
