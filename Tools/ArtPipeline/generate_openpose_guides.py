#!/usr/bin/env python3
"""Generate deterministic OpenPose control maps used by art recipes."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw


PROJECT_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = PROJECT_ROOT / "docs/art-direction/comfyui/guides/openpose"
SIZE = 512

# Project-C only needs coarse animation key poses. Coordinates are normalized
# so the same skeleton can be rendered deterministically at any guide size.
POSES: dict[str, dict[str, tuple[float, float]]] = {
    "idle": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.39, 0.43), "lw": (0.37, 0.58),
        "rs": (0.57, 0.29), "re": (0.61, 0.43), "rw": (0.63, 0.58),
        "lh": (0.46, 0.51), "lk": (0.43, 0.69), "la": (0.41, 0.88),
        "rh": (0.54, 0.51), "rk": (0.57, 0.69), "ra": (0.59, 0.88),
    },
    "idle-breathe": {
        "head": (0.50, 0.18), "neck": (0.50, 0.28), "hip": (0.50, 0.52),
        "ls": (0.43, 0.30), "le": (0.39, 0.44), "lw": (0.37, 0.58),
        "rs": (0.57, 0.30), "re": (0.61, 0.44), "rw": (0.63, 0.58),
        "lh": (0.46, 0.52), "lk": (0.43, 0.70), "la": (0.41, 0.88),
        "rh": (0.54, 0.52), "rk": (0.57, 0.70), "ra": (0.59, 0.88),
    },
    "walk-contact-a": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.37, 0.42), "lw": (0.34, 0.55),
        "rs": (0.57, 0.29), "re": (0.63, 0.40), "rw": (0.67, 0.52),
        "lh": (0.46, 0.51), "lk": (0.39, 0.68), "la": (0.32, 0.86),
        "rh": (0.54, 0.51), "rk": (0.62, 0.69), "ra": (0.68, 0.86),
    },
    "walk-pass": {
        "head": (0.50, 0.18), "neck": (0.50, 0.28), "hip": (0.50, 0.52),
        "ls": (0.43, 0.30), "le": (0.40, 0.43), "lw": (0.43, 0.56),
        "rs": (0.57, 0.30), "re": (0.60, 0.43), "rw": (0.57, 0.56),
        "lh": (0.46, 0.52), "lk": (0.48, 0.70), "la": (0.50, 0.88),
        "rh": (0.54, 0.52), "rk": (0.52, 0.70), "ra": (0.50, 0.88),
    },
    "walk-contact-b": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.37, 0.42), "lw": (0.34, 0.55),
        "rs": (0.57, 0.29), "re": (0.63, 0.40), "rw": (0.67, 0.52),
        "lh": (0.46, 0.51), "lk": (0.56, 0.69), "la": (0.62, 0.86),
        "rh": (0.54, 0.51), "rk": (0.44, 0.69), "ra": (0.38, 0.86),
    },
    "idle-south": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.39, 0.43), "lw": (0.37, 0.58),
        "rs": (0.57, 0.29), "re": (0.61, 0.43), "rw": (0.63, 0.58),
        "lh": (0.46, 0.51), "lk": (0.43, 0.69), "la": (0.41, 0.88),
        "rh": (0.54, 0.51), "rk": (0.57, 0.69), "ra": (0.59, 0.88),
    },
    "walk-south-contact": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.37, 0.42), "lw": (0.34, 0.55),
        "rs": (0.57, 0.29), "re": (0.63, 0.40), "rw": (0.67, 0.52),
        "lh": (0.46, 0.51), "lk": (0.39, 0.68), "la": (0.32, 0.86),
        "rh": (0.54, 0.51), "rk": (0.62, 0.69), "ra": (0.68, 0.86),
    },
    "walk-east-contact": {
        "head": (0.52, 0.17), "neck": (0.50, 0.27), "hip": (0.48, 0.51),
        "ls": (0.45, 0.29), "le": (0.39, 0.41), "lw": (0.36, 0.53),
        "rs": (0.56, 0.29), "re": (0.62, 0.42), "rw": (0.65, 0.55),
        "lh": (0.45, 0.51), "lk": (0.38, 0.68), "la": (0.31, 0.85),
        "rh": (0.52, 0.51), "rk": (0.58, 0.70), "ra": (0.64, 0.88),
    },
    "walk-north-contact": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.50, 0.51),
        "ls": (0.43, 0.29), "le": (0.38, 0.42), "lw": (0.34, 0.54),
        "rs": (0.57, 0.29), "re": (0.62, 0.42), "rw": (0.66, 0.54),
        "lh": (0.46, 0.51), "lk": (0.55, 0.69), "la": (0.62, 0.87),
        "rh": (0.54, 0.51), "rk": (0.45, 0.69), "ra": (0.38, 0.87),
    },
    "walk-west-contact": {
        "head": (0.48, 0.17), "neck": (0.50, 0.27), "hip": (0.52, 0.51),
        "ls": (0.44, 0.29), "le": (0.38, 0.42), "lw": (0.35, 0.55),
        "rs": (0.55, 0.29), "re": (0.61, 0.41), "rw": (0.64, 0.53),
        "lh": (0.48, 0.51), "lk": (0.42, 0.70), "la": (0.36, 0.88),
        "rh": (0.55, 0.51), "rk": (0.62, 0.68), "ra": (0.69, 0.85),
    },
    "attack-windup": {
        "head": (0.50, 0.17), "neck": (0.50, 0.27), "hip": (0.49, 0.52),
        "ls": (0.43, 0.29), "le": (0.35, 0.22), "lw": (0.33, 0.10),
        "rs": (0.57, 0.29), "re": (0.64, 0.18), "rw": (0.61, 0.08),
        "lh": (0.46, 0.52), "lk": (0.42, 0.70), "la": (0.38, 0.88),
        "rh": (0.53, 0.52), "rk": (0.59, 0.69), "ra": (0.64, 0.87),
    },
    "attack-impact": {
        "head": (0.49, 0.18), "neck": (0.49, 0.28), "hip": (0.47, 0.53),
        "ls": (0.42, 0.30), "le": (0.34, 0.40), "lw": (0.28, 0.51),
        "rs": (0.56, 0.30), "re": (0.67, 0.35), "rw": (0.79, 0.39),
        "lh": (0.44, 0.53), "lk": (0.37, 0.69), "la": (0.31, 0.86),
        "rh": (0.51, 0.53), "rk": (0.58, 0.69), "ra": (0.65, 0.86),
    },
    "attack-recovery": {
        "head": (0.52, 0.18), "neck": (0.51, 0.28), "hip": (0.50, 0.53),
        "ls": (0.44, 0.30), "le": (0.40, 0.43), "lw": (0.37, 0.55),
        "rs": (0.58, 0.30), "re": (0.64, 0.42), "rw": (0.68, 0.52),
        "lh": (0.47, 0.53), "lk": (0.42, 0.70), "la": (0.38, 0.87),
        "rh": (0.54, 0.53), "rk": (0.59, 0.69), "ra": (0.64, 0.87),
    },
    "hit": {
        "head": (0.46, 0.19), "neck": (0.49, 0.29), "hip": (0.54, 0.53),
        "ls": (0.42, 0.31), "le": (0.35, 0.42), "lw": (0.31, 0.54),
        "rs": (0.56, 0.31), "re": (0.63, 0.42), "rw": (0.68, 0.53),
        "lh": (0.51, 0.53), "lk": (0.44, 0.70), "la": (0.39, 0.87),
        "rh": (0.57, 0.53), "rk": (0.62, 0.69), "ra": (0.67, 0.86),
    },
    "fall": {
        "head": (0.39, 0.35), "neck": (0.45, 0.39), "hip": (0.57, 0.58),
        "ls": (0.40, 0.40), "le": (0.33, 0.48), "lw": (0.27, 0.57),
        "rs": (0.50, 0.39), "re": (0.57, 0.48), "rw": (0.64, 0.55),
        "lh": (0.54, 0.57), "lk": (0.48, 0.70), "la": (0.42, 0.84),
        "rh": (0.60, 0.59), "rk": (0.68, 0.69), "ra": (0.75, 0.80),
    },
    "death": {
        "head": (0.25, 0.67), "neck": (0.34, 0.65), "hip": (0.55, 0.70),
        "ls": (0.34, 0.59), "le": (0.26, 0.52), "lw": (0.18, 0.49),
        "rs": (0.36, 0.71), "re": (0.44, 0.79), "rw": (0.52, 0.83),
        "lh": (0.55, 0.66), "lk": (0.67, 0.60), "la": (0.79, 0.62),
        "rh": (0.56, 0.74), "rk": (0.69, 0.78), "ra": (0.82, 0.76),
    },
    # 액터 계약 v2(2.5~3등신)용 치비 골격 — 기존 골격은 ~5등신이라 그대로 쓰면
    # 생성 결과가 리얼 비율로 회귀한다. 머리 유닛 ≈ 전체 높이의 36%.
    # 방향이 달라도 y 행(crown/neck/hip/ankle)은 동일해야 한다 — 방향 간 비율 일관성의 근거.
    "chibi-idle": {
        "head": (0.50, 0.26), "neck": (0.50, 0.42), "hip": (0.50, 0.58),
        "ls": (0.42, 0.44), "le": (0.39, 0.53), "lw": (0.37, 0.62),
        "rs": (0.58, 0.44), "re": (0.61, 0.53), "rw": (0.63, 0.62),
        "lh": (0.455, 0.58), "lk": (0.44, 0.73), "la": (0.43, 0.87),
        "rh": (0.545, 0.58), "rk": (0.56, 0.73), "ra": (0.57, 0.87),
    },
    # 3/4 우향(동쪽) — 어깨·골반 폭을 좁혀 측면감을 만들고 머리를 진행 방향으로 민다.
    "chibi-idle-east": {
        "head": (0.53, 0.26), "neck": (0.50, 0.42), "hip": (0.50, 0.58),
        "ls": (0.45, 0.44), "le": (0.42, 0.53), "lw": (0.41, 0.62),
        "rs": (0.56, 0.44), "re": (0.59, 0.53), "rw": (0.60, 0.62),
        "lh": (0.465, 0.58), "lk": (0.45, 0.73), "la": (0.44, 0.87),
        "rh": (0.535, 0.58), "rk": (0.55, 0.73), "ra": (0.56, 0.87),
    },
    # 등면(북쪽) — 얼굴 키포인트를 그리지 않는다(NO_FACE_POSES).
    "chibi-idle-north": {
        "head": (0.50, 0.26), "neck": (0.50, 0.42), "hip": (0.50, 0.58),
        "ls": (0.58, 0.44), "le": (0.61, 0.53), "lw": (0.63, 0.62),
        "rs": (0.42, 0.44), "re": (0.39, 0.53), "rw": (0.37, 0.62),
        "lh": (0.545, 0.58), "lk": (0.56, 0.73), "la": (0.57, 0.87),
        "rh": (0.455, 0.58), "rk": (0.44, 0.73), "ra": (0.43, 0.87),
    },
}

# 등면 등 얼굴 키포인트를 그리면 안 되는 포즈. OpenPose는 얼굴 점 유무로 앞/뒤를 판정한다.
NO_FACE_POSES = {"chibi-idle-north"}

# 리얼 골격(~5등신) 포즈를 치비 앵커 행으로 사상하는 조각별 선형 y-리맵.
# 좌: 리얼 골격의 공용 행(head/neck/hip/knee/ankle) → 우: 치비 골격의 같은 관절 행.
# 애니 키포즈를 손으로 다시 그리지 않고 기존 포즈 사전을 재사용하기 위한 장치다.
# fall/death 처럼 몸이 눕는 포즈는 y-리맵이 근사가 되므로 결과를 리뷰 시트에서 확인한다.
CHIBI_Y_ANCHORS = (
    (0.17, 0.26),
    (0.27, 0.42),
    (0.51, 0.58),
    (0.69, 0.73),
    (0.88, 0.87),
)

# 치비 애니 키포즈: 파생 이름 → 리얼 원본 포즈 이름.
CHIBI_REMAPPED_POSES = {
    "chibi-walk-contact-a": "walk-contact-a",
    "chibi-walk-pass": "walk-pass",
    "chibi-walk-contact-b": "walk-contact-b",
    "chibi-attack-windup": "attack-windup",
    "chibi-attack-impact": "attack-impact",
    "chibi-attack-recovery": "attack-recovery",
    "chibi-hit": "hit",
    "chibi-fall": "fall",
    "chibi-death": "death",
}


def chibi_y(value: float) -> float:
    anchors = CHIBI_Y_ANCHORS
    if value <= anchors[0][0]:
        return anchors[0][1] + (value - anchors[0][0])
    for (x0, y0), (x1, y1) in zip(anchors, anchors[1:]):
        if value <= x1:
            return y0 + (value - x0) * (y1 - y0) / (x1 - x0)
    return anchors[-1][1] + (value - anchors[-1][0])


def chibi_remap(points: dict[str, tuple[float, float]]) -> dict[str, tuple[float, float]]:
    return {joint: (x, chibi_y(y)) for joint, (x, y) in points.items()}

# 좌우 미러 파생 포즈: 원본 포즈의 x를 뒤집고 좌/우 관절을 맞바꾼다.
# BODY_18은 색으로 좌우를 식별하므로 단순 이미지 플립이 아니라 관절 재명명이 필요하다.
MIRRORED_POSES = {"chibi-idle-west": "chibi-idle-east"}

_SIDE_SWAP = {
    "ls": "rs", "rs": "ls", "le": "re", "re": "le", "lw": "rw", "rw": "lw",
    "lh": "rh", "rh": "lh", "lk": "rk", "rk": "lk", "la": "ra", "ra": "la",
}


def mirror_pose(points: dict[str, tuple[float, float]]) -> dict[str, tuple[float, float]]:
    return {
        _SIDE_SWAP.get(joint, joint): (1.0 - x, y)
        for joint, (x, y) in points.items()
    }
OUTPUT_PROFILES = {
    "actor-slinger": tuple(
        name
        for name in POSES
        if name != "idle-breathe" and not name.startswith("chibi-")
    ),
    "actor-survivor": (
        "idle",
        "idle-breathe",
        "walk-contact-a",
        "walk-pass",
        "walk-contact-b",
        "attack-windup",
        "attack-impact",
        "attack-recovery",
        "hit",
        "fall",
        "death",
    ),
    # 치비 골격은 얼굴 키포인트 간격도 머리 크기에 맞춰 커야 한다(face_scale).
    # 4방향: south(기본) · east · west(미러 파생) · north(등면, 얼굴 키포인트 없음).
    # 애니 키포즈는 CHIBI_REMAPPED_POSES가 리얼 포즈 사전에서 파생한다.
    "actor-chibi": (
        "chibi-idle",
        "chibi-idle-east",
        "chibi-idle-west",
        "chibi-idle-north",
        *CHIBI_REMAPPED_POSES,
    ),
}

# 프로파일별 얼굴 키포인트 배율. 기본 1.0은 기존 가이드와 픽셀 단위로 동일한 출력을 유지한다.
PROFILE_FACE_SCALE = {"actor-chibi": 2.6}

BODY_18_COLORS = (
    (255, 0, 0),
    (255, 85, 0),
    (255, 170, 0),
    (255, 255, 0),
    (170, 255, 0),
    (85, 255, 0),
    (0, 255, 0),
    (0, 255, 85),
    (0, 255, 170),
    (0, 255, 255),
    (0, 170, 255),
    (0, 85, 255),
    (0, 0, 255),
    (85, 0, 255),
    (170, 0, 255),
    (255, 0, 255),
    (255, 0, 170),
    (255, 0, 85),
)

# OpenPose BODY_18 limb order and colors must match the map used to train the
# SD1.5 ControlNet. A merely colorful stick figure is not interchangeable:
# the model uses both topology and color to identify left/right joints.
BODY_18_BONES = (
    ("neck", "rs", 0),
    ("neck", "ls", 1),
    ("rs", "re", 2),
    ("re", "rw", 3),
    ("ls", "le", 4),
    ("le", "lw", 5),
    ("neck", "rh", 6),
    ("rh", "rk", 7),
    ("rk", "ra", 8),
    ("neck", "lh", 9),
    ("lh", "lk", 10),
    ("lk", "la", 11),
    ("neck", "head", 12),
    ("head", "reye", 13),
    ("reye", "rear", 14),
    ("head", "leye", 15),
    ("leye", "lear", 16),
)


def point(value: tuple[float, float]) -> tuple[int, int]:
    return round(value[0] * SIZE), round(value[1] * SIZE)


def render_pose(
    points: dict[str, tuple[float, float]],
    face_scale: float = 1.0,
    draw_face: bool = True,
) -> Image.Image:
    image = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    body = dict(points)
    head_x, head_y = points["head"]
    body.update(
        {
            "reye": (head_x + 0.018 * face_scale, head_y - 0.006 * face_scale),
            "rear": (head_x + 0.038 * face_scale, head_y),
            "leye": (head_x - 0.018 * face_scale, head_y - 0.006 * face_scale),
            "lear": (head_x - 0.038 * face_scale, head_y),
        }
    )
    face_joints = {"reye", "rear", "leye", "lear"}
    for start, end, color_index in BODY_18_BONES:
        if not draw_face and (start in face_joints or end in face_joints):
            continue
        draw.line(
            (point(body[start]), point(body[end])),
            fill=BODY_18_COLORS[color_index],
            width=10,
        )

    joint_order = (
        "head",
        "neck",
        "rs",
        "re",
        "rw",
        "ls",
        "le",
        "lw",
        "rh",
        "rk",
        "ra",
        "lh",
        "lk",
        "la",
        "reye",
        "leye",
        "rear",
        "lear",
    )
    for color_index, joint in enumerate(joint_order):
        if not draw_face and joint in face_joints:
            continue
        x, y = point(body[joint])
        draw.ellipse(
            (x - 7, y - 7, x + 7, y + 7),
            fill=BODY_18_COLORS[color_index],
        )
    return image


def resolve_pose(name: str) -> dict[str, tuple[float, float]]:
    if name in MIRRORED_POSES:
        return mirror_pose(POSES[MIRRORED_POSES[name]])
    if name in CHIBI_REMAPPED_POSES:
        return chibi_remap(POSES[CHIBI_REMAPPED_POSES[name]])
    return POSES[name]


def main() -> int:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for prefix, names in OUTPUT_PROFILES.items():
        face_scale = PROFILE_FACE_SCALE.get(prefix, 1.0)
        for name in names:
            pose = resolve_pose(name)
            source_name = MIRRORED_POSES.get(name, name)
            destination = OUTPUT_DIR / f"{prefix}-{name}.png"
            render_pose(
                pose,
                face_scale,
                draw_face=source_name not in NO_FACE_POSES,
            ).save(destination)
            print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
