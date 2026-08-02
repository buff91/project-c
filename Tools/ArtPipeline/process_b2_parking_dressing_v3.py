#!/usr/bin/env python3
"""Build isolated B2 floor-prop volume candidates without touching runtime art.

This v3 study responds to the q0 live capture, where the right-side wheel stop
and fallen sign collapse into floor scratches.  The approved target reads them
through three things that v2 under-emphasises: a broad top plane, a darker front
lip, and a sparse but legible amber retention/wayfinding mark.  V3 draws those
planes directly at the half-resolution logical grid, then enlarges by exact 2x
nearest-neighbour sampling.

The generated sprites are deliberately quarantined in
``/private/tmp/project-c-b2-floor-v3``.  They are review candidates, not runtime
or Aseprite sources.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

from process_b2_parking_dressing_v2 import CANVAS_SIZE, neutralize_floor_source
from torchstone_palette import load_gpl_entries, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
RUNTIME_ART = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = RUNTIME_ART / "env-floor.png"
TARGET_Q0 = ROOT / "docs/art-direction/project-c-b2-prop-quality-target-q0-v1.png"
LIVE_Q0 = ROOT / "docs/captures/shared-floor-material-q0-live-v1.png"
OUTPUT_ROOT = Path("/private/tmp/project-c-b2-floor-v3")
COMPARISON_PREVIEW = OUTPUT_ROOT / "b2-floor-v3-comparison.png"
VIEW_COUNT = 4
NATIVE_SCALE = 2
TRANSPARENT = (5, 7, 12, 0)


@dataclass(frozen=True)
class VolumeCandidateSpec:
    output_name: str
    logical_size: tuple[int, int]
    ground_y: int

    @property
    def native_size(self) -> tuple[int, int]:
        return tuple(value * NATIVE_SCALE for value in self.logical_size)


SPECS = (
    VolumeCandidateSpec("env-floor-b2-parking-stop", (44, 14), 51),
    VolumeCandidateSpec("env-floor-b2-fallen-sign", (42, 13), 51),
)

_PALETTE = dict(load_gpl_entries())


def _color(name: str, alpha: int = 255) -> tuple[int, int, int, int]:
    red, green, blue = _PALETTE[name]
    return red, green, blue, alpha


def _parking_stop_logical(view: int) -> Image.Image:
    """A thick rubber/steel stop with anchor plates and retained amber bands."""
    image = Image.new("RGBA", (44, 14), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    reverse = view >= 2

    # Compact contact shadow and two low mounting plates are the supporting
    # floor props.  They remain visibly subordinate to the main blocking mass.
    draw.polygon(((0, 8), (36, 13), (43, 12), (7, 6)), fill=_color("dark-void"))
    draw.polygon(((0, 7), (5, 5), (10, 6), (4, 9)), fill=_color("grey-1"))
    draw.polygon(((34, 10), (40, 8), (43, 9), (39, 12)), fill=_color("grey-1"))
    draw.line(((1, 7), (5, 6)), fill=_color("grey-3"), width=1)
    draw.line(((38, 10), (42, 9)), fill=_color("grey-3"), width=1)
    draw.point((3, 7), fill=_color("grey-5"))
    draw.point((40, 10), fill=_color("grey-5"))

    # Main prism: broad lit top, dark front plane, and a cool end cap.  The y=0
    # top ridge and y=12 front foot establish the approved 28px total volume.
    draw.polygon(
        ((3, 5), (10, 0), (41, 7), (34, 12), (4, 7)),
        fill=_color("dark-cool"),
    )
    draw.polygon(((4, 4), (10, 0), (40, 7), (34, 10)), fill=_color("grey-3"))
    draw.polygon(((7, 4), (11, 2), (37, 7), (33, 9)), fill=_color("grey-2"))
    draw.polygon(((4, 4), (34, 10), (34, 12), (4, 6)), fill=_color("grey-1"))
    draw.polygon(((34, 10), (40, 7), (40, 9), (34, 12)), fill=_color("grey-2"))
    draw.line(((10, 0), (40, 7)), fill=_color("grey-4"), width=1)
    draw.line(((4, 6), (34, 12)), fill=_color("dark-cool"), width=1)

    band_positions = (12, 31) if not reverse else (13, 32)
    for x in band_positions:
        top = max(1, round((x - 9) * 7 / 31))
        draw.line(((x, top), (x, min(11, top + 4))), fill=_color("rust-4"), width=1)
        draw.line(
            ((x + 1, min(10, top + 1)), (x + 1, min(11, top + 3))),
            fill=_color("rust-3"),
            width=1,
        )
        draw.point((x, top), fill=_color("sig-hazard"))
        draw.point((x + 1, min(10, top + 1)), fill=_color("sig-hazard"))

    wear_x = 25 if not reverse else 16
    wear_y = max(2, round((wear_x - 7) * 7 / 31))
    draw.line(
        ((wear_x, wear_y), (wear_x + 2, min(10, wear_y + 1))),
        fill=_color("rust-1"),
        width=1,
    )

    if view % 2 == 1:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return image


def _fallen_sign_logical(view: int) -> Image.Image:
    """A face-up bevelled sign slab with a broken bracket and route chevrons."""
    image = Image.new("RGBA", (42, 13), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    reverse = view >= 2

    draw.polygon(((0, 7), (32, 12), (41, 10), (9, 4)), fill=_color("dark-void"))

    # Broken fastening tabs make the slab read as fallen hardware instead of a
    # decal.  Their bolts are the only detached auxiliary marks in the cell.
    draw.polygon(((0, 6), (4, 4), (8, 5), (4, 8)), fill=_color("grey-1"))
    draw.polygon(((34, 9), (39, 7), (41, 8), (38, 11)), fill=_color("grey-1"))
    draw.point((3, 6), fill=_color("grey-5"))
    draw.point((38, 9), fill=_color("grey-5"))

    draw.polygon(((1, 5), (9, 0), (41, 5), (32, 11)), fill=_color("grey-1"))
    draw.polygon(((4, 5), (10, 2), (37, 5), (31, 9)), fill=_color("dark-cool"))
    draw.line(((9, 0), (41, 5)), fill=_color("grey-4"), width=1)
    draw.line(((1, 5), (32, 11)), fill=_color("grey-3"), width=1)
    draw.line(((32, 11), (41, 5)), fill=_color("grey-2"), width=1)
    draw.line(((5, 5), (31, 9)), fill=_color("grey-2"), width=1)

    # The broad rust-amber chevrons survive room-scale lighting.  Only their two
    # tips use signal orange, keeping signal area below the five-percent budget.
    center_x = 23 if not reverse else 17
    center_y = 3 + round(center_x * 4 / 41)
    for offset in (-5, 1):
        x = center_x + offset
        draw.line(
            (
                (x - 2, max(2, center_y - 1)),
                (x, center_y),
                (x - 2, min(9, center_y + 1)),
            ),
            fill=_color("rust-4"),
            width=1,
        )
        draw.point((x, center_y), fill=_color("sig-hazard"))
        draw.point((x - 1, center_y), fill=_color("sig-hazard"))

    crack_x = 30 if not reverse else 8
    crack_y = 2 + round(crack_x * 4 / 41)
    draw.line(
        ((crack_x, crack_y), (crack_x - 1, min(9, crack_y + 2))),
        fill=_color("rust-1"),
        width=1,
    )
    draw.point((6 if not reverse else 35, 4 if not reverse else 7), fill=_color("rust-2"))

    if view % 2 == 1:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return image


def _logical_prop(spec: VolumeCandidateSpec, view: int) -> Image.Image:
    if view < 0 or view >= VIEW_COUNT:
        raise ValueError(f"invalid B2 dressing view: {view}")
    if spec.output_name.endswith("parking-stop"):
        image = _parking_stop_logical(view)
    elif spec.output_name.endswith("fallen-sign"):
        image = _fallen_sign_logical(view)
    else:
        raise ValueError(f"unknown B2 v3 dressing spec: {spec.output_name}")
    if image.size != spec.logical_size:
        raise ValueError(f"{spec.output_name} logical size {image.size} != {spec.logical_size}")
    return image


def build_prop_overlay(spec: VolumeCandidateSpec, view: int) -> Image.Image:
    logical = _logical_prop(spec, view)
    native = logical.resize(spec.native_size, Image.Resampling.NEAREST)
    native = lock_rgba_to_palette(native)
    native.putalpha(native.getchannel("A").point(lambda value: 255 if value else 0))

    left = (CANVAS_SIZE[0] - native.width) // 2
    top = spec.ground_y - native.height + 1
    overlay = Image.new("RGBA", CANVAS_SIZE, TRANSPARENT)
    overlay.alpha_composite(native, (left, top))
    return overlay


def _compose_floor(overlay: Image.Image, neutral_floor: Image.Image) -> Image.Image:
    composed = lock_rgba_to_palette(neutral_floor.convert("RGBA"))
    composed.alpha_composite(overlay)
    composed = lock_rgba_to_palette(composed)
    composed.putalpha(composed.getchannel("A").point(lambda value: 255 if value else 0))
    return composed


def build_outputs(base_floor: Image.Image) -> dict[str, Image.Image]:
    neutral_floor = neutralize_floor_source(base_floor)
    outputs: dict[str, Image.Image] = {}
    for spec in SPECS:
        for view in range(VIEW_COUNT):
            name = f"{spec.output_name}-view-{view}"
            outputs[name] = _compose_floor(
                build_prop_overlay(spec, view),
                neutral_floor,
            )
    return outputs


def _crop_panel(path: Path, bounds: tuple[int, int, int, int], size: tuple[int, int]) -> Image.Image:
    source = Image.open(path).convert("RGBA")
    return ImageOps.fit(source.crop(bounds), size, method=Image.Resampling.LANCZOS)


def _font(size: int) -> ImageFont.ImageFont:
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def _asset_strip(images: dict[str, Image.Image], scale: int = 3) -> Image.Image:
    strip = Image.new("RGBA", (CANVAS_SIZE[0] * VIEW_COUNT, CANVAS_SIZE[1]), _color("dark-void"))
    for index in range(VIEW_COUNT):
        strip.alpha_composite(images[f"view-{index}"], (CANVAS_SIZE[0] * index, 0))
    return strip.resize((strip.width * scale, strip.height * scale), Image.Resampling.NEAREST)


def build_comparison_preview(outputs: dict[str, Image.Image]) -> Image.Image:
    """Place target/live evidence above v2/v3 asset strips for visual approval."""
    width = 1536
    panel_height = 360
    label_height = 34
    strip_height = CANVAS_SIZE[1] * 3
    height = panel_height + label_height + (label_height + strip_height) * 4 + 24
    preview = Image.new("RGBA", (width, height), _color("dark-void"))
    draw = ImageDraw.Draw(preview)
    font = _font(22)

    target = _crop_panel(TARGET_Q0, (730, 250, 1390, 610), (width // 2, panel_height))
    live = _crop_panel(LIVE_Q0, (1300, 560, 2180, 1090), (width // 2, panel_height))
    preview.alpha_composite(target, (0, 0))
    preview.alpha_composite(live, (width // 2, 0))
    draw.rectangle((0, 0, width // 2 - 1, 31), fill=_color("ui-panel-solid"))
    draw.rectangle((width // 2, 0, width - 1, 31), fill=_color("ui-panel-solid"))
    draw.text((14, 5), "APPROVED TARGET: RIGHT PROP CLUSTER", fill=_color("ui-text-cool"), font=font)
    draw.text((width // 2 + 14, 5), "CURRENT Q0: FLOOR PROPS COLLAPSE", fill=_color("ui-text-cool"), font=font)

    cursor_y = panel_height + label_height
    for spec in SPECS:
        runtime = {
            f"view-{view}": Image.open(
                RUNTIME_ART / f"{spec.output_name}-view-{view}.png"
            ).convert("RGBA")
            for view in range(VIEW_COUNT)
        }
        candidate = {
            f"view-{view}": outputs[f"{spec.output_name}-view-{view}"]
            for view in range(VIEW_COUNT)
        }
        short_name = "PARKING STOP" if spec.output_name.endswith("parking-stop") else "FALLEN SIGN"
        draw.text((14, cursor_y - label_height + 6), f"V2  {short_name}", fill=_color("ui-dim"), font=font)
        preview.alpha_composite(_asset_strip(runtime), (0, cursor_y))
        cursor_y += strip_height + label_height
        draw.text(
            (14, cursor_y - label_height + 6),
            f"V3  {short_name} - TOP / FRONT / AMBER",
            fill=_color("sig-gold"),
            font=font,
        )
        preview.alpha_composite(_asset_strip(candidate), (0, cursor_y))
        cursor_y += strip_height + label_height
    return preview


def main() -> None:
    required = (BASE_FLOOR, TARGET_Q0, LIVE_Q0)
    for path in required:
        if not path.exists():
            raise FileNotFoundError(path)

    outputs = build_outputs(Image.open(BASE_FLOOR).convert("RGBA"))
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(OUTPUT_ROOT / f"{name}.png", optimize=True)
    build_comparison_preview(outputs).save(COMPARISON_PREVIEW, optimize=True)
    print(f"wrote {len(outputs)} isolated B2 v3 candidates and comparison to {OUTPUT_ROOT}")


if __name__ == "__main__":
    main()
