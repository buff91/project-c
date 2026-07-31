#!/usr/bin/env python3
"""Conform the approved B2 dressing sources into complete 128x64 floor tiles.

The ImageGen files are references, not runtime assets. This processor removes
their chroma backgrounds, downsizes them with hard alpha, locks them to the
Torchstone palette, and composites each low prop onto the canonical floor.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageEnhance

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "docs/art-direction"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
CANVAS_SIZE = (128, 64)
ALPHA_CUTOFF = 80


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


def fit_object(source: Image.Image, spec: DressingSpec) -> Image.Image:
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


def build_output(source: Image.Image, base_floor: Image.Image, spec: DressingSpec) -> Image.Image:
    if base_floor.size != CANVAS_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    prop = fit_object(extract_object(source), spec)
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


def build_outputs(
    sources: dict[str, Image.Image],
    base_floor: Image.Image,
) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for spec in SPECS:
        if spec.source_name not in sources:
            raise ValueError(f"missing B2 dressing source: {spec.source_name}")
        outputs[spec.output_name] = build_output(sources[spec.source_name], base_floor, spec)
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
