#!/usr/bin/env python3
"""Conform one top-down B2 2x2 macro floor into 16 view-aware cells.

The generated source is intentionally not four independent floor sprites. It is
one continuous, orthographic 2x2 material patch. For each camera quarter this
processor rotates that complete patch, projects it onto one 256x128 isometric
master, applies the shared palette/despeckle pass to the master, and only then
splits it into four cell-owned 128x64 sprites. Processing master-first keeps the
parking paint, wear, and cracks continuous across cell boundaries while the
final cell ownership preserves the existing FOV, tint, and sorting contracts.
"""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from pathlib import Path
from statistics import median
from typing import Callable, Iterable

from PIL import Image, ImageChops

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-b2-macro-floor-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
PREVIEW = ROOT / "docs/captures/b2-macro-floor-conform-preview-v1.png"

TOPDOWN_SIZE = (256, 256)
MASTER_SIZE = (256, 128)
SPRITE_SIZE = (128, 64)
ALPHA_CUTOFF = 80
PIXEL_CLUSTER = 2

# Physical roles never rotate. Their view-space windows do. Role ordering is
# the gameplay contract: r0=(0,0), r1=(1,0), r2=(0,1), r3=(1,1).
ROLE_COORDS = ((0, 0), (1, 0), (0, 1), (1, 1))
CELL_WINDOWS = {
    (0, 0): (64, 0),
    (1, 0): (128, 32),
    (0, 1): (0, 32),
    (1, 1): (64, 64),
}


def rotate_grid_coord(coord: tuple[int, int], view: int) -> tuple[int, int]:
    """Rotate one 2x2 physical coordinate into a camera-relative coordinate."""
    x, y = coord
    if view == 0:
        return x, y
    if view == 1:
        return y, 1 - x
    if view == 2:
        return 1 - x, 1 - y
    if view == 3:
        return 1 - y, x
    raise ValueError(f"invalid B2 macro-floor view: {view}")


@dataclass(frozen=True)
class ViewSpec:
    index: int
    role_windows: tuple[tuple[int, int], ...]

    @property
    def windows(self) -> tuple[tuple[int, int], ...]:
        return self.role_windows


VIEWS = tuple(
    ViewSpec(
        view,
        tuple(CELL_WINDOWS[rotate_grid_coord(coord, view)] for coord in ROLE_COORDS),
    )
    for view in range(4)
)


@dataclass(frozen=True)
class MacroFloorBuild:
    masters: dict[int, Image.Image]
    outputs: dict[str, Image.Image]


def _flat_data(image: Image.Image) -> Iterable:
    if hasattr(image, "get_flattened_data"):
        return image.get_flattened_data()
    return image.getdata()


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def _remove_edge_connected_chroma(
    image: Image.Image,
    predicate: Callable[[tuple[int, int, int, int]], bool],
) -> None:
    """Clear only exterior magenta, never an authored interior accent."""
    width, height = image.size
    pixels = image.load()
    visited = bytearray(width * height)
    pending: deque[tuple[int, int]] = deque()

    def enqueue(px: int, py: int) -> None:
        index = py * width + px
        if visited[index]:
            return
        pixel = pixels[px, py]
        if pixel[3] != 0 and not predicate(pixel):
            return
        visited[index] = 1
        pending.append((px, py))

    for px in range(width):
        enqueue(px, 0)
        enqueue(px, height - 1)
    for py in range(1, height - 1):
        enqueue(0, py)
        enqueue(width - 1, py)

    while pending:
        px, py = pending.popleft()
        red, green, blue, alpha = pixels[px, py]
        if alpha != 0 and predicate((red, green, blue, alpha)):
            pixels[px, py] = (red, green, blue, 0)
        if px > 0:
            enqueue(px - 1, py)
        if px + 1 < width:
            enqueue(px + 1, py)
        if py > 0:
            enqueue(px, py - 1)
        if py + 1 < height:
            enqueue(px, py + 1)


def _harden_alpha(image: Image.Image) -> Image.Image:
    hardened = image.convert("RGBA")
    hardened.putalpha(
        hardened.getchannel("A").point(
            lambda value: 255 if value >= ALPHA_CUTOFF else 0
        )
    )
    return hardened


