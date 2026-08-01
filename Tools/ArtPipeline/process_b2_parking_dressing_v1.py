#!/usr/bin/env python3
"""Conform the approved B2 dressing sources into complete 128x64 floor tiles.

The ImageGen files are references, not runtime assets. This processor removes
their chroma backgrounds, downsizes them with hard alpha, locks them to the
Torchstone palette, and composites each low prop onto the canonical floor.
"""

from dataclasses import dataclass
import math
from pathlib import Path

from PIL import Image, ImageEnhance

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "docs/art-direction"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
CANVAS_SIZE = (128, 64)
ALPHA_CUTOFF = 80
ISO_AXIS_SLOPE = 0.5


@dataclass(frozen=True)
class DressingSpec:
    source_name: str
    output_name: str
    maximum_size: tuple[int, int]
    ground_y: int
    brightness: float


SPECS = (
    DressingSpec(
        "project-c-b2-parking-wheel-stop-source-v1.png",
        "env-floor-b2-parking-stop",
        (104, 42),
        52,
        0.82,
    ),
    DressingSpec(
        "project-c-b2-fallen-wayfinding-source-v1.png",
        "env-floor-b2-fallen-sign",
        (88, 44),
        52,
        0.74,
    ),
)


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def extract_object(source: Image.Image) -> Image.Image:
    """Remove the generated magenta field and crop to visible object bounds."""
    object_image = source.convert("RGBA")
    pixels = object_image.load()
    for y in range(object_image.height):
        for x in range(object_image.width):
            if _is_chroma(pixels[x, y]):
                pixels[x, y] = (5, 7, 12, 0)

    alpha = object_image.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    object_image.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("B2 dressing source contains no visible object")
    return object_image.crop(bounds)


def _alpha_covariance(source: Image.Image) -> tuple[float, float, float]:
    """Return xx/yy/xy covariance for the visible object silhouette."""
    alpha = source.getchannel("A")
    alpha_data = (
        alpha.get_flattened_data()
        if hasattr(alpha, "get_flattened_data")
        else alpha.getdata()
    )
    count = 0
    sum_x = 0.0
    sum_y = 0.0
    sum_xx = 0.0
    sum_yy = 0.0
    sum_xy = 0.0
    for index, alpha_value in enumerate(alpha_data):
        if alpha_value < ALPHA_CUTOFF:
            continue
        px = index % source.width
        py = index // source.width
        count += 1
        sum_x += px
        sum_y += py
        sum_xx += px * px
        sum_yy += py * py
        sum_xy += px * py
    if count < 2:
        raise ValueError("B2 dressing source needs at least two visible pixels")

    covariance_xx = sum_xx - sum_x * sum_x / count
    covariance_yy = sum_yy - sum_y * sum_y / count
    covariance_xy = sum_xy - sum_x * sum_y / count
    return covariance_xx, covariance_yy, covariance_xy


def _principal_axis_slope_from_covariance(
    covariance_xx: float,
    covariance_yy: float,
    covariance_xy: float,
) -> float:
    angle = 0.5 * math.atan2(
        2.0 * covariance_xy,
        covariance_xx - covariance_yy,
    )
    return math.tan(angle)


def principal_axis_slope(source: Image.Image) -> float:
    """Measure the screen-space long axis of a low floor prop."""
    return _principal_axis_slope_from_covariance(*_alpha_covariance(source))


