#!/usr/bin/env python3
"""Build Project-C's polished Collapsed Transit environment slice.

The high-resolution source sheet is an ImageGen style-transfer of six original
Project-C sprites. It is never sliced directly into the game: this processor
extracts each fixed cell, hardens alpha, downsamples at final resolution, and
reduces local color noise while preserving the richer material rendering that
was lost in the earlier code-drawn placeholder pass.
"""

from collections import deque
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from torchstone_palette import despeckle, lock_to_palette


ROOT = Path(__file__).resolve().parents[2]
SOURCE = (
    ROOT
    / "docs/art-direction/project-c-collapsed-transit-environment-source-v4.png"
)
# 2026-07-30: 하행 계단 특수 소스를 environment-neon-stairs-v1(C04 채택)로 교체 —
# v3 채택 라운드에서 빠졌던 마지막 병원판 소스다. 상행 셀은 스타일 참조용이라 소비하지 않는다.
STAIRS_SOURCE = (
    ROOT
    / "docs/art-direction/project-c-arcade-stairs-source-v1.png"
)
OUTPUT = ROOT / "Assets/_Project/Art/Environment"
CELL_SIZE = (512, 512)
STAIRS_CELL_SIZE = 627
ALPHA_CUTOFF = 80
BACKDROP_SIZE = (128, 64)

# 배경은 미탐색 구조를 드러내지 않는 한 장짜리 다이아몬드다. 모든 색은
# project-c-torchstone.gpl의 공용 토큰이며 최종 저장 전에 다시 팔레트 잠금한다.
BACKDROP_PANEL = (21, 23, 29, 255)
BACKDROP_SHADOW = (5, 7, 12, 255)
BACKDROP_SEAM = (10, 13, 19, 255)
BACKDROP_EDGE = (44, 49, 56, 255)


@dataclass(frozen=True)
class SpriteSpec:
    source_name: str
    cell_index: int
    size: tuple[int, int]
    output_names: tuple[str, ...]


# 128-레짐(128×64 타일 / PPU 128) — 모든 캔버스가 구 64-레짐의 정확히 ×2다.
SPECS = (
    SpriteSpec("floor", 0, (128, 64), ("env-floor",)),
    SpriteSpec(
        "wall",
        1,
        (64, 112),
        ("env-wall-rising-right", "env-wall-rising-left"),
    ),
    SpriteSpec(
        "wall-light",
        2,
        (64, 112),
        ("env-wall-torch-rising-right", "env-wall-torch-rising-left"),
    ),
    SpriteSpec(
        "door-closed",
        3,
        (128, 160),
        ("env-door-closed-rising-right", "env-door-closed-rising-left"),
    ),
    SpriteSpec(
        "door-open",
        4,
        (128, 160),
        ("env-door-open-rising-right", "env-door-open-rising-left"),
    ),
    SpriteSpec(
        "stairs",
        5,
        (128, 112),
        (
            "env-stairs-rising-right",
            "env-stairs-rising-left",
            "env-stairs-up-rising-right",
            "env-stairs-up-rising-left",
        ),
    ),
)


def _is_magenta_chroma(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, alpha = pixel
    return (
        alpha > 0
        and red > 70
        and blue > 50
        and red + blue > green * 2 + 80
    )


def strip_chroma_spill(cell: Image.Image) -> None:
    """Drop only magenta chroma connected to the exterior background.

    Transparent pixels are traversable so detached edge fringe is still cleaned. Opaque
    non-chroma pixels stop the fill, preserving enclosed magenta and cyan neon accents.
    """
    width, height = cell.size
    pixels = cell.load()
    visited = bytearray(width * height)
    pending: deque[tuple[int, int]] = deque()

    def enqueue(px: int, py: int) -> None:
        index = py * width + px
        if visited[index]:
            return
        pixel = pixels[px, py]
        if pixel[3] != 0 and not _is_magenta_chroma(pixel):
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
        red, green, blue, alpha = pixels[px, py]
        if _is_magenta_chroma((red, green, blue, alpha)):
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
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"source cell {index} contains no visible pixels")
    return cell.crop(bounds)


def extract_stairs_cell(sheet: Image.Image) -> Image.Image:
    cell = sheet.crop((0, 0, STAIRS_CELL_SIZE, STAIRS_CELL_SIZE)).convert("RGBA")
    strip_chroma_spill(cell)
    alpha = cell.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    cell.putalpha(alpha)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("descending stair source contains no visible pixels")
    return cell.crop(bounds)


def reduce_colors(image: Image.Image) -> Image.Image:
    """Lock RGB to the shared Torchstone palette (no dither, hard alpha edge).

    Was an independent MEDIANCUT-32 quantize; now every sheet shares the master
    .gpl indices so environment/props/actors/UI cohere. See torchstone_palette.
    잠금 직후 despeckle 패스가 고립 1px 스펙클을 병합한다 — 렌더링 문법 계약
    §1-d(plan v2): 스타일 트랜스퍼의 잔점 노이즈가 최종 산출물에 남지 않는다.
    """
    alpha = image.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_CUTOFF else 0
    )
    rgb = Image.new("RGB", image.size, (5, 7, 12))
    rgb.paste(image, mask=alpha)
    reduced = lock_to_palette(rgb).convert("RGBA")
    reduced.putalpha(alpha)
    return despeckle(reduced)


