from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_hospital_dressing_v1 import SHEET_SIZE, SPECS, build_outputs
from torchstone_palette import load_gpl


class HospitalDressingProcessorTests(unittest.TestCase):
    def test_outputs_have_contract_sizes_hard_alpha_and_shared_palette(self) -> None:
        sheet = Image.new("RGBA", SHEET_SIZE, (255, 0, 255, 255))
        draw = ImageDraw.Draw(sheet)
        for index in range(6):
            left = index % 3 * 512
            top = index // 3 * 512
            draw.rectangle(
                (left + 96, top + 112, left + 415, top + 399),
                fill=(91 + index * 7, 72, 57, 255),
            )

        base_floor = Image.new("RGBA", (128, 64), (31, 31, 27, 255))
        outputs = build_outputs(sheet, base_floor)

        expected_names = {
            output_name
            for spec in SPECS
            for output_name in spec.output_names
        }
        self.assertEqual(expected_names, set(outputs))
        palette = set(load_gpl())
        for spec in SPECS:
            for output_name in spec.output_names:
                image = outputs[output_name]
                self.assertEqual(spec.size, image.size)
                self.assertTrue(
                    set(image.getchannel("A").get_flattened_data()).issubset({0, 255})
                )
                self.assertTrue(
                    {pixel[:3] for pixel in image.get_flattened_data()}.issubset(palette)
                )
                if spec.size == (128, 64):
                    self.assertNotIn(
                        0,
                        image.getchannel("A").get_flattened_data(),
                        f"{output_name} lost the base floor below its dressing",
                    )

    def test_rejects_wrong_sheet_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected hospital dressing"):
            build_outputs(
                Image.new("RGBA", (1024, 1024)),
                Image.new("RGBA", (128, 64)),
            )


if __name__ == "__main__":
    unittest.main()
