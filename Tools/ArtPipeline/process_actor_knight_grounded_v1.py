#!/usr/bin/env python3
"""Conform the approved grounded expeditioner source into one 96x128 idle sprite.

This pass intentionally publishes only a static Frame_0.  The current automatic
multi-frame draft does not meet the actor anatomy/foot-lock gate, so animation
remains disabled until a hand-authored directional timeline is approved.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image

from torchstone_palette import load_gpl_entries


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-expeditioner-grounded-source-v1.png"
RUNTIME_OUTPUT = ROOT / "Assets/_Project/Art/Runtime/actor-knight.png"
PREVIEW = ROOT / "docs/captures/actor-knight-grounded-conform-preview-v1.png"

CANVAS_SIZE = (96, 128)
VISIBLE_MAX = (82, 116)
GROUND_ROW = 123
ALPHA_CUTOFF = 80
PREVIEW_SCALE = 6
PIXEL_CLUSTER = 2

# One actor must not sample the whole project/UI palette.  These 24 named
# Torchstone entries are the material ramps this field medic actually uses.
ACTOR_PALETTE_NAMES = (
    "dark-void",
    "dark-cool",
    "dark-warm",
    "grey-1",
    "grey-2",
    "grey-3",
    "grey-4",
    "grey-5",
    "grey-6",
    "rust-1",
    "rust-2",
    "rust-3",
    "fabric-1",
    "fabric-2",
    "fabric-3",
    "fabric-4",
    "fabric-5",
    "sig-warning",
    "sig-warning-deep",
    "skin-1",
    "skin-2",
    "skin-3",
    "hair-blonde-1",
    "hair-blonde-2",
)


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    """Match generated exterior magenta while preserving authored warm accents."""
    red, green, blue, _ = pixel
    return red >= 180 and blue >= 170 and green <= 105 and red + blue >= green * 4


def _remove_chroma(image: Image.Image) -> None:
    """Clear the flat key field, including gaps enclosed by the silhouette.

    This source contract has no authored magenta.  Limiting removal to the
    exterior leaves trapped key pixels between an arm, pouch, and torso, which
    read as a false neon costume accent after palette locking.
    """
    pixels = image.load()
    for py in range(image.height):
        for px in range(image.width):
            red, green, blue, alpha = pixels[px, py]
            if alpha != 0 and _is_chroma((red, green, blue, alpha)):
                pixels[px, py] = (red, green, blue, 0)


def _harden_alpha(image: Image.Image) -> Image.Image:
    result = image.convert("RGBA")
    result.putalpha(
        result.getchannel("A").point(
            lambda value: 255 if value >= ALPHA_CUTOFF else 0
        )
    )
    return result


def _lock_actor_palette(image: Image.Image) -> Image.Image:
    entries = dict(load_gpl_entries())
    missing = [name for name in ACTOR_PALETTE_NAMES if name not in entries]
    if missing:
        raise ValueError(f"grounded actor palette entries are missing: {missing}")
    colors = [entries[name] for name in ACTOR_PALETTE_NAMES]
    palette = Image.new("P", (1, 1))
    flat = [channel for color in colors for channel in color]
    flat += list(colors[0]) * (256 - len(colors))
    palette.putpalette(flat)

    source = image.convert("RGBA")
    alpha = source.getchannel("A")
    rgb = Image.new("RGB", source.size, colors[0])
    rgb.paste(source, mask=alpha)
    locked = rgb.quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")
    locked.putalpha(alpha)
    return locked


def extract_subject(source: Image.Image) -> Image.Image:
    subject = source.convert("RGBA")
    _remove_chroma(subject)
    subject = _harden_alpha(subject)
    bounds = subject.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("grounded expeditioner source contains no visible subject")
    return subject.crop(bounds)


def build_actor(source: Image.Image) -> Image.Image:
    subject = extract_subject(source)
    working_max = (
        VISIBLE_MAX[0] // PIXEL_CLUSTER,
        VISIBLE_MAX[1] // PIXEL_CLUSTER,
    )
    scale = min(
        working_max[0] / subject.width,
        working_max[1] / subject.height,
    )
    size = (
        max(1, round(subject.width * scale)),
        max(1, round(subject.height * scale)),
    )
    subject = _harden_alpha(subject.resize(size, Image.Resampling.BOX))
    subject = _lock_actor_palette(subject)
    subject = _harden_alpha(subject)

    working_canvas = (
        CANVAS_SIZE[0] // PIXEL_CLUSTER,
        CANVAS_SIZE[1] // PIXEL_CLUSTER,
    )
    canvas = Image.new("RGBA", working_canvas, (0, 0, 0, 0))
    left = (working_canvas[0] - subject.width) // 2
    ground_row = GROUND_ROW // PIXEL_CLUSTER
    top = ground_row - subject.height + 1
    if top < 0:
        raise ValueError(f"grounded expeditioner exceeds actor canvas: {subject.size}")
    canvas.alpha_composite(subject, (left, top))
    return canvas.resize(CANVAS_SIZE, Image.Resampling.NEAREST)


def build_preview(actor: Image.Image) -> Image.Image:
    """Nearest-neighbor gameplay-scale inspection on the project void color."""
    width = CANVAS_SIZE[0] * PREVIEW_SCALE
    height = CANVAS_SIZE[1] * PREVIEW_SCALE
    preview = Image.new("RGBA", (width, height), (5, 7, 12, 255))
    enlarged = actor.resize((width, height), Image.Resampling.NEAREST)
    preview.alpha_composite(enlarged)
    return preview.convert("RGB")


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    actor = build_actor(Image.open(SOURCE).convert("RGBA"))
    RUNTIME_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    actor.save(RUNTIME_OUTPUT, optimize=True)
    build_preview(actor).save(PREVIEW, optimize=True)
    print(f"wrote grounded actor: {RUNTIME_OUTPUT}")
    print(f"wrote preview: {PREVIEW}")


if __name__ == "__main__":
    main()