def extract_topdown(source: Image.Image) -> Image.Image:
    """Extract and normalize the generated square top-down material patch."""
    patch = source.convert("RGBA")
    _remove_edge_connected_chroma(patch, _is_chroma)
    patch = _harden_alpha(patch)
    bounds = patch.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("B2 macro-floor source contains no visible patch")
    patch = patch.crop(bounds)

    width, height = patch.size
    square_tolerance = max(2, round(max(width, height) * 0.03))
    if abs(width - height) > square_tolerance:
        raise ValueError(
            f"B2 macro-floor source must be square after chroma crop: {patch.size}"
        )

    return _harden_alpha(patch.resize(TOPDOWN_SIZE, Image.Resampling.BOX))


def rotate_topdown(topdown: Image.Image, view: int) -> Image.Image:
    """Rotate the full patch before projection, matching ``rotate_grid_coord``.

    View 1 is a logical clockwise world rotation. With raster Y increasing
    downward, that same coordinate transform is PIL's visual ROTATE_90.
    """
    if topdown.width != topdown.height:
        raise ValueError(f"B2 macro-floor top-down patch must be square: {topdown.size}")
    rotations = (
        None,
        Image.Transpose.ROTATE_90,
        Image.Transpose.ROTATE_180,
        Image.Transpose.ROTATE_270,
    )
    if view < 0 or view >= len(rotations):
        raise ValueError(f"invalid B2 macro-floor view: {view}")
    transpose = rotations[view]
    return topdown.copy() if transpose is None else topdown.transpose(transpose)


def project_topdown(topdown: Image.Image) -> Image.Image:
    """Project one complete 256x256 top-down patch onto a 2:1 iso master.

    The inverse affine map is derived from:
      screen_x = 128 + (source_x - source_y) / 2
      screen_y = (source_x + source_y) / 4
    """
    if topdown.size != TOPDOWN_SIZE:
        raise ValueError(f"unexpected B2 macro-floor top-down size: {topdown.size}")
    return topdown.transform(
        MASTER_SIZE,
        Image.Transform.AFFINE,
        (1.0, 2.0, -128.0, -1.0, 2.0, 128.0),
        resample=Image.Resampling.BILINEAR,
        fillcolor=(5, 7, 12, 0),
    )


def _base_master(base_floor: Image.Image) -> Image.Image:
    base_floor = _harden_alpha(base_floor)
    if base_floor.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    # Painter order is stable, though the final master colors are shared at any
    # overlapping edge and therefore independent of role split order.
    for coord in ((0, 0), (0, 1), (1, 0), (1, 1)):
        master.alpha_composite(base_floor, CELL_WINDOWS[coord])
    return master


def _median_visible_luma(image: Image.Image) -> float:
    values = []
    for red, green, blue, alpha in _flat_data(image.convert("RGBA")):
        if alpha >= ALPHA_CUTOFF:
            values.append((299 * red + 587 * green + 114 * blue) / 1000.0)
    if not values:
        raise ValueError("B2 macro-floor patch has no visible material pixels")
    return float(median(values))


def _apply_surface_signal(
    base: Image.Image,
    projected: Image.Image,
    neutral_luma: float,
) -> Image.Image:
    """Transfer low-frequency wear/paint while retaining canonical floor form."""
    result = base.copy()
    base_pixels = base.load()
    source_pixels = projected.load()
    target_pixels = result.load()
    neutral_rgb = (84.0, 91.0, 97.0)  # Torchstone grey-3.

    for py in range(MASTER_SIZE[1]):
        for px in range(MASTER_SIZE[0]):
            base_red, base_green, base_blue, base_alpha = base_pixels[px, py]
            if base_alpha == 0:
                continue
            red, green, blue, source_alpha = source_pixels[px, py]
            if source_alpha < ALPHA_CUTOFF:
                continue

            source_luma = (299 * red + 587 * green + 114 * blue) / 1000.0
            delta = max(-24.0, min(60.0, source_luma - neutral_luma))
            chroma = max(red, green, blue) - min(red, green, blue)
            tint_strength = 0.42 if chroma >= 24 else 0.18
            opacity = source_alpha / 255.0

            # Keep the canonical dark diamond outline intact. Interior concrete
            # is gently unified so the generated cross-cell marks, rather than
            # four repeated per-tile value patterns, carry the macro reading.
            base_luma = (
                299 * base_red + 587 * base_green + 114 * base_blue
            ) / 1000.0
            if base_luma <= 45.0:
                continue

            channels = (base_red, base_green, base_blue)
            source_channels = (red, green, blue)
            conformed = []
            for channel, neutral, source_channel in zip(
                channels, neutral_rgb, source_channels
            ):
                unified = channel * 0.68 + neutral * 0.32
                signaled = (
                    unified
                    + delta * 0.72
                    + (source_channel - source_luma) * tint_strength
                )
                value = channel * (1.0 - opacity) + signaled * opacity
                conformed.append(max(0, min(255, round(value))))
            target_pixels[px, py] = (*conformed, base_alpha)
    return result


