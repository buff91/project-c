#!/usr/bin/env python3
"""Build the Torchstone UI action icon set from its generated source sheet."""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-torchstone-ui-icons-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
CELL_SIZE = 418
CANVAS_SIZE = (24, 24)
VISIBLE_SIZE = (22, 22)
ALPHA_CUTOFF = 80

TORCHSTONE_PALETTE = tuple(
    tuple(bytes.fromhex(value))
    for value in (
        "05070C",
        "07090E",
        "0A0D13",
        "4A4038",
        "98866F",
        "CFC0AE",
        "9A6B22",
        "FFBD41",
        "FFD554",
        "97907E",
        "EADFC8",
        "45100B",
        "D8452A",
        "14343A",
        "1C4347",
        "4FA7A0",
        "9ADFE8",
        "7FB241",
    )
)


@dataclass(frozen=True)
class IconSpec:
    source_name: str
    cell_index: int
    output_name: str


SPECS = (
    IconSpec("settings", 0, "ui-settings"),
    IconSpec("menu", 1, "ui-menu"),
    IconSpec("rotate-left", 2, "ui-rotate-left"),
    IconSpec("rotate-right", 3, "ui-rotate-right"),
    IconSpec("backpack", 4, "ui-backpack"),
    IconSpec("wait", 5, "ui-wait"),
    IconSpec("melee", 6, "ui-melee"),
    IconSpec("ranged", 7, "ui-ranged"),
    IconSpec("interact", 8, "ui-interact"),
)


def strip_chroma_spill(cell: Image.Image) -> None:
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
    x = index % 3 * CELL_SIZE
    y = index // 3 * CELL_SIZE
    cell = sheet.crop((x, y, x + CELL_SIZE, y + CELL_SIZE)).convert("RGBA")
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"source cell {index} contains no visible pixels")
    return cell.crop(bounds)


def nearest_palette_color(color: tuple[int, int, int]) -> tuple[int, int, int]:
    red, green, blue = color
    return min(
        TORCHSTONE_PALETTE,
        key=lambda candidate:
            (candidate[0] - red) ** 2
            + (candidate[1] - green) ** 2
            + (candidate[2] - blue) ** 2,
    )


def snap_to_palette(image: Image.Image) -> Image.Image:
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    source_pixels = image.load()
    output_pixels = result.load()
    color_cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = source_pixels[x, y]
            if alpha < ALPHA_CUTOFF:
                continue
            source_color = (red, green, blue)
            target = color_cache.get(source_color)
            if target is None:
                target = nearest_palette_color(source_color)
                color_cache[source_color] = target
            output_pixels[x, y] = (*target, 255)
    return result


def build_icon(source: Image.Image) -> Image.Image:
    scale = min(
        VISIBLE_SIZE[0] / source.width,
        VISIBLE_SIZE[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = snap_to_palette(source.resize(size, Image.Resampling.BOX))
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - sprite.width) // 2
    y = (CANVAS_SIZE[1] - sprite.height) // 2
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (CELL_SIZE * 3, CELL_SIZE * 3):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for spec in SPECS:
        icon = build_icon(extract_cell(sheet, spec.cell_index))
        icon.save(OUTPUT / f"{spec.output_name}.png", optimize=True)

    print(f"wrote {len(SPECS)} Torchstone UI icons to {OUTPUT}")


if __name__ == "__main__":
    main()
