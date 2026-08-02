#!/usr/bin/env python3
"""Conform the approved B2 barrel-bay board into eight cell-owned floor tiles.

The approved board supplies the same two-cell containment bay in four camera
quarters. Each quadrant is normalized as one 192x96 master, palette-locked and
despeckled before it is split into the service/ring and drain/grate 128x64
cells. Master-first processing preserves the hose and rust channel at the
shared edge while cell ownership keeps FOV, sorting, and floor fades intact.
"""

from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from PIL import Image, ImageChops, ImageEnhance

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-b2-barrel-bay-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
SHEET_SIZE = (1536, 1024)
QUADRANT_SIZE = (768, 512)
MASTER_SIZE = (192, 96)
SPRITE_SIZE = (128, 64)
ALPHA_CUTOFF = 80
PIXEL_CLUSTER = 2
BRIGHTNESS = 0.82


@dataclass(frozen=True)
class ViewSpec:
    index: int
    service_window: tuple[int, int]
    drain_window: tuple[int, int]

    @property
    def windows(self) -> tuple[tuple[int, int], tuple[int, int]]:
        return self.service_window, self.drain_window


VIEWS = (
    ViewSpec(0, (64, 0), (0, 32)),
    ViewSpec(1, (0, 0), (64, 32)),
    ViewSpec(2, (0, 32), (64, 0)),
    ViewSpec(3, (64, 32), (0, 0)),
)


@dataclass(frozen=True)
class BarrelBayBuild:
    masters: dict[int, Image.Image]
    outputs: dict[str, Image.Image]


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def _remove_edge_connected_chroma(
    image: Image.Image,
    predicate: Callable[[tuple[int, int, int, int]], bool],
) -> None:
    """Clear only exterior magenta so small authored signal colors survive."""
    width, height = image.size
    pixels = image.load()
    visited = bytearray(width * height)
    pending: deque[tuple[int, int]] = deque()

    def enqueue(px: int, py: int) -> None:
        index = py * width + px
        if visited[index]:
            return
        pixel = pixels[px, py]
        if pixel[3] != 0 and not predicate(pixel):
            return
        visited[index] = 1
        pending.append((px, py))

    for px in range(width):
        enqueue(px, 0)
        enqueue(px, height - 1)
    for py in range(1, height - 1):
        enqueue(0, py)
        enqueue(width - 1, py)

    while pending:
        px, py = pending.popleft()
        red, green, blue, alpha = pixels[px, py]
        if alpha != 0 and predicate((red, green, blue, alpha)):
            pixels[px, py] = (red, green, blue, 0)
        if px > 0:
            enqueue(px - 1, py)
        if px + 1 < width:
            enqueue(px + 1, py)
        if py > 0:
            enqueue(px, py - 1)
        if py + 1 < height:
            enqueue(px, py + 1)


def _harden_alpha(image: Image.Image) -> Image.Image:
    hardened = image.convert("RGBA")
    hardened.putalpha(
        hardened.getchannel("A").point(
            lambda value: 255 if value >= ALPHA_CUTOFF else 0
        )
    )
    return hardened


def extract_view(sheet: Image.Image, view: int) -> Image.Image:
    """Remove the chroma field and crop one approved 2-cell view to its bounds."""
    if view < 0 or view > 3:
        raise ValueError(f"invalid B2 barrel-bay view: {view}")
    column = view % 2
    row = view // 2
    left = column * QUADRANT_SIZE[0]
    top = row * QUADRANT_SIZE[1]
    candidate = sheet.crop(
        (left, top, left + QUADRANT_SIZE[0], top + QUADRANT_SIZE[1])
    ).convert("RGBA")
    _remove_edge_connected_chroma(candidate, _is_chroma)
    candidate = _harden_alpha(candidate)
    bounds = candidate.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"B2 barrel-bay view {view} is empty")
    return candidate.crop(bounds)


def _base_master(base_floor: Image.Image, spec: ViewSpec) -> Image.Image:
    base_floor = _harden_alpha(base_floor)
    if base_floor.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")
    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    for left, top in spec.windows:
        master.alpha_composite(base_floor, (left, top))
    return master


