#!/usr/bin/env python3
"""Build the Torchstone UI action icon set from its generated source sheet."""

import colorsys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-torchstone-ui-icons-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
CELL_SIZE = 418
CANVAS_SIZE = (32, 32)
VISIBLE_SIZE = (30, 30)
ALPHA_CUTOFF = 80

@dataclass(frozen=True)
class IconSpec:
    source_name: str
    cell_index: int
    output_name: str


SPECS = (
    IconSpec("settings", 0, "ui-settings"),
    IconSpec("menu", 1, "ui-menu"),
    IconSpec("rotate-left", 2, "ui-rotate-left"),
    IconSpec("rotate-right", 3, "ui-rotate-right"),
    IconSpec("backpack", 4, "ui-backpack"),
    IconSpec("wait", 5, "ui-wait"),
    IconSpec("melee", 6, "ui-melee"),
    IconSpec("ranged", 7, "ui-ranged"),
    IconSpec("interact", 8, "ui-interact"),
)


def strip_chroma_spill(cell: Image.Image) -> None:
    pixels = cell.load()
    for y in range(cell.height):
        for x in range(cell.width):
            red, green, blue, alpha = pixels[x, y]
            is_magenta_spill = (
                red > 70
                and blue > 50
                and red + blue > green * 2 + 80
            )
            if is_magenta_spill:
                pixels[x, y] = (red, green, blue, 0)



# --- 웜 → 쿨 리맵 (v1.8) -------------------------------------------------
# UI 계조가 판타지 시절 웜 토프/크림이라 청흑 바탕 + 네온과 색온도가 부딪쳤다
# (docs/UI_DESIGN_SYSTEM.md v1.8). 소스 시트는 웜이라 팔레트만 갈아서는 안 바뀐다.
#
# ⚠ **일괄 시프트는 하지 않는다.** process_hospital_dressing_v1 의 주석이 남긴 교훈이다 —
#    과거 WARM_GAIN 전역 시프트가 모든 패널을 웜 브라운으로 밀어 네온 시설을 일반 폐허로
#    만들었다. 여기서는 **휴 대역 + 채도로 게이트**해서 무채색 금속/석재 계조만 옮기고,
#    신호색(골드·토치·틸·HP·러스트)은 손대지 않는다.
#
# 값(명도)은 보존한다 — 셰이딩 구조를 유지해야 도트 형태가 안 무너진다.

WARM_HUE_MIN = 15.0 / 360.0   # 붉은 주황 아래는 HP·러스트 영역이라 제외
WARM_HUE_MAX = 70.0 / 360.0   # 노랑 위는 xp 그린 영역
WARM_SAT_MAX = 0.38           # 이 위는 골드(0.67)·토치(0.75) 같은 신호색이다

# .gpl 의 쿨 램프. (명도, RGB) 오름차순 — grey-1/2/3/4/5 + ui-text-cool.
COOL_RAMP = (
    (44, 49, 56),
    (59, 63, 69),
    (84, 91, 97),
    (107, 113, 120),
    (148, 155, 161),
    (223, 231, 242),
)


def _cool_for_value(value: float) -> tuple[int, int, int]:
    """명도 0..1 을 쿨 램프 위의 색으로 보간한다."""
    if value <= 0.0:
        return COOL_RAMP[0]
    if value >= 1.0:
        return COOL_RAMP[-1]
    span = len(COOL_RAMP) - 1
    pos = value * span
    low = min(int(pos), span - 1)
    frac = pos - low
    a, b = COOL_RAMP[low], COOL_RAMP[low + 1]
    return tuple(round(a[i] + (b[i] - a[i]) * frac) for i in range(3))


def is_warm_neutral(red: int, green: int, blue: int) -> bool:
    """웜 대역의 저채도 픽셀(석재·크림 계조)인가 — 신호색은 False."""
    hue, _light, sat = colorsys.rgb_to_hls(red / 255, green / 255, blue / 255)
    if sat > WARM_SAT_MAX:
        return False
    return WARM_HUE_MIN <= hue <= WARM_HUE_MAX


def cool_shift(cell: Image.Image) -> Image.Image:
    """웜 무채 계조만 쿨 램프로 옮긴다. 명도 보존, 신호색 불변."""
    shifted = cell.copy()
    pixels = shifted.load()
    for y in range(shifted.height):
        for x in range(shifted.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha == 0 or not is_warm_neutral(red, green, blue):
                continue
            value = max(red, green, blue) / 255
            new_red, new_green, new_blue = _cool_for_value(value)
            pixels[x, y] = (new_red, new_green, new_blue, alpha)
    return shifted

def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 3 * CELL_SIZE
    y = index // 3 * CELL_SIZE
    cell = sheet.crop((x, y, x + CELL_SIZE, y + CELL_SIZE)).convert("RGBA")
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"source cell {index} contains no visible pixels")
    return cell.crop(bounds)


def build_icon(source: Image.Image) -> Image.Image:
    scale = min(
        VISIBLE_SIZE[0] / source.width,
        VISIBLE_SIZE[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    # 리맵은 축소 **전**에 한다 — 축소 뒤에는 웜/쿨이 섞인 중간 픽셀이 생겨
    # 휴 게이트를 통과하지 못하고 얼룩으로 남는다.
    shifted = cool_shift(source)
    sprite = lock_rgba_to_palette(shifted.resize(size, Image.Resampling.BOX))
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - sprite.width) // 2
    y = (CANVAS_SIZE[1] - sprite.height) // 2
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (CELL_SIZE * 3, CELL_SIZE * 3):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for spec in SPECS:
        icon = build_icon(extract_cell(sheet, spec.cell_index))
        icon.save(OUTPUT / f"{spec.output_name}.png", optimize=True)

    print(f"wrote {len(SPECS)} Torchstone UI icons to {OUTPUT}")


if __name__ == "__main__":
    main()
