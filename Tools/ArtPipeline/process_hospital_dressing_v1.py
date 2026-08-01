#!/usr/bin/env python3
"""Conform the approved dressing board into runtime sprites.

v0.3.3 아케이드 재발주: 소스가 폐병원 보드에서 아케이드 소품 보드(자판기·네온
간판·홀로 패널)로 교체됐다. 출력 파일명(env-floor-*/env-wall-*)과 슬롯 계약은
구판을 유지한다 — 코드의 hospital* 슬롯명과 같은 이유다.
"""

from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

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


def _remove_edge_connected_chroma(
    image: Image.Image,
    predicate: Callable[[tuple[int, int, int, int]], bool],
) -> None:
    """Remove only chroma reachable from the exterior background.

    Transparent pixels are traversable so a detached fringe around the subject is still
    considered exterior. Opaque non-chroma pixels stop the fill, preserving cyan and
    magenta lights enclosed by the wall silhouette.
    """
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
        red, green, blue, alpha_value = pixels[px, py]
        if alpha_value != 0 and predicate((red, green, blue, alpha_value)):
            pixels[px, py] = (red, green, blue, 0)

        if px > 0:
            enqueue(px - 1, py)
        if px + 1 < width:
            enqueue(px + 1, py)
        if py > 0:
            enqueue(px, py - 1)
        if py + 1 < height:
            enqueue(px, py + 1)


def extract_cell(sheet: Image.Image, index: int) -> Image.Image:
    x = index % 3 * CELL_SIZE[0]
    y = index // 3 * CELL_SIZE[1]
    cell = sheet.crop((x, y, x + CELL_SIZE[0], y + CELL_SIZE[1])).convert("RGBA")
    _remove_edge_connected_chroma(cell, _is_chroma)

    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"hospital dressing cell {index} contains no visible pixels")
    return cell.crop(bounds)


# 바닥 오버레이만 기본 바닥 명도에 맞춘다. 벽 몸통은 생성 소스의 중립/쿨 grey-* 램프를
# 그대로 팔레트 잠금해야 한다. 과거 WARM_GAIN 시프트는 모든 아케이드 패널을 웜 브라운으로
# 밀어 네온 시설이 일반 산업 폐허처럼 보이게 만들었다.
FLOOR_VALUE_SCALE = 0.92


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
        if spec.size == (128, 64):
            cell = dim_floor_overlay(cell)
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