def reproject_to_isometric_axis(source: Image.Image) -> Image.Image:
    """Shear one upright prop until its floor axis follows the 2:1 diamond.

    A rotation would also tilt every vertical post and sign face. A vertical shear keeps
    screen-space verticals vertical while correcting the generated source's shallow
    ground-plane angle. The shear is solved from the alpha covariance so both approved
    sources converge on the same +1:2 screen axis without per-image magic numbers.
    """
    covariance_xx, covariance_yy, covariance_xy = _alpha_covariance(source)

    def projected_slope(shear: float) -> float:
        projected_xy = covariance_xy + shear * covariance_xx
        projected_yy = (
            covariance_yy
            + 2.0 * shear * covariance_xy
            + shear * shear * covariance_xx
        )
        return _principal_axis_slope_from_covariance(
            covariance_xx,
            projected_yy,
            projected_xy,
        )

    low = -1.0
    high = 1.0
    if not projected_slope(low) <= ISO_AXIS_SLOPE <= projected_slope(high):
        raise ValueError("B2 dressing source has no solvable horizontal principal axis")
    for _ in range(48):
        middle = (low + high) * 0.5
        if projected_slope(middle) < ISO_AXIS_SLOPE:
            low = middle
        else:
            high = middle
    shear = (low + high) * 0.5

    width, height = source.size
    center_x = (width - 1) * 0.5
    left_shift = shear * -center_x
    right_shift = shear * ((width - 1) - center_x)
    minimum_shift = min(left_shift, right_shift)
    maximum_shift = max(left_shift, right_shift)
    padding = 2
    output_height = math.ceil(height + maximum_shift - minimum_shift) + padding * 2
    output_offset = padding - minimum_shift

    # PIL expects the inverse map: output pixel -> source pixel.
    inverse = (
        1.0,
        0.0,
        0.0,
        -shear,
        1.0,
        shear * center_x - output_offset,
    )
    return source.transform(
        (width, output_height),
        Image.Transform.AFFINE,
        inverse,
        resample=Image.Resampling.BICUBIC,
        fillcolor=(5, 7, 12, 0),
    )


def fit_object(source: Image.Image, spec: DressingSpec) -> Image.Image:
    source = reproject_to_isometric_axis(source)
    scale = min(
        spec.maximum_size[0] / source.width,
        spec.maximum_size[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    fitted = source.resize(size, Image.Resampling.BOX)
    fitted = ImageEnhance.Brightness(fitted).enhance(spec.brightness)
    alpha = fitted.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    fitted.putalpha(alpha)
    return despeckle(lock_rgba_to_palette(fitted))


def compose_floor_variant(
    prop: Image.Image,
    base_floor: Image.Image,
    spec: DressingSpec,
) -> Image.Image:
    if base_floor.size != CANVAS_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    left = (CANVAS_SIZE[0] - prop.width) // 2
    top = spec.ground_y - prop.height
    if left < 0 or top < 0 or left + prop.width > 128 or spec.ground_y > 64:
        raise ValueError(f"{spec.output_name} does not fit its floor canvas")

    composed = lock_rgba_to_palette(base_floor.convert("RGBA"))
    composed.alpha_composite(prop, (left, top))
    alpha = composed.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    composed.putalpha(alpha)
    return despeckle(lock_rgba_to_palette(composed))


def build_output(source: Image.Image, base_floor: Image.Image, spec: DressingSpec) -> Image.Image:
    """Build the legacy, view-0-compatible complete floor tile."""
    prop = fit_object(extract_object(source), spec)
    return compose_floor_variant(prop, base_floor, spec)


def build_outputs(
    sources: dict[str, Image.Image],
    base_floor: Image.Image,
) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for spec in SPECS:
        if spec.source_name not in sources:
            raise ValueError(f"missing B2 dressing source: {spec.source_name}")
        prop = fit_object(extract_object(sources[spec.source_name]), spec)
        mirrored = prop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        # A single approved source cannot reveal its unseen 180-degree back face. Keep
        # upright verticals intact: opposite facings share art for now, while the two
        # floor-axis parities use an exact horizontal mirror. Four independent slots let
        # hand-authored Aseprite views replace these interim pairs without code changes.
        view_props = (prop, mirrored, prop, mirrored)
        for view, view_prop in enumerate(view_props):
            outputs[f"{spec.output_name}-view-{view}"] = compose_floor_variant(
                view_prop,
                base_floor,
                spec,
            )
        outputs[spec.output_name] = outputs[f"{spec.output_name}-view-0"].copy()
    return outputs


def main() -> None:
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    source_paths = {spec.source_name: SOURCE_ROOT / spec.source_name for spec in SPECS}
    for path in source_paths.values():
        if not path.exists():
            raise FileNotFoundError(path)

    outputs = build_outputs(
        {name: Image.open(path).convert("RGBA") for name, path in source_paths.items()},
        Image.open(BASE_FLOOR).convert("RGBA"),
    )
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    print(f"wrote {len(outputs)} B2 floor dressing sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
