#!/usr/bin/env python3
"""Shared Torchstone palette lock for the art conform pipeline.

All postapoc source sheets are style-transfer generations. Historically each
processor reduced colors with an independent MEDIANCUT-32 quantize, so every
sheet drifted to its own palette and the assets did not cohere. Locking every
sprite to the shared master palette (``project-c-torchstone.gpl`` == the
``DesignSystem.uss`` tokens) is what binds environment/props/actors/items and
the screen-space UI into one style.

Usage (inside a processor's ``reduce_colors``)::

    from torchstone_palette import lock_to_palette
    reduced = lock_to_palette(rgb).convert("RGBA")
"""

from functools import lru_cache
from pathlib import Path

from PIL import Image

GPL_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl"
)

# 액터 아이덴티티 램프(피부·머리)는 기본 잠금에서 제외한다(옵트인). 갈색 피부톤은
# fabric/rust/stone 램프가 점유한 회랑 한가운데라, 팔레트에 있는 것만으로 최근접
# 양자화가 재료 시트의 암부 픽셀을 뺏는다(실측 2026-07-30: 629px, 값 조정으로는
# 절도 0인 피부톤이 존재하지 않음). 얼굴이 노출되는 자산의 conform만
# ``include_identity=True``로 연다.
IDENTITY_PREFIXES = ("skin-", "hair-")


@lru_cache(maxsize=1)
def load_gpl_entries(
    gpl_path: Path = GPL_PATH,
) -> tuple[tuple[str, tuple[int, int, int]], ...]:
    """Parse a GIMP ``.gpl`` into ordered ``(name, rgb)`` entries (<=256 colors).

    Color lines start with a digit (``R G B<TAB>name``); GIMP/Name/Columns and
    ``#`` comment lines are skipped.
    """
    entries: list[tuple[str, tuple[int, int, int]]] = []
    for line in gpl_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or not stripped[0].isdigit():
            continue
        parts = stripped.split()
        red, green, blue = (int(value) for value in parts[:3])
        name = parts[3] if len(parts) > 3 else f"#{red:02X}{green:02X}{blue:02X}"
        entries.append((name, (red, green, blue)))
    if not entries:
        raise ValueError(f"no colors parsed from {gpl_path}")
    if len(entries) > 256:
        raise ValueError(f"{gpl_path} has {len(entries)} colors (max 256 for a P palette)")
    return tuple(entries)


@lru_cache(maxsize=1)
def load_gpl(gpl_path: Path = GPL_PATH) -> tuple[tuple[int, int, int], ...]:
    """Ordered RGB tuple of every ``.gpl`` entry (identity ramps included)."""
    return tuple(rgb for _, rgb in load_gpl_entries(gpl_path))


@lru_cache(maxsize=2)
def _palette_image(include_identity: bool = False) -> Image.Image:
    colors = [
        rgb
        for name, rgb in load_gpl_entries()
        if include_identity or not name.startswith(IDENTITY_PREFIXES)
    ]
    palette = Image.new("P", (1, 1))
    flat = [channel for rgb in colors for channel in rgb]
    # 미사용 슬롯은 검정(0,0,0)이 아니라 첫 색으로 패딩한다 — 검정으로 패딩하면
    # 최근접 양자화가 어두운 픽셀을 팔레트에 없는 순수 검정으로 스냅한다(실측 아티팩트).
    flat += list(colors[0]) * (256 - len(colors))
    palette.putpalette(flat)
    return palette


def lock_to_palette(rgb: Image.Image, *, include_identity: bool = False) -> Image.Image:
    """Quantize an RGB image to the fixed Torchstone palette (no dither).

    Replaces per-sheet ``quantize(colors=N, method=MEDIANCUT)`` so every asset
    shares the same indices. Alpha handling stays in the caller. Identity ramps
    (``skin-*``/``hair-*``) stay out of the lock unless ``include_identity`` —
    only face-bearing art opts in.
    """
    if rgb.mode != "RGB":
        rgb = rgb.convert("RGB")
    return rgb.quantize(
        palette=_palette_image(include_identity), dither=Image.Dither.NONE
    ).convert("RGB")


def lock_rgba_to_palette(image: Image.Image, *, include_identity: bool = False) -> Image.Image:
    """Lock visible RGB to Torchstone while preserving the source alpha."""
    source = image.convert("RGBA")
    alpha = source.getchannel("A")
    rgb = Image.new("RGB", source.size, load_gpl()[0])
    rgb.paste(source, mask=alpha)
    locked = lock_to_palette(rgb, include_identity=include_identity).convert("RGBA")
    locked.putalpha(alpha)
    return locked


_NEIGHBOR_OFFSETS = (
    (-1, -1), (0, -1), (1, -1),
    (-1, 0), (1, 0),
    (-1, 1), (0, 1), (1, 1),
)


def despeckle(image: Image.Image, passes: int = 2) -> Image.Image:
    """Merge isolated 1px opaque speckles into their neighborhood color.

    렌더링 문법 계약(§1-d, plan v2): 최종 해상도에서 고립 1px 노이즈 금지 —
    생성이 계약을 어겨도 conform이 최종 산출물에서 문법을 강제한다.
    **팔레트 잠금 후 호출을 전제로 한다**(잠금 전에 돌리면 양자화가 새
    스펙클을 다시 만든다).

    불투명 픽셀 중 8방 이웃에 같은 RGB가 하나도 없는 고립 픽셀을, 이웃
    불투명 색의 다수결로 병합한다(동수면 스캔 순서상 첫 후보). 이웃에
    불투명 픽셀이 없으면 그대로 둔다. 알파 채널은 어떤 경우에도 건드리지
    않는다. 한 패스는 스냅샷을 읽고 결과에 쓰므로 병합은 패스 내 동시적
    (결정적)이며, 변화가 없으면 조기 종료한다.
    """
    result = image.copy() if image.mode == "RGBA" else image.convert("RGBA")
    width, height = result.size
    for _ in range(passes):
        previous = result.copy()
        snapshot = previous.load()
        target = result.load()
        changed = False
        for y in range(height):
            for x in range(width):
                red, green, blue, alpha = snapshot[x, y]
                if alpha == 0:
                    continue
                candidates: dict[tuple[int, int, int], int] = {}
                isolated = True
                for dx, dy in _NEIGHBOR_OFFSETS:
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < width and 0 <= ny < height):
                        continue
                    n_red, n_green, n_blue, n_alpha = snapshot[nx, ny]
                    if n_alpha == 0:
                        continue
                    if (n_red, n_green, n_blue) == (red, green, blue):
                        isolated = False
                        break
                    key = (n_red, n_green, n_blue)
                    candidates[key] = candidates.get(key, 0) + 1
                if not isolated or not candidates:
                    continue
                best_color: tuple[int, int, int] | None = None
                best_count = 0
                # dict는 삽입(첫 등장) 순서를 보존한다 — 동수면 첫 후보가 이긴다.
                for color, count in candidates.items():
                    if count > best_count:
                        best_color, best_count = color, count
                target[x, y] = (*best_color, alpha)
                changed = True
        if not changed:
            break
    return result


if __name__ == "__main__":
    parsed = load_gpl()
    print(f"{len(parsed)} colors loaded from {GPL_PATH.name}")
