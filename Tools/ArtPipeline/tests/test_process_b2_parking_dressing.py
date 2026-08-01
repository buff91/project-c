from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_parking_dressing_v1 import (
    CANVAS_SIZE,
    SPECS,
    build_outputs,
    fit_object,
    principal_axis_slope,
)
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

        expected_names = {spec.output_name for spec in SPECS}
        expected_names.update(
            f"{spec.output_name}-view-{view}"
            for spec in SPECS
            for view in range(4)
        )
        self.assertEqual(expected_names, set(outputs))
        palette = set(load_gpl())
        for image in outputs.values():
            self.assertEqual(CANVAS_SIZE, image.size)
            self.assertTrue(
                set(image.getchannel("A").get_flattened_data()).issubset({0, 255})
            )
            visible = [pixel for pixel in image.get_flattened_data() if pixel[3] > 0]
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))
            self.assertGreater(len(set(visible)), 1, "dressing vanished below the floor")

        for spec in SPECS:
            view_zero = outputs[f"{spec.output_name}-view-0"]
            view_one = outputs[f"{spec.output_name}-view-1"]
            view_two = outputs[f"{spec.output_name}-view-2"]
            view_three = outputs[f"{spec.output_name}-view-3"]
            self.assertEqual(view_zero.tobytes(), view_two.tobytes())
            self.assertEqual(view_one.tobytes(), view_three.tobytes())
            self.assertNotEqual(view_zero.tobytes(), view_one.tobytes())
            self.assertEqual(view_zero.tobytes(), outputs[spec.output_name].tobytes())

    def test_fit_reprojects_shallow_generated_prop_to_exact_two_to_one_axis(self) -> None:
        source = Image.new("RGBA", (420, 260), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        draw.polygon(
            ((30, 106), (370, 174), (350, 224), (10, 156)),
            fill=(84, 91, 97, 255),
        )
        draw.rectangle((60, 90, 82, 150), fill=(44, 49, 56, 255))

        fitted = fit_object(source, SPECS[0])
        mirrored = fitted.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

        self.assertAlmostEqual(0.5, principal_axis_slope(fitted), delta=0.06)
        self.assertAlmostEqual(-0.5, principal_axis_slope(mirrored), delta=0.06)

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
