import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import generate_runtime_art_v2 as runtime_art
from process_b2_prop_quality_v4 import build_source_assets as build_b2_prop_quality_assets


class GenerateRuntimeArtV2Tests(unittest.TestCase):
    def test_only_explosive_barrel_delegates_to_b2_quality_owner(self):
        old_output = runtime_art.OUTPUT
        old_only = runtime_art._ONLY_NAME
        old_written = list(runtime_art._WRITTEN)
        try:
            with tempfile.TemporaryDirectory() as temporary:
                runtime_art.OUTPUT = Path(temporary)
                runtime_art._ONLY_NAME = "prop-explosive-barrel"
                runtime_art._WRITTEN.clear()

                runtime_art.main()

                output = runtime_art.OUTPUT / "prop-explosive-barrel.png"
                self.assertEqual(
                    build_b2_prop_quality_assets()
                    .outputs["prop-explosive-barrel"]
                    .tobytes(),
                    Image.open(output).convert("RGBA").tobytes(),
                )
                self.assertEqual(["prop-explosive-barrel"], runtime_art._WRITTEN)
                self.assertEqual([output], list(runtime_art.OUTPUT.glob("*.png")))
        finally:
            runtime_art.OUTPUT = old_output
            runtime_art._ONLY_NAME = old_only
            runtime_art._WRITTEN[:] = old_written

    def test_only_player_marker_writes_corner_ticks_without_touching_other_assets(self):
        old_output = runtime_art.OUTPUT
        old_only = runtime_art._ONLY_NAME
        old_written = list(runtime_art._WRITTEN)
        try:
            with tempfile.TemporaryDirectory() as temporary:
                runtime_art.OUTPUT = Path(temporary)
                runtime_art._ONLY_NAME = "marker-player"
                runtime_art._WRITTEN.clear()

                runtime_art.main()

                output = runtime_art.OUTPUT / "marker-player.png"
                self.assertTrue(output.exists())
                self.assertEqual(["marker-player"], runtime_art._WRITTEN)
                self.assertEqual([output], list(runtime_art.OUTPUT.glob("*.png")))

                image = Image.open(output).convert("RGBA")
                self.assertEqual((128, 64), image.size)
                self.assertEqual(0, image.getpixel((64, 32))[3])
                self.assertGreater(image.getpixel((64, 4))[3], 0)
                self.assertGreater(image.getpixel((122, 32))[3], 0)
                self.assertGreater(image.getpixel((64, 58))[3], 0)
                self.assertGreater(image.getpixel((4, 32))[3], 0)
        finally:
            runtime_art.OUTPUT = old_output
            runtime_art._ONLY_NAME = old_only
            runtime_art._WRITTEN[:] = old_written

    def test_only_target_marker_writes_open_corner_brackets(self):
        old_output = runtime_art.OUTPUT
        old_only = runtime_art._ONLY_NAME
        old_written = list(runtime_art._WRITTEN)
        try:
            with tempfile.TemporaryDirectory() as temporary:
                runtime_art.OUTPUT = Path(temporary)
                runtime_art._ONLY_NAME = "marker-target"
                runtime_art._WRITTEN.clear()

                runtime_art.main()

                output = runtime_art.OUTPUT / "marker-target.png"
                self.assertTrue(output.exists())
                self.assertEqual(["marker-target"], runtime_art._WRITTEN)
                self.assertEqual([output], list(runtime_art.OUTPUT.glob("*.png")))

                image = Image.open(output).convert("RGBA")
                self.assertEqual((128, 64), image.size)
                self.assertEqual(0, image.getpixel((64, 32))[3])
                self.assertEqual(0, image.getpixel((94, 32))[3])
                self.assertGreater(image.getpixel((64, 4))[3], 0)
                self.assertGreater(image.getpixel((122, 32))[3], 0)
                self.assertGreater(image.getpixel((64, 58))[3], 0)
                self.assertGreater(image.getpixel((4, 32))[3], 0)
        finally:
            runtime_art.OUTPUT = old_output
            runtime_art._ONLY_NAME = old_only
            runtime_art._WRITTEN[:] = old_written


if __name__ == "__main__":
    unittest.main()
