#!/usr/bin/env python3
"""Conform the approved B2 service-wall board into six cell-owned wall sprites.

ImageGen supplied three full-wall candidates per screen slope. The left, middle,
and right thirds are selected from candidates C0/C1/C2 respectively, then the
assembled 192x176 master is palette-locked and despeckled once before the
64x112 runtime cells are cut. Processing the master first keeps seam pixels from
being changed independently on either side of a cell boundary.
"""

from collections import deque
from dataclasses import dataclass
from pathlib import Path
from statistics import median
from typing import Callable

from PIL import Image

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-b2-service-wall-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
SHEET_SIZE = (1536, 1024)
CELL_SIZE = (512, 512)
MASTER_SIZE = (192, 176)
SPRITE_SIZE = (64, 112)
ALPHA_CUTOFF = 80
OVERSAMPLE = 4
TARGET_FACE_HEIGHT = 80.0
PIXEL_CLUSTER = 2
SIGNAL_MAGENTA = (230, 68, 184, 255)


@dataclass(frozen=True)
class DirectionSpec:
    name: str
    row: int
    target_slope: float
    windows: tuple[tuple[int, int], ...]


DIRECTIONS = (
    DirectionSpec(
        "rising-right",
        0,
        -0.5,
        ((0, 64), (64, 32), (128, 0)),
    ),
    DirectionSpec(
        "rising-left",
        1,
        0.5,
        ((0, 0), (64, 32), (128, 64)),
    ),
)


@dataclass(frozen=True)
class ServiceWallBuild:
    masters: dict[str, Image.Image]
    outputs: dict[str, Image.Image]


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def _remove_edge_connected_chroma(
    image: Image.Image,
    predicate: Callable[[tuple[int, int, int, int]], bool],
) -> None:
    """Clear exterior magenta without deleting enclosed cyan/magenta indicators."""
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
    alpha = hardened.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    hardened.putalpha(alpha)
    return hardened


def extract_candidate(sheet: Image.Image, column: int, row: int) -> Image.Image:
    left = column * CELL_SIZE[0]
    top = row * CELL_SIZE[1]
    candidate = sheet.crop(
        (left, top, left + CELL_SIZE[0], top + CELL_SIZE[1])
    ).convert("RGBA")
    _remove_edge_connected_chroma(candidate, _is_chroma)
    candidate = _harden_alpha(candidate)
    bounds = candidate.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"B2 service-wall candidate ({column}, {row}) is empty")
    return candidate.crop(bounds)


def _fit_line(points: list[tuple[float, float]]) -> tuple[float, float]:
    if len(points) < 8:
        raise ValueError("service-wall envelope needs at least eight columns")
    mean_x = sum(point[0] for point in points) / len(points)
    mean_y = sum(point[1] for point in points) / len(points)
    denominator = sum((point[0] - mean_x) ** 2 for point in points)
    if denominator <= 0:
        raise ValueError("service-wall envelope has no horizontal span")
    slope = sum(
        (point[0] - mean_x) * (point[1] - mean_y) for point in points
    ) / denominator
    return slope, mean_y - slope * mean_x


def _robust_line(points: list[tuple[float, float]]) -> tuple[float, float]:
    kept = points
    for _ in range(4):
        slope, intercept = _fit_line(kept)
        residuals = [abs(y - (slope * x + intercept)) for x, y in kept]
        center = median(residuals)
        deviations = [abs(value - center) for value in residuals]
        threshold = max(2.0, center + 3.0 * median(deviations))
        filtered = [
            point
            for point, residual in zip(kept, residuals)
            if residual <= threshold
        ]
        if len(filtered) == len(kept) or len(filtered) < 8:
            break
        kept = filtered
    return _fit_line(kept)


def envelope_lines(image: Image.Image) -> tuple[tuple[float, float], tuple[float, float]]:
    """Fit robust top and bottom wall-face lines over the central 80% width."""
    alpha = image.getchannel("A")
    pixels = alpha.load()
    margin = max(1, round(image.width * 0.1))
    top_points: list[tuple[float, float]] = []
    bottom_points: list[tuple[float, float]] = []
    for x in range(margin, image.width - margin):
        visible = [y for y in range(image.height) if pixels[x, y] >= ALPHA_CUTOFF]
        if not visible:
            continue
        top_points.append((float(x), float(visible[0])))
        bottom_points.append((float(x), float(visible[-1])))
    return _robust_line(top_points), _robust_line(bottom_points)


def normalize_candidate(source: Image.Image, target_slope: float) -> Image.Image:
    """Map a generated full wall to the exact 192x176 isometric master contract."""
    (top_slope, top_intercept), (bottom_slope, bottom_intercept) = envelope_lines(
        source
    )
    center_x = (source.width - 1) * 0.5
    top_center = top_slope * center_x + top_intercept
    bottom_center = bottom_slope * center_x + bottom_intercept
    face_height = bottom_center - top_center
    if face_height <= 8:
        raise ValueError("B2 service-wall candidate has no stable vertical face")

    scale_x = MASTER_SIZE[0] / source.width
    scale_y = TARGET_FACE_HEIGHT / face_height
    source_slope = (top_slope + bottom_slope) * 0.5
    projected_slope = source_slope * scale_y / scale_x
    shear = target_slope - projected_slope

    target_center_x = MASTER_SIZE[0] * 0.5
    target_foot_y = MASTER_SIZE[1] - MASTER_SIZE[0] * 0.25
    offset_y = target_foot_y - bottom_center * scale_y - shear * target_center_x

    width = MASTER_SIZE[0] * OVERSAMPLE
    height = MASTER_SIZE[1] * OVERSAMPLE
    inverse = (
        1.0 / (OVERSAMPLE * scale_x),
        0.0,
        0.0,
        -shear / (OVERSAMPLE * scale_y),
        1.0 / (OVERSAMPLE * scale_y),
        -offset_y / scale_y,
    )
    transformed = source.transform(
        (width, height),
        Image.Transform.AFFINE,
        inverse,
        resample=Image.Resampling.BICUBIC,
        fillcolor=(5, 7, 12, 0),
    )
    return transformed.resize(MASTER_SIZE, Image.Resampling.BOX)


