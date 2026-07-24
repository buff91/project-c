#!/usr/bin/env python3
"""Generate deterministic pixel-art frames for the prototype action wheel.

These PNGs are swap-ready runtime fallbacks. Final production art can replace
them with Aseprite exports without changing the UXML or controller contract.
"""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"

SIZE = (72, 64)
OUTER = [(18, 1), (53, 1), (70, 32), (53, 62), (18, 62), (1, 32)]
INNER = [(20, 5), (51, 5), (65, 32), (51, 58), (20, 58), (6, 32)]

VOID = "#05070C"
PANEL = "#0A0D13"
STONE = "#98866F"
STONE_LIT = "#CFC0AE"
STONE_DIM = "#4A4038"
GOLD = "#FFD554"
GOLD_DEEP = "#9A6B22"
ACTION = "#2A1E10"


def draw_frame(path: Path, *, hover: bool) -> None:
    image = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    outline = GOLD_DEEP if hover else STONE
    highlight = GOLD if hover else STONE_LIT
    shadow = GOLD_DEEP if hover else STONE_DIM
    fill = ACTION if hover else PANEL

    draw.polygon(OUTER, fill=outline)
    draw.polygon(INNER, fill=fill)

    # Pixel bevel: top/upper-left catches light, lower-right falls into shadow.
    draw.line([OUTER[0], OUTER[1], OUTER[2]], fill=highlight, width=2)
    draw.line([OUTER[5], OUTER[0]], fill=highlight, width=2)
    draw.line([OUTER[2], OUTER[3], OUTER[4], OUTER[5]], fill=shadow, width=2)

    # Industrial fastening/rust marks keep the frame in the post-apoc material family.
    fastener = GOLD if hover else GOLD_DEEP
    draw.rectangle((4, 30, 6, 33), fill=fastener)
    draw.rectangle((65, 30, 67, 33), fill=fastener)
    draw.point((18, 3), fill=VOID)
    draw.point((53, 60), fill=VOID)

    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def main() -> None:
    draw_frame(OUTPUT / "ui-action-hex.png", hover=False)
    draw_frame(OUTPUT / "ui-action-hex-hover.png", hover=True)
    print("Generated ui-action-hex.png and ui-action-hex-hover.png")


if __name__ == "__main__":
    main()
