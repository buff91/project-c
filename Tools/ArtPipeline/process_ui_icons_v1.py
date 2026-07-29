#!/usr/bin/env python3
"""Build the Torchstone UI action icon set from its generated source sheet."""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-torchstone-ui-icons-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
CELL_SIZE = 418
CANVAS_SIZE = (32, 32)
VISIBLE_SIZE = (30, 30)
ALPHA_CUTOFF = 80

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


def build_icon(source: Image.Image) -> Image.Image:
    scale = min(
        VISIBLE_SIZE[0] / source.width,
        VISIBLE_SIZE[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = lock_rgba_to_palette(source.resize(size, Image.Resampling.BOX))
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
