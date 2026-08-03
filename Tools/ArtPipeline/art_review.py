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
DEFAULT_BATCH_DIR = (
    PROJECT_ROOT / "docs/art-direction/comfyui/batches"
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
DEFAULT_WORKFLOW_TYPES_PATH = (
    PROJECT_ROOT / "docs/art-direction/comfyui/workflow-types.yaml"
)
# Unity 슬롯 ID의 발급처. 여기 없는 슬롯은 정식 승격 대상이 아니다 — 게시해봐야
# 에디터가 읽지 않는 죽은 파일이 된다. 목록을 복제하지 않고 원본을 읽는다.
UNITY_SLOT_SOURCE = (
    PROJECT_ROOT
    / "Assets/_Project/Editor/ArtPipeline/ProjectCAsepritePipeline.cs"
)
# 몬스터 표시명의 SSOT. 파이프라인이 "기업 보안 사수"를 다시 타이핑하면 게임과
# 어긋난다 — DungeonCatalog 가 보스 이름에 대해 지키는 규칙과 같다.
UNITY_MONSTER_SOURCE = (
    PROJECT_ROOT / "Assets/_Project/Scripts/Core/MonsterRoster.cs"
)
# 정식 슬롯에 실제로 파일을 쓰는 승격 방식. 나머지는 검수/중간 산출물이다.
PUBLISHING_PROMOTION = "aseprite"
VALID_PROMOTIONS = frozenset(
    {
        PUBLISHING_PROMOTION,
        "animation-review-only",
        "manual-processor",
        "concept-only",
    }
)
VALID_JOB_STATES = {
    "queued",
    "running",
    "awaiting_review",
    "failed",
    "complete",
    "cancelled",
}
FINISHED_JOB_STATES = {"awaiting_review", "failed", "complete", "cancelled"}
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
VALID_APPLY_STATES = {
    "queued",
    "planning",
    "applying",
    "needs_input",
    "complete",
    "failed",
    "cancelled",
}

# 사람이 레시피를 고를 때 쓰는 축이다. `category`(무엇의 슬롯인가)나
# `use`(무슨 용도인가)와 다르다 — 같은 actor 카테고리라도 콘셉트 탐색과 런타임
# 스프라이트와 애니 키포즈는 고르는 순간의 목적이 서로 다르기 때문이다.
# 순서가 곧 목록·드롭다운 표시 순서다.
ASSET_TYPES: tuple[tuple[str, str], ...] = (
    ("concept", "컨셉"),
    ("environment", "배경"),
    ("character", "캐릭터"),
    ("animation", "애니메이션"),
    ("effect", "이펙트"),
    ("prop", "소품·아이템"),
    ("ui", "UI"),
)
ASSET_TYPE_LABELS = dict(ASSET_TYPES)
VALID_ASSET_TYPES = frozenset(ASSET_TYPE_LABELS)
ANIMATION_USES = frozenset({"animation-source", "animation-review-only"})


def derive_asset_type(category: str, use: str) -> str:
    """`purpose.asset_type`이 없는 레시피의 폴백.

    콘셉트 탐색이 가장 강한 신호다 — 어느 카테고리든 콘셉트는 콘셉트다.
    그다음은 카테고리가 결정하되, actor만 용도에 따라 캐릭터와 애니메이션으로
    갈린다.
    """
    if use == "concept":
        return "concept"
    if category == "effect":
        return "effect"
    if category == "environment":
        return "environment"
    if category == "ui":
        return "ui"
    if category in {"item", "prop", "marker"}:
        return "prop"
    if use in ANIMATION_USES:
        return "animation"
    return "character"


def asset_type_label(asset_type: str) -> str:
    return ASSET_TYPE_LABELS.get(asset_type, asset_type)


class ReviewError(RuntimeError):
    """Raised for invalid recipes or state transitions."""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def make_id(prefix: str) -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    return f"{prefix}-{stamp}-{secrets.token_hex(3)}"


def elapsed_seconds(start: str | None, end: str | None) -> float | None:
    if not start or not end:
        return None
    try:
        return (
            datetime.fromisoformat(end) - datetime.fromisoformat(start)
        ).total_seconds()
    except ValueError:
        return None


def format_duration(seconds: float | None) -> str:
    """사람이 읽는 소요 시간. 발주는 분 단위가 기본 눈금이다."""
    if seconds is None:
        return "?"
    seconds = max(0.0, float(seconds))
    if seconds < 90:
        return f"{seconds:.0f}초"
    minutes = seconds / 60
    if minutes < 90:
        return f"{minutes:.0f}분"
    return f"{minutes / 60:.1f}시간"


def progress_view(
    row: Any,
    *,
    unit_seconds: float | None = None,
) -> dict[str, Any]:
    """job 행 하나를 CLI·뷰어가 같이 쓰는 진행 표시로 바꾼다."""
    raw = row["progress_json"] if "progress_json" in row.keys() else None
    progress: dict[str, Any] = {}
    if raw:
        try:
            loaded = json.loads(raw)
            if isinstance(loaded, dict):
                progress = loaded
        except json.JSONDecodeError:
            progress = {}
    total = int(progress.get("units_total") or 0)
    done = int(progress.get("units_done") or 0)
    step = progress.get("step")
    step_max = progress.get("step_max")
    within = 0.0
    if isinstance(step, (int, float)) and isinstance(step_max, (int, float)):
        if step_max:
            within = min(1.0, max(0.0, float(step) / float(step_max)))
    fraction: float | None = None
    if total > 0:
        fraction = min(1.0, (done + within) / total)
    running = row["status"] == "running"
    elapsed = (
        elapsed_seconds(row["started_at"], utc_now())
        if running
        else elapsed_seconds(row["started_at"], row["finished_at"])
    )
    remaining: float | None = None
    if running and total > 0:
        left = max(0.0, total - (done + within))
        rate = unit_seconds
        if done > 0 and elapsed:
            rate = elapsed / (done + within) if (done + within) else rate
        if rate:
            remaining = left * rate
    return {
        "status": row["status"],
        "units_total": total or None,
        "units_done": done,
        "fraction": fraction,
        "percent": round(fraction * 100) if fraction is not None else None,
        "stage": progress.get("stage"),
        "shot": progress.get("shot"),
        "candidate": progress.get("candidate"),
        "node": progress.get("node"),
        "step": step,
        "step_max": step_max,
        "elapsed_seconds": elapsed,
        "remaining_seconds": remaining,
        "eta_text": format_duration(remaining) if remaining else None,
    }


RECENT_ALIASES = frozenset({"latest", "last", "^"})
CARET_INDEX_PATTERN = re.compile(r"^\^(\d+)$|^(\d+)$")


def alias_index(token: str) -> int | None:
    """`latest`/`^`/`^3`/`3`을 1부터 세는 최근 목록 번호로 바꾼다.

    번호는 `list_recent_candidates` 순서다 — 선택기가 출력한 번호를 그대로
    다음 명령에 쓸 수 있어야 손으로 ID를 옮기지 않게 된다. 생성 ID는 항상
    접두사로 시작하므로 순수 숫자와 충돌하지 않는다.
    """
    text = (token or "").strip().lower()
    if text in RECENT_ALIASES:
        return 1
    match = CARET_INDEX_PATTERN.match(text)
    if match is None:
        return None
    index = int(match.group(1) or match.group(2))
    return index if index >= 1 else None


def like_escape(value: str) -> str:
    """부분 일치 검색에서 사용자 입력의 LIKE 와일드카드를 문자로 다룬다."""
    return (
        value.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_")
    )


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
    def workflow_ui_path(self) -> Path:
        api_path = self.workflow_path
        suffix = ".api.json"
        if not api_path.name.endswith(suffix):
            raise ReviewError(
                f"Recipe {self.id} workflow must end with {suffix}: {api_path}"
            )
        return api_path.with_name(
            api_path.name.removesuffix(suffix) + ".workflow.json"
        )

    @property
    def candidate_count(self) -> int:
        return int(self.generation.get("candidates", 4))

    @property
    def slot(self) -> str:
        return str(self.purpose["slot"])

    @property
    def asset_type(self) -> str:
        """사람이 고르는 축. 명시값이 없으면 category/use에서 파생한다."""
        declared = str(self.purpose.get("asset_type", "")).strip()
        if declared:
            return declared
        return derive_asset_type(
            str(self.purpose.get("category", "")),
            str(self.purpose.get("use", "")),
        )

    @property
    def asset_type_label(self) -> str:
        return asset_type_label(self.asset_type)

    @property
    def workflow_type(self) -> str:
        return str(self.pipeline.get("type", "")).strip()

    @property
    def adjustments(self) -> tuple[str, ...]:
        """이번 실행에서 레시피 YAML과 달라진 항목. 없으면 빈 튜플."""
        declared = self.document.get("adjustments", [])
        if not isinstance(declared, list):
            return ()
        return tuple(str(item) for item in declared)

    def with_overrides(
        self,
        *,
        workflow_type: str | None = None,
        checkpoint: str | None = None,
        positive: str | None = None,
        negative: str | None = None,
        steps: int | None = None,
        cfg: float | None = None,
        denoise: float | None = None,
        registry: "WorkflowTypeRegistry | None" = None,
    ) -> "Recipe":
        """이번 실행에만 적용할 조정본을 만든다.

        YAML 은 건드리지 않는다. job 이 `recipe_json` 으로 문서 전체를
        스냅샷하므로 조정본도 원본과 똑같이 재현 가능하다. 조정한 항목은
        `adjustments` 에 남겨 카드가 "원본 그대로가 아니다"를 말할 수 있게 한다.
        """
        document = copy.deepcopy(self.document)
        changed: list[str] = []

        if workflow_type and workflow_type != self.workflow_type:
            resolved = (registry or WorkflowTypeRegistry()).get(workflow_type)
            document["pipeline"]["type"] = resolved.id
            if resolved.default_workflow:
                document["pipeline"]["workflow"] = resolved.default_workflow
            changed.append("워크플로")
        if checkpoint and checkpoint != self.pipeline.get("checkpoint"):
            document["pipeline"]["checkpoint"] = checkpoint
            changed.append("모델")
        if positive is not None and positive != self.prompt.get("positive"):
            document["prompt"]["positive"] = positive
            changed.append("긍정 프롬프트")
        if negative is not None and negative != self.prompt.get("negative"):
            document["prompt"]["negative"] = negative
            changed.append("제외 프롬프트")
        if steps is not None and steps != int(self.generation["steps"]):
            document["generation"]["steps"] = steps
            changed.append("Steps")
        if cfg is not None and cfg != float(self.generation["cfg"]):
            document["generation"]["cfg"] = cfg
            changed.append("CFG")
        if (
            denoise is not None
            and denoise != float(self.generation.get("denoise", 1.0))
        ):
            document["generation"]["denoise"] = denoise
            changed.append("Denoise")

        if not changed:
            return self
        document["adjustments"] = [
            *self.adjustments,
            *(item for item in changed if item not in self.adjustments),
        ]
        adjusted = Recipe.from_document(document, path=self.path)
        # 조정으로 계약이 깨지지 않았는지 지금 본다 — 워커가 아니라 폼에서
        # 막아야 사람이 무엇을 잘못 골랐는지 안다.
        adjusted.validate_workflow_type(registry)
        if "워크플로" in changed:
            adjusted.validate_binding_nodes()
        return adjusted

    def with_source_image(
        self,
        source: Path,
        *,
        registry: "WorkflowTypeRegistry | None" = None,
    ) -> "Recipe":
        """승인된 앞 단계 이미지를 이 레시피의 주 입력으로 연결한다.

        캐릭터는 ``style_source``, 환경·VFX img2img는 ``source_sheet``를
        우선한다. 포즈 입력은 별도 계약이므로 승인 원본으로 덮지 않는다.
        """
        resolved_source = source.resolve()
        if not resolved_source.is_file():
            raise ReviewError(f"Source image is missing: {resolved_source}")
        workflow_type = (registry or WorkflowTypeRegistry()).get(
            self.workflow_type
        )
        role = next(
            (
                candidate
                for candidate in ("style_source", "source_sheet")
                if candidate in workflow_type.upload_roles
            ),
            None,
        )
        if role is None:
            raise ReviewError(
                f"Workflow type {workflow_type.id} has no approved-source "
                "input; choose an img2img production method"
            )
        target = workflow_type.node_for_role(role)
        if target is None:
            raise ReviewError(
                f"Workflow type {workflow_type.id} has no node for {role}"
            )

        document = copy.deepcopy(self.document)
        uploads = document["pipeline"].setdefault("uploads", {})
        source_value = relative_project_path(resolved_source)
        if uploads.get(target) == source_value:
            return self
        uploads[target] = source_value
        document["adjustments"] = [
            *self.adjustments,
            *(["승인 소스"] if "승인 소스" not in self.adjustments else []),
        ]
        adjusted = Recipe.from_document(document, path=self.path)
        adjusted.validate_workflow_type(registry)
        return adjusted

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
        declared_type = str(purpose.get("asset_type", "")).strip()
        if declared_type and declared_type not in VALID_ASSET_TYPES:
            known = ", ".join(sorted(VALID_ASSET_TYPES))
            raise ReviewError(
                f"Recipe {path} purpose.asset_type {declared_type!r} "
                f"is unknown; expected one of: {known}"
            )

        output = _mapping(root["output"], "output")
        promotion = str(output.get("promotion", PUBLISHING_PROMOTION))
        if promotion not in VALID_PROMOTIONS:
            known = ", ".join(sorted(VALID_PROMOTIONS))
            raise ReviewError(
                f"Recipe {path} output.promotion {promotion!r} is unknown; "
                f"expected one of: {known}"
            )
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
        if not self.workflow_ui_path.is_file():
            raise ReviewError(
                f"Recipe {self.id} canvas workflow is missing: "
                f"{self.workflow_ui_path}"
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
        self.validate_workflow_type()
        self.validate_binding_nodes()
        self.validate_slot_registration()

    @property
    def promotion(self) -> str:
        return str(self.output.get("promotion", PUBLISHING_PROMOTION))

    @property
    def publishes_to_unity(self) -> bool:
        return self.promotion == PUBLISHING_PROMOTION

    @property
    def target_slots(self) -> tuple[str, ...]:
        """이 레시피가 게시할 수 있는 슬롯 전부 — 샷이 슬롯을 갈아탈 수 있다."""
        slots = [self.slot]
        slots.extend(shot.slot for shot in self.shots if shot.slot)
        return tuple(dict.fromkeys(slots))

    def unity_field_for(
        self,
        slot: str,
        catalog: "SlotCatalog | None" = None,
    ) -> str | None:
        return (catalog or SlotCatalog()).field_for(slot)

    @property
    def slot_display_name(self) -> str | None:
        """슬롯이 게임에서 불리는 이름. 모르는 슬롯은 지어내지 않는다."""
        try:
            return SlotCatalog().describe(self.slot)[0]
        except ReviewError:
            return None

    def validate_slot_registration(
        self,
        catalog: "SlotCatalog | None" = None,
    ) -> None:
        """정식 승격 레시피는 Unity 가 실제로 읽는 슬롯만 겨눌 수 있다.

        슬롯 이름은 정규식만 맞으면 무엇이든 통과했다. 그래서 미등록 슬롯에
        게시하면 `.aseprite` 파일이 만들어지고 아무 일도 일어나지 않는다 —
        에디터가 읽지 않으므로 게임에는 영영 나타나지 않는데 파이프라인은
        "반영 완료"라고 말한다.
        """
        if not self.publishes_to_unity:
            return
        registry = catalog or SlotCatalog()
        known = registry.load_all()
        unregistered = [
            slot for slot in self.target_slots if slot not in known
        ]
        if unregistered:
            raise ReviewError(
                f"Recipe {self.id} promotes to Unity but targets "
                f"unregistered slot(s): {', '.join(unregistered)}. "
                f"Register them in {UNITY_SLOT_SOURCE.name} "
                "(CatalogSlots) first, or use a non-publishing "
                "output.promotion."
            )

    def validate_binding_nodes(self) -> None:
        """바인딩·업로드가 가리키는 노드가 워크플로 JSON 에 실제로 있는지 본다.

        없으면 워커가 ComfyError 로 죽는다 — 6장 생성을 큐에 넣고 몇 분 기다린
        뒤에 알게 되느니 검증에서 잡는다. 특히 워크플로 타입을 바꾸면 노드
        번호 체계가 통째로 달라진다.
        """
        import comfy_batch

        prompt, _, _ = comfy_batch.validate_workflow_pair(
            self.workflow_path
        )
        targets: dict[str, str] = {
            str(target): f"binding {name}"
            for name, target in self.pipeline.get("bindings", {}).items()
        }
        targets.update(
            {str(target): "upload"
             for target in self.pipeline.get("uploads", {})}
        )
        for shot in self.shots:
            targets.update(
                {str(target): f"shot {shot.id} upload"
                 for target in (shot.uploads or {})}
            )
        for target, label in sorted(targets.items()):
            node_id = target.split(".", 1)[0]
            if node_id not in prompt:
                raise ReviewError(
                    f"Recipe {self.id} {label} points at node {node_id!r}, "
                    f"which does not exist in {self.workflow_path.name}"
                )

    def validate_workflow_type(
        self,
        registry: "WorkflowTypeRegistry | None" = None,
    ) -> None:
        """`pipeline.type` 이 선언한 계약을 레시피가 실제로 채우는지 본다.

        타입 문자열만 맞고 바인딩이나 업로드가 비어 있으면 ComfyUI 는 조용히
        기본값으로 생성한다 — seed 재현성이 무너지는데 아무도 모른다.
        """
        declared = str(self.pipeline.get("type", "")).strip()
        if not declared:
            raise ReviewError(f"Recipe {self.id} pipeline.type is required")
        workflow_type = (registry or WorkflowTypeRegistry()).get(declared)

        bindings = set(self.pipeline.get("bindings", {}))
        missing_bindings = [
            name
            for name in workflow_type.required_bindings
            if name not in bindings
        ]
        if missing_bindings:
            raise ReviewError(
                f"Recipe {self.id} type {declared} requires bindings "
                f"{', '.join(missing_bindings)}"
            )

        # 업로드는 레시피 전체 또는 샷 하나가 채우면 된다 — 포즈 가이드처럼
        # 샷마다 다른 입력이 있기 때문이다.
        recipe_uploads = set(self.pipeline.get("uploads", {}))
        for target in workflow_type.required_uploads:
            if target in recipe_uploads:
                continue
            unfilled = [
                shot.id
                for shot in self.shots
                if target not in (shot.uploads or {})
            ]
            if unfilled:
                raise ReviewError(
                    f"Recipe {self.id} type {declared} requires upload "
                    f"{target}; missing on shot(s): {', '.join(unfilled)}"
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
            "asset_type": self.asset_type,
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


class SlotCatalog:
    """Unity 가 실제로 읽는 아트 슬롯의 발급 목록.

    `ProjectCAsepritePipeline.CatalogSlots` 가 슬롯 ID → `IsoVisualCatalog`
    필드명의 SSOT다. 파이썬이 목록을 따로 들고 있으면 반드시 어긋나므로
    원본을 그대로 읽는다. 여기 없는 슬롯 ID는 존재하지 않는 것이다.
    """

    _ENTRY = re.compile(r'\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}')

    def __init__(self, path: Path = UNITY_SLOT_SOURCE):
        self.path = path

    def load_all(self) -> dict[str, str]:
        if not self.path.is_file():
            raise ReviewError(f"Unity slot source is missing: {self.path}")
        source = self.path.read_text(encoding="utf-8")
        try:
            start = source.index("CatalogSlots =")
            block = source[start:]
            block = block[: block.index("};")]
        except ValueError as exc:
            raise ReviewError(
                f"{self.path} has no readable CatalogSlots block"
            ) from exc
        slots = {
            slot: field for slot, field in self._ENTRY.findall(block)
        }
        if not slots:
            raise ReviewError(f"{self.path} declares no catalog slots")
        return slots

    def field_for(self, slot: str) -> str | None:
        return self.load_all().get(slot)

    def is_registered(self, slot: str) -> bool:
        return slot in self.load_all()

    _ARCHETYPE = re.compile(
        r"public\s+static\s+readonly\s+MonsterArchetype\s+(\w+)\s*="
    )
    _DISPLAY_NAME = re.compile(r'displayName:\s*"([^"]+)"')
    _TAGS = re.compile(r"</?\w+[^>]*>")

    def monster_names(
        self,
        path: Path = UNITY_MONSTER_SOURCE,
    ) -> dict[str, tuple[str, str]]:
        """archetype 필드명 → (표시명, 한 줄 설명).

        표시명을 파이프라인이 다시 적으면 게임과 갈린다. `MonsterRoster` 를
        읽는다 — 없으면 이름 없이 슬롯 ID 만 보여주지, 지어내지 않는다.
        """
        if not path.is_file():
            return {}
        lines = path.read_text(encoding="utf-8").splitlines()
        found: dict[str, tuple[str, str]] = {}
        doc: list[str] = []
        for index, line in enumerate(lines):
            stripped = line.strip()
            if stripped.startswith("///"):
                # 바로 위에 붙은 주석만 이 선언의 것이다 — 사이에 다른 코드가
                # 끼면 클래스 주석을 몬스터 설명으로 착각한다.
                doc.append(stripped.lstrip("/").strip())
                continue
            match = self._ARCHETYPE.search(stripped)
            if match is None:
                if stripped:
                    doc = []
                continue
            tail = "\n".join(lines[index:index + 12])
            display = self._DISPLAY_NAME.search(tail)
            summary = self._TAGS.sub("", " ".join(doc)).strip()
            # 첫 문장까지만 — 카드 한 줄에 들어가야 한다.
            headline = summary.split(".")[0].strip()
            found[match.group(1)] = (
                display.group(1) if display else match.group(1),
                headline,
            )
            doc = []
        return found

    def describe(self, slot: str) -> tuple[str | None, str]:
        """슬롯의 (표시명, 설명). 이름을 아는 슬롯만 이름이 있다."""
        field = self.field_for(slot)
        if not field:
            return None, ""
        archetype = field[:1].upper() + field[1:]
        return self.monster_names().get(archetype, (None, ""))


@dataclass(frozen=True)
class WorkflowType:
    """`pipeline.type` 하나의 계약. workflow-types.yaml 이 소유한다."""

    document: dict[str, Any]

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def label(self) -> str:
        return str(self.document.get("label") or self.id)

    @property
    def summary_text(self) -> str:
        return str(self.document.get("summary", "")).strip()

    @property
    def default_workflow(self) -> str:
        return str(self.document.get("default_workflow", "")).strip()

    @property
    def required_bindings(self) -> tuple[str, ...]:
        requires = _mapping(self.document.get("requires", {}), "requires")
        return tuple(str(name) for name in requires.get("bindings", []))

    @property
    def upload_roles(self) -> dict[str, str]:
        """역할 이름 → ComfyUI NODE.INPUT. 대상은 역할만 알면 된다."""
        requires = _mapping(self.document.get("requires", {}), "requires")
        uploads = requires.get("uploads", {}) or {}
        if not isinstance(uploads, dict):
            raise ReviewError(
                f"Workflow type {self.id} requires.uploads must be a "
                "mapping of role -> NODE.INPUT"
            )
        return {str(role): str(target) for role, target in uploads.items()}

    @property
    def required_uploads(self) -> tuple[str, ...]:
        return tuple(sorted(self.upload_roles.values()))

    def node_for_role(self, role: str) -> str | None:
        return self.upload_roles.get(role)

    @property
    def supports_denoise(self) -> bool:
        supports = _mapping(self.document.get("supports", {}), "supports")
        return bool(supports.get("denoise", False))

    @property
    def supports_controlnet(self) -> bool:
        supports = _mapping(self.document.get("supports", {}), "supports")
        return bool(supports.get("controlnet", False))

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "label": self.label,
            "summary": self.summary_text,
            "default_workflow": self.default_workflow,
            "requires": {
                "bindings": list(self.required_bindings),
                "uploads": dict(self.upload_roles),
            },
            "supports": {
                "denoise": self.supports_denoise,
                "controlnet": self.supports_controlnet,
            },
        }


class WorkflowTypeRegistry:
    def __init__(self, path: Path = DEFAULT_WORKFLOW_TYPES_PATH):
        self.path = path.resolve()

    def load_all(self) -> dict[str, WorkflowType]:
        if not self.path.is_file():
            raise ReviewError(
                f"Workflow type registry is missing: {self.path}"
            )
        with self.path.open("r", encoding="utf-8") as handle:
            document = _mapping(yaml.safe_load(handle) or {}, "workflow-types")
        if document.get("schema_version") != 1:
            raise ReviewError(
                f"{self.path} has unsupported schema_version "
                f"{document.get('schema_version')!r}"
            )
        types: dict[str, WorkflowType] = {}
        for index, raw in enumerate(document.get("types", []) or []):
            entry = _mapping(raw, f"types[{index}]")
            if not str(entry.get("id", "")).strip():
                raise ReviewError(f"{self.path} types[{index}] has no id")
            workflow_type = WorkflowType(document=entry)
            if workflow_type.id in types:
                raise ReviewError(
                    f"Duplicate workflow type {workflow_type.id!r}"
                )
            types[workflow_type.id] = workflow_type
        if not types:
            raise ReviewError(f"{self.path} declares no workflow types")
        return types

    def get(self, type_id: str) -> WorkflowType:
        types = self.load_all()
        try:
            return types[type_id]
        except KeyError as exc:
            available = ", ".join(types) or "(none)"
            raise ReviewError(
                f"Unknown workflow type {type_id!r}; available: {available}"
            ) from exc


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


@dataclass(frozen=True)
class BatchItem:
    id: str
    recipe_id: str
    candidate_count: int = 1
    shot: str | None = None
    shot_cycle: tuple[str, ...] = ()
    notes: str = ""


@dataclass(frozen=True)
class BatchPlan:
    path: Path
    document: dict[str, Any]

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def name(self) -> str:
        return str(self.document["name"])

    @property
    def description(self) -> str:
        return str(self.document.get("description", ""))

    @property
    def items(self) -> tuple[BatchItem, ...]:
        result: list[BatchItem] = []
        for index, raw in enumerate(self.document["items"]):
            item = _mapping(raw, f"items[{index}]")
            cycle = item.get("shot_cycle", [])
            result.append(
                BatchItem(
                    id=str(item["id"]),
                    recipe_id=str(item["recipe"]),
                    candidate_count=_positive_int(
                        item.get("count", 1),
                        f"items[{index}].count",
                    ),
                    shot=(
                        str(item["shot"])
                        if item.get("shot")
                        else None
                    ),
                    shot_cycle=tuple(str(value) for value in cycle),
                    notes=str(item.get("notes", "")),
                )
            )
        return tuple(result)

    @classmethod
    def load(cls, path: Path) -> "BatchPlan":
        resolved = path.resolve()
        try:
            document = yaml.safe_load(resolved.read_text(encoding="utf-8"))
        except (OSError, yaml.YAMLError) as exc:
            raise ReviewError(f"Cannot load batch {resolved}: {exc}") from exc
        root = _mapping(document, "batch")
        if root.get("schema_version") != 1:
            raise ReviewError(
                f"Batch {resolved} has unsupported schema_version"
            )
        for key in ("id", "name", "items"):
            if not root.get(key):
                raise ReviewError(f"Batch {resolved} is missing {key!r}")
        if not isinstance(root["items"], list):
            raise ReviewError(f"Batch {resolved} items must be a list")
        plan = cls(path=resolved, document=root)
        item_ids = [item.id for item in plan.items]
        if len(item_ids) != len(set(item_ids)):
            raise ReviewError(f"Batch {resolved} has duplicate item ids")
        for item in plan.items:
            if item.shot and item.shot_cycle:
                raise ReviewError(
                    f"Batch item {item.id} cannot set shot and shot_cycle"
                )
            if not item.id or not SHOT_ID_PATTERN.fullmatch(item.id):
                raise ReviewError(f"Invalid batch item id {item.id!r}")
        return plan

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "description": self.description,
            "items": [
                {
                    "id": item.id,
                    "recipe_id": item.recipe_id,
                    "candidate_count": item.candidate_count,
                    "shot": item.shot,
                    "shot_cycle": list(item.shot_cycle),
                    "notes": item.notes,
                }
                for item in self.items
            ],
        }

    def validate_recipes(self, recipes: RecipeRegistry) -> None:
        for item in self.items:
            recipe = recipes.get(item.recipe_id)
            available_shots = {shot.id for shot in recipe.shots}
            requested = (
                ([item.shot] if item.shot else [])
                + list(item.shot_cycle)
            )
            unknown = [shot for shot in requested if shot not in available_shots]
            if unknown:
                raise ReviewError(
                    f"Batch {self.id} item {item.id} references unknown "
                    f"shots {unknown} in {recipe.id}"
                )


class BatchRegistry:
    def __init__(self, directory: Path = DEFAULT_BATCH_DIR):
        self.directory = directory.resolve()

    def load_all(self) -> dict[str, BatchPlan]:
        batches: dict[str, BatchPlan] = {}
        if not self.directory.is_dir():
            return batches
        for path in sorted(self.directory.glob("*.yaml")):
            plan = BatchPlan.load(path)
            if plan.id in batches:
                raise ReviewError(f"Duplicate batch id {plan.id!r}")
            batches[plan.id] = plan
        return batches

    def get(self, batch_id: str) -> BatchPlan:
        batches = self.load_all()
        try:
            return batches[batch_id]
        except KeyError as exc:
            available = ", ".join(sorted(batches)) or "(none)"
            raise ReviewError(
                f"Unknown batch {batch_id!r}; available: {available}"
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
    batch_id TEXT,
    batch_item_id TEXT,
    started_at TEXT,
    finished_at TEXT,
    progress_json TEXT,
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
    approved_snapshot_path TEXT,
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

CREATE TABLE IF NOT EXISTS outbox_deliveries (
    outbox_id INTEGER NOT NULL REFERENCES outbox(id) ON DELETE CASCADE,
    step_key TEXT NOT NULL,
    created_at TEXT NOT NULL,
    PRIMARY KEY(outbox_id, step_key)
);

CREATE TABLE IF NOT EXISTS batch_runs (
    id TEXT PRIMARY KEY,
    plan_id TEXT NOT NULL,
    plan_path TEXT NOT NULL,
    plan_json TEXT NOT NULL,
    requested_by TEXT NOT NULL,
    notes TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS batch_cursors (
    plan_id TEXT NOT NULL,
    item_id TEXT NOT NULL,
    next_index INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(plan_id, item_id)
);

CREATE TABLE IF NOT EXISTS apply_requests (
    id TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL REFERENCES candidates(id) ON DELETE CASCADE,
    requested_by TEXT NOT NULL,
    intent TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'queued',
    plan_json TEXT,
    result_json TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    error TEXT
);

CREATE INDEX IF NOT EXISTS idx_jobs_status ON jobs(status, created_at);
CREATE INDEX IF NOT EXISTS idx_candidates_job ON candidates(job_id, ordinal);
CREATE INDEX IF NOT EXISTS idx_feedback_status ON feedback(status, created_at);
CREATE INDEX IF NOT EXISTS idx_actions_status ON actions(status, created_at);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox(status, created_at);
CREATE INDEX IF NOT EXISTS idx_apply_status
ON apply_requests(status, created_at);
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
            outbox_columns = {
                row["name"]
                for row in connection.execute(
                    "PRAGMA table_info(outbox)"
                ).fetchall()
            }
            if "attempts" not in outbox_columns:
                connection.execute(
                    "ALTER TABLE outbox "
                    "ADD COLUMN attempts INTEGER NOT NULL DEFAULT 0"
                )
            job_columns = {
                row["name"]
                for row in connection.execute(
                    "PRAGMA table_info(jobs)"
                ).fetchall()
            }
            for column in (
                "batch_id",
                "batch_item_id",
                "started_at",
                "finished_at",
                "progress_json",
            ):
                if column not in job_columns:
                    connection.execute(
                        f"ALTER TABLE jobs ADD COLUMN {column} TEXT"
                    )
            connection.execute(
                "CREATE INDEX IF NOT EXISTS idx_jobs_batch "
                "ON jobs(batch_id, created_at)"
            )
            candidate_columns = {
                row["name"]
                for row in connection.execute(
                    "PRAGMA table_info(candidates)"
                ).fetchall()
            }
            if "approved_snapshot_path" not in candidate_columns:
                connection.execute(
                    "ALTER TABLE candidates "
                    "ADD COLUMN approved_snapshot_path TEXT"
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
            recovered += connection.execute(
                """
                UPDATE apply_requests
                SET status = 'queued', updated_at = ?
                WHERE status IN ('planning', 'applying') AND updated_at < ?
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
        batch_id: str | None = None,
        batch_item_id: str | None = None,
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
                    parent_candidate_id, batch_id, batch_item_id,
                    created_at, updated_at
                ) VALUES (
                    ?, ?, ?, ?, ?, 'queued', ?, ?, ?, ?, ?, ?, ?, ?, ?
                )
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
                    batch_id,
                    batch_item_id,
                    now,
                    now,
                ),
            )
        return job_id

    def create_batch_run(
        self,
        plan: BatchPlan,
        *,
        requested_by: str,
        jobs: list[tuple[str, Recipe, int, str | None, str]],
        notes: str = "",
    ) -> tuple[str, list[str]]:
        """Persist one validated batch and all of its generation jobs."""
        batch_id = make_id("BATCH")
        with self.connect() as connection:
            connection.execute(
                """
                INSERT INTO batch_runs (
                    id, plan_id, plan_path, plan_json, requested_by,
                    notes, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    batch_id,
                    plan.id,
                    relative_project_path(plan.path),
                    canonical_json(plan.document),
                    requested_by,
                    notes,
                    utc_now(),
                ),
            )
        job_ids = [
            self.create_job(
                recipe,
                requested_by=requested_by,
                candidate_count=count,
                notes=item_notes,
                batch_id=batch_id,
                batch_item_id=item_id,
            )
            for item_id, recipe, count, _shot_id, item_notes in jobs
        ]
        return batch_id, job_ids

    def next_batch_shot(
        self,
        plan_id: str,
        item_id: str,
        shots: tuple[str, ...],
    ) -> str | None:
        if not shots:
            return None
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            row = connection.execute(
                """
                SELECT next_index FROM batch_cursors
                WHERE plan_id = ? AND item_id = ?
                """,
                (plan_id, item_id),
            ).fetchone()
            index = int(row["next_index"]) if row else 0
            selected = shots[index % len(shots)]
            connection.execute(
                """
                INSERT INTO batch_cursors (plan_id, item_id, next_index)
                VALUES (?, ?, ?)
                ON CONFLICT(plan_id, item_id) DO UPDATE SET
                    next_index = excluded.next_index
                """,
                (plan_id, item_id, index + 1),
            )
        return selected

    def list_batch_runs(self, limit: int = 20) -> list[dict[str, Any]]:
        with self.connect() as connection:
            rows = connection.execute(
                """
                SELECT b.*,
                       COUNT(j.id) AS job_count,
                       SUM(CASE WHEN j.status = 'queued' THEN 1 ELSE 0 END)
                           AS queued_count,
                       SUM(CASE WHEN j.status = 'running' THEN 1 ELSE 0 END)
                           AS running_count,
                       SUM(CASE WHEN j.status = 'failed' THEN 1 ELSE 0 END)
                           AS failed_count,
                       SUM(CASE WHEN j.status = 'cancelled' THEN 1 ELSE 0 END)
                           AS cancelled_count,
                       SUM(CASE WHEN j.status = 'awaiting_review' THEN 1 ELSE 0 END)
                           AS review_count
                FROM batch_runs b
                LEFT JOIN jobs j ON j.batch_id = b.id
                GROUP BY b.id
                ORDER BY b.created_at DESC
                LIMIT ?
                """,
                (limit,),
            ).fetchall()
        return [row_dict(row) for row in rows]

    def get_batch_run(self, batch_id: str) -> dict[str, Any]:
        with self.connect() as connection:
            batch = connection.execute(
                "SELECT * FROM batch_runs WHERE id = ?",
                (batch_id,),
            ).fetchone()
            jobs = connection.execute(
                """
                SELECT * FROM jobs
                WHERE batch_id = ?
                ORDER BY created_at
                """,
                (batch_id,),
            ).fetchall()
        if batch is None:
            raise ReviewError(f"Unknown batch run {batch_id}")
        return {
            **row_dict(batch),
            "plan_json": json.loads(batch["plan_json"]),
            "jobs": [row_dict(job) for job in jobs],
        }

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
                    ORDER BY created_at DESC, rowid DESC
                    LIMIT ?
                    """,
                    (status, limit),
                ).fetchall()
            return connection.execute(
                """
                SELECT * FROM jobs
                ORDER BY created_at DESC, rowid DESC
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
                SET status = 'running', updated_at = ?, started_at = ?,
                    finished_at = NULL, progress_json = NULL, error = NULL
                WHERE id = ? AND status = 'queued'
                """,
                (now, now, row["id"]),
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
        now = utc_now()
        finished = now if status in FINISHED_JOB_STATES else None
        with self.connect() as connection:
            connection.execute(
                """
                UPDATE jobs
                SET status = ?, updated_at = ?, error = ?,
                    finished_at = COALESCE(?, finished_at)
                WHERE id = ?
                """,
                (status, now, error, finished, job_id),
            )

    def set_job_progress(
        self,
        job_id: str,
        progress: dict[str, Any] | None,
    ) -> None:
        """워커가 지금 어디쯤인지 DB에 남긴다.

        발주는 장당 수백 초라 콘솔 로그만으로는 밖에서 진행을 볼 수 없다.
        CLI·뷰어가 같은 값을 읽도록 상태 DB를 유일한 통로로 쓴다.
        """
        with self.connect() as connection:
            connection.execute(
                "UPDATE jobs SET progress_json = ? WHERE id = ?",
                (canonical_json(progress) if progress else None, job_id),
            )

    def unit_seconds(
        self,
        *,
        recipe_id: str | None = None,
        sample: int = 20,
    ) -> float | None:
        """완료된 job에서 잰 장당 초의 중앙값.

        문서의 실측 표를 사람이 옮겨 적는 대신 실제 이력에서 낸다. 표본이
        없으면 None — 추정치를 지어내지 않는다.
        """
        clause = "AND recipe_id = ?" if recipe_id else ""
        params: list[Any] = [recipe_id] if recipe_id else []
        params.append(max(1, sample))
        with self.connect() as connection:
            rows = connection.execute(
                f"""
                SELECT started_at, finished_at, progress_json
                FROM jobs
                WHERE started_at IS NOT NULL
                  AND finished_at IS NOT NULL
                  AND status IN ('awaiting_review', 'complete')
                  {clause}
                ORDER BY finished_at DESC
                LIMIT ?
                """,
                params,
            ).fetchall()
        samples: list[float] = []
        for row in rows:
            units = 0
            if row["progress_json"]:
                try:
                    units = int(
                        json.loads(row["progress_json"]).get("units_total", 0)
                    )
                except (json.JSONDecodeError, TypeError, ValueError):
                    units = 0
            if units <= 0:
                continue
            elapsed = elapsed_seconds(row["started_at"], row["finished_at"])
            if elapsed is None or elapsed <= 0:
                continue
            samples.append(elapsed / units)
        if not samples:
            return None
        samples.sort()
        middle = len(samples) // 2
        if len(samples) % 2:
            return samples[middle]
        return (samples[middle - 1] + samples[middle]) / 2

    def cancel_job(self, job_id: str) -> None:
        with self.connect() as connection:
            cursor = connection.execute(
                """
                UPDATE jobs
                SET status = 'cancelled', updated_at = ?, error = NULL
                WHERE id = ? AND status = 'queued'
                """,
                (utc_now(), job_id),
            )
        if cursor.rowcount == 0:
            job = self.get_job(job_id)
            raise ReviewError(
                f"Job {job_id} cannot be cancelled from {job['status']}"
            )

    def retry_job(self, job_id: str) -> None:
        with self.connect() as connection:
            cursor = connection.execute(
                """
                UPDATE jobs
                SET status = 'queued', updated_at = ?, error = NULL
                WHERE id = ? AND status = 'failed'
                """,
                (utc_now(), job_id),
            )
        if cursor.rowcount == 0:
            job = self.get_job(job_id)
            raise ReviewError(
                f"Job {job_id} cannot be retried from {job['status']}"
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
            return self._candidate_is_approved(connection, candidate_id)

    def approved_candidate_source(self, candidate_id: str) -> Path:
        """명시적으로 승인된 후보의 불변 snapshot 원본을 반환한다."""
        with self.connect() as connection:
            candidate = connection.execute(
                "SELECT * FROM candidates WHERE id = ?",
                (candidate_id,),
            ).fetchone()
            if candidate is None:
                raise ReviewError(f"Unknown candidate {candidate_id}")
            if not self._candidate_is_approved(connection, candidate_id):
                raise ReviewError(
                    f"{candidate_id} has no current explicit approval"
                )
            snapshot_value = candidate["approved_snapshot_path"]
        if not snapshot_value:
            raise ReviewError(
                f"{candidate_id} has no approval snapshot"
            )
        source = project_path(snapshot_value) / "raw.png"
        if not source.is_file():
            raise ReviewError(
                f"{candidate_id} approval source is missing: {source}"
            )
        return source

    @staticmethod
    def _candidate_is_approved(
        connection: sqlite3.Connection,
        candidate_id: str,
    ) -> bool:
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

    def list_recent_candidates(
        self,
        limit: int = 24,
        *,
        recipe_id: str | None = None,
        status: str | None = None,
    ) -> list[sqlite3.Row]:
        """리뷰 순서의 SSOT.

        뷰어 격자, CLI 선택기 번호, `^N` 별칭이 모두 이 순서를 쓴다 — 같은 축을
        세 UI가 다르게 정렬하면 "3번 채택"이 서로 다른 후보를 가리킨다.
        """
        clauses: list[str] = []
        params: list[Any] = []
        if recipe_id:
            clauses.append("jobs.recipe_id = ?")
            params.append(recipe_id)
        if status:
            clauses.append("candidates.status = ?")
            params.append(status)
        where = f"WHERE {' AND '.join(clauses)}" if clauses else ""
        params.append(max(1, limit))
        with self.connect() as connection:
            return connection.execute(
                f"""
                SELECT
                    candidates.*,
                    jobs.recipe_id AS recipe_id,
                    jobs.status AS job_status,
                    jobs.notes AS job_notes,
                    jobs.created_at AS job_created_at
                FROM candidates
                JOIN jobs ON jobs.id = candidates.job_id
                {where}
                ORDER BY jobs.created_at DESC, jobs.rowid DESC,
                         candidates.ordinal ASC
                LIMIT ?
                """,
                params,
            ).fetchall()

    def resolve_candidate_id(self, token: str | None) -> str:
        """별칭·부분 일치를 실제 후보 ID로 바꾼다.

        `ART-...-C01`을 카드에서 터미널로 옮겨 적는 동작이 판정마다 반복돼서,
        `latest`/`^N`/`<recipe>@^N`/부분 일치를 같은 자리에서 받는다.
        """
        raw = (token or "").strip()
        if not raw:
            raise ReviewError("Candidate reference is empty")
        with self.connect() as connection:
            exact = connection.execute(
                "SELECT id FROM candidates WHERE id = ?",
                (raw,),
            ).fetchone()
        if exact is not None:
            return str(exact["id"])

        recipe_id: str | None = None
        reference = raw
        if "@" in raw:
            head, _, tail = raw.partition("@")
            recipe_id = head.strip() or None
            reference = tail.strip() or "^"

        index = alias_index(reference)
        if index is not None:
            rows = self.list_recent_candidates(index, recipe_id=recipe_id)
            if len(rows) < index:
                scope = f" for recipe {recipe_id}" if recipe_id else ""
                raise ReviewError(
                    f"Only {len(rows)} recent candidates{scope} — "
                    f"{raw!r} points past the end"
                )
            return str(rows[index - 1]["id"])
        if recipe_id is not None:
            raise ReviewError(
                f"{raw!r} is not a recipe alias — use "
                f"'{recipe_id}@latest' or '{recipe_id}@^2'"
            )
        return self._match_candidate_reference(raw)

    def _match_candidate_reference(self, raw: str) -> str:
        with self.connect() as connection:
            from_job = connection.execute(
                """
                SELECT id FROM candidates
                WHERE job_id = ?
                ORDER BY ordinal
                """,
                (raw,),
            ).fetchall()
            if len(from_job) == 1:
                return str(from_job[0]["id"])
            if from_job:
                names = ", ".join(str(row["id"]) for row in from_job)
                raise ReviewError(
                    f"Job {raw} has {len(from_job)} candidates: {names}"
                )
            matches = connection.execute(
                """
                SELECT id FROM candidates
                WHERE id LIKE ? ESCAPE '\\'
                ORDER BY id DESC
                LIMIT 6
                """,
                (f"%{like_escape(raw)}%",),
            ).fetchall()
        if len(matches) == 1:
            return str(matches[0]["id"])
        if not matches:
            raise ReviewError(f"Unknown candidate {raw}")
        names = ", ".join(str(row["id"]) for row in matches)
        raise ReviewError(f"{raw!r} matches several candidates: {names}")

    def resolve_job_id(self, token: str | None) -> str:
        """후보 해석기와 같은 어휘를 job 인자에도 준다."""
        raw = (token or "").strip()
        if not raw:
            raise ReviewError("Job reference is empty")
        with self.connect() as connection:
            exact = connection.execute(
                "SELECT id FROM jobs WHERE id = ?",
                (raw,),
            ).fetchone()
            if exact is not None:
                return str(exact["id"])
            owner = connection.execute(
                "SELECT job_id FROM candidates WHERE id = ?",
                (raw,),
            ).fetchone()
        if owner is not None:
            return str(owner["job_id"])

        index = alias_index(raw)
        if index is not None:
            rows = self.list_jobs(limit=index)
            if len(rows) < index:
                raise ReviewError(
                    f"Only {len(rows)} recent jobs — "
                    f"{raw!r} points past the end"
                )
            return str(rows[index - 1]["id"])

        with self.connect() as connection:
            matches = connection.execute(
                """
                SELECT id FROM jobs
                WHERE id LIKE ? ESCAPE '\\'
                ORDER BY created_at DESC
                LIMIT 6
                """,
                (f"%{like_escape(raw)}%",),
            ).fetchall()
        if len(matches) == 1:
            return str(matches[0]["id"])
        if not matches:
            raise ReviewError(f"Unknown job {raw}")
        names = ", ".join(str(row["id"]) for row in matches)
        raise ReviewError(f"{raw!r} matches several jobs: {names}")

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

    def set_approved_snapshot(
        self,
        candidate_id: str,
        snapshot_path: Path,
    ) -> None:
        with self.connect() as connection:
            cursor = connection.execute(
                """
                UPDATE candidates
                SET approved_snapshot_path = ?, updated_at = ?
                WHERE id = ?
                """,
                (
                    relative_project_path(snapshot_path),
                    utc_now(),
                    candidate_id,
                ),
            )
        if cursor.rowcount == 0:
            raise ReviewError(f"Unknown candidate {candidate_id}")

    def create_apply_request(
        self,
        candidate_id: str,
        *,
        requested_by: str,
        intent: str = "",
    ) -> str:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            candidate = connection.execute(
                "SELECT * FROM candidates WHERE id = ?",
                (candidate_id,),
            ).fetchone()
            if candidate is None:
                raise ReviewError(f"Unknown candidate {candidate_id}")
            if not self._candidate_is_approved(connection, candidate_id):
                raise ReviewError(
                    f"{candidate_id} has no current explicit approval"
                )
            if (
                candidate["status"] not in {"approved", "prepared"}
                or not candidate["approved_snapshot_path"]
            ):
                raise ReviewError(
                    f"{candidate_id} cannot be applied from "
                    f"{candidate['status']} without an approval snapshot"
                )
            active = connection.execute(
                """
                SELECT id, status FROM apply_requests
                WHERE candidate_id = ?
                  AND status IN ('queued', 'planning', 'applying', 'needs_input')
                ORDER BY created_at DESC
                LIMIT 1
                """,
                (candidate_id,),
            ).fetchone()
            if active:
                if not intent:
                    return str(active["id"])
                if active["status"] == "queued":
                    connection.execute(
                        """
                        UPDATE apply_requests
                        SET intent = ?, updated_at = ?, error = NULL
                        WHERE id = ?
                        """,
                        (intent, utc_now(), active["id"]),
                    )
                    return str(active["id"])
                if active["status"] == "needs_input":
                    connection.execute(
                        """
                        UPDATE apply_requests
                        SET status = 'queued', intent = ?, updated_at = ?,
                            error = NULL
                        WHERE id = ?
                        """,
                        (intent, utc_now(), active["id"]),
                    )
                    self._enqueue_outbox(
                        connection,
                        "apply_queued",
                        {
                            "apply_request_id": str(active["id"]),
                            "candidate_id": candidate_id,
                        },
                    )
                    return str(active["id"])
                raise ReviewError(
                    f"Apply request {active['id']} is already "
                    f"{active['status']}; create a new request after it finishes"
                )
            request_id = make_id("APPLY")
            now = utc_now()
            connection.execute(
                """
                INSERT INTO apply_requests (
                    id, candidate_id, requested_by, intent, status,
                    created_at, updated_at
                ) VALUES (?, ?, ?, ?, 'queued', ?, ?)
                """,
                (
                    request_id,
                    candidate_id,
                    requested_by,
                    intent,
                    now,
                    now,
                ),
            )
            self._enqueue_outbox(
                connection,
                "apply_queued",
                {
                    "apply_request_id": request_id,
                    "candidate_id": candidate_id,
                },
            )
        return request_id

    def list_apply_requests(
        self,
        *,
        status: str | None = None,
        limit: int = 50,
    ) -> list[sqlite3.Row]:
        with self.connect() as connection:
            if status:
                return connection.execute(
                    """
                    SELECT * FROM apply_requests
                    WHERE status = ?
                    ORDER BY created_at DESC
                    LIMIT ?
                    """,
                    (status, limit),
                ).fetchall()
            return connection.execute(
                """
                SELECT * FROM apply_requests
                ORDER BY created_at DESC
                LIMIT ?
                """,
                (limit,),
            ).fetchall()

    def get_apply_request(self, request_id: str) -> sqlite3.Row:
        with self.connect() as connection:
            row = connection.execute(
                "SELECT * FROM apply_requests WHERE id = ?",
                (request_id,),
            ).fetchone()
        if row is None:
            raise ReviewError(f"Unknown apply request {request_id}")
        return row

    def claim_apply_request(
        self,
        request_id: str | None = None,
    ) -> sqlite3.Row | None:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            if request_id:
                row = connection.execute(
                    """
                    SELECT * FROM apply_requests
                    WHERE id = ? AND status = 'queued'
                    """,
                    (request_id,),
                ).fetchone()
            else:
                row = connection.execute(
                    """
                    SELECT * FROM apply_requests
                    WHERE status = 'queued'
                    ORDER BY created_at
                    LIMIT 1
                    """
                ).fetchone()
            if row is None:
                return None
            candidate = connection.execute(
                "SELECT * FROM candidates WHERE id = ?",
                (row["candidate_id"],),
            ).fetchone()
            valid = (
                candidate is not None
                and candidate["status"] in {"approved", "prepared"}
                and bool(candidate["approved_snapshot_path"])
                and self._candidate_is_approved(
                    connection,
                    row["candidate_id"],
                )
            )
            if not valid:
                message = "Candidate approval is no longer valid"
                connection.execute(
                    """
                    UPDATE apply_requests
                    SET status = 'cancelled', updated_at = ?, error = ?
                    WHERE id = ?
                    """,
                    (utc_now(), message, row["id"]),
                )
                self._enqueue_outbox(
                    connection,
                    "apply_status",
                    {
                        "apply_request_id": row["id"],
                        "status": "cancelled",
                        "error": message,
                        "result": None,
                    },
                )
                return None
            connection.execute(
                """
                UPDATE apply_requests
                SET status = 'planning', updated_at = ?, error = NULL
                WHERE id = ? AND status = 'queued'
                """,
                (utc_now(), row["id"]),
            )
            return connection.execute(
                "SELECT * FROM apply_requests WHERE id = ?",
                (row["id"],),
            ).fetchone()

    def set_apply_request_status(
        self,
        request_id: str,
        status: str,
        *,
        plan: dict[str, Any] | None = None,
        result: dict[str, Any] | None = None,
        error: str | None = None,
        expected_statuses: tuple[str, ...] | None = None,
    ) -> None:
        if status not in VALID_APPLY_STATES:
            raise ReviewError(f"Invalid apply request status {status!r}")
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            expected_clause = ""
            expected_values: tuple[str, ...] = ()
            if expected_statuses:
                expected_clause = (
                    " AND status IN ("
                    + ", ".join("?" for _ in expected_statuses)
                    + ")"
                )
                expected_values = expected_statuses
            cursor = connection.execute(
                f"""
                UPDATE apply_requests
                SET status = ?,
                    plan_json = COALESCE(?, plan_json),
                    result_json = COALESCE(?, result_json),
                    updated_at = ?,
                    error = ?
                WHERE id = ?{expected_clause}
                """,
                (
                    status,
                    canonical_json(plan) if plan is not None else None,
                    canonical_json(result) if result is not None else None,
                    utc_now(),
                    error,
                    request_id,
                    *expected_values,
                ),
            )
            if cursor.rowcount == 0:
                raise ReviewError(
                    f"Apply request {request_id} does not exist or changed "
                    "state concurrently"
                )
            self._enqueue_outbox(
                connection,
                "apply_status",
                {
                    "apply_request_id": request_id,
                    "status": status,
                    "error": error,
                    "result": result,
                },
            )

    def cancel_apply_requests_for_candidate(
        self,
        candidate_id: str,
        *,
        reason: str,
    ) -> int:
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            rows = connection.execute(
                """
                SELECT id FROM apply_requests
                WHERE candidate_id = ?
                  AND status IN ('queued', 'planning', 'applying', 'needs_input')
                """,
                (candidate_id,),
            ).fetchall()
            now = utc_now()
            for row in rows:
                connection.execute(
                    """
                    UPDATE apply_requests
                    SET status = 'cancelled', updated_at = ?, error = ?
                    WHERE id = ?
                    """,
                    (now, reason, row["id"]),
                )
                self._enqueue_outbox(
                    connection,
                    "apply_status",
                    {
                        "apply_request_id": row["id"],
                        "status": "cancelled",
                        "error": reason,
                        "result": None,
                    },
                )
            return len(rows)

    def apply_context(self, request_id: str) -> dict[str, Any]:
        request = self.get_apply_request(request_id)
        candidate = self.get_candidate(request["candidate_id"])
        job = self.get_job(candidate["job_id"])
        recipe = recipe_from_job(job)
        with self.connect() as connection:
            feedback = connection.execute(
                """
                SELECT * FROM feedback
                WHERE candidate_id = ?
                ORDER BY created_at
                """,
                (candidate["id"],),
            ).fetchall()
        return {
            "request": row_dict(request),
            "candidate": {
                **row_dict(candidate),
                "raw_path": str(project_path(candidate["raw_path"])),
                "prepared_path": (
                    str(project_path(candidate["prepared_path"]))
                    if candidate["prepared_path"]
                    else None
                ),
                "aseprite_path": (
                    str(project_path(candidate["aseprite_path"]))
                    if candidate["aseprite_path"]
                    else None
                ),
                "approved_snapshot_path": (
                    str(project_path(candidate["approved_snapshot_path"]))
                    if candidate["approved_snapshot_path"]
                    else None
                ),
            },
            "job": {
                **row_dict(job),
                "recipe_json": json.loads(job["recipe_json"]),
            },
            "recipe": recipe.summary(),
            "feedback": [row_dict(row) for row in feedback],
        }

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
                WHERE status IN ('pending', 'processing')
                ORDER BY created_at
                LIMIT ?
                """,
                (limit,),
            ).fetchall()

    def start_feedback(
        self,
        feedback_id: int,
        detail: str,
    ) -> bool:
        with self.connect() as connection:
            feedback = connection.execute(
                "SELECT * FROM feedback WHERE id = ?",
                (feedback_id,),
            ).fetchone()
            if feedback is None:
                raise ReviewError(f"Feedback {feedback_id} does not exist")
            if feedback["status"] == "processing":
                return False
            if feedback["status"] != "pending":
                raise ReviewError(
                    f"Feedback {feedback_id} is already "
                    f"{feedback['status']}"
                )
            connection.execute(
                """
                UPDATE feedback
                SET status = 'processing'
                WHERE id = ? AND status = 'pending'
                """,
                (feedback_id,),
            )
            self._enqueue_outbox(
                connection,
                "feedback_progress",
                {
                    "feedback_id": feedback_id,
                    "job_id": feedback["job_id"],
                    "candidate_id": feedback["candidate_id"],
                    "detail": detail,
                },
            )
            return True

    def resolve_feedback(self, feedback_id: int, resolution: str) -> None:
        with self.connect() as connection:
            feedback = connection.execute(
                "SELECT * FROM feedback WHERE id = ?",
                (feedback_id,),
            ).fetchone()
            if feedback is None:
                raise ReviewError(f"Feedback {feedback_id} does not exist")
            cursor = connection.execute(
                """
                UPDATE feedback
                SET status = 'resolved', resolved_at = ?, resolution = ?
                WHERE id = ? AND status IN ('pending', 'processing')
                """,
                (utc_now(), resolution, feedback_id),
            )
            if cursor.rowcount == 0:
                raise ReviewError(
                    f"Feedback {feedback_id} is already "
                    f"{feedback['status']}"
                )
            self._enqueue_outbox(
                connection,
                "feedback_resolved",
                {
                    "feedback_id": feedback_id,
                    "job_id": feedback["job_id"],
                    "candidate_id": feedback["candidate_id"],
                    "resolution": resolution,
                },
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

    @staticmethod
    def _enqueue_outbox(
        connection: sqlite3.Connection,
        kind: str,
        payload: dict[str, Any],
    ) -> int:
        now = utc_now()
        cursor = connection.execute(
            """
            INSERT INTO outbox (
                kind, payload_json, status, created_at, updated_at
            ) VALUES (?, ?, 'pending', ?, ?)
            """,
            (kind, canonical_json(payload), now, now),
        )
        return int(cursor.lastrowid)

    def enqueue_outbox(self, kind: str, payload: dict[str, Any]) -> int:
        with self.connect() as connection:
            return self._enqueue_outbox(connection, kind, payload)

    def outbox_delivery_done(self, outbox_id: int, step_key: str) -> bool:
        with self.connect() as connection:
            row = connection.execute(
                """
                SELECT 1 FROM outbox_deliveries
                WHERE outbox_id = ? AND step_key = ?
                """,
                (outbox_id, step_key),
            ).fetchone()
        return row is not None

    def mark_outbox_delivery(self, outbox_id: int, step_key: str) -> None:
        with self.connect() as connection:
            connection.execute(
                """
                INSERT OR IGNORE INTO outbox_deliveries (
                    outbox_id, step_key, created_at
                ) VALUES (?, ?, ?)
                """,
                (outbox_id, step_key, utc_now()),
            )

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

    def find_candidate_details_slack_message(
        self,
        candidate_id: str,
    ) -> sqlite3.Row | None:
        with self.connect() as connection:
            return connection.execute(
                """
                SELECT * FROM slack_messages
                WHERE candidate_id = ? AND kind = 'candidate-details'
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


def enforce_color_area_limits(
    path: Path,
    quality_gates: dict[str, Any],
    *,
    alpha_cutoff: int = 80,
) -> dict[str, float]:
    """Reject palette-legal images that spend too much area on signal colors."""
    limits = quality_gates.get("color_area_limits", {}) or {}
    if not isinstance(limits, dict):
        raise ReviewError("quality_gates.color_area_limits must be a mapping")
    if not limits:
        return {}

    with Image.open(path) as source:
        rgba = source.convert("RGBA")
    pixels = (
        rgba.get_flattened_data()
        if hasattr(rgba, "get_flattened_data")
        else rgba.getdata()
    )
    visible = [
        (red, green, blue)
        for red, green, blue, alpha in pixels
        if alpha >= alpha_cutoff
    ]
    if not visible:
        raise ReviewError(f"color area gate requires visible pixels: {path}")

    measured: dict[str, float] = {}
    for role, raw_spec in limits.items():
        if not isinstance(raw_spec, dict):
            raise ReviewError(
                f"color_area_limits.{role} must be a mapping"
            )
        raw_colors = raw_spec.get("colors", []) or []
        maximum = raw_spec.get("maximum_fraction")
        if not isinstance(raw_colors, list) or not raw_colors:
            raise ReviewError(
                f"color_area_limits.{role}.colors must be a non-empty list"
            )
        if not isinstance(maximum, (int, float)) or not 0 <= maximum <= 1:
            raise ReviewError(
                f"color_area_limits.{role}.maximum_fraction must be in 0..1"
            )

        colors: set[tuple[int, int, int]] = set()
        for raw_color in raw_colors:
            match = re.fullmatch(r"#([0-9a-fA-F]{6})", str(raw_color))
            if match is None:
                raise ReviewError(
                    f"color_area_limits.{role} has invalid RGB hex "
                    f"{raw_color!r}"
                )
            value = int(match.group(1), 16)
            colors.add((value >> 16, (value >> 8) & 255, value & 255))

        fraction = sum(pixel in colors for pixel in visible) / len(visible)
        measured[str(role)] = fraction
        if fraction > float(maximum):
            raise ReviewError(
                f"{role} color area {fraction:.1%} exceeds "
                f"{float(maximum):.1%}: {path}"
            )
    return measured


def recipe_from_job(row: sqlite3.Row) -> Recipe:
    document = json.loads(row["recipe_json"])
    return Recipe.from_document(
        document,
        path=project_path(row["recipe_path"]),
    )


def row_dict(row: sqlite3.Row) -> dict[str, Any]:
    return {key: row[key] for key in row.keys()}
