#!/usr/bin/env python3
"""Shared Torchstone palette lock for the art conform pipeline.

All postapoc source sheets are style-transfer generations. Historically each
processor reduced colors with an independent MEDIANCUT-32 quantize, so every
sheet drifted to its own palette and the assets did not cohere. Locking every
sprite to the shared master palette (``project-c-torchstone.gpl`` == the
``DesignSystem.uss`` tokens) is what binds environment/props/actors/items and
the screen-space UI into one style.

Usage (inside a processor's ``reduce_colors``)::

    from torchstone_palette import lock_to_palette
    reduced = lock_to_palette(rgb).convert("RGBA")
"""

from functools import lru_cache
from pathlib import Path

from PIL import Image

GPL_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl"
)


@lru_cache(maxsize=1)
def load_gpl(gpl_path: Path = GPL_PATH) -> tuple[tuple[int, int, int], ...]:
    """Parse a GIMP ``.gpl`` into an ordered RGB tuple (<=256 colors).

    Color lines start with a digit (``R G B<TAB>name``); GIMP/Name/Columns and
    ``#`` comment lines are skipped.
    """
    colors: list[tuple[int, int, int]] = []
    for line in gpl_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or not stripped[0].isdigit():
            continue
        red, green, blue = (int(value) for value in stripped.split()[:3])
        colors.append((red, green, blue))
    if not colors:
        raise ValueError(f"no colors parsed from {gpl_path}")
    if len(colors) > 256:
        raise ValueError(f"{gpl_path} has {len(colors)} colors (max 256 for a P palette)")
    return tuple(colors)


@lru_cache(maxsize=1)
def _palette_image(gpl_path: Path = GPL_PATH) -> Image.Image:
    colors = load_gpl(gpl_path)
    palette = Image.new("P", (1, 1))
    flat = [channel for rgb in colors for channel in rgb]
    flat += [0] * (768 - len(flat))  # pad to 256 * 3
    palette.putpalette(flat)
    return palette


def lock_to_palette(rgb: Image.Image) -> Image.Image:
    """Quantize an RGB image to the fixed Torchstone palette (no dither).

    Replaces per-sheet ``quantize(colors=N, method=MEDIANCUT)`` so every asset
    shares the same indices. Alpha handling stays in the caller.
    """
    if rgb.mode != "RGB":
        rgb = rgb.convert("RGB")
    return rgb.quantize(palette=_palette_image(), dither=Image.Dither.NONE).convert("RGB")


if __name__ == "__main__":
    parsed = load_gpl()
    print(f"{len(parsed)} colors loaded from {GPL_PATH.name}")
