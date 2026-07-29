from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_items_v3 import (
    CANVAS_SIZE,
    SOURCE_DIR,
    SPECS,
    ItemSpec,
    build_item,
    remove_border_background,
)
from torchstone_palette import load_gpl


class ItemProcessorV3Tests(unittest.TestCase):
    def test_registry_covers_all_item_sources_once(self) -> None:
        self.assertEqual(12, len(SPECS))
        self.assertEqual(len(SPECS), len({spec.source_name for spec in SPECS}))
        self.assertEqual(len(SPECS), len({spec.output_name for spec in SPECS}))
        self.assertTrue(
            all((SOURCE_DIR / spec.source_name).exists() for spec in SPECS)
        )

    def test_item_is_large_palette_locked_hard_alpha_cutout(self) -> None:
        source = Image.new("RGBA", (128, 128), (255, 0, 255, 255))
        draw = ImageDraw.Draw(source)
        draw.rounded_rectangle(
            (38, 16, 90, 116),
            radius=14,
            fill=(147, 51, 45, 255),
            outline=(17, 24, 32, 255),
            width=8,
        )
        spec = ItemSpec("source.png", "output.png", (54, 54), 7)

        output = build_item(source, spec)

        self.assertEqual(CANVAS_SIZE, output.size)
        self.assertTrue(
            set(output.getchannel("A").get_flattened_data()).issubset({0, 255})
        )
        self.assertTrue(
            {pixel[:3] for pixel in output.get_flattened_data() if pixel[3]}
            .issubset(set(load_gpl()))
        )
        bounds = output.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        self.assertGreaterEqual(bounds[2] - bounds[0], 24)
        self.assertGreaterEqual(bounds[3] - bounds[1], 50)
        self.assertEqual(CANVAS_SIZE[1] - spec.bottom_padding, bounds[3])

    def test_border_flood_removes_neutral_generated_plate(self) -> None:
        source = Image.new("RGBA", (32, 32), (121, 122, 122, 255))
        pixels = source.load()
        for y in range(source.height):
            for x in range(source.width):
                variation = (x + y) % 3
                pixels[x, y] = (
                    121 + variation,
                    122 + variation,
                    122 + variation,
                    255,
                )
        draw = ImageDraw.Draw(source)
        draw.rectangle((10, 6, 22, 27), fill=(147, 51, 45, 255))
        draw.rectangle((13, 3, 19, 8), fill=(17, 24, 32, 255))

        cutout = remove_border_background(source)

        self.assertEqual(0, cutout.getpixel((0, 0))[3])
        self.assertEqual(0, cutout.getpixel((31, 31))[3])
        self.assertEqual(255, cutout.getpixel((16, 16))[3])


if __name__ == "__main__":
    unittest.main()
