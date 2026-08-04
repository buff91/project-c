#!/usr/bin/env python3
"""Conform the adopted depth-band board into calm, repeat-safe floor tops.

The generated board remains useful as a placement hint, but it no longer owns
runtime geometry or material color.  Every output starts from the canonical
``env-floor`` pixels, keeps its quiet three-pixel perimeter, and replaces a
fixed budget of complete 2x2 blocks with named cool-grey roles.  Consequently
an ImageGen rerun cannot restore the oversized warm diamonds or one long dark
crack that used to stamp across every dungeon cell.

The ``-raised`` PNGs intentionally share the corresponding flat top verbatim.
Unity owns the actual height lip in ``GetMappedTileSprite`` through
``DrawExtrudedSides`` and the raised surface role; baking another front face in
the source tile would render the lip twice.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageFilter

from process_shared_floor_material_v1 import (
    OUTER_BAND_PIXELS,
    canonical_diamond_mask,
    encode_png,
    floor_source_colors,
    outer_band_mask,
)
from torchstone_palette import load_gpl_entries


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-band-floors-source-v1.png"
BASE_FLOOR = ROOT / "Assets/_Project/Art/Environment/env-floor.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
SHEET_SIZE = (1536, 1024)
CELL_SIZE = (512, 512)
SPRITE_SIZE = (128, 64)
ALPHA_CUTOFF = 80
PIXEL_CLUSTER = 2

# Exact shared-pixel budgets.  The remainder is authored detail, but most of it
# still resolves to Unity's Stone role; CONTRAST_RATIOS is the smaller visible
# Shadow budget.  Rounding happens in complete 2x2 blocks.
SHARED_RATIOS = {
    "mid": 0.90,
    "deep": 0.84,
    "boss": 0.78,
}
CONTRAST_RATIOS = {
    "mid": 0.03,
    "deep": 0.05,
    "boss": 0.07,
}

COOL_FLOOR_COLOR_NAMES = ("grey-2", "grey-3", "grey-4")


@dataclass(frozen=True)
class BandSpec:
    band: str
    cell_index: int
    output_name: str


# Both rows use the approved flat-cell placement hint.  The generated raised
# row contained a painted side face, while the runtime already extrudes that
# face.  Keeping six output names preserves the catalog/file contract.
SPECS = (
    BandSpec("mid", 0, "env-floor-mid"),
    BandSpec("deep", 1, "env-floor-deep"),
    BandSpec("boss", 2, "env-floor-boss"),
    BandSpec("mid", 0, "env-floor-mid-raised"),
    BandSpec("deep", 1, "env-floor-deep-raised"),
    BandSpec("boss", 2, "env-floor-boss-raised"),
)


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 3 * CELL_SIZE[0]
    y = index // 3 * CELL_SIZE[1]
    cell = sheet.crop((x, y, x + CELL_SIZE[0], y + CELL_SIZE[1])).convert("RGBA")
    pixels = cell.load()
    for py in range(cell.height):
        for px in range(cell.width):
            if _is_chroma(pixels[px, py]):
                pixels[px, py] = (5, 7, 12, 0)

    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"band floor cell {index} contains no visible pixels")
    return cell.crop(bounds)


def _named_colors() -> dict[str, tuple[int, int, int, int]]:
    entries = dict(load_gpl_entries())
    missing = [name for name in COOL_FLOOR_COLOR_NAMES if name not in entries]
    if missing:
        raise ValueError(f"band-floor palette entries are missing: {missing}")
    return {name: (*entries[name], 255) for name in COOL_FLOOR_COLOR_NAMES}


def _validate_base_floor(base_floor: Image.Image) -> Image.Image:
    base = base_floor.convert("RGBA")
    if base.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base.size}")

    alpha = base.getchannel("A")
    if not set(_pixels(alpha)).issubset({0, 255}):
        raise ValueError("shared floor alpha must be hard 0/255")
    canonical = canonical_diamond_mask()
    if alpha.tobytes() != canonical.tobytes():
        raise ValueError("shared floor must use the canonical diamond mask")

    legal_base = set(floor_source_colors().values())
    visible = {pixel for pixel in _pixels(base) if pixel[3] > 0}
    if not visible.issubset(legal_base):
        raise ValueError(
            "shared floor must use only the named grey-3/grey-4 base colors"
        )
    return base


def _source_luminance_hint(source: Image.Image) -> Image.Image:
    """Normalize one generated cell into a neutral 128x64 luminance field."""
    resized = source.resize(SPRITE_SIZE, Image.Resampling.BOX).convert("RGBA")
    visible_values = [
        round(red * 0.2126 + green * 0.7152 + blue * 0.0722)
        for red, green, blue, alpha in _pixels(resized)
        if alpha >= ALPHA_CUTOFF
    ]
    if not visible_values:
        raise ValueError("band floor placement hint contains no visible pixels")
    background = sorted(visible_values)[len(visible_values) // 2]
    field = Image.new("L", SPRITE_SIZE, background)
    source_luma = resized.convert("L")
    field.paste(source_luma, mask=resized.getchannel("A"))
    return field


def _stable_order(block_x: int, block_y: int, salt: int) -> int:
    # Python's built-in hash is process-salted.  These fixed integer primes
    # provide a stable tie-break for flat sources without introducing RNG state.
    value = block_x * 73856093
    value ^= block_y * 19349663
    value ^= (salt + 1) * 83492791
    value ^= value >> 13
    return value & 0x7FFFFFFF


def _ranked_blocks(
    source: Image.Image,
    mask: Image.Image,
    protected_band: Image.Image,
    salt: int,
) -> list[tuple[int, int]]:
    """Rank safe 2x2 blocks by local source contrast, then stable position."""
    field = _source_luminance_hint(source)
    blurred = field.filter(ImageFilter.BoxBlur(4))
    field_pixels = field.load()
    blur_pixels = blurred.load()
    mask_pixels = mask.load()
    band_pixels = protected_band.load()
    ranked: list[tuple[int, int, int, int]] = []

    for top in range(0, SPRITE_SIZE[1], PIXEL_CLUSTER):
        for left in range(0, SPRITE_SIZE[0], PIXEL_CLUSTER):
            points = tuple(
                (left + dx, top + dy)
                for dy in range(PIXEL_CLUSTER)
                for dx in range(PIXEL_CLUSTER)
            )
            if not all(
                mask_pixels[x, y] and not band_pixels[x, y]
                for x, y in points
            ):
                continue

            # Dark local residuals carry cracks; absolute residuals retain a
            # small amount of authored placement from lighter wear without
            # copying its generated color or large connected silhouette.
            residual = 0
            dark_residual = 0
            for x, y in points:
                delta = blur_pixels[x, y] - field_pixels[x, y]
                residual += abs(delta)
                dark_residual += max(0, delta)
            block_x = left // PIXEL_CLUSTER
            block_y = top // PIXEL_CLUSTER
            score = dark_residual * 3 + residual
            ranked.append(
                (-score, _stable_order(block_x, block_y, salt), block_y, block_x)
            )

    ranked.sort()
    return [(block_x, block_y) for _, _, block_y, block_x in ranked]


def _select_contrast_blocks(
    ranked: list[tuple[int, int]],
    count: int,
) -> list[tuple[int, int]]:
    """Select separated 2x2 accents so no generated crack can become a line."""
    selected: list[tuple[int, int]] = []
    for candidate in ranked:
        if any(
            abs(candidate[0] - other[0]) <= 1
            and abs(candidate[1] - other[1]) <= 1
            for other in selected
        ):
            continue
        selected.append(candidate)
        if len(selected) == count:
            return selected
    raise ValueError(
        f"canonical floor cannot place {count} separated contrast blocks"
    )


def _paint_block(
    output: Image.Image,
    block: tuple[int, int],
    color: tuple[int, int, int, int] | None,
    base_colors: dict[str, tuple[int, int, int, int]],
) -> None:
    pixels = output.load()
    left = block[0] * PIXEL_CLUSTER
    top = block[1] * PIXEL_CLUSTER
    for offset_y in range(PIXEL_CLUSTER):
        for offset_x in range(PIXEL_CLUSTER):
            x = left + offset_x
            y = top + offset_y
            if color is not None:
                pixels[x, y] = color
            else:
                # Toggle between the two source greys.  Every selected pixel is
                # observably authored in the PNG, while both values still map to
                # the same calm Stone role at runtime.
                pixels[x, y] = (
                    base_colors["mid"]
                    if pixels[x, y] == base_colors["wear"]
                    else base_colors["wear"]
                )


def build_band_sprite(
    source: Image.Image,
    base_floor: Image.Image,
    band: str,
    salt: int,
) -> Image.Image:
    if band not in SHARED_RATIOS or band not in CONTRAST_RATIOS:
        raise ValueError(f"unknown floor band: {band}")

    mask = canonical_diamond_mask()
    protected = outer_band_mask(mask, OUTER_BAND_PIXELS)
    ranked = _ranked_blocks(source, mask, protected, salt)
    visible_count = sum(value > 0 for value in _pixels(mask))
    total_blocks = round(
        visible_count * (1.0 - SHARED_RATIOS[band]) / (PIXEL_CLUSTER ** 2)
    )
    contrast_blocks = round(
        visible_count * CONTRAST_RATIOS[band] / (PIXEL_CLUSTER ** 2)
    )
    if total_blocks > len(ranked):
        raise ValueError(f"{band} detail budget exceeds canonical floor interior")
    if contrast_blocks > total_blocks:
        raise ValueError(f"{band} contrast budget exceeds its detail budget")

    contrast = _select_contrast_blocks(ranked, contrast_blocks)
    contrast_set = set(contrast)
    subtle = [block for block in ranked if block not in contrast_set][
        : total_blocks - contrast_blocks
    ]
    if len(subtle) != total_blocks - contrast_blocks:
        raise ValueError(f"{band} cannot fill its subtle detail budget")

    output = base_floor.copy()
    colors = _named_colors()
    base_colors = floor_source_colors()
    for block in subtle:
        _paint_block(output, block, None, base_colors)
    for block in contrast:
        _paint_block(output, block, colors["grey-2"], base_colors)
    return output


def build_outputs(
    sheet: Image.Image,
    base_floor: Image.Image,
) -> dict[str, Image.Image]:
    if sheet.size != SHEET_SIZE:
        raise ValueError(f"unexpected band floor source size: {sheet.size}")
    base = _validate_base_floor(base_floor)

    tops: dict[str, Image.Image] = {}
    for spec in SPECS:
        if spec.band not in tops:
            tops[spec.band] = build_band_sprite(
                extract_cell(sheet, spec.cell_index),
                base,
                spec.band,
                spec.cell_index,
            )

    return {spec.output_name: tops[spec.band].copy() for spec in SPECS}


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    outputs = build_outputs(
        Image.open(SOURCE).convert("RGBA"),
        Image.open(BASE_FLOOR).convert("RGBA"),
    )
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        (OUTPUT / f"{name}.png").write_bytes(encode_png(image))
    print(f"wrote {len(outputs)} calm band floor sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