def normalize_view(
    topdown: Image.Image,
    base_floor: Image.Image,
    spec: ViewSpec,
) -> Image.Image:
    """Rotate/project/conform one complete 2x2 master before any role split."""
    rotated = rotate_topdown(topdown, spec.index)
    projected = project_topdown(rotated)
    base = _base_master(base_floor)
    union_alpha = base.getchannel("A")
    transferred = _apply_surface_signal(
        base,
        projected,
        _median_visible_luma(rotated),
    )

    # The authored pixel cluster is established once across the whole master.
    # Applying this per cell would create four independent seam treatments.
    small = transferred.resize(
        (MASTER_SIZE[0] // PIXEL_CLUSTER, MASTER_SIZE[1] // PIXEL_CLUSTER),
        Image.Resampling.BOX,
    )
    small = despeckle(lock_rgba_to_palette(_harden_alpha(small)))
    master = small.resize(MASTER_SIZE, Image.Resampling.NEAREST)
    master.putalpha(union_alpha)
    return _harden_alpha(master)


def _output_name(role: int, view: int) -> str:
    return f"env-floor-b2-macro-role-{role}-view-{view}"


def split_master(
    master: Image.Image,
    base_floor: Image.Image,
    spec: ViewSpec,
) -> dict[str, Image.Image]:
    """Split one processed master into physical-role-owned cell canvases."""
    base_alpha = _harden_alpha(base_floor).getchannel("A")
    outputs: dict[str, Image.Image] = {}
    for role, (left, top) in enumerate(spec.role_windows):
        cell = master.crop(
            (left, top, left + SPRITE_SIZE[0], top + SPRITE_SIZE[1])
        )
        cell.putalpha(ImageChops.multiply(cell.getchannel("A"), base_alpha))
        outputs[_output_name(role, spec.index)] = _harden_alpha(cell)
    return outputs


def reassemble_outputs(
    outputs: dict[str, Image.Image],
    spec: ViewSpec,
) -> Image.Image:
    """Rebuild one master from physical roles for seam-loss regression tests."""
    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    for role, window in enumerate(spec.role_windows):
        master.alpha_composite(outputs[_output_name(role, spec.index)], window)
    return master


def build_assets(source: Image.Image, base_floor: Image.Image) -> MacroFloorBuild:
    if base_floor.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    topdown = extract_topdown(source)
    masters: dict[int, Image.Image] = {}
    outputs: dict[str, Image.Image] = {}
    for spec in VIEWS:
        master = normalize_view(topdown, base_floor, spec)
        outputs.update(split_master(master, base_floor, spec))
        masters[spec.index] = master
    return MacroFloorBuild(masters, outputs)


def _preview(build: MacroFloorBuild) -> Image.Image:
    preview = Image.new(
        "RGBA",
        (MASTER_SIZE[0] * 2, MASTER_SIZE[1] * 2),
        (5, 7, 12, 255),
    )
    for spec in VIEWS:
        left = (spec.index % 2) * MASTER_SIZE[0]
        top = (spec.index // 2) * MASTER_SIZE[1]
        preview.alpha_composite(build.masters[spec.index], (left, top))
    return preview.resize(
        (preview.width * 4, preview.height * 4),
        Image.Resampling.NEAREST,
    )


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    build = build_assets(
        Image.open(SOURCE).convert("RGBA"),
        Image.open(BASE_FLOOR).convert("RGBA"),
    )
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build.outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    _preview(build).save(PREVIEW, optimize=True)
    print(f"wrote {len(build.outputs)} B2 macro-floor sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
