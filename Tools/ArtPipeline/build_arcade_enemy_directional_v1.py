#!/usr/bin/env python3
"""Build production-ready directional timelines for the arcade enemy roster.

The six approved runtime PNGs are the identity source.  This deterministic pass
does not redraw anatomy or invent equipment: it projects each south-facing
silhouette into rear/profile key poses, applies role-specific mechanical motion,
keeps the 48x64 authoring grid and 2x2 clusters, and writes manifests consumed by
``aseprite_build_animation.lua``.

Each official source keeps the approved PNG as an untagged Frame_0, followed by
80 tagged frames: idle/walk/attack/hit/fall/death x north/east/south/west.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
from dataclasses import dataclass
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw

from process_arcade_occupation_actors_v1 import ACTOR_PALETTE_NAMES
from torchstone_palette import load_gpl_entries


ROOT = Path(__file__).resolve().parents[2]
RUNTIME_ROOT = ROOT / "Assets/_Project/Art/Runtime"
PALETTE = ROOT / "Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl"
OUTPUT_ROOT = ROOT / "docs/art-direction/comfyui/output/arcade-enemy-directional-v1"
PREVIEW = ROOT / "docs/captures/arcade-enemy-directional-conform-preview-v1.png"
MOTION_PREVIEW = ROOT / "docs/captures/arcade-enemy-directional-motion-preview-v1.gif"

CANVAS_SIZE = (96, 128)
WORKING_SIZE = (48, 64)
PIXEL_CLUSTER = 2
GROUND_ROW = 123
WORKING_GROUND_ROW = GROUND_ROW // PIXEL_CLUSTER
SAFE_WORKING_SIZE = (46, 60)
DIRECTIONS = ("north", "east", "south", "west")
STATES = ("idle", "walk", "attack", "hit", "fall", "death")
FRAME_COUNTS = {
    "idle": 4,
    "walk": 3,
    "attack": 3,
    "hit": 3,
    "fall": 2,
    "death": 5,
}
DURATIONS = {
    "idle": (140, 140, 140, 140),
    "walk": (100, 100, 100),
    "attack": (95, 95, 95),
    "hit": (70, 70, 70),
    "fall": (90, 120),
    "death": (70, 80, 90, 110, 180),
}


@dataclass(frozen=True)
class EnemySpec:
    asset_name: str
    actor_key: str
    motion: str
    side_scale: float
    signal_anchor: tuple[float, float]
    signal_length: int = 1
    mirror_into_east: bool = False

    @property
    def runtime_path(self) -> Path:
        return RUNTIME_ROOT / f"{self.asset_name}.png"

    @property
    def output_dir(self) -> Path:
        return OUTPUT_ROOT / self.asset_name

    @property
    def frame_dir(self) -> Path:
        return self.output_dir / "frames"

    @property
    def manifest_path(self) -> Path:
        return self.output_dir / "animation-manifest.json"

    @property
    def draft_path(self) -> Path:
        return self.output_dir / f"{self.asset_name}-directional-v1.aseprite"


SPECS = (
    EnemySpec("actor-goblin", "goblin", "assault", 0.72, (0.50, 0.13), 1, True),
    EnemySpec("actor-skeleton", "skeleton", "robot", 0.74, (0.50, 0.14), 2, True),
    EnemySpec("actor-slime", "slime", "hound", 0.82, (0.17, 0.46)),
    EnemySpec("actor-slinger", "slinger", "marksman", 0.72, (0.50, 0.15), 2),
    EnemySpec("actor-arc-drone", "arcDrone", "drone", 0.78, (0.24, 0.48)),
    EnemySpec("actor-grave-warden", "graveWarden", "warden", 0.76, (0.50, 0.16), 3),
)


@dataclass(frozen=True)
class FrameSpec:
    tag: str
    index: int
    image: Image.Image
    duration_ms: int


@dataclass(frozen=True)
class LocalMove:
    """Move one semantic shell region while the torso/chassis stays planted.

    Coordinates are normalized to the current opaque bounds.  Keeping these
    moves on the 48x64 authoring grid makes the result an actual cut-pixel key
    pose rather than a scaled, rotated, or row-sheared copy of the whole actor.
    """

    region: tuple[float, float, float, float]
    offset: tuple[int, int]


def palette_entries() -> dict[str, tuple[int, int, int]]:
    entries = dict(load_gpl_entries())
    missing = [name for name in ACTOR_PALETTE_NAMES if name not in entries]
    if missing:
        raise ValueError(f"actor palette entries missing: {missing}")
    return entries


def allowed_colors() -> set[tuple[int, int, int]]:
    entries = palette_entries()
    return {entries[name] for name in ACTOR_PALETTE_NAMES}


def _to_working(image: Image.Image) -> Image.Image:
    if image.size != CANVAS_SIZE:
        raise ValueError(f"enemy identity canvas mismatch: {image.size}")
    return image.convert("RGBA").resize(WORKING_SIZE, Image.Resampling.NEAREST)


def _to_runtime(image: Image.Image) -> Image.Image:
    if image.size != WORKING_SIZE:
        raise ValueError(f"working canvas mismatch: {image.size}")
    return image.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def _ground_subject(subject: Image.Image, *, shift_x: int = 0) -> Image.Image:
    bounds = subject.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot ground an empty enemy pose")
    subject = subject.crop(bounds)
    scale = min(
        1.0,
        SAFE_WORKING_SIZE[0] / subject.width,
        SAFE_WORKING_SIZE[1] / subject.height,
    )
    if scale < 1.0:
        subject = subject.resize(
            (
                max(1, round(subject.width * scale)),
                max(1, round(subject.height * scale)),
            ),
            Image.Resampling.NEAREST,
        )
    canvas = Image.new("RGBA", WORKING_SIZE, (0, 0, 0, 0))
    left = (WORKING_SIZE[0] - subject.width) // 2 + shift_x
    left = max(1, min(left, WORKING_SIZE[0] - subject.width - 1))
    top = WORKING_GROUND_ROW - subject.height + 1
    canvas.alpha_composite(subject, (left, top))
    return canvas


def scale_grounded(
    image: Image.Image,
    *,
    scale_x: float = 1.0,
    scale_y: float = 1.0,
    shift_x: int = 0,
) -> Image.Image:
    working = _to_working(image)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot scale an empty enemy pose")
    subject = working.crop(bounds)
    size = (
        max(1, round(subject.width * scale_x)),
        max(1, round(subject.height * scale_y)),
    )
    subject = subject.resize(size, Image.Resampling.NEAREST)
    return _to_runtime(_ground_subject(subject, shift_x=shift_x))


def row_shift_grounded(
    image: Image.Image,
    *,
    top_shift: float = 0.0,
    bottom_shift: float = 0.0,
) -> Image.Image:
    """Shift whole authoring rows to make a grounded weight transfer."""
    working = _to_working(image)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot shift an empty enemy pose")
    result = Image.new("RGBA", WORKING_SIZE, (0, 0, 0, 0))
    span = max(1, bounds[3] - bounds[1] - 1)
    source = working.load()
    target = result.load()
    for y in range(bounds[1], bounds[3]):
        t = (y - bounds[1]) / span
        dx = round(top_shift * (1.0 - t) + bottom_shift * t)
        for x in range(bounds[0], bounds[2]):
            pixel = source[x, y]
            nx = x + dx
            if pixel[3] != 0 and 0 <= nx < WORKING_SIZE[0]:
                target[nx, y] = pixel
    return _to_runtime(_ground_subject(result))


def local_articulate(
    image: Image.Image,
    moves: tuple[LocalMove, ...],
    *,
    preserve_source: bool = False,
) -> Image.Image:
    """Translate disjoint limb/weapon regions on the working grid.

    Unlike the grounded affine helpers, this deliberately leaves the central
    body pixels byte-for-byte in place.  The unmoved core is important both for
    identity retention and for readable weapon/leg articulation at game scale.
    ``preserve_source`` keeps the original joint pixels as a bridge for narrow
    profile poses while the translated shell extends the silhouette.
    """
    working = _to_working(image)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot articulate an empty enemy pose")
    width = max(1, bounds[2] - bounds[0])
    height = max(1, bounds[3] - bounds[1])
    source = working.load()
    selected: dict[tuple[int, int], tuple[tuple[int, int, int, int], tuple[int, int]]] = {}
    for move in moves:
        left, top, right, bottom = move.region
        for y in range(bounds[1], bounds[3]):
            normalized_y = (y - bounds[1] + 0.5) / height
            if not top <= normalized_y < bottom:
                continue
            for x in range(bounds[0], bounds[2]):
                normalized_x = (x - bounds[0] + 0.5) / width
                if not left <= normalized_x < right:
                    continue
                pixel = source[x, y]
                # A lifted limb must not take the last contact pixels off the
                # shared floor row.  Keep those sole/tread pixels planted while
                # the rest of the local part rises.
                if y == WORKING_GROUND_ROW and move.offset[1] < 0:
                    continue
                if pixel[3] != 0 and (x, y) not in selected:
                    selected[(x, y)] = (pixel, move.offset)
    if not selected:
        raise ValueError("local enemy articulation selected no opaque pixels")

    result = working.copy()
    target = result.load()
    core = (
        bounds[0] + math.floor(width * 0.38),
        bounds[1] + math.floor(height * 0.30),
        bounds[0] + math.ceil(width * 0.62),
        bounds[1] + math.ceil(height * 0.62),
    )
    if not preserve_source:
        for x, y in selected:
            target[x, y] = (0, 0, 0, 0)
    for (x, y), (pixel, (dx, dy)) in selected.items():
        nx = max(1, min(x + dx, WORKING_SIZE[0] - 2))
        ny = max(1, min(y + dy, WORKING_GROUND_ROW))
        if preserve_source and core[0] <= nx < core[2] and core[1] <= ny < core[3]:
            continue
        target[nx, ny] = pixel
    return _to_runtime(result)


def rotate_grounded(
    image: Image.Image,
    angle: float,
    *,
    preserve_low_profile: bool = False,
) -> Image.Image:
    working = _to_working(image)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot rotate an empty enemy pose")
    subject = working.crop(bounds).rotate(
        angle,
        resample=Image.Resampling.NEAREST,
        expand=True,
    )
    rotated_bounds = subject.getchannel("A").getbbox()
    if rotated_bounds is None:
        raise ValueError("enemy collapse pose became empty")
    subject = subject.crop(rotated_bounds)
    if preserve_low_profile and subject.height > bounds[3] - bounds[1] + 3:
        subject = subject.resize(
            (subject.width, bounds[3] - bounds[1] + 3),
            Image.Resampling.NEAREST,
        )
    return _to_runtime(_ground_subject(subject))


def _paint_opaque_run(
    image: Image.Image,
    anchor: tuple[float, float],
    colors: tuple[tuple[int, int, int], ...],
) -> Image.Image:
    """Paint the nearest opaque horizontal run, preserving silhouette and alpha."""
    working = _to_working(image)
    alpha = working.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("cannot paint an empty enemy pose")
    length = len(colors)
    target_x = bounds[0] + anchor[0] * (bounds[2] - bounds[0] - 1)
    target_y = bounds[1] + anchor[1] * (bounds[3] - bounds[1] - 1)
    candidates: list[tuple[float, int, int]] = []
    for y in range(bounds[1], bounds[3]):
        for x in range(bounds[0], bounds[2] - length + 1):
            if all(alpha.getpixel((x + offset, y)) != 0 for offset in range(length)):
                center_x = x + (length - 1) / 2
                candidates.append(
                    (abs(center_x - target_x) + abs(y - target_y) * 1.35, y, x)
                )
    if not candidates:
        raise ValueError("enemy pose has no opaque signal run")
    _, y, x = min(candidates)
    pixels = working.load()
    for offset, color in enumerate(colors):
        pixels[x + offset, y] = (*color, 255)
    return _to_runtime(working)


def restore_signal(
    image: Image.Image,
    spec: EnemySpec,
    direction: str = "south",
) -> Image.Image:
    entries = palette_entries()
    colors = tuple(
        entries["sig-warning"] if index == spec.signal_length // 2
        else entries["sig-warning-deep"]
        for index in range(spec.signal_length)
    )
    anchor = spec.signal_anchor
    if direction == "east":
        anchor = (min(0.82, anchor[0] + 0.18), anchor[1])
    elif direction == "north":
        anchor = (0.50, anchor[1])
    # 변형 전 identity에 있던 IFF가 회전/투영 뒤 남아 있으면 새 run과 중복된다.
    # 먼저 신호색을 몸체 암부로 되돌린 뒤 역할별 한 run만 다시 심는다.
    return _paint_opaque_run(remove_signal(image), anchor, colors)


def remove_signal(image: Image.Image) -> Image.Image:
    entries = palette_entries()
    hostile = {entries["sig-warning"], entries["sig-warning-deep"]}
    replacement = entries["dark-cool"]
    result = image.convert("RGBA").copy()
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha != 0 and (red, green, blue) in hostile:
                pixels[x, y] = (*replacement, alpha)
    return result


def _rear_silhouette_moves(spec: EnemySpec) -> tuple[LocalMove, ...]:
    """Expose rear-mounted mass instead of reusing the front alpha contour."""
    if spec.motion == "hound":
        return (
            LocalMove((0.00, 0.24, 0.30, 0.86), (-2, 0)),
            LocalMove((0.70, 0.18, 1.00, 0.78), (2, -1)),
        )
    if spec.motion == "drone":
        return (
            LocalMove((0.00, 0.12, 0.31, 0.72), (-2, -1)),
            LocalMove((0.69, 0.20, 1.00, 0.80), (2, 0)),
        )
    shoulder_reach = 2 if spec.motion in {"robot", "warden"} else 1
    return (
        LocalMove((0.00, 0.18, 0.30, 0.64), (-shoulder_reach, -1)),
        LocalMove((0.70, 0.24, 1.00, 0.68), (shoulder_reach, 0)),
    )


def _profile_silhouette_moves(spec: EnemySpec) -> tuple[LocalMove, ...]:
    """Give the east plate a leading weapon/sensor and tucked rear mass."""
    if spec.motion in {"hound", "drone"}:
        return (
            LocalMove((0.00, 0.30, 0.28, 0.82), (1, 0)),
            LocalMove((0.72, 0.10, 1.00, 0.72), (2, -1)),
        )
    weapon_reach = 3 if spec.motion == "marksman" else 2
    return (
        LocalMove((0.00, 0.30, 0.27, 0.60), (1, 0)),
        LocalMove((0.73, 0.22, 1.00, 0.76), (weapon_reach, -1)),
    )


def rear_view(image: Image.Image, spec: EnemySpec) -> Image.Image:
    """Build a distinct rear plate from the approved asymmetric silhouette."""
    rear = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT).convert("RGBA")
    entries = palette_entries()
    highlight_map = {
        entries["grey-6"]: entries["grey-4"],
        entries["grey-5"]: entries["grey-3"],
        entries["fabric-5"]: entries["fabric-3"],
        entries["rust-4"]: entries["rust-2"],
        entries["sig-warning"]: entries["dark-warm"],
        entries["sig-warning-deep"]: entries["dark-warm"],
    }
    working = _to_working(rear)
    bounds = working.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("cannot build a rear view from an empty enemy")
    pixels = working.load()
    rear_limit = bounds[1] + round((bounds[3] - bounds[1]) * 0.68)
    for y in range(bounds[1], rear_limit):
        for x in range(bounds[0], bounds[2]):
            red, green, blue, alpha = pixels[x, y]
            replacement = highlight_map.get((red, green, blue))
            if alpha != 0 and replacement is not None:
                pixels[x, y] = (*replacement, alpha)
    rear = _to_runtime(working)

    # Rear packs, shoulder plates, pursuit-hound haunches, and drone pods are cut and
    # repositioned locally.  This changes the alpha silhouette itself; north is
    # no longer a recolored/mirrored copy of the approved south plate.
    rear = local_articulate(
        rear,
        _rear_silhouette_moves(spec),
        preserve_source=True,
    )

    # A central dark seam/back plate is the depth cue that keeps north from
    # reading as a merely dimmed south frame.  It never grows the silhouette.
    seam_color = entries["dark-cool"]
    seam_length = 2 if spec.motion in {"hound", "drone"} else 3
    rear = _paint_opaque_run(
        rear,
        (0.50, 0.40 if spec.motion in {"hound", "drone"} else 0.35),
        tuple(seam_color for _ in range(seam_length)),
    )
    return restore_signal(rear, spec, "north")


def canonical_views(spec: EnemySpec) -> dict[str, Image.Image]:
    if not spec.runtime_path.exists():
        raise FileNotFoundError(spec.runtime_path)
    south = Image.open(spec.runtime_path).convert("RGBA")
    east_source = (
        south.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if spec.mirror_into_east
        else south
    )
    east = scale_grounded(east_source, scale_x=spec.side_scale)
    east = row_shift_grounded(east, top_shift=2.0, bottom_shift=-0.5)
    east = local_articulate(
        east,
        _profile_silhouette_moves(spec),
        preserve_source=True,
    )
    east = restore_signal(east, spec, "east")
    return {
        "north": rear_view(south, spec),
        "east": east,
        "south": south,
        "west": east.transpose(Image.Transpose.FLIP_LEFT_RIGHT),
    }


def _motion_sign(direction: str) -> float:
    return -1.0 if direction == "north" else 1.0


def _facing_vector(direction: str) -> tuple[int, int]:
    vectors = {
        "north": (0, -1),
        "east": (1, 0),
        "south": (0, 1),
        "west": (-1, 0),
    }
    try:
        return vectors[direction]
    except KeyError as error:
        raise ValueError(f"unsupported enemy facing: {direction}") from error


def _local_motion_moves(
    spec: EnemySpec,
    state: str,
    phase: int,
    direction: str,
) -> tuple[LocalMove, ...]:
    """Role-aware limb/weapon regions for non-affine action key poses."""
    forward_x, forward_y = _facing_vector(direction)
    swing = 1 if phase < 0 else -1

    if spec.motion == "hound":
        if state == "walk":
            if forward_x:
                left_offset = (-2 if phase < 0 else -1, 0 if phase < 0 else -1)
                right_offset = (1 if phase < 0 else 2, -1 if phase < 0 else 0)
            else:
                left_offset = (-1, forward_y * swing)
                right_offset = (1, -forward_y * swing)
            return (
                LocalMove((0.00, 0.34, 0.31, 0.96), left_offset),
                LocalMove((0.69, 0.28, 1.00, 0.96), right_offset),
            )
        if state == "attack":
            reach = 3 if phase > 0 else -1
            return (
                LocalMove(
                    (0.00, 0.28, 0.27, 0.82),
                    (-forward_x, -forward_y),
                ),
                LocalMove(
                    (0.70, 0.08, 1.00, 0.78),
                    (forward_x * reach, forward_y * reach),
                ),
            )
        recoil = (-forward_x, -forward_y)
        return (
            LocalMove((0.00, 0.08, 0.31, 0.70), (recoil[0] - 1, recoil[1])),
            LocalMove((0.69, 0.08, 1.00, 0.70), (recoil[0] + 1, recoil[1])),
        )

    if spec.motion == "drone":
        if state == "walk":
            if forward_x:
                left_offset = (-2 if phase < 0 else -1, 0 if phase < 0 else -1)
                right_offset = (1 if phase < 0 else 2, -1 if phase < 0 else 0)
            else:
                left_offset = (-1, forward_y * swing)
                right_offset = (1, -forward_y * swing)
            return (
                LocalMove((0.00, 0.12, 0.30, 0.78), left_offset),
                LocalMove((0.70, 0.12, 1.00, 0.78), right_offset),
            )
        if state == "attack":
            reach = 3 if phase > 0 else -1
            return (
                LocalMove(
                    (0.00, 0.18, 0.28, 0.72),
                    (-forward_x, -forward_y),
                ),
                LocalMove(
                    (0.72, 0.08, 1.00, 0.72),
                    (forward_x * reach, forward_y * reach),
                ),
            )
        recoil = (-3 * forward_x, -3 * forward_y)
        return (
            LocalMove((0.00, 0.10, 0.30, 0.70), (recoil[0] - 1, recoil[1])),
            LocalMove((0.70, 0.10, 1.00, 0.70), (recoil[0] + 1, recoil[1])),
        )

    if state == "walk":
        # Opposing boot lifts keep one foot on the shared baseline.
        if forward_x:
            left_offset = (-forward_x * swing, -1 if phase < 0 else 0)
            right_offset = (forward_x * swing, 0 if phase < 0 else -1)
        else:
            left_offset = (-1, forward_y * swing)
            right_offset = (1, -forward_y * swing)
        return (
            LocalMove((0.00, 0.62, 0.34, 1.00), left_offset),
            LocalMove((0.66, 0.62, 1.00, 1.00), right_offset),
        )
    if state == "attack":
        reach = 3 if spec.motion in {"marksman", "warden"} else 2
        if phase < 0:
            reach = -1
        return (
            LocalMove(
                (0.00, 0.28, 0.30, 0.66),
                (-forward_x, -forward_y),
            ),
            LocalMove(
                (0.70, 0.20, 1.00, 0.78),
                (forward_x * reach, forward_y * reach),
            ),
        )
    if state == "hit":
        recoil = 2 if spec.motion in {"assault", "marksman", "warden"} else 1
        recoil_offset = (-forward_x * recoil, -forward_y * recoil)
        return (
            LocalMove(
                (0.00, 0.12, 0.31, 0.62),
                (recoil_offset[0] - 1, recoil_offset[1]),
            ),
            LocalMove(
                (0.69, 0.16, 1.00, 0.64),
                (recoil_offset[0] + 1, recoil_offset[1]),
            ),
        )
    raise ValueError(f"unsupported local enemy motion: {state}")


def _articulated_key_pose(
    base: Image.Image,
    spec: EnemySpec,
    state: str,
    phase: int,
    direction: str,
) -> Image.Image:
    return local_articulate(
        base,
        _local_motion_moves(spec, state, phase, direction),
        # 동작 키포즈는 관절 끝을 뻗되 원래 몸체 연결 픽셀을 남긴다. 측면처럼
        # 실루엣이 얇은 뷰에서 큰 직사각 컷을 통째로 비우면 팔다리가 공중에
        # 분리되어 보이므로, 원본 픽셀이 관절 브리지 역할을 맡는다.
        preserve_source=True,
    )


def _idle_frames(base: Image.Image, spec: EnemySpec) -> tuple[Image.Image, ...]:
    if spec.motion == "drone":
        return (
            base,
            scale_grounded(base, scale_y=0.96),
            base,
            scale_grounded(base, scale_x=0.97, scale_y=0.98),
        )
    if spec.motion == "hound":
        return (
            base,
            row_shift_grounded(base, top_shift=1.0),
            base,
            row_shift_grounded(base, top_shift=-1.0),
        )
    amount = 0.975 if spec.motion in {"robot", "warden"} else 0.96
    return (
        base,
        scale_grounded(base, scale_y=0.985),
        base,
        scale_grounded(base, scale_y=amount),
    )


def _walk_frames(
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    return (
        _articulated_key_pose(base, spec, "walk", -1, direction),
        base,
        _articulated_key_pose(base, spec, "walk", 1, direction),
    )


def _attack_frames(
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    release_source = base
    if spec.motion == "drone":
        entries = palette_entries()
        release_source = _paint_opaque_run(
            base,
            (0.50, 0.12),
            (entries["sig-ice"], entries["sig-teal-item"]),
        )
    return (
        _articulated_key_pose(base, spec, "attack", -1, direction),
        _articulated_key_pose(release_source, spec, "attack", 1, direction),
        base,
    )


def _hit_frames(
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    squash = 0.94 if spec.motion in {"hound", "drone"} else 0.96
    strong = _articulated_key_pose(base, spec, "hit", 1, direction)
    return (strong, scale_grounded(strong, scale_y=squash), base)


def _fall_frames(
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    sign = _motion_sign(direction)
    if spec.motion == "hound":
        return (
            rotate_grounded(base, 7.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 16.0 * sign, preserve_low_profile=True),
        )
    if spec.motion == "drone":
        return (
            rotate_grounded(base, 14.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 32.0 * sign, preserve_low_profile=True),
        )
    return (
        rotate_grounded(base, 18.0 * sign),
        rotate_grounded(base, 48.0 * sign),
    )


def _death_frames(
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    sign = _motion_sign(direction)
    if spec.motion == "hound":
        poses = (
            base,
            rotate_grounded(base, 5.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 11.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 17.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 20.0 * sign, preserve_low_profile=True),
        )
    elif spec.motion == "drone":
        poses = (
            base,
            rotate_grounded(base, 12.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 28.0 * sign, preserve_low_profile=True),
            rotate_grounded(base, 58.0 * sign),
            rotate_grounded(base, 84.0 * sign),
        )
    else:
        poses = (
            row_shift_grounded(base, top_shift=2.0 * sign),
            rotate_grounded(base, 18.0 * sign),
            rotate_grounded(base, 48.0 * sign),
            rotate_grounded(base, 70.0 * sign),
            rotate_grounded(base, 88.0 * sign),
        )
    return (*poses[:-1], remove_signal(poses[-1]))


def _state_images(
    state: str,
    base: Image.Image,
    spec: EnemySpec,
    direction: str,
) -> tuple[Image.Image, ...]:
    if state == "idle":
        return _idle_frames(base, spec)
    if state == "walk":
        return _walk_frames(base, spec, direction)
    if state == "attack":
        return _attack_frames(base, spec, direction)
    if state == "hit":
        return _hit_frames(base, spec, direction)
    if state == "fall":
        return _fall_frames(base, spec, direction)
    if state == "death":
        return _death_frames(base, spec, direction)
    raise ValueError(f"unsupported enemy state: {state}")


def build_frames(spec: EnemySpec) -> list[FrameSpec]:
    views = canonical_views(spec)
    by_key: dict[tuple[str, str], tuple[Image.Image, ...]] = {}
    for state in STATES:
        for direction in ("north", "east", "south"):
            images = _state_images(state, views[direction], spec, direction)
            if state == "death":
                images = tuple(
                    restore_signal(image, spec, direction)
                    for image in images[:-1]
                ) + (remove_signal(images[-1]),)
            else:
                images = tuple(restore_signal(image, spec, direction) for image in images)
            by_key[(state, direction)] = images
        by_key[(state, "west")] = tuple(
            image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            for image in by_key[(state, "east")]
        )

    frames: list[FrameSpec] = []
    for state in STATES:
        durations = DURATIONS[state]
        for direction in DIRECTIONS:
            images = by_key[(state, direction)]
            if len(images) != FRAME_COUNTS[state]:
                raise AssertionError(f"{spec.asset_name} {state}-{direction} frame count")
            for index, (image, duration) in enumerate(zip(images, durations, strict=True)):
                frames.append(FrameSpec(f"{state}-{direction}", index, image, duration))
    return frames


def assert_frame_contract(image: Image.Image, colors: set[tuple[int, int, int]]) -> None:
    if image.size != CANVAS_SIZE:
        raise AssertionError(f"enemy frame canvas mismatch: {image.size}")
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise AssertionError("empty enemy animation frame")
    if bounds[0] < 2 or bounds[2] > CANVAS_SIZE[0] - 2 or bounds[1] < 2:
        raise AssertionError(f"enemy frame touches safe canvas edge: {bounds}")
    if bounds[3] != GROUND_ROW + 1:
        raise AssertionError(f"enemy frame lost ground baseline: {bounds}")
    if set(alpha.get_flattened_data()) - {0, 255}:
        raise AssertionError("enemy frame alpha is not hard")
    pixels = rgba.load()
    for y in range(0, CANVAS_SIZE[1], PIXEL_CLUSTER):
        for x in range(0, CANVAS_SIZE[0], PIXEL_CLUSTER):
            block = {
                pixels[x + dx, y + dy]
                for dx in range(PIXEL_CLUSTER)
                for dy in range(PIXEL_CLUSTER)
            }
            if len(block) != 1:
                raise AssertionError(f"enemy frame breaks 2x2 cluster at {(x, y)}")
    used = {
        (red, green, blue)
        for red, green, blue, alpha_value in rgba.get_flattened_data()
        if alpha_value != 0
    }
    illegal = used - colors
    if illegal:
        raise AssertionError(f"enemy frame has off-palette colors: {sorted(illegal)}")


def _save_frame(spec: EnemySpec, frame: FrameSpec) -> Path:
    path = spec.frame_dir / f"{frame.tag}-{frame.index:02d}.png"
    frame.image.save(path, optimize=True)
    return path


def build_manifest_payload(
    spec: EnemySpec,
    frames: list[FrameSpec],
    *,
    save_frames: bool,
) -> dict:
    grouped: dict[str, list[FrameSpec]] = defaultdict(list)
    order: list[str] = []
    for frame in frames:
        if frame.tag not in grouped:
            order.append(frame.tag)
        grouped[frame.tag].append(frame)
    clips = []
    for tag in order:
        clip_frames = []
        for frame in grouped[tag]:
            source = (
                _save_frame(spec, frame)
                if save_frames
                else spec.frame_dir / f"{frame.tag}-{frame.index:02d}.png"
            )
            clip_frames.append({"source": str(source), "duration_ms": frame.duration_ms})
        clips.append(
            {
                "tag": tag,
                "loop": tag.startswith("idle-") or tag.startswith("walk-"),
                "frames": clip_frames,
            }
        )
    return {
        "schema_version": 1,
        "canvas": list(CANVAS_SIZE),
        "palette": str(PALETTE),
        "output": str(spec.draft_path),
        "leading_frames": [{"source": str(spec.runtime_path), "duration_ms": 180}],
        "clips": clips,
    }


def write_preview(all_frames: dict[str, list[FrameSpec]]) -> None:
    columns = tuple((state, direction) for direction in DIRECTIONS for state in STATES)
    key_index = {"idle": 1, "walk": 0, "attack": 1, "hit": 0, "fall": 1, "death": 4}
    label_height = 14
    cell_width = CANVAS_SIZE[0]
    cell_height = CANVAS_SIZE[1] + label_height
    preview = Image.new(
        "RGB",
        (cell_width * len(columns), cell_height * len(SPECS)),
        (5, 7, 12),
    )
    draw = ImageDraw.Draw(preview)
    for row, spec in enumerate(SPECS):
        lookup = {(frame.tag, frame.index): frame.image for frame in all_frames[spec.asset_name]}
        for column, (state, direction) in enumerate(columns):
            image = lookup[(f"{state}-{direction}", key_index[state])]
            x = column * cell_width
            y = row * cell_height
            preview.paste(image, (x, y), image)
            draw.text((x + 2, y + CANVAS_SIZE[1]), f"{state[:3]}-{direction[0]}", fill=(223, 231, 242))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW, optimize=True)


def write_motion_preview(all_frames: dict[str, list[FrameSpec]]) -> None:
    """Animate every approved state while keeping all four directions visible."""
    label_width = 112
    header_height = 24
    background = (5, 7, 12)
    width = label_width + CANVAS_SIZE[0] * len(DIRECTIONS)
    height = header_height + CANVAS_SIZE[1] * len(SPECS)
    lookups = {
        spec.asset_name: {
            (frame.tag, frame.index): frame.image
            for frame in all_frames[spec.asset_name]
        }
        for spec in SPECS
    }
    animation: list[Image.Image] = []
    durations: list[int] = []
    for state in STATES:
        for index in range(FRAME_COUNTS[state]):
            page = Image.new("RGB", (width, height), background)
            draw = ImageDraw.Draw(page)
            draw.text(
                (4, 6),
                f"{state.upper()}  {index + 1}/{FRAME_COUNTS[state]}",
                fill=(255, 194, 82),
            )
            for column, direction in enumerate(DIRECTIONS):
                draw.text(
                    (label_width + column * CANVAS_SIZE[0] + 4, 6),
                    direction.upper(),
                    fill=(223, 231, 242),
                )
            for row, spec in enumerate(SPECS):
                y = header_height + row * CANVAS_SIZE[1]
                draw.text((4, y + 4), spec.actor_key, fill=(121, 211, 220))
                for column, direction in enumerate(DIRECTIONS):
                    image = lookups[spec.asset_name][
                        (f"{state}-{direction}", index)
                    ]
                    x = label_width + column * CANVAS_SIZE[0]
                    page.paste(image, (x, y), image)
            animation.append(page)
            durations.append(DURATIONS[state][index])

    MOTION_PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    animation[0].save(
        MOTION_PREVIEW,
        save_all=True,
        append_images=animation[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=True,
    )


def selected_specs(asset_name: str | None) -> tuple[EnemySpec, ...]:
    if asset_name is None:
        return SPECS
    matches = tuple(spec for spec in SPECS if spec.asset_name == asset_name)
    if not matches:
        raise ValueError(f"unknown enemy asset: {asset_name}")
    return matches


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--actor", choices=[spec.asset_name for spec in SPECS])
    args = parser.parse_args()
    chosen = selected_specs(args.actor)
    colors = allowed_colors()
    all_frames: dict[str, list[FrameSpec]] = {}
    for spec in chosen:
        spec.frame_dir.mkdir(parents=True, exist_ok=True)
        frames = build_frames(spec)
        for frame in frames:
            assert_frame_contract(frame.image, colors)
        payload = build_manifest_payload(spec, frames, save_frames=True)
        spec.manifest_path.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        all_frames[spec.asset_name] = frames
        print(f"wrote {len(frames)} frames and 24 tags: {spec.asset_name}")
    if len(chosen) == len(SPECS):
        write_preview(all_frames)
        print(f"wrote enemy directional conform preview: {PREVIEW}")
        write_motion_preview(all_frames)
        print(f"wrote enemy directional motion preview: {MOTION_PREVIEW}")


if __name__ == "__main__":
    main()
