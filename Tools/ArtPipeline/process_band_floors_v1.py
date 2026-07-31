#!/usr/bin/env python3
"""Conform the adopted depth-band floor board into the six band floor sprites.

플랜 v2 배치 1-1 — 소스는 `environment-band-floors-v1` 레시피 채택본
(`project-c-band-floors-source-v1.png`, 1536×1024 · 3×2 셀 512px).
열 = mid/deep/boss, 행 = 기본/raised(얕은 전면 립). 정식 파일명으로 저장하면
`ProjectCArtImporter`/카탈로그가 자동 연결하고 절차 BandOverlay 는 꺼진다
(`BandFloorFallsBackToShared`). §1-c: 석재 기본색은 깊이와 무관해야 하므로
바탕색을 env-floor 에 맞추는 명도 정합 게이트를 함께 둔다.
"""

from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import despeckle, lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/art-direction/project-c-band-floors-source-v1.png"
BASE_FLOOR = ROOT / "Assets/_Project/Art/Environment/env-floor.png"
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
SHEET_SIZE = (1536, 1024)
CELL_SIZE = (512, 512)
SPRITE_SIZE = (128, 64)
ALPHA_CUTOFF = 80
# §1-c 게이트 — 밴드 바닥의 가시 평균 밝기가 기본 바닥에서 이 이상 벗어나면 실패한다.
BASE_VALUE_TOLERANCE = 0.08


@dataclass(frozen=True)
class BandSpec:
    cell_index: int
    output_name: str


SPECS = (
    BandSpec(0, "env-floor-mid"),
    BandSpec(1, "env-floor-deep"),
    BandSpec(2, "env-floor-boss"),
    BandSpec(3, "env-floor-mid-raised"),
    BandSpec(4, "env-floor-deep-raised"),
    BandSpec(5, "env-floor-boss-raised"),
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
        raise ValueError(f"band floor cell {index} contains no visible pixels")
    return cell.crop(bounds)


# 웜 가드 — 생성이 석재색을 한색(세이지/블루 그레이)으로 끌고 가는 드리프트를 conform에서
# 결정론적으로 되돌린다(드레싱 정합 패스와 같은 방식). 저채도 몸통만 밀고 신호 악센트
# (hazard 앰버·틸 심)와 청보라 암부는 남긴다.
WARM_BAND = (40, 235)
WARM_SAT_MAX = 0.28
WARM_GAIN = (1.12, 1.00, 0.86)


def warm_guard(cell: Image.Image) -> Image.Image:
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


# 틸 억제 — 틸은 Hole/출구 신호색 예약이라 밴드 바닥에서는 boss 의 "이상 심" 한 곳에만
# 허용한다(§1-c). 생성이 mid/deep 에 흘린 틸 계열(물웅덩이 등)은 중성 콘크리트로 되돌린다.
def suppress_teal(cell: Image.Image) -> Image.Image:
    cleaned = cell.copy()
    pixels = cleaned.load()
    for py in range(cleaned.height):
        for px in range(cleaned.width):
            red, green, blue, alpha_value = pixels[px, py]
            if alpha_value == 0:
                continue
            if blue > red + 20 and green > red + 10:
                value = max(red, green, blue)
                pixels[px, py] = (
                    round(value * 0.95),
                    round(value * 0.92),
                    round(value * 0.88),
                    alpha_value,
                )
    return cleaned


# 잠금 후에도 틸 계열 팔레트 항목이 non-boss 산출물에 남으면 실패시키는 금지 목록.
TEAL_PALETTE_FAMILY = frozenset(
    {
        (55, 106, 103),   # anomaly-2
        (79, 167, 160),   # anomaly-3
        (154, 223, 232),  # anomaly-4
        (198, 244, 247),  # sig-ice
        (56, 153, 166),   # sig-teal-item
        (61, 225, 232),   # sig-neon-cyan
    }
)


def build_sprite(source: Image.Image, allow_teal: bool = True) -> Image.Image:
    if not allow_teal:
        source = suppress_teal(source)
    resized = warm_guard(source).resize(SPRITE_SIZE, Image.Resampling.BOX)
    alpha = resized.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    resized.putalpha(alpha)
    # 잠금 직후 despeckle — 렌더링 문법 계약 §1-d(plan v2): 고립 1px 노이즈 금지.
    return despeckle(lock_rgba_to_palette(resized))


def _mean_value(image: Image.Image) -> float:
    pixels = [p for p in image.get_flattened_data() if p[3] > 0]
    return sum(max(p[0], p[1], p[2]) for p in pixels) / len(pixels) / 255


def build_outputs(
    sheet: Image.Image,
    base_floor: Image.Image,
) -> dict[str, Image.Image]:
    if sheet.size != SHEET_SIZE:
        raise ValueError(f"unexpected band floor source size: {sheet.size}")
    if base_floor.size != SPRITE_SIZE:
        raise ValueError(f"unexpected base floor size: {base_floor.size}")

    base_value = _mean_value(base_floor.convert("RGBA"))
    outputs: dict[str, Image.Image] = {}
    for spec in SPECS:
        is_boss = spec.output_name.startswith("env-floor-boss")
        sprite = build_sprite(extract_cell(sheet, spec.cell_index), allow_teal=is_boss)
        drift = abs(_mean_value(sprite) - base_value)
        if drift > BASE_VALUE_TOLERANCE:
            raise ValueError(
                f"{spec.output_name} drifts from the shared floor value "
                f"({drift:.3f} > {BASE_VALUE_TOLERANCE}) — §1-c base color gate"
            )
        if not is_boss:
            leaked = {
                p[:3] for p in sprite.get_flattened_data() if p[3] > 0
            } & TEAL_PALETTE_FAMILY
            if leaked:
                raise ValueError(
                    f"{spec.output_name} keeps reserved teal colors {sorted(leaked)} "
                    "— teal is allowed only on boss tiles (§1-c)"
                )
        outputs[spec.output_name] = sprite
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
    print(f"wrote {len(outputs)} band floor sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
