#!/usr/bin/env python3
"""Conform the approved shared-floor swatch into the canonical ``env-floor``.

The generated source owns only low-frequency wear placement.  This processor owns
everything that is a runtime contract: the 128x64 hard-alpha diamond, a quiet
three-pixel perimeter, an exact raw-wear budget, and 2x2 authored pixel clusters.
Both legal greys deliberately sit in Unity's ``Stone`` luminance band.  The source
PNG keeps subtle editable wear, while repeated runtime tiles collapse to one calm
surface role instead of stamping identical dark/light blobs across every cell.
"""

from __future__ import annotations

from functools import lru_cache
from heapq import heappop, heappush
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageFilter

from torchstone_palette import load_gpl_entries


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-shared-floor-material-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment/env-floor.png"
PREVIEW = ROOT / "docs/captures/shared-floor-material-conform-preview-v1.png"

CANVAS_SIZE = (128, 64)
PIXEL_CLUSTER = 2
ALPHA_CUTOFF = 80
OUTER_BAND_PIXELS = 3
WEAR_RATIO = 0.08
WEAR_MASSES = 3
PREVIEW_SCALE = 5

# Both source greys map through PrototypeEnvironmentSprites' .28-.50 luminance
# interval to Stone.  Runtime variation belongs to world-space overlays and light,
# not a motif baked identically into every repeated shared tile.
WEAR_SOURCE_NAME = "grey-3"
MID_SOURCE_NAME = "grey-4"


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


@lru_cache(maxsize=1)
def floor_source_colors() -> dict[str, tuple[int, int, int, int]]:
    """Resolve the two legal neutral colors from the shared palette SSOT."""
    entries = dict(load_gpl_entries())
    names = {
        "wear": WEAR_SOURCE_NAME,
        "mid": MID_SOURCE_NAME,
    }
    missing = [name for name in names.values() if name not in entries]
    if missing:
        raise ValueError(f"shared-floor palette entries are missing: {missing}")
    return {role: (*entries[name], 255) for role, name in names.items()}


def _is_magenta_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, alpha = pixel
    return (
        alpha >= ALPHA_CUTOFF
        and red >= 170
        and blue >= 170
        and green <= 120
        and red > green * 1.45
        and blue > green * 1.45
    )


def extract_material(source: Image.Image) -> Image.Image:
    """Crop the non-chroma square material field from the generated source."""
    rgba = source.convert("RGBA")
    usable = Image.new("L", rgba.size, 0)
    source_pixels = rgba.load()
    mask_pixels = usable.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            pixel = source_pixels[x, y]
            if pixel[3] >= ALPHA_CUTOFF and not _is_magenta_chroma(pixel):
                mask_pixels[x, y] = 255

    bounds = usable.getbbox()
    if bounds is None:
        raise ValueError("shared-floor source contains no non-chroma material")
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    ratio = width / height
    if not 0.90 <= ratio <= 1.10:
        raise ValueError(f"shared-floor material must be square, got {width}x{height}")
    return rgba.crop(bounds).convert("RGB")


def canonical_diamond_mask() -> Image.Image:
    """Return the existing 4,098-pixel hard-alpha tile silhouette exactly."""
    width, height = CANVAS_SIZE
    mask = Image.new("L", CANVAS_SIZE, 0)
    pixels = mask.load()
    for y in range(height):
        for x in range(width):
            diamond = abs((x - 63.5) / 64.0) + abs((y - 31.5) / 32.0)
            if diamond <= 1.0:
                pixels[x, y] = 255
    # The historical/import contract requires all four canvas extremes to be
    # opaque even where the half-pixel equation excludes one side of a corner.
    for point in ((64, 0), (127, 32), (64, 63), (0, 32)):
        pixels[point] = 255
    return mask


def outer_band_mask(mask: Image.Image, radius: int = OUTER_BAND_PIXELS) -> Image.Image:
    """Return visible pixels within ``radius`` pixels of transparency.

    ``MinFilter(2r+1)`` is a deterministic square erosion.  It intentionally
    protects slightly more than a Euclidean three-pixel rim, so no dark cluster
    can accidentally read as a closed border along the steep diamond edge.
    """
    if mask.mode != "L" or mask.size != CANVAS_SIZE:
        raise ValueError(f"unexpected canonical mask: mode={mask.mode}, size={mask.size}")
    eroded = mask.filter(ImageFilter.MinFilter(radius * 2 + 1))
    band = Image.new("L", CANVAS_SIZE, 0)
    mask_pixels = mask.load()
    eroded_pixels = eroded.load()
    band_pixels = band.load()
    for y in range(CANVAS_SIZE[1]):
        for x in range(CANVAS_SIZE[0]):
            if mask_pixels[x, y] and not eroded_pixels[x, y]:
                band_pixels[x, y] = 255
    return band


