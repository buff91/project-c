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
