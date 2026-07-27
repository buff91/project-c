#!/usr/bin/env python3
"""대상(subject) × 방법(method) → 하나의 완전한 레시피 문서.

예전에는 레시피 한 파일이 "무엇을 만드나"와 "어떻게 만드나"를 함께 들고 있었다.
그래서 같은 캐릭터를 겨누는 레시피 6개에서 43개 필드가 글자 그대로 같았고,
새 캐릭터 하나를 추가하려면 108줄을 복사해 4곳만 고쳐야 했다. 대상이 선택지로
보이지 않고 레시피 **이름**에 숨는 것도 같은 원인이다.

- 대상(`subjects/*.yaml`)  : 슬롯 · 정체성 프롬프트 · 가이드 이미지
- 대상 묶음(`subject-sets/*.yaml`) : 함께 검수하는 대상들(이펙트 6종처럼)
- 방법(`methods/*.yaml`)   : 워크플로 · 모델 · LoRA · 생성값 · 출력 규격 · 포즈 목록

`resolve()` 가 둘을 합쳐 **기존 레시피와 똑같은 모양의 문서**를 만든다. 그래서
하위 경로(assignments · 샷 전개 · Aseprite 마감 · 게시)는 아무것도 바뀌지 않고,
job 은 지금처럼 합성 결과 전체를 `recipe_json` 으로 스냅샷하므로 재현성도 그대로다.
"""

from __future__ import annotations

import copy
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import yaml

from art_review import (
    PROJECT_ROOT,
    Recipe,
    ReviewError,
    SlotCatalog,
    WorkflowTypeRegistry,
    _mapping,
    project_path,
)


DEFAULT_SUBJECT_DIR = PROJECT_ROOT / "docs/art-direction/comfyui/subjects"
DEFAULT_SUBJECT_SET_DIR = (
    PROJECT_ROOT / "docs/art-direction/comfyui/subject-sets"
)
DEFAULT_METHOD_DIR = PROJECT_ROOT / "docs/art-direction/comfyui/methods"


def _load_yaml(path: Path, label: str) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        document = _mapping(yaml.safe_load(handle) or {}, label)
    if document.get("schema_version") != 1:
        raise ReviewError(
            f"{path} has unsupported schema_version "
            f"{document.get('schema_version')!r}"
        )
    return document


@dataclass(frozen=True)
class Subject:
    """무엇을 만드나 — 슬롯 하나와 그 정체성."""

    path: Path
    document: dict[str, Any]

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def name(self) -> str:
        """표시명. 안 적으면 게임의 로스터가 아는 이름을 쓴다."""
        declared = str(self.document.get("name", "")).strip()
        if declared:
            return declared
        known = SlotCatalog().describe(self.slot)[0]
        return known or self.id

    @property
    def slot(self) -> str:
        return str(self.document["slot"])

    @property
    def asset_type(self) -> str:
        return str(self.document["asset_type"])

    @property
    def identity(self) -> str:
        """정체성 프롬프트 — 이 대상을 이 대상이게 하는 문장."""
        return str(_mapping(
            self.document.get("prompt", {}), "prompt"
        ).get("positive", "")).strip()

    @property
    def excludes(self) -> str:
        return str(_mapping(
            self.document.get("prompt", {}), "prompt"
        ).get("negative", "")).strip()

    @property
    def signature_pose(self) -> str:
        """이 대상의 대표 동작. 슬링어는 슬링을 돌리고, 기사는 검을 든다."""
        return str(_mapping(
            self.document.get("prompt", {}), "prompt"
        ).get("signature_pose", "")).strip()

    @property
    def readability_goal(self) -> str:
        return str(self.document.get("readability_goal", "")).strip()

    def guides(self, variants: dict[str, str] | None = None) -> dict[str, str]:
        """역할 → 가이드 이미지. 노드 번호는 워크플로 타입이 안다.

        한 역할에 여러 후보를 둘 수 있다. 같은 캐릭터라도 기본 자세는 원화를,
        애니 키포즈는 런타임 스프라이트를 정체성 앵커로 쓰는 식이다. **경로는
        대상이 알고, 어느 변형을 쓸지는 방법이 고른다** — 방법에 경로를 적으면
        대상마다 방법이 갈라진다.
        """
        chosen = variants or {}
        found: dict[str, str] = {}
        for role, source in (self.document.get("guides", {}) or {}).items():
            role = str(role)
            if isinstance(source, dict):
                want = chosen.get(role, "default")
                if want not in source:
                    available = ", ".join(sorted(source))
                    raise ReviewError(
                        f"Subject {self.id} guide {role!r} has no variant "
                        f"{want!r}; available: {available}"
                    )
                found[role] = str(source[want])
            else:
                found[role] = str(source)
        return found

    @property
    def pose_guides(self) -> dict[str, str]:
        """포즈 샷 id → 그 포즈의 가이드 이미지."""
        return {
            str(shot): str(source)
            for shot, source in (
                self.document.get("pose_guides", {}) or {}
            ).items()
        }

    def validate_files(self) -> None:
        for role, source in {**self.guides(), **self.pose_guides}.items():
            path = project_path(source)
            if not path.is_file():
                raise ReviewError(
                    f"Subject {self.id} guide {role} is missing: {path}"
                )

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "slot": self.slot,
            "asset_type": self.asset_type,
            "guides": sorted(self.guides()),
            "pose_guides": sorted(self.pose_guides),
        }

    @classmethod
    def load(cls, path: Path) -> "Subject":
        document = _load_yaml(path, "subject")
        for key in ("id", "slot", "asset_type"):
            if not str(document.get(key, "")).strip():
                raise ReviewError(f"Subject {path} is missing {key!r}")
        return cls(path=path, document=document)


