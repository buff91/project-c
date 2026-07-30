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
        sprite = build_sprite(extract_cell(sheet, spec.cell_index), spec.size)
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
