#!/usr/bin/env python3
"""Build the first polished Collapsed Transit actor slice."""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-collapsed-transit-actors-source-v2.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
CELL_SIZE = 627
CANVAS_SIZE = (48, 64)
VISIBLE_SIZE = (46, 60)
GROUND_Y = 62
ALPHA_CUTOFF = 80


@dataclass(frozen=True)
class ActorSpec:
    source_name: str
    cell_index: int
    output_names: tuple[str, ...]


SPECS = (
    ActorSpec("bulwark", 0, ("actor-player", "actor-knight")),
    ActorSpec("scavenger", 1, ("actor-goblin",)),
    ActorSpec("sentry", 2, ("actor-skeleton",)),
    ActorSpec("ooze", 3, ("actor-slime",)),
)


def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 2 * CELL_SIZE
    y = index // 2 * CELL_SIZE
    cell = sheet.crop((x, y, x + CELL_SIZE, y + CELL_SIZE)).convert("RGBA")
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


def build_actor(source: Image.Image) -> Image.Image:
    scale = min(
        VISIBLE_SIZE[0] / source.width,
        VISIBLE_SIZE[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = reduce_colors(source.resize(size, Image.Resampling.BOX))

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - sprite.width) // 2
    y = GROUND_Y - sprite.height
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (CELL_SIZE * 2, CELL_SIZE * 2):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    written = 0
    for spec in SPECS:
        actor = build_actor(extract_cell(sheet, spec.cell_index))
        for output_name in spec.output_names:
            actor.save(OUTPUT / f"{output_name}.png", optimize=True)
            written += 1

    print(f"wrote {written} Collapsed Transit actors to {OUTPUT}")


if __name__ == "__main__":
    main()