@dataclass(frozen=True)
class SubjectSet:
    """함께 검수하는 대상 묶음. 이펙트 6종처럼 한 판에 같이 보는 것들."""

    path: Path
    document: dict[str, Any]

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def name(self) -> str:
        return str(self.document.get("name") or self.id)

    @property
    def asset_type(self) -> str:
        return str(self.document["asset_type"])

    @property
    def member_ids(self) -> tuple[str, ...]:
        members = self.document.get("members", []) or []
        return tuple(str(member) for member in members)

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "asset_type": self.asset_type,
            "members": list(self.member_ids),
        }

    @classmethod
    def load(cls, path: Path) -> "SubjectSet":
        document = _load_yaml(path, "subject-set")
        for key in ("id", "asset_type"):
            if not str(document.get(key, "")).strip():
                raise ReviewError(f"Subject set {path} is missing {key!r}")
        if not document.get("members"):
            raise ReviewError(f"Subject set {path} has no members")
        return cls(path=path, document=document)


@dataclass(frozen=True)
class Method:
    """어떻게 만드나 — 대상이 바뀌어도 그대로인 전부."""

    path: Path
    document: dict[str, Any]

    @property
    def id(self) -> str:
        return str(self.document["id"])

    @property
    def name(self) -> str:
        return str(self.document["name"])

    @property
    def applies_to(self) -> tuple[str, ...]:
        """이 방법을 쓸 수 있는 에셋 타입."""
        return tuple(
            str(item) for item in (self.document.get("applies_to", []) or [])
        )

    @property
    def poses(self) -> tuple[dict[str, Any], ...]:
        """방법이 정하는 포즈 목록. 비면 대상 하나당 한 장이다."""
        return tuple(
            _mapping(item, f"poses[{index}]")
            for index, item in enumerate(self.document.get("poses", []) or [])
        )

    @property
    def prompt(self) -> dict[str, Any]:
        return _mapping(self.document.get("prompt", {}), "prompt")

    def accepts(self, asset_type: str) -> bool:
        return not self.applies_to or asset_type in self.applies_to

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "applies_to": list(self.applies_to),
            "poses": [str(pose.get("id")) for pose in self.poses],
            "workflow_type": str(
                _mapping(self.document.get("pipeline", {}), "pipeline")
                .get("type", "")
            ),
        }

    @classmethod
    def load(cls, path: Path) -> "Method":
        document = _load_yaml(path, "method")
        for key in ("id", "name", "output", "pipeline", "generation"):
            if key not in document:
                raise ReviewError(f"Method {path} is missing {key!r}")
        return cls(path=path, document=document)


class _Registry:
    """*.yaml 한 디렉터리를 읽는 공통 골격."""

    loader: Any = None
    label = "entry"

    def __init__(self, directory: Path):
        self.directory = directory.resolve()

    def load_all(self) -> dict[str, Any]:
        found: dict[str, Any] = {}
        if not self.directory.is_dir():
            return found
        for path in sorted(self.directory.glob("*.yaml")):
            entry = self.loader.load(path)
            if entry.id in found:
                raise ReviewError(
                    f"Duplicate {self.label} id {entry.id!r}"
                )
            found[entry.id] = entry
        return found

    def get(self, entry_id: str) -> Any:
        entries = self.load_all()
        try:
            return entries[entry_id]
        except KeyError as exc:
            available = ", ".join(sorted(entries)) or "(none)"
            raise ReviewError(
                f"Unknown {self.label} {entry_id!r}; available: {available}"
            ) from exc