def build_sprite(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    resized = source.resize(size, Image.Resampling.BOX)
    return reduce_colors(resized)


def build_floor_sprite(source: Image.Image) -> Image.Image:
    """Extract one top-only panel from the generated multi-panel floor slab.

    The source cell is a 2x2-ish presentation slab with a baked front edge. Scaling the
    whole slab into 128x64 makes its internal panel grid look like the tile axes and then
    duplicates the side wall that the runtime already draws. The clean upper panel is the
    centered diamond in the top half of the trimmed source.
    """
    left = source.width // 4
    right = source.width - left
    bottom = max(1, source.height // 2)
    panel = source.crop((left, 0, right, bottom)).resize(
        BACKDROP_SIZE,
        Image.Resampling.BOX,
    )
    panel = reduce_colors(panel)

    # Runtime owns elevation sides. Keep only the canonical 2:1 top face and touch all
    # four canvas edges so Unity's Aseprite importer cannot trim a 128x64 tile smaller.
    alpha = Image.new("L", BACKDROP_SIZE, 0)
    pixels = alpha.load()
    for py in range(BACKDROP_SIZE[1]):
        for px in range(BACKDROP_SIZE[0]):
            diamond = abs((px - 63.5) / 64) + abs((py - 31.5) / 32)
            if diamond <= 1:
                pixels[px, py] = 255
    for px, py in ((64, 0), (127, 32), (64, 63), (0, 32)):
        pixels[px, py] = 255
    panel.putalpha(alpha)
    return panel


def build_dungeon_backdrop() -> Image.Image:
    """Build a low-contrast full-floor backing plate for the FOV void.

    The tiny deterministic texture is stretched to the current dungeon's
    generated bounds. It contains no room/corridor information, so it improves
    tone separation without leaking unexplored layout.
    """
    width, height = BACKDROP_SIZE
    image = Image.new("RGBA", BACKDROP_SIZE, (0, 0, 0, 0))
    pixels = image.load()
    for py in range(height):
        for px in range(width):
            diamond = abs((px - 63.5) / 64) + abs((py - 31.5) / 32)
            if diamond > 1:
                continue

            # 방 구조가 아닌 공용 환기 덕트/케이블 실루엣. 전체 생성 영역에 같은
            # 패턴을 늘여 쓰므로 미탐색 복도나 문 위치를 암시하지 않는다.
            vertical_cable = (
                px in (24, 25, 102, 103) and 16 <= py <= 47
            )
            cable_joint = (
                py in (18, 19, 44, 45) and
                (18 <= px <= 32 or 96 <= px <= 110)
            )
            vent_slats = (
                45 <= px <= 82 and
                py in (18, 19, 23, 24, 28, 29)
            )

            if diamond > 0.975:
                color = BACKDROP_EDGE
            elif vertical_cable or cable_joint or vent_slats:
                color = BACKDROP_EDGE
            elif (px * 13 + py * 7) % 47 == 0:
                color = BACKDROP_SHADOW
            elif (px * 5 + py * 11) % 71 == 0:
                color = BACKDROP_SEAM
            else:
                color = BACKDROP_PANEL
            pixels[px, py] = color

    return reduce_colors(image)


def build_fitted_sprite(
    source: Image.Image,
    canvas_size: tuple[int, int],
    visible_size: tuple[int, int],
    ground_y: int,
) -> Image.Image:
    scale = min(
        visible_size[0] / source.width,
        visible_size[1] / source.height,
    )
    size = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    sprite = reduce_colors(source.resize(size, Image.Resampling.BOX))
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = (canvas_size[0] - sprite.width) // 2
    y = ground_y - sprite.height
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def save(image: Image.Image, name: str) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT / f"{name}.png", optimize=True)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not STAIRS_SOURCE.exists():
        raise FileNotFoundError(STAIRS_SOURCE)

    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (1536, 1024):
        raise ValueError(f"unexpected source sheet size: {sheet.size}")

    # 기본 벽과 벽 작업등은 B2 품질 패스의 조용한 공용 shell이 정식 주인이다.
    # 구 고해상도 시트를 다시 처리해도 네이티브 픽셀 벽을 덮어쓰지 않는다.
    from process_b2_prop_quality_v4 import build_source_assets

    b2_quality = build_source_assets().outputs
    written = 0
    for spec in SPECS:
        source = extract_cell(sheet, spec.cell_index)
        sprite = (
            build_floor_sprite(source)
            if spec.source_name == "floor"
            else build_sprite(source, spec.size)
        )
        for output_index, output_name in enumerate(spec.output_names):
            # Every second directional output is the mirrored partner.
            output = b2_quality.get(output_name)
            if output is None:
                output = (
                    sprite
                    if output_index % 2 == 0
                    else sprite.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                )
            save(output, output_name)
            written += 1

    stairs_sheet = Image.open(STAIRS_SOURCE).convert("RGBA")
    if stairs_sheet.size != (STAIRS_CELL_SIZE * 2, STAIRS_CELL_SIZE * 2):
        raise ValueError(f"unexpected stair source sheet size: {stairs_sheet.size}")
    stairs_down = build_fitted_sprite(
        extract_stairs_cell(stairs_sheet),
        canvas_size=(128, 80),
        visible_size=(128, 76),
        ground_y=78,
    )
    save(stairs_down, "env-stairs-down-rising-right")
    save(
        stairs_down.transpose(Image.Transpose.FLIP_LEFT_RIGHT),
        "env-stairs-down-rising-left",
    )
    written += 2

    save(build_dungeon_backdrop(), "env-dungeon-backdrop")
    written += 1

    print(f"wrote {written} Collapsed Transit environment sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
