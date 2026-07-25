#!/usr/bin/env python3
"""Build Project-C's polished Collapsed Transit environment slice.

The high-resolution source sheet is an ImageGen style-transfer of six original
Project-C sprites. It is never sliced directly into the game: this processor
extracts each fixed cell, hardens alpha, downsamples at final resolution, and
reduces local color noise while preserving the richer material rendering that
was lost in the earlier code-drawn placeholder pass.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = (
    ROOT
    / "docs/art-direction/project-c-collapsed-transit-environment-source-v2.png"
)
STAIRS_SOURCE = (
    ROOT
    / "docs/art-direction/project-c-collapsed-transit-stairs-source-v2.png"
)
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
CELL_SIZE = (512, 512)
STAIRS_CELL_SIZE = 627
ALPHA_CUTOFF = 80


@dataclass(frozen=True)
class SpriteSpec:
    source_name: str
    cell_index: int
    size: tuple[int, int]
    output_names: tuple[str, ...]


# 128-레짐(128×64 타일 / PPU 128) — 모든 캔버스가 구 64-레짐의 정확히 ×2다.
SPECS = (
    SpriteSpec("floor", 0, (128, 64), ("env-floor",)),
    SpriteSpec(
        "wall",
        1,
        (64, 112),
        ("env-wall-rising-right", "env-wall-rising-left"),
    ),
    SpriteSpec(
        "wall-light",
        2,
        (64, 112),
        ("env-wall-torch-rising-right", "env-wall-torch-rising-left"),
    ),
    SpriteSpec(
        "door-closed",
        3,
        (128, 160),
        ("env-door-closed-rising-right", "env-door-closed-rising-left"),
    ),
    SpriteSpec(
        "door-open",
        4,
        (128, 160),
        ("env-door-open-rising-right", "env-door-open-rising-left"),
    ),
    SpriteSpec(
        "stairs",
        5,
        (128, 112),
        (
            "env-stairs-rising-right",
            "env-stairs-rising-left",
            "env-stairs-up-rising-right",
            "env-stairs-up-rising-left",
        ),
    ),
)


def strip_chroma_spill(cell: Image.Image) -> None:
    """Drop opaque magenta fringe left by generated chroma-key edges."""
    pixels = cell.load()
    for y in range(cell.height):
        for x in range(cell.width):
            red, green, blue, alpha = pixels[x, y]
            is_magenta_spill = (
                red > 70
                and blue > 50
                and red + blue > green * 2 + 80
            )
            if is_magenta_spill:
                pixels[x, y] = (red, green, blue, 0)


def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 3 * CELL_SIZE[0]
    y = index // 3 * CELL_SIZE[1]
    cell = sheet.crop((x, y, x + CELL_SIZE[0], y + CELL_SIZE[1])).convert("RGBA")
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"source cell {index} contains no visible pixels")
    return cell.crop(bounds)


def extract_stairs_cell(sheet: Image.Image) -> Image.Image:
    cell = sheet.crop((0, 0, STAIRS_CELL_SIZE, STAIRS_CELL_SIZE)).convert("RGBA")
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("descending stair source contains no visible pixels")
    return cell.crop(bounds)


def reduce_colors(image: Image.Image) -> Image.Image:
    """Lock RGB to the shared Torchstone palette (no dither, hard alpha edge).

    Was an independent MEDIANCUT-32 quantize; now every sheet shares the master
    .gpl indices so environment/props/actors/UI cohere. See torchstone_palette.
    """
    alpha = image.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    rgb = Image.new("RGB", image.size, (5, 7, 12))
    rgb.paste(image, mask=alpha)
    reduced = lock_to_palette(rgb).convert("RGBA")
    reduced.putalpha(alpha)
    return reduced


def build_sprite(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    resized = source.resize(size, Image.Resampling.BOX)
    return reduce_colors(resized)


def build_fitted_sprite(
    source: Image.Image,
    canvas_size: tuple[int, int],
    visible_size: tuple[int, int],
    ground_y: int,
) -> Image.Image:
    scale = min(
        visible_size[0] / source.width,
        visible_size[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = reduce_colors(source.resize(size, Image.Resampling.BOX))
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = (canvas_size[0] - sprite.width) // 2
    y = ground_y - sprite.height
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def save(image: Image.Image, name: str) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT / f"{name}.png", optimize=True)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not STAIRS_SOURCE.exists():
        raise FileNotFoundError(STAIRS_SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (1536, 1024):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    written = 0
    for spec in SPECS:
        sprite = build_sprite(extract_cell(sheet, spec.cell_index), spec.size)
        for output_index, output_name in enumerate(spec.output_names):
            # Every second directional output is the mirrored partner.
            output = (
                sprite
                if output_index % 2 == 0
                else sprite.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            )
            save(output, output_name)
            written += 1

    stairs_sheet = Image.open(STAIRS_SOURCE).convert("RGBA")
    if stairs_sheet.size != (STAIRS_CELL_SIZE * 2, STAIRS_CELL_SIZE * 2):
        raise ValueError(f"unexpected stair source sheet size: {stairs_sheet.size}")
    stairs_down = build_fitted_sprite(
        extract_stairs_cell(stairs_sheet),
        canvas_size=(128, 80),
        visible_size=(128, 76),
        ground_y=78,
    )
    save(stairs_down, "env-stairs-down-rising-right")
    save(
        stairs_down.transpose(Image.Transpose.FLIP_LEFT_RIGHT),
        "env-stairs-down-rising-left",
    )
    written += 2

    print(f"wrote {written} Collapsed Transit environment sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
