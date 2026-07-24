#!/usr/bin/env python3
"""Lock the code-drawn item sprites to the shared Torchstone palette.

Items are NOT part of the collapsed-transit style-transfer batch — they are
hand-authored at final resolution by ``generate_runtime_art_v2.py`` with its own
``P`` palette, so there is no ``reduce_colors`` to swap. This idempotent pass
re-quantizes the existing ``item-*.png`` to ``project-c-torchstone.gpl`` (original
alpha preserved) so items share the same indices as environment/actors/props/UI.

Run after any item regen. It touches ONLY ``item-*.png`` — never actors/props,
which are owned by the postapoc processors.
"""

from pathlib import Path

from PIL import Image

from torchstone_palette import lock_to_palette

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Assets/_Project/Art/Runtime"
VOID = (5, 7, 12)


def lock_item(path: Path) -> None:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    rgb = Image.new("RGB", image.size, VOID)
    rgb.paste(image, mask=alpha)
    locked = lock_to_palette(rgb).convert("RGBA")
    locked.putalpha(alpha)  # keep original edges, lock colour only
    locked.save(path, optimize=True)


def main() -> None:
    items = sorted(RUNTIME.glob("item-*.png"))
    if not items:
        raise FileNotFoundError(f"no item-*.png under {RUNTIME}")
    for path in items:
        lock_item(path)
    print(f"locked {len(items)} item sprites to the Torchstone palette")


if __name__ == "__main__":
    main()
