#!/usr/bin/env python3
"""Generate Torchstone 9-slice UI sprites (window frame, gold glow, gauge tick).

Promotes the design-system components that USS cannot express with borders alone
(cut corners, gold glow, gauge segment ticks) into dot 9-slice sprites. Wired in
``DesignSystem.uss`` §9-slice; verified in ``DesignSystemGallery`` inside Unity.
All colours come from the shared Torchstone palette.
"""
from pathlib import Path

from PIL import Image, ImageDraw

from torchstone_palette import lock_rgba_to_palette

OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Art/Runtime"

VOID = (5, 7, 12, 255)
PANEL = (10, 13, 19, 255)
# v1.8: 웜 토프 → 쿨 스틸. 값은 DesignSystem.uss 의 --pc-stone* 와 같다.
S_DIM = (44, 49, 56, 255); STONE = (84, 91, 97, 255); S_LIT = (223, 231, 242, 255)
# UI 크롬 액센트 — .gpl sig-neon-magenta. 월드 네온이 아니라 화면 크롬 전용이다.
ACCENT = (230, 68, 184, 255)
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


VIG = 96   # vignette canvas (slice 24 -> 48px stretchable centre)
VIG_SLICE = 24


def vignette():
    """Screen-space edge darkening as a 9-slice with a fully transparent centre.

    Deliberately NOT a URP post-process Vignette override. Three reasons:
    * a smooth radial gradient over 1-bit pixel art reads as a shader effect
      bolted onto a sprite game -- it fights the dither language, it doesn't join it;
    * the scene clear colour is already ``--pc-void`` (#05070C), so a multiply
      vignette has almost nothing left to darken -- the whole visible effect lands
      on lit room edges, which a sprite produces far more cheaply;
    * it would need ``m_RenderPostProcessing`` plus a Volume in each of three
      scenes and a full-screen blit.

    Only the transparent centre stretches, so the dither dots keep their size at
    every panel scale -- the same trick ``.pc-window`` already uses.
    """
    im = Image.new("RGBA", (VIG, VIG), (0, 0, 0, 0))
    px = im.load()
    peak = 0.70          # max alpha at the very corner
    reach = VIG_SLICE    # darkening depth, in px, from each edge

    for y in range(VIG):
        for x in range(VIG):
            # Distance inward from the nearest horizontal / vertical edge.
            dx = min(x, VIG - 1 - x)
            dy = min(y, VIG - 1 - y)
            if dx >= reach and dy >= reach:
                continue
            # Combine both axes so corners go darkest, edges less so.
            fx = max(0.0, 1.0 - dx / reach)
            fy = max(0.0, 1.0 - dy / reach)
            falloff = 1.0 - (1.0 - fx) * (1.0 - fy)
            a = peak * falloff * falloff        # squared = softer shoulder
            if a <= 0.0:
                continue
            # 2x2 ordered dither so the ramp stays dotted instead of smooth.
            threshold = ((x & 1) * 2 + (y & 1) + 0.5) / 4.0
            quantised = int(a * 255)
            if (a * 255 - quantised) > threshold:
                quantised += 1
            if quantised <= 0:
                continue
            px[x, y] = (VOID[0], VOID[1], VOID[2], min(255, quantised))
    return im



BR = 12   # bracket tile canvas (slice 5 -> 2px stretchable centre)
BR_SLICE = 5
BR_LEN = 4    # 모서리에서 뻗는 길이


def bracket_frame():
    """모서리만 그리는 크롬 프레임 — 사각 테두리 대신 계기처럼 읽힌다.

    UI Toolkit USS 에는 ``::before``/``::after`` 가 없어서 코너 브래킷을 의사요소로
    만들 수 없다. 그래서 이 프로젝트가 이미 쓰는 방식(9-slice 승격)을 따른다 —
    가장자리 슬라이스만 늘어나고 **모서리 타일은 크기를 유지**하므로, 패널이 아무리
    커져도 브래킷 길이가 그대로다. 늘어나는 구간은 비워 둬서 변이 그려지지 않는다.
    """
    im = Image.new("RGBA", (BR, BR), (0, 0, 0, 0))
    px = im.load()
    hairline = (ACCENT[0], ACCENT[1], ACCENT[2], 76)   # 상단 헤어라인 30%
    for y in range(BR):
        for x in range(BR):
            on_left, on_right = x == 0, x == BR - 1
            on_top, on_bottom = y == 0, y == BR - 1
            if not (on_left or on_right or on_top or on_bottom):
                continue
            near_x = min(x, BR - 1 - x) < BR_LEN
            near_y = min(y, BR - 1 - y) < BR_LEN
            if near_x and near_y:
                px[x, y] = ACCENT          # 네 모서리만 진하게
            elif on_top:
                px[x, y] = hairline        # 위 변만 옅은 헤어라인(판금 베벨)
    return im

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    lock_rgba_to_palette(bracket_frame()).save(OUT / "ui-bracket-frame.png")
    lock_rgba_to_palette(window_frame(S_LIT, STONE, S_DIM)).save(
        OUT / "ui-window-frame.png"
    )
    lock_rgba_to_palette(window_frame(ICE, TEAL, TEAL_BG)).save(
        OUT / "ui-window-frame-teal.png"
    )
    lock_rgba_to_palette(glow_frame()).save(OUT / "ui-glow-frame.png")
    lock_rgba_to_palette(gauge_tick()).save(OUT / "ui-gauge-tick.png")
    # 비네트는 팔레트 잠금을 하지 않는다 — 알파 램프가 본체인데 lock 은 RGB 를 인덱스로
    # 스냅하면서 알파 계조를 뭉갠다. RGB 는 이미 --pc-void 단색이라 잠글 것도 없다.
    vignette().save(OUT / "ui-vignette.png")
    print(f"wrote 5 Torchstone 9-slice UI sprites to {OUT}")


if __name__ == "__main__":
    main()
