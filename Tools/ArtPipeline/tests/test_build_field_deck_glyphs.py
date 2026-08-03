import sys
import unittest
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from build_field_deck_glyphs_v1 import BUILDERS, OUTPUT, SIZE, build_all


class FieldDeckGlyphTests(unittest.TestCase):
    def test_glyphs_are_native_hard_alpha_and_palette_limited(self) -> None:
        glyphs = build_all()
        self.assertEqual(set(BUILDERS), set(glyphs))

        for name, image in glyphs.items():
            with self.subTest(name=name):
                self.assertEqual((SIZE, SIZE), image.size)
                self.assertLessEqual(
                    set(image.getchannel("A").get_flattened_data()),
                    {0, 255},
                )
                visible = {
                    pixel[:3]
                    for pixel in image.get_flattened_data()
                    if pixel[3] != 0
                }
                self.assertGreaterEqual(len(visible), 1)
                self.assertLessEqual(len(visible), 3)
                self.assertIsNotNone(image.getchannel("A").getbbox())

    def test_published_glyphs_keep_unity_meta_files(self) -> None:
        for name in BUILDERS:
            with self.subTest(name=name):
                path = OUTPUT / f"ui-field-{name}.png"
                self.assertTrue(path.is_file(), path)
                self.assertTrue(Path(f"{path}.meta").is_file(), f"{path}.meta")


if __name__ == "__main__":
    unittest.main()
