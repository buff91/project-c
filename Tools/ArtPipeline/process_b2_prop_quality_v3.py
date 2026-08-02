#!/usr/bin/env python3
"""Conform the approved B2 production sheet into native runtime candidates.

The ImageGen sheet is a source reference, not a runtime spritesheet.  Each
transparent component is cropped by its measured alpha bbox, reduced once to
the 32x56 logical wall regime (or a centered logical prop silhouette), locked
to the shared Torchstone palette, hard-alphaed, and nearest-upscaled to 2x
clusters.  The evaluation build writes only to ``/private/tmp``.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw

from process_b2_prop_quality_v1 import (
    DIRECTIONS,
    PALETTE,
    PIXEL_CLUSTER,
    PROP_LOGICAL_SIZE,
    PROP_SIZE,
    SERVICE_MASTER_SIZE,
    TRANSPARENT,
    WALL_LOGICAL_SIZE,
    WALL_SIZE,
    WallDirection,
)
from torchstone_palette import lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-b2-prop-production-sheet-v2.png"
OUTPUT = Path("/private/tmp/project-c-b2-v3")
ASSET_PREVIEW = OUTPUT / "b2-prop-quality-v3-assets.png"
FEATURE_PREVIEW = OUTPUT / "b2-prop-quality-v3-feature-comparison.png"
ALPHA_CUTOFF = 32


@dataclass(frozen=True)
class SourceCrop:
    key: str
    bbox: tuple[int, int, int, int]


# Measured from the source's alpha-connected components at cutoff 32.  Keeping
# these explicit makes a regenerated/replaced source fail loudly instead of
# silently feeding a neighbouring object into a runtime slot.
SOURCE_CROPS = (
    SourceCrop("wall-1-base", (87, 54, 312, 568)),
    SourceCrop("wall-2-hose", (399, 49, 628, 554)),
    SourceCrop("wall-3-vent", (713, 51, 940, 557)),
    SourceCrop("wall-4-quiet", (1021, 54, 1248, 565)),
    SourceCrop("wall-5-terminal", (1312, 51, 1591, 567)),
    SourceCrop("fuel-cell", (286, 594, 421, 832)),
)
SOURCE_CROP_BY_KEY = {crop.key: crop for crop in SOURCE_CROPS}


@dataclass(frozen=True)
class B2PropQualityV3Build:
    outputs: dict[str, Image.Image]
    service_masters: dict[str, Image.Image]
    logical_sources: dict[str, Image.Image]


def _threshold_alpha(image: Image.Image) -> Image.Image:
    hardened = image.convert("RGBA")
    alpha = hardened.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    hardened.putalpha(alpha)
    return hardened


def _finish_logical(image: Image.Image) -> Image.Image:
    locked = lock_rgba_to_palette(image.convert("RGBA"))
    locked = _threshold_alpha(locked)
    return locked.resize(
        (image.width * PIXEL_CLUSTER, image.height * PIXEL_CLUSTER),
        Image.Resampling.NEAREST,
    )


def _crop(sheet: Image.Image, key: str) -> Image.Image:
    if key not in SOURCE_CROP_BY_KEY:
        raise ValueError(f"unknown B2 production-sheet crop: {key}")
    spec = SOURCE_CROP_BY_KEY[key]
    cropped = sheet.crop(spec.bbox).convert("RGBA")
    thresholded = cropped.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    expected = (0, 0, cropped.width, cropped.height)
    if thresholded.getbbox() != expected:
        raise ValueError(
            f"B2 production-sheet crop {key} no longer matches {spec.bbox}: "
            f"{thresholded.getbbox()}"
        )
    return cropped


def _conform_wall(source: Image.Image, *, mirrored: bool) -> Image.Image:
    # The source walls are deliberately taller than their runtime cells.  A
    # controlled non-uniform reduction fills the exact wall canvas and preserves
    # terminal/reel size at the 32x56 authored pixel regime.
    logical = source.resize(WALL_LOGICAL_SIZE, Image.Resampling.BOX)
    logical = lock_rgba_to_palette(logical)
    logical = _threshold_alpha(logical)
    if mirrored:
        logical = logical.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return logical


def _conform_fuel_cell(source: Image.Image) -> Image.Image:
    # Keep the approved compact cylinder at 56x84 final pixels, centered with its
    # bottom at y118.  The source has no ground shadow; runtime remains the AO SSOT.
    fitted = source.resize((28, 42), Image.Resampling.BOX)
    fitted = _threshold_alpha(lock_rgba_to_palette(fitted))
    logical = Image.new("RGBA", PROP_LOGICAL_SIZE, TRANSPARENT)
    logical.alpha_composite(fitted, (18, 17))
    return logical


def _assemble_service_master(
    outputs: dict[str, Image.Image],
    direction: WallDirection,
) -> Image.Image:
    master = Image.new("RGBA", SERVICE_MASTER_SIZE, TRANSPARENT)
    for segment, (left, upper) in enumerate(direction.service_windows):
        master.alpha_composite(
            outputs[f"env-wall-b2-service-segment-{segment}-{direction.name}"],
            (left * PIXEL_CLUSTER, upper * PIXEL_CLUSTER),
        )
    return master


def reassemble_service_outputs(
    outputs: dict[str, Image.Image],
    direction: WallDirection,
) -> Image.Image:
    return _assemble_service_master(outputs, direction)


def build_assets(sheet: Image.Image) -> B2PropQualityV3Build:
    if sheet.size != (1672, 941):
        raise ValueError(f"unexpected B2 production-sheet size: {sheet.size}")

    wall_sources = {
        key: _crop(sheet, key)
        for key in (
            "wall-1-base",
            "wall-2-hose",
            "wall-3-vent",
            "wall-4-quiet",
            "wall-5-terminal",
        )
    }
    fuel_source = _crop(sheet, "fuel-cell")

    outputs: dict[str, Image.Image] = {}
    service_masters: dict[str, Image.Image] = {}
    logical_sources: dict[str, Image.Image] = {}
    for direction in DIRECTIONS:
        mirrored = direction.slope > 0
        walls = {
            key: _conform_wall(source, mirrored=mirrored)
            for key, source in wall_sources.items()
        }
        logical_sources.update(
            {f"{key}-{direction.name}": image.copy() for key, image in walls.items()}
        )

        # Legacy slot names stay stable while their visual semantics follow the
        # approved arcade-tower sheet ("pipes" now owns the vent/service face).
        outputs[f"env-wall-{direction.name}"] = _finish_logical(walls["wall-1-base"])
        outputs[f"env-wall-torch-{direction.name}"] = _finish_logical(
            walls["wall-4-quiet"]
        )
        outputs[f"env-wall-pipes-{direction.name}"] = _finish_logical(
            walls["wall-3-vent"]
        )
        outputs[f"env-wall-cabinet-{direction.name}"] = _finish_logical(
            walls["wall-5-terminal"]
        )
        outputs[f"env-wall-b2-service-segment-0-{direction.name}"] = _finish_logical(
            walls["wall-2-hose"]
        )
        outputs[f"env-wall-b2-service-segment-1-{direction.name}"] = _finish_logical(
            walls["wall-4-quiet"]
        )
        outputs[f"env-wall-b2-service-segment-2-{direction.name}"] = _finish_logical(
            walls["wall-3-vent"]
        )
        service_masters[direction.name] = _assemble_service_master(outputs, direction)

    fuel_logical = _conform_fuel_cell(fuel_source)
    logical_sources["fuel-cell"] = fuel_logical.copy()
    outputs["prop-explosive-barrel"] = _finish_logical(fuel_logical)
    return B2PropQualityV3Build(outputs, service_masters, logical_sources)


def _paste_scaled(
    canvas: Image.Image,
    sprite: Image.Image,
    position: tuple[int, int],
    scale: int,
) -> None:
    canvas.alpha_composite(
        sprite.resize(
            (sprite.width * scale, sprite.height * scale),
            Image.Resampling.NEAREST,
        ),
        position,
    )


def build_asset_preview(build: B2PropQualityV3Build) -> Image.Image:
    canvas = Image.new("RGBA", (1536, 768), PALETTE["dark-void"])
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((16, 16, 592, 752), fill=PALETTE["ui-panel-solid"])
    draw.rectangle((608, 16, 1024, 752), fill=PALETTE["ui-panel-solid"])
    draw.rectangle((1040, 16, 1520, 752), fill=PALETTE["ui-panel-solid"])

    variants = ("", "torch-", "pipes-", "cabinet-")
    for row, direction in enumerate(DIRECTIONS):
        for column, variant in enumerate(variants):
            name = f"env-wall-{variant}{direction.name}"
            x = 32 + column * 140
            y = 32 + row * 280
            draw.rectangle((x - 4, y - 4, x + 132, y + 228), fill=PALETTE["ui-inset"])
            _paste_scaled(canvas, build.outputs[name], (x, y), 2)
            draw.rectangle(
                (x, y + 232, x + 24, y + 235),
                fill=PALETTE["sig-hazard"] if column else PALETTE["grey-5"],
            )

    for row, direction in enumerate(DIRECTIONS):
        x = 624
        y = 24 + row * 368
        draw.rectangle((x - 4, y - 4, x + 388, y + 356), fill=PALETTE["ui-inset"])
        _paste_scaled(canvas, build.service_masters[direction.name], (x, y), 2)

    draw.rectangle((1084, 156, 1476, 620), fill=PALETTE["ui-inset"])
    _paste_scaled(canvas, build.outputs["prop-explosive-barrel"], (1088, 192), 3)
    draw.rectangle((1088, 596, 1144, 603), fill=PALETTE["sig-hazard"])
    return canvas


def _fit_reference(
    image: Image.Image,
    maximum: tuple[int, int],
) -> Image.Image:
    scale = min(maximum[0] / image.width, maximum[1] / image.height)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    return image.resize(size, Image.Resampling.NEAREST)


def build_feature_preview(
    build: B2PropQualityV3Build,
    sheet: Image.Image,
) -> Image.Image:
    """Large source-vs-conformed proof for hose, terminal, and cylinder."""
    canvas = Image.new("RGBA", (1800, 1100), PALETTE["dark-void"])
    draw = ImageDraw.Draw(canvas)
    features = (
        ("wall-2-hose", "env-wall-b2-service-segment-0-rising-right", 4),
        ("wall-5-terminal", "env-wall-cabinet-rising-right", 4),
        ("fuel-cell", "prop-explosive-barrel", 4),
    )
    for column, (source_key, output_name, scale) in enumerate(features):
        panel_x = column * 600
        draw.rectangle(
            (panel_x + 12, 12, panel_x + 588, 1088),
            fill=PALETTE["ui-panel-solid"],
        )
        source = _fit_reference(_crop(sheet, source_key), (500, 500))
        source_x = panel_x + 300 - source.width // 2
        canvas.alpha_composite(source, (source_x, 20))
        draw.rectangle(
            (panel_x + 32, 526, panel_x + 568, 534),
            fill=PALETTE["sig-hazard"],
        )
        output = build.outputs[output_name]
        output_x = panel_x + 300 - output.width * scale // 2
        output_y = 556 if output_name != "prop-explosive-barrel" else 574
        _paste_scaled(canvas, output, (output_x, output_y), scale)
        draw.rectangle(
            (panel_x + 32, 1060, panel_x + 180, 1067),
            fill=PALETTE["sig-teal-item"],
        )
    return canvas


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    sheet = Image.open(SOURCE).convert("RGBA")
    build = build_assets(sheet)

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build.outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    for direction, image in build.service_masters.items():
        image.save(OUTPUT / f"env-wall-b2-service-master-{direction}.png", optimize=True)
    build_asset_preview(build).save(ASSET_PREVIEW, optimize=True)
    build_feature_preview(build, sheet).save(FEATURE_PREVIEW, optimize=True)
    print(
        f"wrote {len(build.outputs)} production-sheet conform candidates, "
        f"{len(build.service_masters)} masters, and previews to {OUTPUT}"
    )


if __name__ == "__main__":
    main()
