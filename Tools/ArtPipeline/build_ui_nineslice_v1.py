#!/usr/bin/env python3
"""Generate Torchstone 9-slice UI sprites (window frame, gold glow, gauge tick).

Promotes the design-system components that USS cannot express with borders alone
(cut corners, gold glow, gauge segment ticks) into dot 9-slice sprites. Wired in
``DesignSystem.uss`` §9-slice; verified in ``DesignSystemGallery`` inside Unity.
All colours come from the shared Torchstone palette.
"""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Art/Runtime"

VOID = (5, 7, 12, 255)
PANEL = (10, 13, 19, 255)
S_DIM = (74, 64, 56, 255); STONE = (152, 134, 111, 255); S_LIT = (207, 192, 174, 255)
ICE = (154, 223, 232, 255); TEAL = (79, 167, 160, 255); TEAL_BG = (20, 52, 58, 255)
GOLD = (255, 213, 84, 255); TORCH = (255, 189, 65, 255); GOLD_D = (154, 107, 34, 255)

S = 32   # frame canvas
CH = 6   # corner chamfer


def _in_oct(x, y, inset, cham):
    lo, hi = inset, S - 1 - inset
    if not (lo <= x <= hi and lo <= y <= hi):
        return False
    return (
        (x - lo) + (y - lo) >= cham and (hi - x) + (y - lo) >= cham
        and (x - lo) + (hi - y) >= cham and (hi - x) + (hi - y) >= cham
    )


def window_frame(lit, mid, dim):
    """Chamfered bevel frame with a baked panel fill (→ true cut corners)."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    px = im.load()
    for y in range(S):
        for x in range(S):
            if not _in_oct(x, y, 0, CH):
                continue
            if _in_oct(x, y, 3, CH - 3):
                px[x, y] = PANEL
                continue
            top_left = min(x, y) <= min(S - 1 - x, S - 1 - y)
            if not _in_oct(x, y, 1, CH - 1):
                px[x, y] = VOID
            elif not _in_oct(x, y, 2, CH - 2):
                px[x, y] = lit if top_left else dim
            else:
                px[x, y] = mid if top_left else dim
    return im


def glow_frame():
    """Bright gold frame with a soft outer amber halo, transparent centre."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    px = im.load()
    for y in range(S):
        for x in range(S):
            if _in_oct(x, y, 4, CH - 4):
                continue
            if _in_oct(x, y, 0, CH):
                if not _in_oct(x, y, 1, CH - 1):
                    px[x, y] = TORCH
                elif not _in_oct(x, y, 2, CH - 2):
                    px[x, y] = GOLD
                else:
                    px[x, y] = GOLD_D
            else:
                for d in range(1, 4):
                    if _in_oct(x, y, -d, CH):
                        px[x, y] = (TORCH[0], TORCH[1], TORCH[2], int(150 / d))
                        break
    return im


def gauge_tick():
    """8x12 segment-divider tile, repeated horizontally over a gauge fill."""
    im = Image.new("RGBA", (8, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([(0, 1), (0, 10)], fill=(5, 7, 12, 210))
    d.line([(1, 1), (1, 10)], fill=(74, 64, 56, 90))
    return im


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    window_frame(S_LIT, STONE, S_DIM).save(OUT / "ui-window-frame.png")
    window_frame(ICE, TEAL, TEAL_BG).save(OUT / "ui-window-frame-teal.png")
    glow_frame().save(OUT / "ui-glow-frame.png")
    gauge_tick().save(OUT / "ui-gauge-tick.png")
    print(f"wrote 4 Torchstone 9-slice UI sprites to {OUT}")


if __name__ == "__main__":
    main()
