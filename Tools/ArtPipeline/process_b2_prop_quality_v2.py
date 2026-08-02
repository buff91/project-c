#!/usr/bin/env python3
"""Author the q0-target-aligned B2 wall/prop quality slice in native pixels.

v1 proved the runtime slot/palette/pixel-cluster contract, but its broad blank
planes fell well short of the approved q0 mockup's material density.  v2 keeps
the same deterministic 128-regime and strengthens what the target actually
communicates: structural posts, inset-panel bevels, distinct service modules,
a hose reel with its lower service box, and a compact cylindrical fuel cell.

This is an evaluation build.  ``main`` writes only to
``/private/tmp/project-c-b2-v2``; it never touches Runtime, Environment, or
Aseprite SSOT files.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw

from process_b2_prop_quality_v1 import (
    DIRECTIONS,
    PALETTE,
    PIXEL_CLUSTER,
    PROP_LOGICAL_SIZE,
    PROP_SIZE,
    SERVICE_LOGICAL_SIZE,
    SERVICE_MASTER_SIZE,
    TRANSPARENT,
    WALL_SIZE,
    WallDirection,
    _clip_to_mask,
    _face_point,
    _face_rect,
    _finish_logical,
    _service_top_y,
    _split_service_master,
    _top_y,
    _wall_mask,
)


ROOT = Path(__file__).resolve().parents[2]
TARGET_Q0 = ROOT / "docs/art-direction/project-c-b2-prop-quality-target-q0-v1.png"
LIVE_Q0 = ROOT / "Assets/_Project/Captures/b2-prop-quality-q0-live-v1.png"
OUTPUT = Path("/private/tmp/project-c-b2-v2")
ASSET_PREVIEW = OUTPUT / "b2-prop-quality-v2-assets.png"
COMPARISON_PREVIEW = OUTPUT / "b2-prop-quality-v2-comparison-preview.png"


@dataclass(frozen=True)
class B2PropQualityV2Build:
    outputs: dict[str, Image.Image]
    service_masters: dict[str, Image.Image]


def _draw_bevelled_panel_shell(direction: WallDirection) -> Image.Image:
    image = Image.new("RGBA", (WALL_SIZE[0] // 2, WALL_SIZE[1] // 2), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    # Heavy structural shell: bright top bevel, paired uprights, recessed center,
    # and a deep kick plinth.  These are the strong frame/value groups missing
    # from the live q0 while retaining a calm, actor-readable central plane.
    _face_rect(draw, top, (0, 0, 31, 40), PALETTE["dark-cool"])
    _face_rect(draw, top, (0, 0, 31, 5), PALETTE["grey-3"])
    _face_rect(draw, top, (1, 0, 30, 1), PALETTE["grey-5"])
    _face_rect(draw, top, (1, 2, 30, 3), PALETTE["grey-4"])
    _face_rect(draw, top, (0, 5, 31, 7), PALETTE["dark-void"])

    # Left/right frame posts carry separate light and shadow faces, so each cell
    # reads as a bolted wall bay rather than a flat parallelogram.
    _face_rect(draw, top, (0, 6, 4, 37), PALETTE["grey-2"])
    _face_rect(draw, top, (1, 7, 2, 36), PALETTE["grey-4"])
    _face_rect(draw, top, (3, 7, 4, 36), PALETTE["dark-void"])
    _face_rect(draw, top, (27, 6, 31, 37), PALETTE["grey-2"])
    _face_rect(draw, top, (28, 7, 29, 36), PALETTE["grey-4"])
    _face_rect(draw, top, (30, 7, 31, 36), PALETTE["dark-void"])

    # Inset panel with explicit top/left bevel and lower shadow return.
    _face_rect(draw, top, (4, 8, 27, 34), PALETTE["grey-3"])
    _face_rect(draw, top, (5, 9, 26, 33), PALETTE["dark-void"])
    _face_rect(draw, top, (6, 10, 25, 31), PALETTE["grey-1"])
    _face_rect(draw, top, (7, 11, 24, 29), PALETTE["grey-2"])
    _face_rect(draw, top, (7, 11, 24, 12), PALETTE["grey-3"])
    _face_rect(draw, top, (7, 29, 24, 31), PALETTE["dark-cool"])

    # Reinforced plinth and restrained, block-scale oxidation.
    _face_rect(draw, top, (0, 35, 31, 40), PALETTE["dark-void"])
    _face_rect(draw, top, (1, 35, 30, 37), PALETTE["grey-3"])
    _face_rect(draw, top, (2, 38, 29, 39), PALETTE["grey-1"])
    _face_rect(draw, top, (5, 32, 9, 35), PALETTE["rust-1"])
    _face_rect(draw, top, (6, 32, 8, 33), PALETTE["rust-3"])
    _face_rect(draw, top, (21, 33, 25, 35), PALETTE["rust-2"])

    for x in (2, 29):
        for vertical in (9, 32):
            draw.point(_face_point(top, x, vertical), fill=PALETTE["grey-5"])
    for x in (8, 23):
        draw.point(_face_point(top, x, 15), fill=PALETTE["grey-4"])

    return _clip_to_mask(image, _wall_mask(direction))


def _draw_utility_light(base: Image.Image, direction: WallDirection) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    _face_rect(
        draw,
        top,
        (12, 12, 20, 25),
        PALETTE["dark-void"],
        outline=PALETTE["grey-4"],
    )
    _face_rect(draw, top, (14, 14, 18, 21), PALETTE["rust-2"])
    _face_rect(draw, top, (14, 14, 18, 17), PALETTE["sig-gold-deep"])
    _face_rect(draw, top, (15, 14, 17, 16), PALETTE["sig-torch"])
    _face_rect(draw, top, (15, 22, 17, 25), PALETTE["grey-3"])
    cable = (
        _face_point(top, 16, 25),
        _face_point(top, 16, 29),
        _face_point(top, 22, 29),
        _face_point(top, 22, 33),
    )
    draw.line(cable, fill=PALETTE["dark-void"], width=2)
    return _clip_to_mask(image, _wall_mask(direction))


def _draw_hose_and_box(
    image: Image.Image,
    top: Callable[[int], int],
    *,
    offset_x: int,
) -> None:
    draw = ImageDraw.Draw(image)
    center_x = offset_x + 12
    center_v = 20
    center_y = top(center_x) + center_v

    # Recessed square mount gives the circular reel a convincing wall depth.
    _face_rect(
        draw,
        top,
        (offset_x + 3, 9, offset_x + 23, 29),
        PALETTE["dark-void"],
        outline=PALETTE["grey-3"],
    )
    _face_rect(draw, top, (offset_x + 5, 11, offset_x + 21, 27), PALETTE["grey-1"])
    draw.ellipse(
        (center_x - 8, center_y - 8, center_x + 8, center_y + 8),
        fill=PALETTE["dark-void"],
    )
    draw.ellipse(
        (center_x - 7, center_y - 7, center_x + 7, center_y + 7),
        fill=PALETTE["rust-3"],
    )
    draw.ellipse(
        (center_x - 5, center_y - 5, center_x + 5, center_y + 5),
        fill=PALETTE["dark-warm"],
        outline=PALETTE["rust-4"],
    )
    draw.ellipse(
        (center_x - 3, center_y - 3, center_x + 3, center_y + 3),
        fill=PALETTE["grey-2"],
        outline=PALETTE["grey-5"],
    )
    draw.rectangle(
        (center_x - 1, center_y - 1, center_x + 1, center_y + 1),
        fill=PALETTE["dark-cool"],
    )

    # The approved q0's crucial secondary mass: a small service box below the
    # reel.  It makes the station read as installed infrastructure, not a decal.
    _face_rect(
        draw,
        top,
        (offset_x + 5, 29, offset_x + 13, 38),
        PALETTE["rust-1"],
        outline=PALETTE["dark-void"],
    )
    _face_rect(draw, top, (offset_x + 6, 30, offset_x + 12, 36), PALETTE["grey-2"])
    _face_rect(draw, top, (offset_x + 7, 31, offset_x + 11, 33), PALETTE["sig-gold-deep"])
    draw.point(
        _face_point(top, offset_x + 9, 32),
        fill=PALETTE["sig-hazard"],
    )

    tail = (
        (center_x + 6, center_y + 3),
        _face_point(top, offset_x + 21, 27),
        _face_point(top, offset_x + 21, 34),
        _face_point(top, offset_x + 17, 36),
    )
    draw.line(tail, fill=PALETTE["rust-2"], width=2)
    coupler = _face_point(top, offset_x + 17, 36)
    draw.rectangle(
        (coupler[0] - 1, coupler[1] - 2, coupler[0] + 1, coupler[1] + 1),
        fill=PALETTE["grey-5"],
    )


def _draw_hose_variant(base: Image.Image, direction: WallDirection) -> Image.Image:
    image = base.copy()
    top = lambda x: _top_y(direction, x)
    _draw_hose_and_box(image, top, offset_x=0)
    return _clip_to_mask(image, _wall_mask(direction))


def _draw_kiosk(base: Image.Image, direction: WallDirection) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    # Narrow, deep-bevelled ticket/payment kiosk matching the target's vertical
    # proportion.  All pixels remain within the wall mask and 10px above its foot.
    _face_rect(
        draw,
        top,
        (9, 8, 23, 34),
        PALETTE["dark-void"],
        outline=PALETTE["grey-4"],
    )
    _face_rect(draw, top, (10, 9, 22, 33), PALETTE["grey-6"])
    _face_rect(draw, top, (11, 10, 21, 20), PALETTE["grey-3"])
    _face_rect(draw, top, (12, 11, 20, 18), PALETTE["dark-void"])
    _face_rect(draw, top, (12, 22, 20, 25), PALETTE["grey-2"])
    _face_rect(draw, top, (13, 23, 18, 23), PALETTE["dark-cool"])
    _face_rect(draw, top, (12, 28, 20, 31), PALETTE["grey-3"])
    _face_rect(draw, top, (13, 29, 19, 30), PALETTE["rust-1"])
    _face_rect(draw, top, (10, 9, 13, 10), PALETTE["rust-3"])
    draw.point(_face_point(top, 20, 22), fill=PALETTE["sig-warning"])
    return _clip_to_mask(image, _wall_mask(direction))


def _draw_service_master(
    direction: WallDirection,
    base: Image.Image,
) -> Image.Image:
    master = Image.new("RGBA", SERVICE_LOGICAL_SIZE, TRANSPARENT)
    for left, upper in direction.service_windows:
        master.alpha_composite(base, (left, upper))
    draw = ImageDraw.Draw(master)
    top = lambda x: _service_top_y(direction, x)

    # One continuous conduit and one lower pipe bind all three functional bays.
    _face_rect(draw, top, (2, 6, 93, 9), PALETTE["dark-void"])
    _face_rect(draw, top, (3, 6, 92, 7), PALETTE["grey-4"])
    _face_rect(draw, top, (2, 35, 93, 38), PALETTE["dark-cool"])
    _face_rect(draw, top, (3, 35, 92, 36), PALETTE["rust-1"])

    # Segment 0 — hose station plus the target's lower service box.
    _draw_hose_and_box(master, top, offset_x=0)

    # Segment 1 — breaker spine and analog gauge, compact enough to leave a
    # broad readable plane around it.
    _face_rect(
        draw,
        top,
        (38, 10, 53, 32),
        PALETTE["dark-void"],
        outline=PALETTE["grey-3"],
    )
    _face_rect(draw, top, (40, 12, 51, 30), PALETTE["grey-2"])
    _face_rect(draw, top, (42, 14, 49, 20), PALETTE["dark-cool"])
    gauge_center = _face_point(top, 45, 17)
    draw.ellipse(
        (gauge_center[0] - 2, gauge_center[1] - 2, gauge_center[0] + 2, gauge_center[1] + 2),
        fill=PALETTE["grey-5"],
        outline=PALETTE["rust-2"],
    )
    draw.point(gauge_center, fill=PALETTE["sig-torch"])
    _face_rect(draw, top, (43, 23, 48, 27), PALETTE["sig-gold-deep"])
    _face_rect(draw, top, (44, 24, 47, 25), PALETTE["sig-hazard"])
    draw.line(
        (
            _face_point(top, 45, 9),
            _face_point(top, 45, 12),
        ),
        fill=PALETTE["grey-4"],
        width=2,
    )

    # Segment 2 — a wide louvered access terminal.  This different horizontal
    # grammar prevents the service strip from looking like cloned wall decals.
    _face_rect(
        draw,
        top,
        (68, 10, 91, 32),
        PALETTE["dark-void"],
        outline=PALETTE["grey-4"],
    )
    _face_rect(draw, top, (70, 12, 89, 30), PALETTE["grey-1"])
    _face_rect(draw, top, (72, 14, 87, 22), PALETTE["dark-cool"])
    for vertical in (15, 18, 21):
        draw.line(
            (
                _face_point(top, 73, vertical),
                _face_point(top, 86, vertical),
            ),
            fill=PALETTE["grey-3"],
            width=1,
        )
    _face_rect(draw, top, (73, 25, 82, 27), PALETTE["grey-2"])
    _face_rect(draw, top, (84, 25, 87, 28), PALETTE["dark-cool"])
    draw.point(_face_point(top, 86, 26), fill=PALETTE["sig-neon-magenta"])

    union = Image.new("L", SERVICE_LOGICAL_SIZE, 0)
    wall_mask = _wall_mask(direction)
    for left, upper in direction.service_windows:
        union.paste(wall_mask, (left, upper), wall_mask)
    return _clip_to_mask(master, union)


def _draw_cylindrical_fuel_cell() -> Image.Image:
    image = Image.new("RGBA", PROP_LOGICAL_SIZE, TRANSPARENT)
    draw = ImageDraw.Draw(image)

    # Centered carry handle and cap; no camera-facing side valve and no baked AO.
    draw.rectangle((23, 19, 41, 22), fill=PALETTE["dark-void"])
    draw.rectangle((25, 17, 39, 20), fill=PALETTE["grey-3"])
    draw.rectangle((28, 19, 36, 22), fill=TRANSPARENT)
    draw.rectangle((29, 21, 35, 25), fill=PALETTE["dark-cool"])
    draw.rectangle((30, 21, 34, 23), fill=PALETTE["grey-5"])
    draw.point((32, 22), fill=PALETTE["sig-warning"])

    # Rounded shoulders and bottom ring are what distinguish a pressure cylinder
    # from the live q0's rectangular crate silhouette.
    draw.ellipse((19, 24, 45, 34), fill=PALETTE["dark-void"])
    draw.rectangle((19, 29, 45, 53), fill=PALETTE["dark-void"])
    draw.ellipse((19, 48, 45, 58), fill=PALETTE["dark-void"])
    draw.ellipse((21, 26, 43, 34), fill=PALETTE["grey-4"])
    draw.rectangle((21, 30, 43, 52), fill=PALETTE["grey-2"])
    draw.ellipse((21, 48, 43, 56), fill=PALETTE["grey-2"])

    # Cylindrical roll: shared cool shadow, central steel highlight, warm worn edge.
    draw.rectangle((21, 31, 24, 51), fill=PALETTE["dark-cool"])
    draw.rectangle((25, 30, 28, 53), fill=PALETTE["grey-1"])
    draw.rectangle((29, 29, 35, 54), fill=PALETTE["grey-3"])
    draw.rectangle((36, 30, 40, 53), fill=PALETTE["grey-2"])
    draw.rectangle((41, 31, 43, 51), fill=PALETTE["rust-1"])
    draw.ellipse((21, 26, 43, 33), outline=PALETTE["grey-5"], width=1)
    draw.ellipse((23, 28, 41, 32), outline=PALETTE["dark-cool"], width=1)

    # Wraparound hazard band and centered service hatch.
    draw.rectangle((20, 37, 44, 44), fill=PALETTE["rust-1"])
    draw.rectangle((21, 38, 43, 43), fill=PALETTE["dark-warm"])
    for start_x in (24, 33):
        draw.polygon(
            (
                (start_x, 38),
                (start_x + 3, 40),
                (start_x, 42),
                (start_x + 2, 42),
                (start_x + 5, 40),
                (start_x + 2, 38),
            ),
            fill=PALETTE["sig-hazard"],
        )
    draw.rectangle((29, 46, 35, 50), fill=PALETTE["dark-cool"])
    draw.rectangle((30, 47, 34, 49), fill=PALETTE["grey-1"])
    draw.point((32, 47), fill=PALETTE["sig-warning"])

    # Reinforced bottom ring; body ends at logical y58 (final bbox bottom 118).
    draw.ellipse((19, 50, 45, 58), outline=PALETTE["dark-void"], width=2)
    draw.arc((21, 51, 43, 56), 0, 180, fill=PALETTE["grey-4"], width=1)
    alpha = image.getchannel("A").point(lambda value: 255 if value else 0)
    image.putalpha(alpha)
    return image


def build_assets() -> B2PropQualityV2Build:
    outputs: dict[str, Image.Image] = {}
    service_masters: dict[str, Image.Image] = {}

    for direction in DIRECTIONS:
        base = _draw_bevelled_panel_shell(direction)
        outputs[f"env-wall-{direction.name}"] = _finish_logical(base)
        outputs[f"env-wall-torch-{direction.name}"] = _finish_logical(
            _draw_utility_light(base, direction)
        )
        outputs[f"env-wall-pipes-{direction.name}"] = _finish_logical(
            _draw_hose_variant(base, direction)
        )
        outputs[f"env-wall-cabinet-{direction.name}"] = _finish_logical(
            _draw_kiosk(base, direction)
        )

        service = _finish_logical(_draw_service_master(direction, base))
        service_masters[direction.name] = service
        outputs.update(_split_service_master(service, direction))

    outputs["prop-explosive-barrel"] = _finish_logical(
        _draw_cylindrical_fuel_cell()
    )
    return B2PropQualityV2Build(outputs, service_masters)


def reassemble_service_outputs(
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


def build_asset_preview(build: B2PropQualityV2Build) -> Image.Image:
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


def build_comparison_preview(
    build: B2PropQualityV2Build,
    target: Image.Image,
    live: Image.Image,
) -> Image.Image:
    canvas = Image.new("RGBA", (1920, 1380), PALETTE["dark-void"])
    target_view = target.convert("RGBA").resize((960, 540), Image.Resampling.LANCZOS)
    live_view = live.convert("RGBA").resize((960, 540), Image.Resampling.LANCZOS)
    canvas.alpha_composite(target_view, (0, 0))
    canvas.alpha_composite(live_view, (960, 0))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 540, 960, 547), fill=PALETTE["sig-hazard"])
    draw.rectangle((960, 540, 1919, 547), fill=PALETTE["sig-neon-magenta"])
    asset_preview = build_asset_preview(build)
    canvas.alpha_composite(asset_preview, (192, 588))
    draw.rectangle((192, 1360, 1728, 1367), fill=PALETTE["sig-teal-item"])
    return canvas


def main() -> None:
    if not TARGET_Q0.exists():
        raise FileNotFoundError(TARGET_Q0)
    if not LIVE_Q0.exists():
        raise FileNotFoundError(LIVE_Q0)

    build = build_assets()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build.outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    for direction, image in build.service_masters.items():
        image.save(OUTPUT / f"env-wall-b2-service-master-{direction}.png", optimize=True)

    asset_preview = build_asset_preview(build)
    asset_preview.save(ASSET_PREVIEW, optimize=True)
    build_comparison_preview(
        build,
        Image.open(TARGET_Q0),
        Image.open(LIVE_Q0),
    ).save(COMPARISON_PREVIEW, optimize=True)
    print(
        f"wrote {len(build.outputs)} evaluation sprites, "
        f"{len(build.service_masters)} masters, and previews to {OUTPUT}"
    )


if __name__ == "__main__":
    main()
