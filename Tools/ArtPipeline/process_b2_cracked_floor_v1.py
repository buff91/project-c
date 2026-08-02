#!/usr/bin/env python3
"""Conform the approved B2 cracked-floor concept into one flat floor tile.

The generated image owns only the damage pattern. The canonical Project-C floor
owns the diamond, alpha, and value structure so this B2 dressing cannot acquire
a raised slab edge or a false hole/cover silhouette.
"""

from collections import deque
from pathlib import Path

from PIL import Image

from process_b2_parking_dressing_v1 import ALPHA_CUTOFF, extract_object
from process_b2_parking_dressing_v2 import neutralize_floor_source
from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-b2-cracked-floor-source-v1.png"
BASE_FLOOR = ROOT / "Assets/_Project/Art/Environment/env-floor.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment/env-floor-b2-cracked.png"
PREVIEW = ROOT / "docs/captures/b2-cracked-floor-conform-preview-v1.png"
CANVAS_SIZE = (128, 64)

# The generated concrete sits around 0.35 luminance. Only the lowest clusters
# become damage; the rest is discarded in favor of the canonical neutral floor.
DAMAGE_LUMINANCE_LIMIT = 0.325
DAMAGE_INTERIOR_LIMIT = 0.78
MAX_DAMAGE_RATIO = 0.10
MAX_COMPONENTS = 5
MIN_COMPONENT_PIXELS = 2
MAX_RUST_PIXELS = 20

GREY_SHADOW = (44, 49, 56, 255)
GREY_MID = (59, 63, 69, 255)
RUST_DARK = (90, 46, 27, 255)


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def luminance(pixel: tuple[int, int, int, int]) -> float:
    red, green, blue, _ = pixel
    return (red * 0.2126 + green * 0.7152 + blue * 0.0722) / 255.0


def is_warm_damage(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, alpha = pixel
    chroma = max(red, green, blue) - min(red, green, blue)
    return (
        alpha >= ALPHA_CUTOFF
        and chroma >= 32
        and red > blue * 1.15
        and green > blue * 1.05
    )


def is_inside_damage_inset(x: int, y: int) -> bool:
    center_x = (CANVAS_SIZE[0] - 1) * 0.5
    center_y = (CANVAS_SIZE[1] - 1) * 0.5
    normalized = (
        abs(x - center_x) / (CANVAS_SIZE[0] * 0.5)
        + abs(y - center_y) / (CANVAS_SIZE[1] * 0.5)
    )
    return normalized <= DAMAGE_INTERIOR_LIMIT


def extract_source_tile(source: Image.Image) -> Image.Image:
    tile = extract_object(source)
    ratio = tile.width / tile.height
    if not 1.75 <= ratio <= 2.25:
        raise ValueError(f"B2 cracked source must be a 2:1 diamond, got {tile.size}")
    return tile.resize(CANVAS_SIZE, Image.Resampling.BOX)


def _components(mask: set[tuple[int, int]]) -> list[list[tuple[int, int]]]:
    remaining = set(mask)
    components: list[list[tuple[int, int]]] = []
    while remaining:
        start = min(remaining, key=lambda point: (point[1], point[0]))
        remaining.remove(start)
        queue = deque([start])
        component = [start]
        while queue:
            x, y = queue.popleft()
            for offset_y in (-1, 0, 1):
                for offset_x in (-1, 0, 1):
                    if offset_x == 0 and offset_y == 0:
                        continue
                    neighbor = (x + offset_x, y + offset_y)
                    if neighbor not in remaining:
                        continue
                    remaining.remove(neighbor)
                    queue.append(neighbor)
                    component.append(neighbor)
        components.append(component)
    return components


def damage_pixels(source_tile: Image.Image, base_floor: Image.Image) -> set[tuple[int, int]]:
    source = source_tile.convert("RGBA")
    base = base_floor.convert("RGBA")
    candidates: set[tuple[int, int]] = set()
    for y in range(CANVAS_SIZE[1]):
        for x in range(CANVAS_SIZE[0]):
            if base.getpixel((x, y))[3] == 0 or not is_inside_damage_inset(x, y):
                continue
            pixel = source.getpixel((x, y))
            if pixel[3] >= ALPHA_CUTOFF and luminance(pixel) < DAMAGE_LUMINANCE_LIMIT:
                candidates.add((x, y))

    components = [
        component
        for component in _components(candidates)
        if len(component) >= MIN_COMPONENT_PIXELS
    ]
    components.sort(key=lambda component: (-len(component), min(component)))
    selected = {
        point
        for component in components[:MAX_COMPONENTS]
        for point in component
    }

    visible_count = sum(1 for pixel in _pixels(base) if pixel[3] > 0)
    limit = max(1, int(visible_count * MAX_DAMAGE_RATIO))
    if len(selected) > limit:
        selected = set(
            sorted(
                selected,
                key=lambda point: (
                    luminance(source.getpixel(point)),
                    point[1],
                    point[0],
                ),
            )[:limit]
        )
    if not selected:
        raise ValueError("B2 cracked source contains no usable damage clusters")
    return selected


def build_output(source: Image.Image, base_floor: Image.Image) -> Image.Image:
    if base_floor.size != CANVAS_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    source_tile = extract_source_tile(source)
    base = despeckle(lock_rgba_to_palette(neutralize_floor_source(base_floor)))
    output = base.copy()
    damage = damage_pixels(source_tile, base)
    for point in damage:
        output.putpixel(
            point,
            GREY_SHADOW if luminance(source_tile.getpixel(point)) < 0.27 else GREY_MID,
        )

    rust_points = [
        (x, y)
        for y in range(CANVAS_SIZE[1])
        for x in range(CANVAS_SIZE[0])
        if base.getpixel((x, y))[3] > 0
        and is_inside_damage_inset(x, y)
        and is_warm_damage(source_tile.getpixel((x, y)))
    ]
    for point in rust_points[:MAX_RUST_PIXELS]:
        output.putpixel(point, RUST_DARK)

    output.putalpha(base.getchannel("A").point(lambda value: 255 if value else 0))
    return despeckle(lock_rgba_to_palette(output))


def build_preview(base_floor: Image.Image, output: Image.Image) -> Image.Image:
    base = despeckle(lock_rgba_to_palette(neutralize_floor_source(base_floor)))
    preview = Image.new("RGBA", (CANVAS_SIZE[0] * 2, CANVAS_SIZE[1]), (5, 7, 12, 255))
    preview.alpha_composite(base, (0, 0))
    preview.alpha_composite(output, (CANVAS_SIZE[0], 0))
    return preview.resize((preview.width * 5, preview.height * 5), Image.Resampling.NEAREST)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    base_floor = Image.open(BASE_FLOOR).convert("RGBA")
    output = build_output(Image.open(SOURCE).convert("RGBA"), base_floor)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    output.save(OUTPUT, optimize=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    build_preview(base_floor, output).save(PREVIEW, optimize=True)
    print(f"wrote flat B2 cracked floor to {OUTPUT}")


if __name__ == "__main__":
    main()
