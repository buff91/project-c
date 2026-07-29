#!/usr/bin/env python3
"""Conform generated PC UI backdrops to Project-C's runtime pixel regime."""

from pathlib import Path

from PIL import Image, ImageOps

from torchstone_palette import lock_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-main-menu-backdrop-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime/ui-main-menu-backdrop.png"
WORKING_SIZE = (480, 270)
RUNTIME_SIZE = (960, 540)


def build_main_menu_backdrop(source: Image.Image) -> Image.Image:
    """Crop to 16:9, reduce detail, lock palette, then preserve chunky pixels."""
    fitted = ImageOps.fit(
        source.convert("RGB"),
        WORKING_SIZE,
        method=Image.Resampling.BOX,
        centering=(0.5, 0.5),
    )
    locked = lock_to_palette(fitted)
    return locked.resize(RUNTIME_SIZE, Image.Resampling.NEAREST)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(SOURCE) as source:
        build_main_menu_backdrop(source).save(OUTPUT, optimize=True)
    print(f"wrote palette-locked main-menu backdrop to {OUTPUT}")


if __name__ == "__main__":
    main()
