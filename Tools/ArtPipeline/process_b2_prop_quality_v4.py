#!/usr/bin/env python3
"""Direct-final-resolution conform for the approved B2 production sheet.

v3 reduced every source through a 32x56 logical wall, which preserved the broad
silhouette but collapsed the production sheet's bolts, bevel steps, hose ribs,
and terminal controls into coarse blocks.  v4 resamples detail directly to the
final 64x112/128x128 canvases, lifts neutral material values into the shared
grey ramps, palette-locks, hardens alpha, and removes only truly isolated 1px
noise.  Wall RGB then conforms to one exact isometric shell instead of keeping
the source's free-standing-card alpha; service cells join in a direction-wide
master before their final split.  A separate quiet-material source board owns
the low-frequency base and unlit display faces so repeated cells no longer all
carry the same centered picture-frame panel.  Intentional 1-2px highlights and
signals remain legal final pixels.

This evaluation processor writes only to ``/private/tmp/project-c-b2-v4``.
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw

from process_b2_prop_quality_v1 import (
    DIRECTIONS,
    PALETTE,
    PIXEL_CLUSTER,
    PROP_SIZE,
    SERVICE_LOGICAL_SIZE,
    SERVICE_MASTER_SIZE,
    TRANSPARENT,
    WALL_LOGICAL_SIZE,
    WALL_SIZE,
    WallDirection,
    _draw_quiet_wall,
    _face_rect,
    _finish_logical,
    _service_top_y,
    _top_y,
)
from process_b2_prop_quality_v3 import (
    ALPHA_CUTOFF,
    SOURCE,
    _crop,
    build_assets as build_v3_assets,
)
from torchstone_palette import load_gpl_entries, lock_rgba_to_palette


OUTPUT = Path("/private/tmp/project-c-b2-v4")
ASSET_PREVIEW = OUTPUT / "b2-prop-quality-v4-assets.png"
COMPARISON_PREVIEW = OUTPUT / "b2-prop-quality-v3-v4-comparison.png"
ROOT = Path(__file__).resolve().parents[2]
MATERIAL_SOURCE = (
    ROOT / "docs/art-direction/project-c-b2-wall-material-source-v1.png"
)
MATERIAL_SOURCE_SIZE = (1672, 941)
MATERIAL_ALPHA_CUTOFF = 4
# The ImageGen board is intentionally black-backed rather than a runtime sheet.
# Crop windows and the measured post-key bounds are explicit so replacing the
# source cannot silently feed a neighbouring module into a catalog slot.
MATERIAL_SOURCE_CROPS = {
    # The quiet third module is safe for the high-frequency base slot.  The
    # first module's small repair plate appears only in authored odd bays.  The
    # cracked middle module is intentionally not promoted: it could imply a
    # secret/breakable wall gameplay state that this material slot does not own.
    "wall-material-base": ((1140, 50, 1540, 835), (15, 18, 386, 781)),
    "wall-material-display": ((150, 50, 540, 835), (8, 18, 379, 781)),
}
FUEL_FIT_SIZE = (58, 96)
FUEL_FIT_POSITION = (35, 22)

SIGNAL_AND_HIGHLIGHT_COLORS = {
    (*rgb, 255)
    for name, rgb in load_gpl_entries()
    if name.startswith("sig-")
    or name in {
        "grey-5",
        "grey-6",
        "rust-4",
        "pc-stone-lit",
        "tile-4",
        "ui-text",
        "ui-text-cool",
        "ui-hp",
    }
}


@dataclass(frozen=True)
class B2PropQualityV4Build:
    outputs: dict[str, Image.Image]
    service_masters: dict[str, Image.Image]


def _hard_alpha(image: Image.Image) -> Image.Image:
    hardened = image.convert("RGBA")
    alpha = hardened.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    hardened.putalpha(alpha)
    return hardened


def _extract_material_source(
    sheet: Image.Image,
    source_key: str,
) -> Image.Image:
    """Extract one black-backed quiet wall module as a hard-alpha source."""
    if sheet.size != MATERIAL_SOURCE_SIZE:
        raise ValueError(
            f"unexpected B2 wall-material source size: {sheet.size}"
        )
    if source_key not in MATERIAL_SOURCE_CROPS:
        raise ValueError(f"unknown B2 wall-material crop: {source_key}")

    crop_box, expected_bounds = MATERIAL_SOURCE_CROPS[source_key]
    cropped = sheet.crop(crop_box).convert("RGBA")
    source_pixels = list(cropped.get_flattened_data())
    keyed_pixels = []
    for y in range(cropped.height):
        row = source_pixels[y * cropped.width:(y + 1) * cropped.width]
        visible_x = [
            x
            for x, (red, green, blue, _) in enumerate(row)
            if max(red, green, blue) >= MATERIAL_ALPHA_CUTOFF
        ]
        first = min(visible_x) if visible_x else cropped.width
        last = max(visible_x) if visible_x else -1
        # The wall modules are solid architectural faces.  Filling between the
        # seeded row bounds retains black inset seams and recesses instead of
        # mistaking them for the black presentation background.
        keyed_pixels.extend(
            (red, green, blue, 255 if first <= x <= last else 0)
            for x, (red, green, blue, _) in enumerate(row)
        )
    keyed = Image.new("RGBA", cropped.size, TRANSPARENT)
    keyed.putdata(keyed_pixels)
    bounds = keyed.getchannel("A").getbbox()
    if bounds != expected_bounds:
        raise ValueError(
            f"B2 wall-material crop {source_key} no longer matches "
            f"{expected_bounds}: {bounds}"
        )
    return keyed.crop(bounds)


def _cluster_quiet_material_source(source: Image.Image) -> Image.Image:
    """Collapse generated texture into deliberate 2x2-or-larger material fields."""
    logical = source.resize(WALL_LOGICAL_SIZE, Image.Resampling.BOX)
    return logical.resize(WALL_SIZE, Image.Resampling.NEAREST)


def _warm_palette_color(
    luminance: float,
    *,
    fuel: bool,
    red_signal: bool,
) -> tuple[int, int, int, int]:
    if red_signal:
        return PALETTE["sig-warning"] if luminance >= 70 else PALETTE["sig-warning-deep"]
    if fuel:
        if luminance >= 105:
            return PALETTE["sig-torch"]
        if luminance >= 72:
            return PALETTE["sig-hazard"]
        if luminance >= 46:
            return PALETTE["sig-gold-deep"]
        if luminance >= 26:
            return PALETTE["rust-2"]
        return PALETTE["rust-1"]
    if luminance >= 110:
        return PALETTE["sig-torch"]
    if luminance >= 78:
        return PALETTE["rust-4"]
    if luminance >= 52:
        return PALETTE["rust-3"]
    if luminance >= 30:
        return PALETTE["rust-2"]
    return PALETTE["rust-1"]


def _lift_material_values(image: Image.Image, source_key: str) -> Image.Image:
    """Lift readable steel midtones while preserving local warm material signals."""
    source = image.convert("RGBA")
    terminal = source_key == "wall-5-terminal"
    fuel = source_key == "fuel-cell"
    pixels: list[tuple[int, int, int, int]] = []
    for index, (red, green, blue, alpha) in enumerate(source.get_flattened_data()):
        if alpha < ALPHA_CUTOFF:
            pixels.append((red, green, blue, alpha))
            continue

        luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722
        warm = red - blue > 18 and red - green > 4
        red_signal = red >= 90 and red > green * 1.65 and red > blue * 1.4
        # The fuel-cell handle/top rim are warm-reflecting steel in the source,
        # not amber gameplay signals.  Only its red status LED stays chromatic in
        # the upper third; the body chevrons and lower rust keep their warm ramp.
        if fuel and index // source.width < 32 and not red_signal:
            warm = False
        if warm:
            pixels.append(_warm_palette_color(luminance, fuel=fuel, red_signal=red_signal))
            continue

        # The production sheet's neutral median is only ~21/255.  This curve
        # moves the broad surfaces to grey-1, bevels to grey-2/3, and leaves the
        # deepest recesses in the blue-black shared shadow ramp.
        lifted = max(0.0, min(235.0, luminance * 1.55 + 10.0))
        if source_key == "wall-material-display":
            # The repair-plate source is brighter than the quiet base board.
            # A restrained pre-lock reduction keeps both variants in one wall
            # material family while the plate silhouette remains distinct.
            lifted = max(0.0, lifted - 8.0)
        if terminal and luminance >= 42:
            if luminance >= 105:
                pixels.append(PALETTE["grey-6"])
            elif luminance >= 76:
                pixels.append(PALETTE["grey-5"])
            elif luminance >= 56:
                pixels.append(PALETTE["grey-4"])
            else:
                pixels.append(PALETTE["grey-3"])
            continue

        # A restrained cool bias keeps neutral wall material out of the broad
        # brown/tile ramps during nearest-palette assignment.
        pixels.append(
            (
                round(lifted * 0.94),
                round(lifted * 0.99),
                round(lifted * 1.04),
                alpha,
            )
        )
    lifted_image = Image.new("RGBA", source.size, TRANSPARENT)
    lifted_image.putdata(pixels)
    return lifted_image


def _remove_isolated_noise(image: Image.Image) -> Image.Image:
    """One snapshot pass: clear alpha singletons and merge non-signal specks.

    A 1px color survives when it is a functional signal/highlight or when its
    neighborhood is itself detailed.  Only an unprotected color surrounded by a
    three-pixel-or-stronger majority is considered generated speckle.
    """
    source = image.convert("RGBA")
    result = source.copy()
    snapshot = source.load()
    target = result.load()
    width, height = source.size
    offsets = (
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    )
    for y in range(height):
        for x in range(width):
            pixel = snapshot[x, y]
            if pixel[3] == 0:
                continue
            neighbors = [
                snapshot[x + dx, y + dy]
                for dx, dy in offsets
                if 0 <= x + dx < width
                and 0 <= y + dy < height
                and snapshot[x + dx, y + dy][3] > 0
            ]
            if not neighbors:
                target[x, y] = TRANSPARENT
                continue
            if pixel in SIGNAL_AND_HIGHLIGHT_COLORS:
                continue
            if any(neighbor[:3] == pixel[:3] for neighbor in neighbors):
                continue
            majority, count = Counter(neighbor[:3] for neighbor in neighbors).most_common(1)[0]
            if count >= 3:
                target[x, y] = (*majority, 255)
    return result


def _conform_final(
    source: Image.Image,
    size: tuple[int, int],
    source_key: str,
) -> Image.Image:
    reduced = source.resize(size, Image.Resampling.BOX)
    lifted = _lift_material_values(reduced, source_key)
    locked = lock_rgba_to_palette(lifted)
    hardened = _hard_alpha(locked)
    cleaned = _remove_isolated_noise(hardened)
    # The final call keeps the shared Torchstone lock as the last color operation.
    return _hard_alpha(lock_rgba_to_palette(cleaned))


def _right_wall_joinery() -> Image.Image:
    """Build the shared cap and kick plate that make cells read as one wall."""
    direction = DIRECTIONS[0]
    return _structural_joinery(
        WALL_SIZE,
        lambda x: _top_y(direction, x // PIXEL_CLUSTER) * PIXEL_CLUSTER,
    )


def _structural_joinery(
    size: tuple[int, int],
    top_for_x,
) -> Image.Image:
    """Draw exact face-relative cap/plinth bands at final pixel resolution.

    Degenerate one-row polygons leave alternating holes along an isometric
    slope.  Writing the bands per column makes every final row continuous while
    retaining the intentional 2px cluster regime.
    """
    image = Image.new("RGBA", size, TRANSPARENT)
    pixels = image.load()
    bands = (
        (0, 1, PALETTE["dark-void"]),
        (2, 3, PALETTE["grey-3"]),
        (4, 5, PALETTE["grey-2"]),
        (6, 7, PALETTE["dark-cool"]),
        # The old bright grey footline repeated as a dotted seam and made the
        # wall float.  This bevel and dark plinth bind it to the floor datum.
        (74, 75, PALETTE["grey-2"]),
        (76, 79, PALETTE["dark-cool"]),
        (80, 81, PALETTE["dark-void"]),
    )
    for x in range(size[0]):
        top = top_for_x(x)
        for first, last, color in bands:
            for vertical in range(first, last + 1):
                y = top + vertical
                if 0 <= y < size[1]:
                    pixels[x, y] = color
    return image


def _wall_contract_parts(
    direction: WallDirection,
) -> tuple[Image.Image, Image.Image, Image.Image]:
    """Return an exact wall shell, alpha contract, and shared joinery layer."""
    shell = _finish_logical(_draw_quiet_wall(DIRECTIONS[0]))
    joinery = _right_wall_joinery()
    if direction.slope > 0:
        shell = shell.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        joinery = joinery.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return shell, shell.getchannel("A"), joinery


def _edge_extend_to_wall_contract(
    detail: Image.Image,
    shell: Image.Image,
    contract: Image.Image,
) -> Image.Image:
    """Keep source detail but replace its free-standing-card silhouette.

    The generated production cells have good internal material detail, but only
    a handful of opaque pixels reach x=0/x=63.  Adjacent 64x112 wall slots are
    therefore disconnected even though their anchors and pivots are correct.
    The contract is a solid isometric wall face.  Missing edge pixels extend the
    nearest source material for a short distance and fall back to the authored
    structural shell when the source row is too far away.
    """
    source = detail.convert("RGBA")
    result = shell.copy()
    source_pixels = source.load()
    target_pixels = result.load()
    contract_pixels = contract.load()
    for y in range(result.height):
        visible_x = [x for x in range(result.width) if source_pixels[x, y][3] > 0]
        for x in range(result.width):
            if contract_pixels[x, y] == 0:
                continue
            pixel = source_pixels[x, y]
            if pixel[3] > 0:
                target_pixels[x, y] = pixel
                continue
            if not visible_x:
                continue
            nearest = min(visible_x, key=lambda candidate: abs(candidate - x))
            if abs(nearest - x) <= 12:
                target_pixels[x, y] = source_pixels[nearest, y]
    result.putalpha(contract)
    return result


def _normalize_joinable_side(
    wall: Image.Image,
    direction: WallDirection,
) -> Image.Image:
    """Replace the production card's inset end-cap with one shared wall post.

    The ImageGen sheet contains a nearly white, full-height rail roughly ten
    pixels inside one edge.  Repeating the same 64px sprite therefore reads as
    a row of self-contained cards even after their alpha shells connect.  Only
    that joinable twelve-pixel band is normalized; the hose, vent, terminal,
    and broad center material remain source-authored.

    The rising-left band is the exact mirror of the canonical rising-right
    band.  Alpha is never written here: the common isometric shell remains the
    geometry contract owned by ``_wall_contract_parts``.
    """
    normalized = wall.copy()
    pixels = normalized.load()
    alpha = normalized.getchannel("A").load()
    for x in range(normalized.width):
        edge_distance = x if direction.slope < 0 else normalized.width - 1 - x
        if edge_distance >= 12:
            continue

        top = _top_y(direction, x // PIXEL_CLUSTER) * PIXEL_CLUSTER
        for y in range(normalized.height):
            if alpha[x, y] == 0:
                continue
            vertical = y - top
            # Cap and plinth are continuous direction-wide datums supplied by
            # joinery after this pass.  Normalize only the card body between.
            if vertical < 8 or vertical > 73:
                continue
            if vertical <= 13:
                color = PALETTE["dark-cool"]
            elif edge_distance <= 1:
                color = PALETTE["dark-void"]
            elif edge_distance <= 3:
                color = PALETTE["grey-3"]
            elif edge_distance <= 7:
                color = PALETTE["dark-cool"]
            elif vertical >= 70:
                color = PALETTE["grey-2"]
            else:
                color = PALETTE["grey-1"]
            pixels[x, y] = color
    return normalized


def _conform_wall(
    source: Image.Image,
    source_key: str,
    direction: WallDirection,
) -> Image.Image:
    detail = _conform_final(source, WALL_SIZE, source_key)
    if direction.slope > 0:
        detail = detail.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

    shell, contract, joinery = _wall_contract_parts(direction)
    wall = _edge_extend_to_wall_contract(detail, shell, contract)
    wall = _normalize_joinable_side(wall, direction)
    wall.alpha_composite(joinery)
    wall.putalpha(contract)
    return _hard_alpha(lock_rgba_to_palette(wall))


def _apply_quiet_material_body(
    structural_wall: Image.Image,
    material_wall: Image.Image,
    direction: WallDirection,
) -> Image.Image:
    """Replace only the quiet center field, keeping every modular datum exact."""
    result = structural_wall.copy()
    result_pixels = result.load()
    material_pixels = material_wall.load()
    alpha = structural_wall.getchannel("A").load()
    # Both directions use the same screen-space safe columns.  Their sloped
    # face-relative rows differ, so derive vertical position from the direction.
    for x in range(12, 52):
        top = _top_y(direction, x // PIXEL_CLUSTER) * PIXEL_CLUSTER
        for vertical in range(8, 74):
            y = top + vertical
            if 0 <= y < result.height and alpha[x, y] > 0:
                result_pixels[x, y] = material_pixels[x, y]
    result.putalpha(structural_wall.getchannel("A"))
    return _hard_alpha(lock_rgba_to_palette(result))


def _conform_fuel_cell(source: Image.Image) -> Image.Image:
    fitted = _conform_final(source, FUEL_FIT_SIZE, "fuel-cell")
    canvas = Image.new("RGBA", PROP_SIZE, TRANSPARENT)
    canvas.alpha_composite(fitted, FUEL_FIT_POSITION)
    return canvas


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


def _service_joinery(direction: WallDirection) -> Image.Image:
    """Draw shared rails once in direction-wide coordinates before splitting."""
    return _structural_joinery(
        SERVICE_MASTER_SIZE,
        lambda x: _service_top_y(direction, x // PIXEL_CLUSTER) * PIXEL_CLUSTER,
    )


def _build_service_master(
    conformed: dict[str, Image.Image],
    direction: WallDirection,
) -> Image.Image:
    master = Image.new("RGBA", SERVICE_MASTER_SIZE, TRANSPARENT)
    union = Image.new("L", SERVICE_MASTER_SIZE, 0)
    source_keys = ("wall-2-hose", "wall-4-quiet", "wall-3-vent")
    for source_key, (left, upper) in zip(source_keys, direction.service_windows):
        position = (left * PIXEL_CLUSTER, upper * PIXEL_CLUSTER)
        cell = conformed[source_key]
        master.alpha_composite(cell, position)
        union.paste(cell.getchannel("A"), position)

    master.alpha_composite(_service_joinery(direction))
    master.putalpha(union)
    return _hard_alpha(lock_rgba_to_palette(master))


def _split_service_master(
    master: Image.Image,
    direction: WallDirection,
) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for segment, (left, upper) in enumerate(direction.service_windows):
        outputs[f"env-wall-b2-service-segment-{segment}-{direction.name}"] = master.crop(
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
    return _assemble_service_master(outputs, direction)


def build_assets(
    sheet: Image.Image,
    material_sheet: Image.Image | None = None,
) -> B2PropQualityV4Build:
    if sheet.size != (1672, 941):
        raise ValueError(f"unexpected B2 production-sheet size: {sheet.size}")
    if material_sheet is None:
        if not MATERIAL_SOURCE.exists():
            raise FileNotFoundError(MATERIAL_SOURCE)
        with Image.open(MATERIAL_SOURCE) as source:
            material_sheet = source.convert("RGBA")
    if material_sheet.size != MATERIAL_SOURCE_SIZE:
        raise ValueError(
            f"unexpected B2 wall-material source size: {material_sheet.size}"
        )

    wall_keys = (
        "wall-1-base",
        "wall-2-hose",
        "wall-3-vent",
        "wall-4-quiet",
        "wall-5-terminal",
    )
    wall_sources = {key: _crop(sheet, key) for key in wall_keys}
    material_sources = {
        key: _cluster_quiet_material_source(
            _extract_material_source(material_sheet, key)
        )
        for key in MATERIAL_SOURCE_CROPS
    }
    outputs: dict[str, Image.Image] = {}
    service_masters: dict[str, Image.Image] = {}
    for direction in DIRECTIONS:
        conformed = {
            key: _conform_wall(source, key, direction)
            for key, source in wall_sources.items()
        }
        material_base = _conform_wall(
            material_sources["wall-material-base"],
            "wall-material-base",
            direction,
        )
        outputs[f"env-wall-{direction.name}"] = _apply_quiet_material_body(
            conformed["wall-1-base"],
            material_base,
            direction,
        )
        outputs[f"env-wall-torch-{direction.name}"] = conformed["wall-4-quiet"]
        outputs[f"env-wall-pipes-{direction.name}"] = conformed["wall-3-vent"]
        material_display = _conform_wall(
            material_sources["wall-material-display"],
            "wall-material-display",
            direction,
        )
        outputs[f"env-wall-window-{direction.name}"] = _apply_quiet_material_body(
            conformed["wall-1-base"],
            material_display,
            direction,
        )
        outputs[f"env-wall-cabinet-{direction.name}"] = conformed["wall-5-terminal"]
        if direction.slope < 0:
            service_master = _build_service_master(conformed, direction)
            service_outputs = _split_service_master(service_master, direction)
        else:
            # The approved camera-quarter contract keeps each named segment in
            # the same semantic slot.  Mirror the already joined canonical run
            # cell-by-cell so left/right remain exact variants without reversing
            # hose/quiet/vent ownership.
            service_outputs = {
                f"env-wall-b2-service-segment-{segment}-{direction.name}": outputs[
                    f"env-wall-b2-service-segment-{segment}-{DIRECTIONS[0].name}"
                ].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                for segment in range(3)
            }
            service_master = _assemble_service_master(service_outputs, direction)
        service_masters[direction.name] = service_master
        outputs.update(service_outputs)

    outputs["prop-explosive-barrel"] = _conform_fuel_cell(_crop(sheet, "fuel-cell"))
    return B2PropQualityV4Build(outputs, service_masters)


def build_source_assets() -> B2PropQualityV4Build:
    """Load the checked-in production sheet for canonical writer delegation."""
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not MATERIAL_SOURCE.exists():
        raise FileNotFoundError(MATERIAL_SOURCE)
    with Image.open(SOURCE) as source, Image.open(MATERIAL_SOURCE) as material:
        return build_assets(
            source.convert("RGBA"),
            material.convert("RGBA"),
        )


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


def build_asset_preview(build: B2PropQualityV4Build) -> Image.Image:
    canvas = Image.new("RGBA", (1680, 768), PALETTE["dark-void"])
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((16, 16, 732, 752), fill=PALETTE["ui-panel-solid"])
    draw.rectangle((748, 16, 1164, 752), fill=PALETTE["ui-panel-solid"])
    draw.rectangle((1180, 16, 1664, 752), fill=PALETTE["ui-panel-solid"])
    variants = ("", "window-", "torch-", "pipes-", "cabinet-")
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
        x = 764
        y = 24 + row * 368
        draw.rectangle((x - 4, y - 4, x + 388, y + 356), fill=PALETTE["ui-inset"])
        _paste_scaled(canvas, build.service_masters[direction.name], (x, y), 2)
    draw.rectangle((1224, 156, 1616, 620), fill=PALETTE["ui-inset"])
    _paste_scaled(canvas, build.outputs["prop-explosive-barrel"], (1228, 192), 3)
    draw.rectangle((1228, 596, 1284, 603), fill=PALETTE["sig-hazard"])
    return canvas


def build_v3_v4_comparison(
    v3,
    v4: B2PropQualityV4Build,
) -> Image.Image:
    """Equal-scale enlargement of the three assets most damaged by v3 reduction."""
    canvas = Image.new("RGBA", (1800, 1100), PALETTE["dark-void"])
    draw = ImageDraw.Draw(canvas)
    names = (
        "env-wall-b2-service-segment-0-rising-right",
        "env-wall-cabinet-rising-right",
        "prop-explosive-barrel",
    )
    for column, name in enumerate(names):
        panel_x = column * 600
        draw.rectangle(
            (panel_x + 12, 12, panel_x + 588, 1088),
            fill=PALETTE["ui-panel-solid"],
        )
        scale = 4
        old = v3.outputs[name]
        new = v4.outputs[name]
        old_x = panel_x + 300 - old.width * scale // 2
        new_x = panel_x + 300 - new.width * scale // 2
        old_y = 24 if name != "prop-explosive-barrel" else 8
        new_y = 574 if name != "prop-explosive-barrel" else 558
        _paste_scaled(canvas, old, (old_x, old_y), scale)
        _paste_scaled(canvas, new, (new_x, new_y), scale)
        draw.rectangle(
            (panel_x + 32, 526, panel_x + 568, 533),
            fill=PALETTE["sig-neon-magenta"],
        )
        draw.rectangle(
            (panel_x + 32, 1060, panel_x + 568, 1067),
            fill=PALETTE["sig-teal-item"],
        )
    return canvas


def main() -> None:
    build = build_source_assets()
    with Image.open(SOURCE) as source:
        v3 = build_v3_assets(source.convert("RGBA"))

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build.outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    for direction, image in build.service_masters.items():
        image.save(OUTPUT / f"env-wall-b2-service-master-{direction}.png", optimize=True)
    build_asset_preview(build).save(ASSET_PREVIEW, optimize=True)
    build_v3_v4_comparison(v3, build).save(COMPARISON_PREVIEW, optimize=True)
    print(
        f"wrote {len(build.outputs)} direct-resolution conform candidates, "
        f"{len(build.service_masters)} masters, and previews to {OUTPUT}"
    )


if __name__ == "__main__":
    main()
