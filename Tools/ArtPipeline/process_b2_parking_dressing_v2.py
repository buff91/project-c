#!/usr/bin/env python3
"""Draw the B2 low/passable dressings at their native runtime resolution.

The approved concept boards remain silhouette references only.  These two props
are deliberately authored on a 64x32 logical canvas and enlarged by an exact
2x nearest-neighbour pass.  That keeps every authored mark on the 2x2 cluster
grid while preserving the existing 128x64 complete-floor sprite contract.

All four views are drawn from one world-fixed construction.  Screen-axis parity
changes with the quarter view, while the damaged end and wear marks rotate with
the object instead of being invented independently per camera angle.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw

from torchstone_palette import load_gpl_entries, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
PREVIEW = ROOT / "docs/captures/b2-right-dressing-conform-preview-v2.png"
CANVAS_SIZE = (128, 64)
VIEW_COUNT = 4
NATIVE_SCALE = 2
TRANSPARENT = (5, 7, 12, 0)
NEUTRAL_SOURCE_RAMP = (
    (0.16, (21, 23, 29, 255)),
    (0.28, (44, 49, 56, 255)),
    (0.50, (107, 113, 120, 255)),
    (1.01, (148, 155, 161, 255)),
)


@dataclass(frozen=True)
class NativeDressingSpec:
    output_name: str
    logical_size: tuple[int, int]
    ground_y: int

    @property
    def native_size(self) -> tuple[int, int]:
        return tuple(dimension * NATIVE_SCALE for dimension in self.logical_size)


SPECS = (
    NativeDressingSpec(
        "env-floor-b2-parking-stop",
        (40, 10),
        51,
    ),
    NativeDressingSpec(
        "env-floor-b2-fallen-sign",
        (38, 9),
        51,
    ),
)

_PALETTE = dict(load_gpl_entries())


def _color(name: str, alpha: int = 255) -> tuple[int, int, int, int]:
    red, green, blue = _PALETTE[name]
    return red, green, blue, alpha


def _parking_stop_logical(view: int) -> Image.Image:
    """Draw a shallow rubber/steel wheel stop in one camera quarter."""
    image = Image.new("RGBA", (40, 10), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    reverse = view >= 2

    # Contact AO, three broad material planes, and two steel end caps.  Drawing
    # at logical half-resolution makes even the one-pixel bolts valid 2x2 marks.
    draw.polygon(
        ((1, 3), (35, 9), (39, 9), (5, 3)),
        fill=_color("dark-void"),
    )
    draw.polygon(
        ((0, 2), (4, 0), (39, 6), (35, 9), (1, 3)),
        fill=_color("dark-cool"),
    )
    draw.polygon(
        ((1, 2), (4, 1), (38, 6), (35, 8)),
        fill=_color("grey-2"),
    )
    draw.polygon(
        ((1, 3), (35, 9), (35, 8), (1, 2)),
        fill=_color("grey-1"),
    )
    draw.line(((5, 1), (35, 6)), fill=_color("grey-3"), width=1)

    draw.polygon(((0, 2), (4, 0), (5, 1), (1, 3)), fill=_color("grey-3"))
    draw.polygon(((35, 8), (38, 6), (39, 6), (39, 8)), fill=_color("grey-4"))
    draw.point((2, 2), fill=_color("grey-5"))
    draw.point((38, 7), fill=_color("grey-5"))

    # Restrained amber retention straps.  The bright signal is only a tiny
    # chip; most of the band belongs to the rust material ramp.
    bands = (7, 31) if not reverse else (8, 32)
    for x in bands:
        y = 1 + round(x * 5 / 39)
        draw.line(((x, y), (x, min(9, y + 2))), fill=_color("rust-4"), width=1)
        draw.point((x + 1, min(8, y + 1)), fill=_color("rust-2"))
    signal_x = bands[0] if not reverse else bands[1]
    signal_y = 1 + round(signal_x * 5 / 39)
    draw.point((signal_x, signal_y), fill=_color("sig-hazard"))

    wear_x = 25 if not reverse else 13
    wear_y = 2 + round(wear_x * 5 / 39)
    draw.line(
        ((wear_x, wear_y), (wear_x + 2, min(8, wear_y + 1))),
        fill=_color("rust-1"),
        width=1,
    )

    if view % 2 == 1:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return image


def _fallen_sign_logical(view: int) -> Image.Image:
    """Draw a cracked, face-up wayfinding slab with a clipped corner."""
    image = Image.new("RGBA", (38, 9), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    reverse = view >= 2

    draw.polygon(
        ((0, 4), (29, 8), (37, 7), (8, 2)),
        fill=_color("dark-void"),
    )
    draw.polygon(
        ((0, 3), (8, 0), (37, 4), (29, 8)),
        fill=_color("grey-1"),
    )
    draw.polygon(
        ((3, 3), (8, 1), (34, 4), (29, 7)),
        fill=_color("dark-cool"),
    )
    draw.line(((8, 1), (34, 4)), fill=_color("grey-3"), width=1)
    draw.line(((3, 3), (29, 7)), fill=_color("grey-2"), width=1)
    draw.line(((1, 3), (7, 1)), fill=_color("grey-4"), width=1)
    draw.line(((30, 7), (37, 4)), fill=_color("grey-4"), width=1)

    # An abstract amber route chevron: readable as industrial wayfinding, not
    # as generated lettering.  Rust carries the area; signal amber is a glint.
    arrow_x = 20 if not reverse else 12
    arrow_y = 2 + round(arrow_x * 4 / 37)
    draw.line(
        (
            (arrow_x - 2, max(1, arrow_y - 1)),
            (arrow_x, arrow_y),
            (arrow_x - 2, min(7, arrow_y + 1)),
        ),
        fill=_color("rust-4"),
        width=1,
    )
    draw.line(
        (
            (arrow_x + 1, max(1, arrow_y - 1)),
            (arrow_x + 3, arrow_y),
            (arrow_x + 1, min(7, arrow_y + 1)),
        ),
        fill=_color("rust-2"),
        width=1,
    )
    draw.point((arrow_x, arrow_y), fill=_color("sig-hazard"))

    crack_x = 27 if not reverse else 8
    crack_y = 2 + round(crack_x * 4 / 37)
    draw.line(
        ((crack_x, crack_y), (crack_x + 1, min(7, crack_y + 2))),
        fill=_color("rust-1"),
        width=1,
    )
    draw.point((4 if not reverse else 34, 3 if not reverse else 6), fill=_color("grey-5"))

    if view % 2 == 1:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return image


def _logical_prop(spec: NativeDressingSpec, view: int) -> Image.Image:
    if view < 0 or view >= VIEW_COUNT:
        raise ValueError(f"invalid B2 dressing view: {view}")
    if spec.output_name.endswith("parking-stop"):
        return _parking_stop_logical(view)
    if spec.output_name.endswith("fallen-sign"):
        return _fallen_sign_logical(view)
    raise ValueError(f"unknown B2 dressing spec: {spec.output_name}")


def build_prop_overlay(spec: NativeDressingSpec, view: int) -> Image.Image:
    """Build one full-canvas native-pixel overlay for contract inspection."""
    logical = _logical_prop(spec, view)
    if logical.size != spec.logical_size:
        raise ValueError(
            f"{spec.output_name} logical size {logical.size} != {spec.logical_size}"
        )
    prop = logical.resize(spec.native_size, Image.Resampling.NEAREST)
    prop = lock_rgba_to_palette(prop)
    prop.putalpha(prop.getchannel("A").point(lambda value: 255 if value else 0))

    left = (CANVAS_SIZE[0] - prop.width) // 2
    top = spec.ground_y - prop.height + 1
    if left < 0 or top < 0 or left + prop.width > 128 or spec.ground_y >= 64:
        raise ValueError(f"{spec.output_name} does not fit its floor canvas")

    overlay = Image.new("RGBA", CANVAS_SIZE, TRANSPARENT)
    overlay.alpha_composite(prop, (left, top))
    return overlay


def neutralize_floor_source(base_floor: Image.Image) -> Image.Image:
    """Keep the base diamond value structure without decorative warm chroma."""
    if base_floor.size != CANVAS_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")
    neutral = base_floor.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in neutral.get_flattened_data():
        if alpha == 0:
            pixels.append(TRANSPARENT)
            continue
        luminance = (
            red * 0.2126 +
            green * 0.7152 +
            blue * 0.0722
        ) / 255.0
        mapped = NEUTRAL_SOURCE_RAMP[-1][1]
        for threshold, color in NEUTRAL_SOURCE_RAMP:
            if luminance < threshold:
                mapped = color
                break
        pixels.append((mapped[0], mapped[1], mapped[2], alpha))
    neutral.putdata(pixels)
    return neutral


def _compose_floor(overlay: Image.Image, neutral_floor: Image.Image) -> Image.Image:
    composed = lock_rgba_to_palette(neutral_floor.convert("RGBA"))
    composed.alpha_composite(overlay)
    composed = lock_rgba_to_palette(composed)
    composed.putalpha(
        composed.getchannel("A").point(lambda value: 255 if value else 0)
    )
    return composed


def build_outputs(base_floor: Image.Image) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    neutral_floor = neutralize_floor_source(base_floor)
    for spec in SPECS:
        for view in range(VIEW_COUNT):
            overlay = build_prop_overlay(spec, view)
            outputs[f"{spec.output_name}-view-{view}"] = _compose_floor(
                overlay,
                neutral_floor,
            )
        outputs[spec.output_name] = outputs[
            f"{spec.output_name}-view-0"
        ].copy()
    return outputs


def build_preview(outputs: dict[str, Image.Image]) -> Image.Image:
    preview = Image.new(
        "RGBA",
        (CANVAS_SIZE[0] * VIEW_COUNT, CANVAS_SIZE[1] * len(SPECS)),
        (5, 7, 12, 255),
    )
    for row, spec in enumerate(SPECS):
        for view in range(VIEW_COUNT):
            preview.alpha_composite(
                outputs[f"{spec.output_name}-view-{view}"],
                (view * CANVAS_SIZE[0], row * CANVAS_SIZE[1]),
            )
    return preview.resize(
        (preview.width * 3, preview.height * 3),
        Image.Resampling.NEAREST,
    )


def main() -> None:
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    outputs = build_outputs(Image.open(BASE_FLOOR).convert("RGBA"))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    build_preview(outputs).save(PREVIEW, optimize=True)
    print(f"wrote {len(outputs)} B2 native-pixel floor dressing sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
