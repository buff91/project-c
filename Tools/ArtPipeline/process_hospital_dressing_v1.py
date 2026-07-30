#!/usr/bin/env python3
"""Conform the approved dressing board into runtime sprites.

v0.3.3 아케이드 재발주: 소스가 폐병원 보드에서 아케이드 소품 보드(자판기·네온
간판·홀로 패널)로 교체됐다. 출력 파일명(env-floor-*/env-wall-*)과 슬롯 계약은
구판을 유지한다 — 코드의 hospital* 슬롯명과 같은 이유다.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-arcade-dressing-source-v1.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
BASE_FLOOR = OUTPUT / "env-floor.png"
SHEET_SIZE = (1536, 1024)
CELL_SIZE = (512, 512)
ALPHA_CUTOFF = 80


@dataclass(frozen=True)
class DressingSpec:
    cell_index: int
    size: tuple[int, int]
    output_names: tuple[str, ...]


SPECS = (
    DressingSpec(0, (128, 64), ("env-floor-grate",)),
    DressingSpec(1, (128, 64), ("env-floor-cracked",)),
    DressingSpec(2, (128, 64), ("env-floor-service",)),
    DressingSpec(
        3,
        (64, 112),
        ("env-wall-pipes-rising-right", "env-wall-pipes-rising-left"),
    ),
    DressingSpec(
        4,
        (64, 112),
        ("env-wall-window-rising-right", "env-wall-window-rising-left"),
    ),
    DressingSpec(
        5,
        (64, 112),
        ("env-wall-cabinet-rising-right", "env-wall-cabinet-rising-left"),
    ),
)


def _is_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return red >= 150 and blue >= 130 and green <= 110 and red + blue >= green * 3


def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 3 * CELL_SIZE[0]
    y = index // 3 * CELL_SIZE[1]
    cell = sheet.crop((x, y, x + CELL_SIZE[0], y + CELL_SIZE[1])).convert("RGBA")
    pixels = cell.load()
    for py in range(cell.height):
        for px in range(cell.width):
            if _is_chroma(pixels[px, py]):
                pixels[px, py] = (5, 7, 12, 0)

    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"hospital dressing cell {index} contains no visible pixels")
    return cell.crop(bounds)


# 2026-07-30 정합 패스 — 드레싱이 1차 슬라이스 기본 셀과 갈라진 두 지점을 conform에서 잡는다:
# 벽은 grey-* 램프(청회색)로 잠겨 웜 브라운 기본 벽과 색온도가 어긋났고, 바닥 오버레이는
# 기본 바닥(V≈0.40)보다 밝아 드레싱 타일이 체커보드 얼룩으로 떴다.
WARM_BAND = (40, 235)  # 웜 시프트 적용 명도 대역 — 청보라 암부·순백 하이라이트는 남긴다(.gpl 원리)
WARM_SAT_MAX = 0.28  # 저채도 몸통만 민다 — 스크린·간판 악센트는 제 색을 지킨다
WARM_GAIN = (1.18, 1.00, 0.78)  # grey-* 최근접을 tile-* 최근접으로 뒤집는 실측 최소 게인
FLOOR_VALUE_SCALE = 0.92


def warm_shift_walls(cell: Image.Image) -> Image.Image:
    """벽 몸통(저채도 콘크리트)을 기본 벽이 잠긴 웜 스톤(tile-*) 램프 쪽으로 민다."""
    shifted = cell.copy()
    pixels = shifted.load()
    for py in range(shifted.height):
        for px in range(shifted.width):
            red, green, blue, alpha_value = pixels[px, py]
            if alpha_value == 0:
                continue
            peak = max(red, green, blue)
            if not WARM_BAND[0] <= peak <= WARM_BAND[1]:
                continue
            if peak and (peak - min(red, green, blue)) / peak > WARM_SAT_MAX:
                continue
            pixels[px, py] = (
                min(255, round(red * WARM_GAIN[0])),
                green,
                min(255, round(blue * WARM_GAIN[2])),
                alpha_value,
            )
    return shifted


def dim_floor_overlay(cell: Image.Image) -> Image.Image:
    """바닥 드레싱 오버레이를 기본 바닥 명도대로 눌러 타일 간 얼룩을 없앤다."""
    dimmed = cell.copy()
    pixels = dimmed.load()
    for py in range(dimmed.height):
        for px in range(dimmed.width):
            red, green, blue, alpha_value = pixels[px, py]
            if alpha_value == 0:
                continue
            pixels[px, py] = (
                round(red * FLOOR_VALUE_SCALE),
                round(green * FLOOR_VALUE_SCALE),
                round(blue * FLOOR_VALUE_SCALE),
                alpha_value,
            )
    return dimmed


def build_sprite(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    resized = source.resize(size, Image.Resampling.BOX)
    alpha = resized.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    resized.putalpha(alpha)
    # 잠금 직후 despeckle — 렌더링 문법 계약 §1-d(plan v2): 고립 1px 노이즈 금지.
    return despeckle(lock_rgba_to_palette(resized))


def build_outputs(
    sheet: Image.Image,
    base_floor: Image.Image,
) -> dict[str, Image.Image]:
    if sheet.size != SHEET_SIZE:
        raise ValueError(f"unexpected hospital dressing source size: {sheet.size}")
    if base_floor.size != (128, 64):
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    outputs: dict[str, Image.Image] = {}
    for spec in SPECS:
        cell = extract_cell(sheet, spec.cell_index)
        cell = dim_floor_overlay(cell) if spec.size == (128, 64) else warm_shift_walls(cell)
        sprite = build_sprite(cell, spec.size)
        if spec.size == (128, 64):
            # 생성 소스의 바닥은 장식 주변에 의도적인 빈 공간이 있다. 이 이미지를
            # 기본 타일과 교체하면 빈 공간이 void로 뚫리므로, 승인된 공용 바닥 위에
            # 합성해 완전한 타일 변주로 만든다.
            composed = lock_rgba_to_palette(base_floor.convert("RGBA"))
            composed.alpha_composite(sprite)
            sprite = despeckle(lock_rgba_to_palette(composed))
        for output_index, output_name in enumerate(spec.output_names):
            outputs[output_name] = (
                sprite
                if output_index == 0
                else sprite.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            )
    return outputs


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BASE_FLOOR.exists():
        raise FileNotFoundError(BASE_FLOOR)

    outputs = build_outputs(
        Image.open(SOURCE).convert("RGBA"),
        Image.open(BASE_FLOOR).convert("RGBA"),
    )
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(OUTPUT / f"{name}.png", optimize=True)
    print(f"wrote {len(outputs)} hospital dressing sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
