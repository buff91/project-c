#!/usr/bin/env python3
"""Build native 12×12 Field Deck HUD glyphs.

The adopted HUD concept treats these as instrument marks, not miniature item
paintings.  Every glyph is authored directly on the runtime pixel grid with
hard alpha and a three-colour cool-steel ramp.  Desktop USS displays them at
12×12, so there is no generated-sheet downscale or fractional resampling.
"""
from pathlib import Path

from PIL import Image, ImageDraw

from torchstone_palette import lock_rgba_to_palette


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Project/Art/Runtime"
SIZE = 12
TRANSPARENT = (0, 0, 0, 0)
DIM = (84, 91, 97, 255)
MID = (148, 155, 161, 255)
LIT = (223, 231, 242, 255)


def canvas() -> Image.Image:
    return Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)


def settings() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    for y in (2, 5, 8):
        draw.line((1, y, 10, y), fill=DIM)
    draw.rectangle((3, 1, 4, 3), fill=LIT)
    draw.rectangle((7, 4, 8, 6), fill=LIT)
    draw.rectangle((5, 7, 6, 9), fill=LIT)
    return image


def menu() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    for y in (2, 5, 8):
        draw.rectangle((2, y, 9, y + 1), fill=LIT)
    return image


def rotate_left() -> Image.Image:
    image = canvas()
    pixels = image.load()
    for x, y in {
        (2, 2), (3, 2), (4, 2), (1, 3), (2, 3), (1, 4),
        (1, 5), (1, 6), (2, 7), (3, 8), (4, 8), (5, 8),
        (6, 7), (7, 6), (7, 5),
    }:
        pixels[x, y] = LIT
    for x, y in {(2, 1), (2, 2), (2, 3), (3, 3)}:
        pixels[x, y] = MID
    return image


def rotate_right() -> Image.Image:
    source = rotate_left()
    return source.transpose(Image.Transpose.FLIP_LEFT_RIGHT)


def backpack() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.line((4, 2, 4, 1, 7, 1, 7, 2), fill=MID)
    draw.rectangle((2, 3, 9, 10), outline=LIT, width=1)
    draw.rectangle((3, 5, 8, 8), outline=MID, width=1)
    draw.line((5, 3, 5, 10), fill=DIM)
    draw.line((6, 3, 6, 10), fill=DIM)
    return image


def wait() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rectangle((2, 1, 9, 2), fill=LIT)
    draw.rectangle((2, 9, 9, 10), fill=LIT)
    draw.line((3, 3, 5, 5, 3, 8), fill=MID, width=1)
    draw.line((8, 3, 6, 5, 8, 8), fill=MID, width=1)
    draw.rectangle((5, 5, 6, 6), fill=LIT)
    return image


def melee() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.line((2, 9, 8, 3), fill=LIT, width=2)
    draw.rectangle((1, 9, 3, 10), fill=MID)
    draw.line((8, 7, 10, 5), fill=MID)
    draw.line((8, 7, 10, 9), fill=MID)
    return image


def ranged() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.line((1, 5, 3, 5), fill=LIT)
    draw.line((8, 5, 10, 5), fill=LIT)
    draw.line((5, 1, 5, 3), fill=LIT)
    draw.line((5, 8, 5, 10), fill=LIT)
    draw.rectangle((4, 4, 6, 6), outline=MID, width=1)
    draw.line((8, 8, 9, 7, 8, 6, 10, 5), fill=MID)
    return image


def interact() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rectangle((3, 5, 7, 9), fill=MID)
    draw.line((3, 5, 3, 2), fill=LIT)
    draw.line((4, 5, 4, 1), fill=LIT)
    draw.line((5, 5, 5, 2), fill=LIT)
    draw.line((6, 5, 6, 3), fill=LIT)
    draw.rectangle((4, 9, 7, 10), fill=LIT)
    draw.line((8, 5, 10, 7, 8, 9), fill=LIT)
    return image


BUILDERS = {
    "settings": settings,
    "menu": menu,
    "rotate-left": rotate_left,
    "rotate-right": rotate_right,
    "backpack": backpack,
    "wait": wait,
    "melee": melee,
    "ranged": ranged,
    "interact": interact,
}


def build_all() -> dict[str, Image.Image]:
    return {
        name: lock_rgba_to_palette(builder())
        for name, builder in BUILDERS.items()
    }


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in build_all().items():
        image.save(OUTPUT / f"ui-field-{name}.png", optimize=True)
    print(f"wrote {len(BUILDERS)} native Field Deck glyphs to {OUTPUT}")


if __name__ == "__main__":
    main()
