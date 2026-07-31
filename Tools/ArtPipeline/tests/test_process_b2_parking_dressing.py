from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_parking_dressing_v1 import CANVAS_SIZE, SPECS, build_outputs
from torchstone_palette import load_gpl


class B2ParkingDressingProcessorTests(unittest.TestCase):
    def test_outputs_are_complete_hard_alpha_palette_locked_floor_tiles(self) -> None:
        sources: dict[str, Image.Image] = {}
        for index, spec in enumerate(SPECS):
            source = Image.new("RGBA", (512, 256), (250, 3, 248, 255))
            draw = ImageDraw.Draw(source)
            draw.polygon(
                ((100, 150), (240, 70), (420, 145), (280, 220)),
                fill=(84 + index * 18, 91, 97, 255),
            )
            sources[spec.source_name] = source

        base_floor = Image.new("RGBA", CANVAS_SIZE, (31, 31, 27, 255))
        outputs = build_outputs(sources, base_floor)

        self.assertEqual({spec.output_name for spec in SPECS}, set(outputs))
        palette = set(load_gpl())
        for image in outputs.values():
            self.assertEqual(CANVAS_SIZE, image.size)
            self.assertTrue(
                set(image.getchannel("A").get_flattened_data()).issubset({0, 255})
            )
            visible = [pixel for pixel in image.get_flattened_data() if pixel[3] > 0]
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))
            self.assertGreater(len(set(visible)), 1, "dressing vanished below the floor")

    def test_rejects_missing_source(self) -> None:
        with self.assertRaisesRegex(ValueError, "missing B2 dressing source"):
            build_outputs({}, Image.new("RGBA", CANVAS_SIZE))

    def test_rejects_wrong_floor_size(self) -> None:
        sources = {
            spec.source_name: Image.new("RGBA", (32, 32), (80, 80, 80, 255))
            for spec in SPECS
        }
        with self.assertRaisesRegex(ValueError, "unexpected base floor size"):
            build_outputs(sources, Image.new("RGBA", (64, 32)))


if __name__ == "__main__":
    unittest.main()
