from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_cracked_floor_v1 import (
    CANVAS_SIZE,
    DAMAGE_INTERIOR_LIMIT,
    MAX_DAMAGE_RATIO,
    RUST_DARK,
    build_output,
    extract_source_tile,
    is_inside_damage_inset,
)
from process_b2_parking_dressing_v2 import neutralize_floor_source
from torchstone_palette import despeckle, load_gpl, lock_rgba_to_palette


MAGENTA = (255, 0, 255, 255)


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _source() -> Image.Image:
    source = Image.new("RGBA", (420, 240), MAGENTA)
    draw = ImageDraw.Draw(source)
    diamond = ((34, 120), (210, 32), (386, 120), (210, 208))
    draw.polygon(diamond, fill=(89, 91, 92, 255))
    draw.polygon(
        ((175, 113), (202, 101), (236, 111), (245, 125), (214, 136), (184, 128)),
        fill=(61, 62, 62, 255),
    )
    draw.line((210, 101, 219, 86, 236, 78), fill=(70, 71, 72, 255), width=4)
    draw.line((184, 128, 168, 140, 158, 156), fill=(72, 73, 74, 255), width=4)
    draw.rectangle((228, 102, 235, 108), fill=(142, 72, 31, 255))
    return source


def _base_floor() -> Image.Image:
    floor = Image.new("RGBA", CANVAS_SIZE, (5, 7, 12, 0))
    ImageDraw.Draw(floor).polygon(
        ((0, 32), (64, 0), (127, 32), (64, 63)),
        fill=(107, 113, 120, 255),
    )
    return floor


class B2CrackedFloorProcessorTests(unittest.TestCase):
    def test_output_is_flat_palette_locked_and_preserves_base_alpha(self) -> None:
        base = _base_floor()
        output = build_output(_source(), base)

        self.assertEqual(CANVAS_SIZE, output.size)
        self.assertEqual(base.getchannel("A").tobytes(), output.getchannel("A").tobytes())
        self.assertTrue(set(_pixels(output.getchannel("A"))).issubset({0, 255}))
        palette = set(load_gpl())
        visible = [pixel for pixel in _pixels(output) if pixel[3] > 0]
        self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))

    def test_damage_is_restrained_and_keeps_outer_diamond_neutral(self) -> None:
        base = _base_floor()
        conformed_base = despeckle(lock_rgba_to_palette(neutralize_floor_source(base)))
        output = build_output(_source(), base)
        changed = [
            (x, y)
            for y in range(CANVAS_SIZE[1])
            for x in range(CANVAS_SIZE[0])
            if output.getpixel((x, y)) != conformed_base.getpixel((x, y))
        ]
        visible_count = sum(1 for pixel in _pixels(output) if pixel[3] > 0)

        self.assertGreater(len(changed), 0)
        self.assertLessEqual(len(changed), int(visible_count * MAX_DAMAGE_RATIO))
        self.assertTrue(all(is_inside_damage_inset(x, y) for x, y in changed))
        self.assertLess(DAMAGE_INTERIOR_LIMIT, 1.0)

    def test_rust_stays_a_small_non_signal_accent(self) -> None:
        output = build_output(_source(), _base_floor())
        rust_count = sum(1 for pixel in _pixels(output) if pixel == RUST_DARK)
        visible_count = sum(1 for pixel in _pixels(output) if pixel[3] > 0)

        self.assertGreater(rust_count, 0)
        self.assertLessEqual(rust_count, 20)
        self.assertLess(rust_count / visible_count, 0.01)
        forbidden = {
            (255, 189, 65),
            (255, 213, 84),
            (224, 166, 43),
            (61, 225, 232),
            (230, 68, 184),
        }
        self.assertFalse(
            any(pixel[:3] in forbidden for pixel in _pixels(output) if pixel[3] > 0)
        )

    def test_rejects_missing_tile_and_wrong_base_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "contains no visible object"):
            extract_source_tile(Image.new("RGBA", (128, 64), MAGENTA))
        with self.assertRaisesRegex(ValueError, "unexpected base floor size"):
            build_output(_source(), Image.new("RGBA", (64, 32)))

    def test_rejects_non_isometric_source_shape(self) -> None:
        source = Image.new("RGBA", (200, 200), MAGENTA)
        ImageDraw.Draw(source).rectangle((70, 20, 130, 180), fill=(80, 82, 84, 255))
        with self.assertRaisesRegex(ValueError, "must be a 2:1 diamond"):
            extract_source_tile(source)


if __name__ == "__main__":
    unittest.main()
