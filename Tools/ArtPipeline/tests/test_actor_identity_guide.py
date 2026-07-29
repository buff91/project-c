from __future__ import annotations

import argparse
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from generate_actor_identity_guide import build_guide, parse_box


class ActorIdentityGuideTests(unittest.TestCase):
    def test_parse_box_requires_four_positive_size_values(self) -> None:
        self.assertEqual((1, 2, 3, 4), parse_box("1,2,3,4"))
        with self.assertRaises(argparse.ArgumentTypeError):
            parse_box("1,2,0,4")
        with self.assertRaises(argparse.ArgumentTypeError):
            parse_box("1,2,3")

    def test_build_guide_can_clear_obsolete_equipment_region(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            source = root / "actor.png"
            destination = root / "guide.png"
            Image.new("RGBA", (2, 2), (10, 20, 30, 255)).save(source)

            build_guide(source, destination, [(0, 0, 16, 16)])

            with Image.open(destination) as guide:
                self.assertEqual((512, 512), guide.size)
                self.assertEqual((255, 0, 255), guide.getpixel((0, 0)))
                self.assertEqual((10, 20, 30), guide.getpixel((256, 256)))


if __name__ == "__main__":
    unittest.main()