def _material_luminance_field(material: Image.Image) -> Image.Image:
    """Collapse generator micro-noise while retaining its broad wear masses."""
    # A small square field is sampled through the inverse isometric projection.
    # BOX averages the large source first; the one-cell box blur avoids selecting
    # incidental single-source-pixel extrema as runtime wear.
    return (
        material.convert("L")
        .resize((64, 64), Image.Resampling.BOX)
        .filter(ImageFilter.BoxBlur(1))
    )


def screen_to_material(x: float, y: float) -> tuple[float, float]:
    """Inverse-project one diamond point into square material coordinates."""
    axis_x = (x - 63.5) / 64.0
    axis_y = (y - 31.5) / 32.0
    return (
        (axis_x + axis_y + 1.0) * 0.5,
        (axis_y - axis_x + 1.0) * 0.5,
    )


def _sample_field(field: Image.Image, x: float, y: float) -> int:
    u, v = screen_to_material(x, y)
    px = min(field.width - 1, max(0, round(u * (field.width - 1))))
    py = min(field.height - 1, max(0, round(v * (field.height - 1))))
    return field.getpixel((px, py))


def _eligible_blocks(mask: Image.Image, band: Image.Image, field: Image.Image):
    """Return fully interior 2x2 blocks with a source-derived rank score."""
    mask_pixels = mask.load()
    band_pixels = band.load()
    blocks: list[tuple[int, int, int]] = []
    for block_y in range(CANVAS_SIZE[1] // PIXEL_CLUSTER):
        for block_x in range(CANVAS_SIZE[0] // PIXEL_CLUSTER):
            left = block_x * PIXEL_CLUSTER
            top = block_y * PIXEL_CLUSTER
            points = (
                (left, top),
                (left + 1, top),
                (left, top + 1),
                (left + 1, top + 1),
            )
            if not all(mask_pixels[x, y] and not band_pixels[x, y] for x, y in points):
                continue
            score = _sample_field(field, left + 0.5, top + 0.5)
            blocks.append((score, block_x, block_y))
    return blocks


def _block_material_point(position: tuple[int, int]) -> tuple[float, float]:
    block_x, block_y = position
    return screen_to_material(
        block_x * PIXEL_CLUSTER + 0.5,
        block_y * PIXEL_CLUSTER + 0.5,
    )


def _select_mass_seeds(
    scores: dict[tuple[int, int], int],
    count: int,
) -> list[tuple[int, int]]:
    """Pick separated source-dark extrema as deterministic wear seeds."""
    ranked = sorted(
        scores,
        key=lambda point: (
            scores[point],
            point[1],
            point[0],
        ),
    )
    # Separation is measured in the original square material plane rather than
    # skewed screen pixels.  Relaxation is deterministic and only matters for a
    # synthetic/degenerate source whose extrema all occupy one tiny patch.
    for minimum_distance in (0.30, 0.24, 0.18, 0.0):
        seeds: list[tuple[int, int]] = []
        material_points: list[tuple[float, float]] = []
        for candidate in ranked:
            candidate_point = _block_material_point(candidate)
            if all(
                (candidate_point[0] - point[0]) ** 2
                + (candidate_point[1] - point[1]) ** 2
                >= minimum_distance ** 2
                for point in material_points
            ):
                seeds.append(candidate)
                material_points.append(candidate_point)
                if len(seeds) == count:
                    return seeds
    raise ValueError(f"shared-floor source cannot provide {count} mass seeds")


def _grow_source_ranked_wear(
    blocks: list[tuple[int, int, int]],
    total_blocks: int,
    mass_count: int,
) -> list[tuple[int, int, int]]:
    """Grow a fixed budget into a few connected source-ranked wear masses.

    Every label grows through four-neighbour blocks, while an eight-neighbour
    exclusion between labels leaves a full 2px midtone gap.  Source luminance is
    the main frontier cost and distance from the seed is a compactness penalty;
    this retains authored wear placement without returning to confetti.
    """
    scores = {
        (block_x, block_y): score
        for score, block_x, block_y in blocks
    }
    if len(scores) < total_blocks:
        raise ValueError("shared-floor mass budget exceeds eligible source blocks")

    seeds = _select_mass_seeds(scores, mass_count)
    target_sizes = [total_blocks // mass_count] * mass_count
    for index in range(total_blocks % mass_count):
        target_sizes[index] += 1

    owner = {seed: index for index, seed in enumerate(seeds)}
    selected = [{seed} for seed in seeds]
    frontiers: list[list[tuple[int, int, int, int, int]]] = [
        [] for _ in range(mass_count)
    ]

    def touches_other_mass(position: tuple[int, int], label: int) -> bool:
        x, y = position
        return any(
            owner.get((x + dx, y + dy), label) != label
            for dy in (-1, 0, 1)
            for dx in (-1, 0, 1)
            if dx != 0 or dy != 0
        )

    def push_neighbors(position: tuple[int, int], label: int) -> None:
        seed_x, seed_y = seeds[label]
        x, y = position
        for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if neighbor not in scores or neighbor in owner:
                continue
            score = scores[neighbor]
            source_cost = score
            distance = abs(neighbor[0] - seed_x) + abs(neighbor[1] - seed_y)
            # Source placement dominates; distance gently keeps each flood broad
            # and compact when nearby cells have almost identical values.
            priority = source_cost * 4 + distance
            heappush(
                frontiers[label],
                (priority, source_cost, distance, neighbor[1], neighbor[0]),
            )

    for label, seed in enumerate(seeds):
        push_neighbors(seed, label)

    while any(len(selected[index]) < target_sizes[index] for index in range(mass_count)):
        progressed = False
        for label in range(mass_count):
            if len(selected[label]) >= target_sizes[label]:
                continue
            frontier = frontiers[label]
            while frontier:
                _, _, _, block_y, block_x = heappop(frontier)
                position = (block_x, block_y)
                if position in owner or touches_other_mass(position, label):
                    continue
                owner[position] = label
                selected[label].add(position)
                push_neighbors(position, label)
                progressed = True
                break
        if not progressed:
            raise ValueError("shared-floor source masses cannot grow without merging")

    return [
        (scores[position], position[0], position[1])
        for mass in selected
        for position in sorted(mass, key=lambda point: (point[1], point[0]))
    ]


def build_floor(source: Image.Image) -> Image.Image:
    """Build one canonical shared floor with source-only, runtime-neutral wear."""
    material = extract_material(source)
    field = _material_luminance_field(material)
    mask = canonical_diamond_mask()
    band = outer_band_mask(mask)
    colors = floor_source_colors()

    output = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    output.paste(colors["mid"], mask=mask)

    blocks = _eligible_blocks(mask, band, field)
    visible_count = sum(value > 0 for value in _pixels(mask))
    wear_blocks = round(visible_count * WEAR_RATIO / (PIXEL_CLUSTER ** 2))
    if len(blocks) < wear_blocks:
        raise ValueError("canonical shared-floor interior has too few 2x2 blocks")

    wear = _grow_source_ranked_wear(
        blocks,
        wear_blocks,
        WEAR_MASSES,
    )

    pixels = output.load()
    for _, block_x, block_y in wear:
        left = block_x * PIXEL_CLUSTER
        top = block_y * PIXEL_CLUSTER
        for offset_y in range(PIXEL_CLUSTER):
            for offset_x in range(PIXEL_CLUSTER):
                pixels[left + offset_x, top + offset_y] = colors["wear"]

    # Alpha is assigned last so transparent RGB never inherits an authored ramp.
    output.putalpha(mask)
    return output


def build_preview(floor: Image.Image) -> Image.Image:
    """Build a nearest-neighbour inspection image without publishing runtime art."""
    if floor.size != CANVAS_SIZE:
        raise ValueError(f"unexpected shared-floor preview input: {floor.size}")
    width = CANVAS_SIZE[0] * PREVIEW_SCALE
    height = CANVAS_SIZE[1] * PREVIEW_SCALE
    preview = Image.new("RGBA", (width, height), (5, 7, 12, 255))
    preview.alpha_composite(floor.resize((width, height), Image.Resampling.NEAREST))
    return preview.convert("RGB")


def encode_png(image: Image.Image) -> bytes:
    """Encode metadata-free deterministic PNG bytes for publishing/tests."""
    buffer = BytesIO()
    image.save(buffer, format="PNG", optimize=False, compress_level=9)
    return buffer.getvalue()


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    floor = build_floor(Image.open(SOURCE).convert("RGBA"))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encode_png(floor))
    PREVIEW.write_bytes(encode_png(build_preview(floor)))
    print(f"wrote shared floor: {OUTPUT}")
    print(f"wrote preview: {PREVIEW}")


if __name__ == "__main__":
    main()