def _inside_windows(x: int, y: int, windows: tuple[tuple[int, int], ...]) -> bool:
    return any(
        left <= x < left + SPRITE_SIZE[0] and top <= y < top + SPRITE_SIZE[1]
        for left, top in windows
    )


def assemble_master(
    candidates: tuple[Image.Image, ...],
    direction: DirectionSpec,
) -> Image.Image:
    if len(candidates) != 3:
        raise ValueError("B2 service wall needs exactly three candidates")
    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    for segment, ((left, top), candidate) in enumerate(
        zip(direction.windows, candidates)
    ):
        if candidate.size != MASTER_SIZE:
            raise ValueError(f"candidate {segment} has unexpected size {candidate.size}")
        window = candidate.crop(
            (left, top, left + SPRITE_SIZE[0], top + SPRITE_SIZE[1])
        )
        master.alpha_composite(window, (left, top))

    # ImageGen placed the authored left-facing C2 status light in the discarded
    # third of its full-wall candidate. Restore the same tiny, palette-legal
    # gameplay residue on the selected cabinet face so camera rotation does not
    # make the room's third accent disappear.
    if direction.name == "rising-left":
        left, top = direction.windows[2]
        signal_left = left + 34
        signal_top = top + 52
        pixels = master.load()
        if all(
            pixels[x, y][3] >= ALPHA_CUTOFF
            for y in range(signal_top, signal_top + 4)
            for x in range(signal_left, signal_left + 4)
        ):
            for y in range(signal_top, signal_top + 4):
                for x in range(signal_left, signal_left + 4):
                    pixels[x, y] = SIGNAL_MAGENTA

    # Runtime canvases are 2x the authored cluster regime. Conform at 96x88,
    # then nearest-upscale so the final 64x112 cells use deliberate 2x2-or-larger
    # clusters like the existing Project-C walls instead of generated 1px noise.
    small = master.resize(
        (MASTER_SIZE[0] // PIXEL_CLUSTER, MASTER_SIZE[1] // PIXEL_CLUSTER),
        Image.Resampling.BOX,
    )
    small = _harden_alpha(small)
    small = despeckle(lock_rgba_to_palette(small))
    master = small.resize(MASTER_SIZE, Image.Resampling.NEAREST)
    pixels = master.load()
    for y in range(master.height):
        for x in range(master.width):
            if pixels[x, y][3] > 0 and not _inside_windows(x, y, direction.windows):
                raise ValueError(f"visible service-wall pixel escaped union at {(x, y)}")
    return master


def split_master(master: Image.Image, direction: DirectionSpec) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for segment, (left, top) in enumerate(direction.windows):
        name = f"env-wall-b2-service-segment-{segment}-{direction.name}"
        outputs[name] = master.crop(
            (left, top, left + SPRITE_SIZE[0], top + SPRITE_SIZE[1])
        )
    return outputs


def reassemble_outputs(
    outputs: dict[str, Image.Image],
    direction: DirectionSpec,
) -> Image.Image:
    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    for segment, (left, top) in enumerate(direction.windows):
        name = f"env-wall-b2-service-segment-{segment}-{direction.name}"
        master.alpha_composite(outputs[name], (left, top))
    return master


def build_assets(sheet: Image.Image) -> ServiceWallBuild:
    if sheet.size != SHEET_SIZE:
        raise ValueError(f"unexpected B2 service-wall source size: {sheet.size}")

    masters: dict[str, Image.Image] = {}
    outputs: dict[str, Image.Image] = {}
    for direction in DIRECTIONS:
        candidates = tuple(
            normalize_candidate(
                extract_candidate(sheet, column, direction.row),
                direction.target_slope,
            )
            for column in range(3)
        )
        master = assemble_master(candidates, direction)
        masters[direction.name] = master
        outputs.update(split_master(master, direction))
    return ServiceWallBuild(masters, outputs)


def main() -> None:
    # v1 ImageGen 축소 경로의 분석 함수는 회귀용으로 남기되, 실제 출력은 승인된
    # 네이티브 픽셀 master가 소유한다. 이 진입점을 다시 실행해도 세대가 되돌아가지 않는다.
    from process_b2_prop_quality_v4 import build_source_assets as build_quality_assets

    quality = build_quality_assets()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    service_outputs = {
        name: image
        for name, image in quality.outputs.items()
        if name.startswith("env-wall-b2-service-segment-")
    }
    for name, image in service_outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    for name, image in quality.service_masters.items():
        image.resize((MASTER_SIZE[0] * 4, MASTER_SIZE[1] * 4), Image.Resampling.NEAREST).save(
            ROOT / f"docs/captures/b2-service-wall-{name}-preview-v1.png",
            optimize=True,
        )
    print(f"wrote {len(service_outputs)} B2 service-wall sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
