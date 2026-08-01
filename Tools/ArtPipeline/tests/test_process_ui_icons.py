from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_ui_icons_v1 import CANVAS_SIZE, build_icon
from torchstone_palette import load_gpl


class UiIconProcessorTests(unittest.TestCase):
    def test_icon_uses_32px_contract_and_shared_palette(self) -> None:
        source = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        draw.polygon(
            ((8, 48), (48, 8), (88, 48), (48, 88)),
            fill=(232, 155, 54, 255),
        )

        output = build_icon(source.crop(source.getbbox()))

        self.assertEqual((32, 32), CANVAS_SIZE)
        self.assertEqual(CANVAS_SIZE, output.size)
        self.assertTrue(
            {pixel[:3] for pixel in output.get_flattened_data() if pixel[3]}
            .issubset(set(load_gpl()))
        )
        bounds = output.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        self.assertGreaterEqual(bounds[2] - bounds[0], 28)
        self.assertGreaterEqual(bounds[3] - bounds[1], 28)


if __name__ == "__main__":
    unittest.main()


class UiIconCoolShiftTests(unittest.TestCase):
    """v1.8 웜 → 쿨 리맵. 게이트가 신호색을 건드리지 않는 것이 핵심 계약이다."""

    def test_warm_neutral_stone_becomes_cool(self) -> None:
        from process_ui_icons_v1 import cool_shift

        warm = Image.new("RGBA", (1, 1), (152, 134, 111, 255))  # 구 --pc-stone
        red, green, blue, _ = cool_shift(warm).getpixel((0, 0))

        self.assertLess(red, blue, "리맵 뒤에도 적색이 청색보다 크면 여전히 웜이다.")

    def test_signal_colours_are_untouched(self) -> None:
        from process_ui_icons_v1 import cool_shift

        # 골드·토치·틸·HP·아이스 — 하나라도 옮겨지면 화면 판독이 무너진다.
        signals = (
            (255, 213, 84, 255),
            (255, 189, 65, 255),
            (79, 167, 160, 255),
            (216, 69, 42, 255),
            (154, 223, 232, 255),
        )
        for colour in signals:
            with self.subTest(colour=colour):
                cell = Image.new("RGBA", (1, 1), colour)
                self.assertEqual(colour, cool_shift(cell).getpixel((0, 0)))

    def test_transparent_pixels_stay_transparent(self) -> None:
        from process_ui_icons_v1 import cool_shift

        cell = Image.new("RGBA", (1, 1), (152, 134, 111, 0))
        self.assertEqual(0, cool_shift(cell).getpixel((0, 0))[3])

    def test_value_is_preserved_so_shading_survives(self) -> None:
        from process_ui_icons_v1 import cool_shift

        dark = cool_shift(Image.new("RGBA", (1, 1), (74, 64, 56, 255))).getpixel((0, 0))
        light = cool_shift(Image.new("RGBA", (1, 1), (207, 192, 174, 255))).getpixel((0, 0))
        self.assertLess(max(dark[:3]), max(light[:3]), "명도 순서가 뒤집혔다.")