def normalize_view(
    source: Image.Image,
    base_floor: Image.Image,
    spec: ViewSpec,
) -> Image.Image:
    """Fit one generated pair to the exact two-diamond master contract."""
    fitted = source.resize(MASTER_SIZE, Image.Resampling.BOX)
    fitted = ImageEnhance.Brightness(fitted).enhance(BRIGHTNESS)
    fitted = _harden_alpha(fitted)

    base = _base_master(base_floor, spec)
    union_alpha = base.getchannel("A")
    fitted.putalpha(ImageChops.multiply(fitted.getchannel("A"), union_alpha))
    base.alpha_composite(fitted)

    small = base.resize(
        (MASTER_SIZE[0] // PIXEL_CLUSTER, MASTER_SIZE[1] // PIXEL_CLUSTER),
        Image.Resampling.BOX,
    )
    small = despeckle(lock_rgba_to_palette(_harden_alpha(small)))
    master = small.resize(MASTER_SIZE, Image.Resampling.NEAREST)
    master.putalpha(ImageChops.multiply(master.getchannel("A"), union_alpha))
    return _harden_alpha(master)


def _output_name(segment: int, view: int) -> str:
    role = "service" if segment == 0 else "drain"
    return f"env-floor-b2-barrel-bay-{role}-view-{view}"


def split_master(
    master: Image.Image,
    base_floor: Image.Image,
    spec: ViewSpec,
) -> dict[str, Image.Image]:
    base_alpha = _harden_alpha(base_floor).getchannel("A")
    outputs: dict[str, Image.Image] = {}
    for segment, (left, top) in enumerate(spec.windows):
        cell = master.crop(
            (left, top, left + SPRITE_SIZE[0], top + SPRITE_SIZE[1])
        )
        cell.putalpha(ImageChops.multiply(cell.getchannel("A"), base_alpha))
        outputs[_output_name(segment, spec.index)] = _harden_alpha(cell)
    return outputs


def reassemble_outputs(
    outputs: dict[str, Image.Image],
    spec: ViewSpec,
) -> Image.Image:
    master = Image.new("RGBA", MASTER_SIZE, (5, 7, 12, 0))
    for segment, (left, top) in enumerate(spec.windows):
        master.alpha_composite(outputs[_output_name(segment, spec.index)], (left, top))
    return master


def build_assets(sheet: Image.Image, base_floor: Image.Image) -> BarrelBayBuild:
    if sheet.size != SHEET_SIZE:
        raise ValueError(f"unexpected B2 barrel-bay source size: {sheet.size}")
    if base_floor.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    masters: dict[int, Image.Image] = {}
    outputs: dict[str, Image.Image] = {}
    for spec in VIEWS:
        processed_master = normalize_view(
            extract_view(sheet, spec.index), base_floor, spec
        )
        view_outputs = split_master(processed_master, base_floor, spec)
        outputs.update(view_outputs)
        # Keep the pre-split master so tests can detect any visible pixel lost
        # while the two overlapping cell canvases are masked and reassembled.
        masters[spec.index] = processed_master
    return BarrelBayBuild(masters, outputs)


def _preview(build: BarrelBayBuild) -> Image.Image:
    preview = Image.new("RGBA", (MASTER_SIZE[0] * 2, MASTER_SIZE[1] * 2), (5, 7, 12, 255))
    for spec in VIEWS:
        left = (spec.index % 2) * MASTER_SIZE[0]
        top = (spec.index // 2) * MASTER_SIZE[1]
        preview.alpha_composite(build.masters[spec.index], (left, top))
    return preview.resize(
        (preview.width * 4, preview.height * 4),
        Image.Resampling.NEAREST,
    )


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    build = build_assets(
        Image.open(SOURCE).convert("RGBA"),
        Image.open(BASE_FLOOR).convert("RGBA"),
    )
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build.outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    _preview(build).save(
        ROOT / "docs/captures/b2-barrel-bay-conform-preview-v1.png",
        optimize=True,
    )
    print(f"wrote {len(build.outputs)} B2 barrel-bay floor sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
