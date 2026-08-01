from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_postapoc_environment_v2 import (
    BACKDROP_EDGE,
    BACKDROP_PANEL,
    BACKDROP_SEAM,
    BACKDROP_SHADOW,
    build_dungeon_backdrop,
    build_floor_sprite,
    build_sprite,
    strip_chroma_spill,
)


class PostapocEnvironmentProcessorTests(unittest.TestCase):
    def test_edge_chroma_is_removed_but_enclosed_neon_is_preserved(self) -> None:
        image = Image.new("RGBA", (32, 32), (5, 7, 12, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((2, 2, 5, 5), fill=(255, 0, 255, 255))
        draw.rectangle((8, 8, 23, 23), fill=(59, 63, 69, 255))
        cyan = (61, 225, 232, 255)
        magenta = (230, 68, 184, 255)
        draw.rectangle((11, 11, 14, 14), fill=cyan)
        draw.rectangle((17, 11, 20, 14), fill=magenta)

        strip_chroma_spill(image)

        self.assertEqual(0, image.getpixel((3, 3))[3])
        self.assertEqual(cyan, image.getpixel((12, 12)))
        self.assertEqual(magenta, image.getpixel((18, 12)))

        runtime = build_sprite(image, image.size)
        runtime_colors = set(runtime.get_flattened_data())
        self.assertIn(cyan, runtime_colors)
        self.assertIn(magenta, runtime_colors)

    def test_backdrop_uses_only_dark_cool_and_grey_ramps(self) -> None:
        backdrop = build_dungeon_backdrop()
        expected = {
            BACKDROP_PANEL,
            BACKDROP_SHADOW,
            BACKDROP_SEAM,
            BACKDROP_EDGE,
        }
        visible = {
            pixel
            for pixel in backdrop.get_flattened_data()
            if pixel[3] > 0
        }

        self.assertEqual((128, 64), backdrop.size)
        self.assertTrue(visible.issubset(expected))
        self.assertIn(BACKDROP_PANEL, visible)
        self.assertIn(BACKDROP_EDGE, visible)
        interior_cable_pixels = sum(
            backdrop.getpixel((px, py)) == BACKDROP_EDGE
            for py in range(8, 56)
            for px in range(12, 116)
            if abs((px - 63.5) / 64) + abs((py - 31.5) / 32) < 0.90
        )
        self.assertGreater(interior_cable_pixels, 100)
        self.assertTrue(all(blue >= red for red, _, blue, _ in visible))
        self.assertNotIn((74, 64, 56, 255), visible)

    def test_floor_extracts_one_top_only_canonical_diamond(self) -> None:
        # A generated source cell is a presentation slab: top panels plus a thick baked
        # front edge. The official floor must consume only its clean upper-center panel.
        source = Image.new("RGBA", (432, 216), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        draw.polygon(
            ((216, 0), (324, 54), (216, 108), (108, 54)),
            fill=(91, 82, 71, 255),
        )
        draw.rectangle((0, 109, 431, 215), fill=(230, 68, 184, 255))

        floor = build_floor_sprite(source)
        alpha = floor.getchannel("A")

        self.assertEqual((128, 64), floor.size)
        self.assertEqual((0, 0, 128, 64), alpha.getbbox())
        self.assertEqual(255, alpha.getpixel((64, 0)))
        self.assertEqual(255, alpha.getpixel((127, 32)))
        self.assertEqual(255, alpha.getpixel((64, 63)))
        self.assertEqual(255, alpha.getpixel((0, 32)))
        self.assertEqual(0, alpha.getpixel((0, 0)))
        visible = [pixel for pixel in floor.get_flattened_data() if pixel[3] > 0]
        self.assertNotIn((230, 68, 184, 255), visible)


if __name__ == "__main__":
    unittest.main()