class SubjectRegistry(_Registry):
    loader = Subject
    label = "subject"

    def __init__(self, directory: Path = DEFAULT_SUBJECT_DIR):
        super().__init__(directory)


class SubjectSetRegistry(_Registry):
    loader = SubjectSet
    label = "subject set"

    def __init__(self, directory: Path = DEFAULT_SUBJECT_SET_DIR):
        super().__init__(directory)


class MethodRegistry(_Registry):
    loader = Method
    label = "method"

    def __init__(self, directory: Path = DEFAULT_METHOD_DIR):
        super().__init__(directory)


def _join_prompt(*parts: str) -> str:
    return ", ".join(part.strip().strip(",") for part in parts if part.strip())


def _shot_document(
    *,
    shot_id: str,
    label: str,
    subject: Subject,
    method: Method,
    pose: dict[str, Any] | None,
    workflow_type: Any,
) -> dict[str, Any]:
    """샷 하나를 기존 레시피의 `shots[]` 모양으로 만든다."""
    pose = pose or {}
    document: dict[str, Any] = {
        "id": shot_id,
        "label": label,
        "slot": subject.slot,
        "prompt_suffix": str(pose.get("prompt_suffix", "")),
        "seed_offset": int(pose.get("seed_offset", 0)),
    }
    if pose.get("negative_suffix"):
        document["negative_suffix"] = str(pose["negative_suffix"])
    if pose.get("output_canvas"):
        document["output_canvas"] = list(pose["output_canvas"])
    if pose.get("overrides"):
        document["overrides"] = dict(pose["overrides"])

    # 포즈 가이드는 대상이 역할로 준다 — 노드 번호는 워크플로 타입이 붙인다.
    uploads: dict[str, str] = {}
    pose_node = workflow_type.node_for_role("pose")
    guide = subject.pose_guides.get(shot_id)
    if guide and pose_node:
        uploads[pose_node] = guide
    if pose.get("uploads"):
        uploads.update(
            {str(key): str(value) for key, value in pose["uploads"].items()}
        )
    if uploads:
        document["uploads"] = uploads
    return document


