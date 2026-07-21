#!/usr/bin/env python3
"""Hand-authored placeholder pixel art for actor/item catalog slots.

교체 파이프라인 검증용 — AI 소스가 준비되면 process_environment_sprites.py 처럼
Temp/ArtPipeline 소스를 정규화하는 흐름으로 대체한다.
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Art/Actors"

# 문자 → RGBA. '.' 은 투명.
PALETTES = {
    "player": {
        "s": (233, 195, 155, 255),  # 피부
        "h": (108, 76, 48, 255),    # 머리카락
        "a": (155, 165, 182, 255),  # 갑옷
        "A": (108, 118, 138, 255),  # 갑옷 음영
        "l": (74, 58, 44, 255),     # 다리/부츠
        "w": (196, 200, 210, 255),  # 검
    },
    "goblin": {
        "g": (108, 158, 74, 255),   # 피부
        "G": (76, 118, 52, 255),    # 피부 음영
        "e": (214, 58, 46, 255),    # 눈
        "c": (122, 96, 60, 255),    # 누더기
        "l": (66, 84, 46, 255),     # 다리
    },
    "potion": {
        "b": (150, 210, 235, 255),  # 유리
        "B": (104, 160, 190, 255),  # 유리 음영
        "r": (214, 68, 88, 255),    # 물약
        "R": (166, 44, 66, 255),    # 물약 음영
        "k": (110, 82, 54, 255),    # 코르크
    },
}

PIXELS = {
    "player": [
        "......hhhh......",
        ".....hhhhhh.....",
        ".....hssssh.....",
        ".....ssssss.....",
        "......ssss......",
        "....aaaaaaaa....",
        "...aaaaaaaaaa.w.",
        "..saaaAAAAaaasw.",
        "..saaaAAAAaaasw.",
        "...aaaaaaaaaa.w.",
        "....aaaaaaaa..w.",
        "....llllllll....",
        "....lll..lll....",
        "....lll..lll....",
        "...llll..llll...",
        "................",
    ],
    "goblin": [
        "................",
        "..G..........G..",
        "..gG.gggggg.Gg..",
        "...ggggggggggg..",
        "...gge.gg.egg...",
        "...gggggggggg...",
        "....gGGGGGGg....",
        "....cccccccc....",
        "...cccccccccc...",
        "...gccccccccg...",
        "....cccccccc....",
        "....llll.lll....",
        "....lll..lll....",
        "...llll..llll...",
        "................",
        "................",
    ],
    "potion": [
        "................",
        "......kkkk......",
        "......kkkk......",
        ".....bBBBBb.....",
        ".....bB..Bb.....",
        "....bB....Bb....",
        "...bB......Bb...",
        "...bB.rrrr.Bb...",
        "...bBrrrrrrBb...",
        "...bBrRRRRrBb...",
        "...bBrRRRRrBb...",
        "....bBrRRrBb....",
        ".....bBBBBb.....",
        "......bbbb......",
        "................",
        "................",
    ],
}

# 출력 캔버스(픽셀 4배 확대). PPU 64 기준 액터 = 1타일 폭.
SCALE = 4


def render(name: str) -> None:
    rows = PIXELS[name]
    palette = PALETTES[name]
    image = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                image.putpixel((x, y), palette[ch])
    scaled = image.resize(
        (image.width * SCALE, image.height * SCALE), Image.Resampling.NEAREST
    )
    scaled.save(OUTPUT / f"actor-{name}.png", optimize=True)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name in PIXELS:
        render(name)
    print(f"wrote {len(PIXELS)} sprites to {OUTPUT}")


if __name__ == "__main__":
    main()
