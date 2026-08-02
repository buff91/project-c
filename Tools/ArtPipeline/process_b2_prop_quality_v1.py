#!/usr/bin/env python3
"""Build the native-pixel B2 wall/prop quality slice.

This processor deliberately authors at half resolution and nearest-neighbour
upscales once.  The final 128-regime sprites therefore use 2x2-or-larger pixel
clusters without antialiasing, per-asset palette drift, or generated speckle.

The six service-wall cells are cut from one direction-wide master.  Continuous
rails and cables are drawn before the cut so the three cells reconnect exactly
in every camera quarter.  Segment 0 owns the B2 hose/socket module; segments 1
and 2 stay broad and quiet so the room reads as one service bay, not three
unrelated prop cards.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw

from torchstone_palette import load_gpl_entries, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
ENVIRONMENT_OUTPUT = ROOT / "Assets/_Project/Art/Environment"
RUNTIME_OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
PREVIEW = ROOT / "docs/captures/b2-prop-quality-conform-preview-v1.png"

PIXEL_CLUSTER = 2
WALL_LOGICAL_SIZE = (32, 56)
WALL_SIZE = (64, 112)
WALL_FACE_HEIGHT = 40
SERVICE_LOGICAL_SIZE = (96, 88)
SERVICE_MASTER_SIZE = (192, 176)
PROP_LOGICAL_SIZE = (64, 64)
PROP_SIZE = (128, 128)


def _palette() -> dict[str, tuple[int, int, int, int]]:
    return {
        name: (*rgb, 255)
        for name, rgb in load_gpl_entries()
    }


PALETTE = _palette()
TRANSPARENT = (*PALETTE["dark-void"][:3], 0)


@dataclass(frozen=True)
class WallDirection:
    name: str
    slope: int
    service_windows: tuple[tuple[int, int], ...]


DIRECTIONS = (
    WallDirection(
        "rising-right",
        -1,
        ((0, 32), (32, 16), (64, 0)),
    ),
    WallDirection(
        "rising-left",
        1,
        ((0, 0), (32, 16), (64, 32)),
    ),
)


@dataclass(frozen=True)
class B2PropQualityBuild:
    outputs: dict[str, Image.Image]
    service_masters: dict[str, Image.Image]


def _top_y(direction: WallDirection, x: int) -> int:
    if direction.slope < 0:
        return (WALL_LOGICAL_SIZE[0] - 1 - x) // 2
    return x // 2


def _service_top_y(direction: WallDirection, x: int) -> int:
    if direction.slope < 0:
        return (SERVICE_LOGICAL_SIZE[0] - 1 - x) // 2
    return x // 2


def _face_point(
    top: Callable[[int], int],
    x: int,
    vertical: int,
) -> tuple[int, int]:
    return x, top(x) + vertical


def _face_polygon(
    top: Callable[[int], int],
    x0: int,
    v0: int,
    x1: int,
    v1: int,
) -> tuple[tuple[int, int], ...]:
    return (
        _face_point(top, x0, v0),
        _face_point(top, x1, v0),
        _face_point(top, x1, v1),
        _face_point(top, x0, v1),
    )


def _face_rect(
    draw: ImageDraw.ImageDraw,
    top: Callable[[int], int],
    bounds: tuple[int, int, int, int],
    fill: tuple[int, int, int, int],
    *,
    outline: tuple[int, int, int, int] | None = None,
    width: int = 1,
) -> None:
    polygon = _face_polygon(top, *bounds)
    draw.polygon(polygon, fill=fill)
    if outline is not None:
        draw.line((*polygon, polygon[0]), fill=outline, width=width, joint="curve")


def _wall_mask(direction: WallDirection) -> Image.Image:
    mask = Image.new("L", WALL_LOGICAL_SIZE, 0)
    pixels = mask.load()
    for x in range(WALL_LOGICAL_SIZE[0]):
        top = _top_y(direction, x)
        for y in range(top, min(mask.height, top + WALL_FACE_HEIGHT + 1)):
            pixels[x, y] = 255
    return mask


def _clip_to_mask(image: Image.Image, mask: Image.Image) -> Image.Image:
    clipped = image.convert("RGBA")
    clipped.putalpha(mask)
    return clipped


def _finish_logical(image: Image.Image) -> Image.Image:
    """Palette-lock once at native logical resolution, then create 2x2 clusters."""
    locked = lock_rgba_to_palette(image.convert("RGBA"))
    alpha = locked.getchannel("A").point(lambda value: 255 if value else 0)
    locked.putalpha(alpha)
    return locked.resize(
        (image.width * PIXEL_CLUSTER, image.height * PIXEL_CLUSTER),
        Image.Resampling.NEAREST,
    )


def _draw_quiet_wall(direction: WallDirection) -> Image.Image:
    image = Image.new("RGBA", WALL_LOGICAL_SIZE, TRANSPARENT)
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    # One broad concrete/steel plane.  Value-grouped rails do the structural
    # work; the middle remains intentionally quiet for actors and targeting UI.
    _face_rect(draw, top, (0, 0, 31, 40), PALETTE["grey-1"])
    _face_rect(draw, top, (0, 0, 31, 3), PALETTE["grey-4"])
    _face_rect(draw, top, (1, 1, 30, 2), PALETTE["grey-5"])
    _face_rect(draw, top, (0, 4, 31, 6), PALETTE["dark-cool"])
    _face_rect(draw, top, (1, 7, 30, 34), PALETTE["grey-2"])
    _face_rect(draw, top, (3, 9, 28, 31), PALETTE["grey-1"])
    _face_rect(draw, top, (0, 35, 31, 40), PALETTE["dark-cool"])
    _face_rect(draw, top, (1, 35, 30, 37), PALETTE["grey-3"])

    # Sparse, broad wear clusters.  No random scatter: repeated wall cells keep
    # a stable material rhythm instead of reading as procedural confetti.
    _face_rect(draw, top, (4, 28, 8, 31), PALETTE["rust-1"])
    _face_rect(draw, top, (5, 28, 7, 29), PALETTE["rust-3"])
    _face_rect(draw, top, (23, 32, 28, 34), PALETTE["grey-1"])
    _face_rect(draw, top, (25, 33, 28, 34), PALETTE["rust-2"])

    # Two paired fasteners survive as deliberate final 2x2 pixels.
    for x in (5, 26):
        px, py = _face_point(top, x, 11)
        draw.point((px, py), fill=PALETTE["grey-4"])

    return _clip_to_mask(image, _wall_mask(direction))


def _draw_torch_variant(
    base: Image.Image,
    direction: WallDirection,
) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    _face_rect(
        draw,
        top,
        (13, 13, 20, 24),
        PALETTE["dark-cool"],
        outline=PALETTE["grey-4"],
    )
    _face_rect(draw, top, (15, 15, 18, 20), PALETTE["rust-2"])
    _face_rect(draw, top, (15, 15, 18, 17), PALETTE["sig-gold-deep"])
    _face_rect(draw, top, (16, 15, 17, 16), PALETTE["sig-torch"])
    _face_rect(draw, top, (16, 21, 17, 24), PALETTE["grey-3"])

    cable = (
        _face_point(top, 17, 24),
        _face_point(top, 17, 28),
        _face_point(top, 22, 28),
        _face_point(top, 22, 32),
    )
    draw.line(cable, fill=PALETTE["dark-void"], width=1)
    return _clip_to_mask(image, _wall_mask(direction))


def _draw_hose_module(
    image: Image.Image,
    top: Callable[[int], int],
    *,
    center_x: int,
    center_v: int,
) -> None:
    draw = ImageDraw.Draw(image)
    center_y = top(center_x) + center_v

    # Flush reel: a readable industrial circle, but entirely within the wall
    # silhouette so it cannot invent a blocking floor footprint.
    draw.ellipse(
        (center_x - 7, center_y - 7, center_x + 7, center_y + 7),
        fill=PALETTE["dark-void"],
    )
    draw.ellipse(
        (center_x - 6, center_y - 6, center_x + 6, center_y + 6),
        fill=PALETTE["grey-4"],
    )
    draw.ellipse(
        (center_x - 5, center_y - 5, center_x + 5, center_y + 5),
        fill=PALETTE["rust-2"],
    )
    draw.ellipse(
        (center_x - 4, center_y - 4, center_x + 4, center_y + 4),
        fill=PALETTE["dark-cool"],
        outline=PALETTE["rust-4"],
    )
    draw.ellipse(
        (center_x - 2, center_y - 2, center_x + 2, center_y + 2),
        fill=PALETTE["grey-2"],
    )
    draw.rectangle(
        (center_x - 1, center_y - 1, center_x + 1, center_y + 1),
        fill=PALETTE["grey-5"],
    )

    tail = (
        (center_x + 5, center_y + 3),
        (center_x + 7, center_y + 7),
        _face_point(top, center_x + 9, center_v + 10),
        _face_point(top, center_x + 11, center_v + 10),
    )
    draw.line(tail, fill=PALETTE["rust-1"], width=2)
    socket_x = center_x + 12
    socket_y = top(socket_x) + center_v + 9
    draw.rectangle(
        (socket_x - 2, socket_y - 2, socket_x + 2, socket_y + 2),
        fill=PALETTE["dark-void"],
        outline=PALETTE["grey-4"],
    )
    draw.point((socket_x, socket_y), fill=PALETTE["sig-hazard"])


def _draw_pipes_variant(
    base: Image.Image,
    direction: WallDirection,
) -> Image.Image:
    image = base.copy()
    top = lambda x: _top_y(direction, x)
    _draw_hose_module(image, top, center_x=13, center_v=20)
    return _clip_to_mask(image, _wall_mask(direction))


def _draw_cabinet_variant(
    base: Image.Image,
    direction: WallDirection,
) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    top = lambda x: _top_y(direction, x)

    # Dirty-ivory payment/ticket kiosk.  The dead display and coin/ticket slots
    # make its purpose legible without relying on generated pseudo-text.
    _face_rect(
        draw,
        top,
        (9, 9, 23, 34),
        PALETTE["grey-3"],
        outline=PALETTE["dark-void"],
    )
    _face_rect(draw, top, (10, 10, 22, 33), PALETTE["grey-6"])
    _face_rect(draw, top, (11, 12, 21, 21), PALETTE["dark-cool"])
    _face_rect(draw, top, (12, 13, 20, 19), PALETTE["dark-void"])
    _face_rect(draw, top, (12, 23, 17, 25), PALETTE["grey-2"])
    _face_rect(draw, top, (18, 23, 21, 27), PALETTE["dark-cool"])
    _face_rect(draw, top, (12, 29, 20, 30), PALETTE["rust-1"])
    _face_rect(draw, top, (10, 10, 13, 11), PALETTE["rust-3"])
    signal_x, signal_y = _face_point(top, 21, 22)
    draw.point((signal_x, signal_y), fill=PALETTE["sig-warning"])
    return _clip_to_mask(image, _wall_mask(direction))


def _build_service_master(
    direction: WallDirection,
    base: Image.Image,
) -> Image.Image:
    master = Image.new("RGBA", SERVICE_LOGICAL_SIZE, TRANSPARENT)
    for left, upper in direction.service_windows:
        master.alpha_composite(base, (left, upper))

    draw = ImageDraw.Draw(master)
    top = lambda x: _service_top_y(direction, x)

    # Shared service infrastructure crosses the cell seams before the split.
    _face_rect(draw, top, (2, 5, 93, 8), PALETTE["dark-cool"])
    _face_rect(draw, top, (3, 5, 92, 6), PALETTE["grey-3"])
    draw.line(
        tuple(_face_point(top, x, 9) for x in (3, 31, 63, 92)),
        fill=PALETTE["rust-1"],
        width=1,
    )

    # Segment 0 is the front-left B2 service end and owns the one strong prop.
    # The whole reel/socket stays inside x=0..31, including its disconnected tail.
    _draw_hose_module(master, top, center_x=12, center_v=22)

    # Segment 1: one narrow maintenance spine, otherwise quiet.
    _face_rect(
        draw,
        top,
        (41, 12, 45, 31),
        PALETTE["grey-2"],
        outline=PALETTE["dark-cool"],
    )
    _face_rect(draw, top, (42, 14, 44, 17), PALETTE["sig-gold-deep"])
    signal_x, signal_y = _face_point(top, 43, 15)
    draw.point((signal_x, signal_y), fill=PALETTE["sig-torch"])

    # Segment 2: a dead service cover and one tiny magenta remnant.  It reads as
    # abandoned signage residue, never as a broad neon wash.
    _face_rect(
        draw,
        top,
        (75, 15, 88, 24),
        PALETTE["grey-1"],
        outline=PALETTE["grey-3"],
    )
    _face_rect(draw, top, (78, 18, 85, 20), PALETTE["dark-cool"])
    magenta_x, magenta_y = _face_point(top, 84, 19)
    draw.point((magenta_x, magenta_y), fill=PALETTE["sig-neon-magenta"])

    # Reconstruct the exact union mask after drawing continuous elements.
    union = Image.new("L", SERVICE_LOGICAL_SIZE, 0)
    wall_mask = _wall_mask(direction)
    for left, upper in direction.service_windows:
        union.paste(wall_mask, (left, upper), wall_mask)
    return _clip_to_mask(master, union)


def _split_service_master(
    master: Image.Image,
    direction: WallDirection,
) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for segment, (left, upper) in enumerate(direction.service_windows):
        outputs[
            f"env-wall-b2-service-segment-{segment}-{direction.name}"
        ] = master.crop(
            (
                left * PIXEL_CLUSTER,
                upper * PIXEL_CLUSTER,
                left * PIXEL_CLUSTER + WALL_SIZE[0],
                upper * PIXEL_CLUSTER + WALL_SIZE[1],
            )
        )
    return outputs


def reassemble_service_outputs(
    outputs: dict[str, Image.Image],
    direction: WallDirection,
) -> Image.Image:
    master = Image.new("RGBA", SERVICE_MASTER_SIZE, TRANSPARENT)
    for segment, (left, upper) in enumerate(direction.service_windows):
        name = f"env-wall-b2-service-segment-{segment}-{direction.name}"
        master.alpha_composite(
            outputs[name],
            (left * PIXEL_CLUSTER, upper * PIXEL_CLUSTER),
        )
    return master


def _draw_fuel_cell() -> Image.Image:
    image = Image.new("RGBA", PROP_LOGICAL_SIZE, TRANSPARENT)
    draw = ImageDraw.Draw(image)

    # Carry handle and centered cap provide immediate pickup/interaction
    # affordances without baking one camera-facing side into the single sprite.
    draw.rectangle((23, 19, 41, 22), fill=PALETTE["dark-void"])
    draw.rectangle((25, 17, 39, 20), fill=PALETTE["grey-3"])
    draw.rectangle((28, 19, 36, 22), fill=TRANSPARENT)
    draw.rectangle((29, 21, 35, 25), fill=PALETTE["dark-cool"])
    draw.rectangle((30, 21, 34, 23), fill=PALETTE["grey-4"])
    draw.point((32, 22), fill=PALETTE["sig-warning"])

    # Compact steel cylinder with a readable three-value roll and reinforced
    # shoulder/bottom rings.  It is smaller than the player and never skull-coded.
    body = ((20, 26), (24, 23), (40, 23), (44, 26), (44, 54), (41, 58), (23, 58), (20, 54))
    draw.polygon(body, fill=PALETTE["grey-1"])
    draw.line((*body, body[0]), fill=PALETTE["dark-void"], width=2, joint="curve")
    draw.rectangle((23, 25, 27, 55), fill=PALETTE["grey-2"])
    draw.rectangle((28, 25, 35, 55), fill=PALETTE["grey-3"])
    draw.rectangle((36, 25, 40, 55), fill=PALETTE["grey-2"])
    draw.rectangle((41, 28, 43, 52), fill=PALETTE["dark-cool"])
    draw.rectangle((22, 27, 42, 30), fill=PALETTE["dark-cool"])
    draw.rectangle((23, 28, 41, 29), fill=PALETTE["grey-5"])
    draw.rectangle((21, 51, 43, 55), fill=PALETTE["dark-cool"])
    draw.rectangle((23, 52, 41, 53), fill=PALETTE["grey-4"])

    # Amber chevrons are the hazardous-content language.  Their area is kept
    # below five percent so they stay an affordance rather than a glowing skin.
    draw.rectangle((23, 36, 41, 42), fill=PALETTE["rust-1"])
    for start_x in (25, 33):
        draw.polygon(
            (
                (start_x, 37),
                (start_x + 3, 39),
                (start_x, 41),
                (start_x + 2, 41),
                (start_x + 5, 39),
                (start_x + 2, 37),
            ),
            fill=PALETTE["sig-hazard"],
        )
    draw.rectangle((24, 45, 29, 47), fill=PALETTE["rust-2"])
    draw.rectangle((25, 45, 27, 45), fill=PALETTE["rust-4"])
    draw.rectangle((30, 46, 34, 49), fill=PALETTE["dark-warm"])
    draw.point((32, 46), fill=PALETTE["sig-warning"])

    # Hard-alpha restore after the intentionally open handle cutout.
    alpha = image.getchannel("A").point(lambda value: 255 if value else 0)
    image.putalpha(alpha)
    return image


def build_assets() -> B2PropQualityBuild:
    outputs: dict[str, Image.Image] = {}
    service_masters: dict[str, Image.Image] = {}

    logical_bases: dict[str, Image.Image] = {}
    for direction in DIRECTIONS:
        base = _draw_quiet_wall(direction)
        logical_bases[direction.name] = base
        outputs[f"env-wall-{direction.name}"] = _finish_logical(base)
        outputs[f"env-wall-torch-{direction.name}"] = _finish_logical(
            _draw_torch_variant(base, direction)
        )
        outputs[f"env-wall-pipes-{direction.name}"] = _finish_logical(
            _draw_pipes_variant(base, direction)
        )
        outputs[f"env-wall-cabinet-{direction.name}"] = _finish_logical(
            _draw_cabinet_variant(base, direction)
        )

        service = _finish_logical(_build_service_master(direction, base))
        service_masters[direction.name] = service
        outputs.update(_split_service_master(service, direction))

    outputs["prop-explosive-barrel"] = _finish_logical(_draw_fuel_cell())
    return B2PropQualityBuild(outputs, service_masters)


def _paste_scaled(
    canvas: Image.Image,
    sprite: Image.Image,
    position: tuple[int, int],
    scale: int,
) -> None:
    enlarged = sprite.resize(
        (sprite.width * scale, sprite.height * scale),
        Image.Resampling.NEAREST,
    )
    canvas.alpha_composite(enlarged, position)


def build_preview(build: B2PropQualityBuild) -> Image.Image:
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
            accent = PALETTE["sig-hazard"] if column else PALETTE["grey-4"]
            draw.rectangle((x, y + 232, x + 24, y + 235), fill=accent)

    for row, direction in enumerate(DIRECTIONS):
        x = 624
        y = 24 + row * 368
        draw.rectangle((x - 4, y - 4, x + 388, y + 356), fill=PALETTE["ui-inset"])
        _paste_scaled(canvas, build.service_masters[direction.name], (x, y), 2)

    draw.rectangle((1084, 156, 1476, 620), fill=PALETTE["ui-inset"])
    _paste_scaled(canvas, build.outputs["prop-explosive-barrel"], (1088, 192), 3)
    draw.rectangle((1088, 596, 1144, 603), fill=PALETTE["sig-hazard"])
    draw.rectangle((1144, 596, 1160, 603), fill=PALETTE["sig-warning"])
    return canvas


def main() -> None:
    # Keep the v1 construction functions importable for regression tests, but
    # never let the legacy CLI write its superseded wall family into live art.
    # The current writer is quarantined in /private/tmp until the promotion
    # script copies the approved candidates into Runtime/Environment + Aseprite.
    from process_b2_prop_quality_v4 import main as build_current_candidates

    print("[deprecated] process_b2_prop_quality_v1 -> process_b2_prop_quality_v4")
    build_current_candidates()


if __name__ == "__main__":
    main()
