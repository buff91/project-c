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
}

BONES = (
    ("neck", "hip", (255, 0, 0)),
    ("head", "neck", (0, 0, 255)),
    ("neck", "ls", (255, 85, 0)),
    ("ls", "le", (255, 170, 0)),
    ("le", "lw", (255, 255, 0)),
    ("neck", "rs", (170, 255, 0)),
    ("rs", "re", (85, 255, 0)),
    ("re", "rw", (0, 255, 0)),
    ("hip", "lh", (0, 255, 85)),
    ("lh", "lk", (0, 255, 170)),
    ("lk", "la", (0, 255, 255)),
    ("hip", "rh", (0, 170, 255)),
    ("rh", "rk", (0, 85, 255)),
    ("rk", "ra", (0, 0, 255)),
)


def point(value: tuple[float, float]) -> tuple[int, int]:
    return round(value[0] * SIZE), round(value[1] * SIZE)


def render_pose(points: dict[str, tuple[float, float]]) -> Image.Image:
    image = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    for start, end, color in BONES:
        draw.line((point(points[start]), point(points[end])), fill=color, width=10)
        for joint in (start, end):
            x, y = point(points[joint])
            draw.ellipse((x - 7, y - 7, x + 7, y + 7), fill=color)
    head_x, head_y = point(points["head"])
    for offset in (-24, -12, 0, 12, 24):
        draw.ellipse(
            (
                head_x + offset - 6,
                head_y - 6,
                head_x + offset + 6,
                head_y + 6,
            ),
            fill=(255, 0, 170),
        )
    return image


def main() -> int:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, pose in POSES.items():
        destination = OUTPUT_DIR / f"actor-slinger-{name}.png"
        render_pose(pose).save(destination)
        print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
