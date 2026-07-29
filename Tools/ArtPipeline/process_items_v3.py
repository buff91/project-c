#!/usr/bin/env python3
"""Conform generated item sources to the shared 64px runtime contract."""

from dataclasses import dataclass
from collections import deque
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "docs/art-direction/item-sources-v3"
OUTPUT_DIR = ROOT / "Assets/_Project/Art/Runtime"
CANVAS_SIZE = (64, 64)
ALPHA_CUTOFF = 80
BACKGROUND_TOLERANCE = 28


@dataclass(frozen=True)
class ItemSpec:
    source_name: str
    output_name: str
    visible_size: tuple[int, int]
    bottom_padding: int


SPECS = (
    ItemSpec("item-potion-source-v3.png", "item-potion.png", (54, 54), 7),
    ItemSpec("item-bomb-source-v3.png", "item-bomb.png", (54, 54), 7),
    ItemSpec(
        "item-frost-bomb-source-v3.png",
        "item-frost-bomb.png",
        (54, 54),
        7,
    ),
    ItemSpec("item-oil-flask-source-v3.png", "item-oil-flask.png", (54, 54), 7),
    ItemSpec(
        "item-throwing-knife-source-v3.png",
        "item-throwing-knife.png",
        (58, 58),
        3,
    ),
    ItemSpec(
        "item-recall-scroll-source-v3.png",
        "item-recall-scroll.png",
        (54, 54),
        5,
    ),
    ItemSpec(
        "item-coin-pouch-source-v3.png",
        "item-coin-pouch.png",
        (54, 50),
        11,
    ),
    ItemSpec("item-gemstone-source-v3.png", "item-gemstone.png", (54, 58), 3),
    ItemSpec("item-relic-source-v3.png", "item-relic.png", (54, 56), 5),
    ItemSpec("item-herb-source-v3.png", "item-herb.png", (58, 54), 9),
    ItemSpec(
        "item-blast-powder-source-v3.png",
        "item-blast-powder.png",
        (52, 52),
        9,
    ),
    ItemSpec(
        "item-frost-shard-source-v3.png",
        "item-frost-shard.png",
        (57, 57),
        7,
    ),
)


def remove_magenta_key(source: Image.Image) -> Image.Image:
    """Remove the generated flat-magenta plate without retaining purple fringe."""
    result = source.convert("RGBA")
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            red, green, blue, alpha = pixels[x, y]
            is_key = (
                red > 105
                and blue > 105
                and green < 110
                and abs(red - blue) < 105
                and red + blue > green * 2 + 150
            )
            if is_key:
                pixels[x, y] = (0, 0, 0, 0)
            elif alpha > 0:
                pixels[x, y] = (red, green, blue, 255)
    return result


def remove_border_background(source: Image.Image) -> Image.Image:
    """Flood-remove a near-flat generated plate when the model ignores the key color."""
    result = source.convert("RGBA")
    pixels = result.load()
    corners = (
        pixels[0, 0][:3],
        pixels[result.width - 1, 0][:3],
        pixels[0, result.height - 1][:3],
        pixels[result.width - 1, result.height - 1][:3],
    )
    reference = tuple(
        sorted(color[channel] for color in corners)[len(corners) // 2]
        for channel in range(3)
    )
    if any(
        max(abs(color[channel] - reference[channel]) for channel in range(3))
        > BACKGROUND_TOLERANCE
        for color in corners
    ):
        return result

    def matches_plate(x: int, y: int) -> bool:
        red, green, blue, alpha = pixels[x, y]
        if alpha == 0:
            return True
        return max(
            abs(red - reference[0]),
            abs(green - reference[1]),
            abs(blue - reference[2]),
        ) <= BACKGROUND_TOLERANCE

    queue = deque()
    visited = set()
    for x in range(result.width):
        queue.append((x, 0))
        queue.append((x, result.height - 1))
    for y in range(result.height):
        queue.append((0, y))
        queue.append((result.width - 1, y))

    while queue:
        x, y = queue.popleft()
        if (x, y) in visited or not matches_plate(x, y):
            continue
        visited.add((x, y))
        pixels[x, y] = (0, 0, 0, 0)
        if x > 0:
            queue.append((x - 1, y))
        if x + 1 < result.width:
            queue.append((x + 1, y))
        if y > 0:
            queue.append((x, y - 1))
        if y + 1 < result.height:
            queue.append((x, y + 1))
    return result


def keep_largest_component(source: Image.Image) -> Image.Image:
    """Discard detached generation debris while retaining the main item silhouette."""
    result = source.convert("RGBA")
    alpha = result.getchannel("A")
    visible = {
        (x, y)
        for y in range(result.height)
        for x in range(result.width)
        if alpha.getpixel((x, y)) > 0
    }
    components = []
    while visible:
        seed = visible.pop()
        component = {seed}
        queue = deque((seed,))
        while queue:
            x, y = queue.popleft()
            for neighbor in (
                (x - 1, y - 1),
                (x, y - 1),
                (x + 1, y - 1),
                (x - 1, y),
                (x + 1, y),
                (x - 1, y + 1),
                (x, y + 1),
                (x + 1, y + 1),
            ):
                if neighbor not in visible:
                    continue
                visible.remove(neighbor)
                component.add(neighbor)
                queue.append(neighbor)
        components.append(component)

    if not components:
        return result
    largest = max(components, key=len)
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            if (x, y) not in largest:
                pixels[x, y] = (0, 0, 0, 0)
    return result


def extract_item(source: Image.Image) -> Image.Image:
    cutout = keep_largest_component(
        remove_border_background(remove_magenta_key(source))
    )
    bounds = cutout.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("item source contains no visible pixels")
    return cutout.crop(bounds)


def build_item(source: Image.Image, spec: ItemSpec) -> Image.Image:
    item = extract_item(source)
    scale = min(
        spec.visible_size[0] / item.width,
        spec.visible_size[1] / item.height,
    )
    size = (
        max(1, round(item.width * scale)),
        max(1, round(item.height * scale)),
    )
    item = item.resize(size, Image.Resampling.BOX)
    alpha = item.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    item.putalpha(alpha)
    item = lock_rgba_to_palette(item)

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - item.width) // 2
    y = CANVAS_SIZE[1] - spec.bottom_padding - item.height
    if y < 0:
        raise ValueError(f"{spec.output_name} exceeds the 64px item canvas")
    canvas.alpha_composite(item, (x, y))
    return canvas


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for spec in SPECS:
        source_path = SOURCE_DIR / spec.source_name
        if not source_path.exists():
            raise FileNotFoundError(source_path)
        output = build_item(Image.open(source_path), spec)
        output.save(OUTPUT_DIR / spec.output_name, optimize=True)

    print(f"wrote {len(SPECS)} conformed item sprites to {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