def resolve(
    method: Method,
    subjects: Iterable[Subject],
    *,
    set_id: str | None = None,
    set_name: str | None = None,
    workflow_types: WorkflowTypeRegistry | None = None,
) -> Recipe:
    """방법 × 대상 → 기존 레시피와 같은 모양의 완전한 문서.

    샷은 두 곳에서 나온다. 방법이 포즈 목록을 가지면 **대상 하나 × 포즈들**,
    묶음이면 **대상들 × 한 장씩**이다. 둘 다 결국 `shots[]` 하나로 떨어지므로
    아래쪽 파이프라인은 차이를 모른다.
    """
    members = list(subjects)
    if not members:
        raise ReviewError(f"Method {method.id} needs at least one subject")

    for subject in members:
        if not method.accepts(subject.asset_type):
            raise ReviewError(
                f"Method {method.id} applies to "
                f"{', '.join(method.applies_to)}; subject {subject.id} is "
                f"{subject.asset_type}"
            )

    registry = workflow_types or WorkflowTypeRegistry()
    pipeline = copy.deepcopy(
        _mapping(method.document["pipeline"], "pipeline")
    )
    workflow_type = registry.get(str(pipeline.get("type", "")))

    lead = members[0]
    document: dict[str, Any] = {
        "schema_version": 1,
        "id": (
            f"{method.id}+{set_id}" if set_id
            else f"{method.id}+{lead.id}"
        ),
        "name": (
            f"{set_name or set_id} · {method.name}" if set_id
            else f"{lead.name} · {method.name}"
        ),
        "purpose": {
            "category": str(
                method.document.get("category")
                or lead.document.get("category")
                or lead.asset_type
            ),
            "asset_type": lead.asset_type,
            "slot": lead.slot,
            "use": str(method.document.get("use", "gameplay")),
            "readability_goal": (
                lead.readability_goal
                or str(method.document.get("readability_goal", ""))
            ),
            "animation_scope": str(
                method.document.get("animation_scope", "")
            ),
        },
        "output": copy.deepcopy(_mapping(method.document["output"], "output")),
        "pipeline": pipeline,
        "generation": copy.deepcopy(
            _mapping(method.document["generation"], "generation")
        ),
        "prompt": {
            # 묶음이면 공통 프롬프트만 본문에 둔다 — 대상별 정체성은 샷의
            # prompt_suffix 로 갈린다. 여기에 첫 멤버를 구우면 나머지 다섯 장이
            # 전부 그 하나를 닮는다.
            "positive": _join_prompt(
                str(method.prompt.get("prefix", "")),
                "" if len(members) > 1 else lead.identity,
                lead.signature_pose
                if method.prompt.get("use_signature_pose")
                and len(members) == 1
                else "",
                str(method.prompt.get("suffix", "")),
            ),
            "negative": _join_prompt(
                str(method.prompt.get("negative", "")),
                "" if len(members) > 1 else lead.excludes,
            ),
        },
        "composed_from": {
            "method": method.id,
            "subjects": [subject.id for subject in members],
            **({"subject_set": set_id} if set_id else {}),
        },
    }
    for optional in ("loras", "controlnets", "quality_gates", "review",
                     "animation", "effect_variants"):
        if optional in method.document:
            document[optional] = copy.deepcopy(method.document[optional])

    # 대상의 가이드 이미지를 역할 → 노드로 옮긴다.
    uploads = dict(pipeline.get("uploads", {}))
    variants = {
        str(role): str(name)
        for role, name in (method.document.get("guide_variants", {}) or {}).items()
    }
    for role, source in lead.guides(variants).items():
        node = workflow_type.node_for_role(role)
        if node is None:
            raise ReviewError(
                f"Workflow type {workflow_type.id} has no upload role "
                f"{role!r} that subject {lead.id} provides; known roles: "
                f"{', '.join(sorted(workflow_type.upload_roles)) or '(none)'}"
            )
        uploads[node] = source
    if uploads:
        pipeline["uploads"] = uploads

    shots: list[dict[str, Any]] = []
    if len(members) > 1:
        # 묶음: 대상마다 한 장. 프롬프트도 대상마다 갈린다.
        for subject in members:
            shot: dict[str, Any] = {
                "id": subject.slot,
                "label": subject.name,
                "slot": subject.slot,
                "prompt_suffix": (
                    f", {subject.identity}" if subject.identity else ""
                ),
                "seed_offset": len(shots) * 97,
            }
            if subject.excludes:
                shot["negative_suffix"] = f", {subject.excludes}"
            canvas = subject.document.get("output_canvas")
            if canvas:
                shot["output_canvas"] = list(canvas)
            shots.append(shot)
    elif method.poses:
        for pose in method.poses:
            shots.append(
                _shot_document(
                    shot_id=str(pose["id"]),
                    label=str(pose.get("label") or pose["id"]),
                    subject=lead,
                    method=method,
                    pose=pose,
                    workflow_type=workflow_type,
                )
            )
    if shots:
        pipeline["shots"] = shots

    return Recipe.from_document(document, path=method.path)


def resolve_by_id(
    method_id: str,
    target_id: str,
    *,
    methods: MethodRegistry | None = None,
    subjects: SubjectRegistry | None = None,
    subject_sets: SubjectSetRegistry | None = None,
) -> Recipe:
    """`target_id` 는 대상 하나이거나 대상 묶음이다."""
    method_registry = methods or MethodRegistry()
    subject_registry = subjects or SubjectRegistry()
    set_registry = subject_sets or SubjectSetRegistry()

    method = method_registry.get(method_id)
    known_sets = set_registry.load_all()
    if target_id in known_sets:
        subject_set = known_sets[target_id]
        members = [
            subject_registry.get(member) for member in subject_set.member_ids
        ]
        return resolve(
            method,
            members,
            set_id=subject_set.id,
            set_name=subject_set.name,
        )
    return resolve(method, [subject_registry.get(target_id)])


def targets_for_method(
    method: Method,
    *,
    subjects: SubjectRegistry | None = None,
    subject_sets: SubjectSetRegistry | None = None,
) -> list[tuple[str, str]]:
    """이 방법에 붙일 수 있는 (id, 표시명) 목록 — 폼 드롭다운이 쓴다."""
    subject_registry = subjects or SubjectRegistry()
    set_registry = subject_sets or SubjectSetRegistry()
    found = [
        (subject_set.id, subject_set.name)
        for subject_set in set_registry.load_all().values()
        if method.accepts(subject_set.asset_type)
    ]
    found.extend(
        (subject.id, subject.name)
        for subject in subject_registry.load_all().values()
        if method.accepts(subject.asset_type)
    )
    return found
