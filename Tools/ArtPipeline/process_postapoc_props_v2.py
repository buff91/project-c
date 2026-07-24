#!/usr/bin/env python3
"""Build the polished Collapsed Transit prop slice."""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-collapsed-transit-props-source-v2.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
CELL_SIZE = 627
ALPHA_CUTOFF = 80


@dataclass(frozen=True)
class PropSpec:
    source_name: str
    cell_index: int
    canvas_size: tuple[int, int]
    visible_size: tuple[int, int]
    ground_y: int
    output_name: str


SPECS = (
    PropSpec("drum-brazier", 0, (64, 64), (60, 58), 62, "prop-campfire"),
    PropSpec(
        "fuel-canister",
        1,
        (64, 64),
        (56, 60),
        62,
        "prop-explosive-barrel",
    ),
    PropSpec("anomaly-gate", 2, (64, 80), (62, 76), 78, "prop-portal"),
    PropSpec("utility-locker", 3, (64, 64), (60, 52), 62, "prop-stash"),
)


def strip_chroma_spill(cell: Image.Image) -> None:
    """Drop opaque magenta fringe left by the generated chroma-key edge."""
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
    x = index % 2 * CELL_SIZE
    y = index // 2 * CELL_SIZE
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


def reduce_colors(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    rgb = Image.new("RGB", image.size, (5, 7, 12))
    rgb.paste(image, mask=alpha)
    reduced = lock_to_palette(rgb).convert("RGBA")  # 공용 .gpl 팔레트 잠금
    reduced.putalpha(alpha)
    return reduced


def build_prop(source: Image.Image, spec: PropSpec) -> Image.Image:
    scale = min(
        spec.visible_size[0] / source.width,
        spec.visible_size[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = reduce_colors(source.resize(size, Image.Resampling.BOX))

    canvas = Image.new("RGBA", spec.canvas_size, (0, 0, 0, 0))
    x = (spec.canvas_size[0] - sprite.width) // 2
    y = spec.ground_y - sprite.height
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (CELL_SIZE * 2, CELL_SIZE * 2):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for spec in SPECS:
        prop = build_prop(extract_cell(sheet, spec.cell_index), spec)
        prop.save(OUTPUT / f"{spec.output_name}.png", optimize=True)

    print(f"wrote {len(SPECS)} Collapsed Transit props to {OUTPUT}")


if __name__ == "__main__":
    main()
