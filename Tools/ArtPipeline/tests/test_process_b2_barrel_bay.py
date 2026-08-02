from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_barrel_bay_v1 import (
    MASTER_SIZE,
    QUADRANT_SIZE,
    SHEET_SIZE,
    SOURCE,
    SPRITE_SIZE,
    VIEWS,
    build_assets,
    reassemble_outputs,
)
from torchstone_palette import load_gpl


def _synthetic_floor() -> Image.Image:
    floor = Image.new("RGBA", SPRITE_SIZE, (5, 7, 12, 0))
    draw = ImageDraw.Draw(floor)
    draw.polygon(
        ((64, 0), (127, 31), (64, 63), (0, 31)),
        fill=(44, 49, 56, 255),
    )
    return floor


def _synthetic_sheet() -> Image.Image:
    sheet = Image.new("RGBA", SHEET_SIZE, (255, 0, 255, 255))
    for spec in VIEWS:
        cell = Image.new("RGBA", QUADRANT_SIZE, (255, 0, 255, 255))
        draw = ImageDraw.Draw(cell)
        # A consistent two-diamond silhouette with distinct service/drain material.
        scale_x = 3
        scale_y = 3
        offset_x = 96
        offset_y = 112
        colors = ((44, 49, 56, 255), (84, 91, 97, 255))
        for segment, (left, top) in enumerate(spec.windows):
            center_x = offset_x + (left + 64) * scale_x
            center_y = offset_y + (top + 32) * scale_y
            draw.polygon(
                (
                    (center_x, center_y - 32 * scale_y),
                    (center_x + 64 * scale_x, center_y),
                    (center_x, center_y + 32 * scale_y),
                    (center_x - 64 * scale_x, center_y),
                ),
                fill=colors[segment],
            )
        column = spec.index % 2
        row = spec.index // 2
        sheet.alpha_composite(
            cell,
            (column * QUADRANT_SIZE[0], row * QUADRANT_SIZE[1]),
        )
    return sheet


class B2BarrelBayProcessorTests(unittest.TestCase):
    def test_outputs_are_eight_palette_locked_hard_alpha_floor_cells(self) -> None:
        build = build_assets(_synthetic_sheet(), _synthetic_floor())
        expected = {
            f"env-floor-b2-barrel-bay-{role}-view-{view}"
            for role in ("service", "drain")
            for view in range(4)
        }
        self.assertEqual(expected, set(build.outputs))
        palette = set(load_gpl())
        for image in build.outputs.values():
            self.assertEqual(SPRITE_SIZE, image.size)
            self.assertTrue(
                set(image.getchannel("A").get_flattened_data()).issubset({0, 255})
            )
            visible = [pixel for pixel in image.get_flattened_data() if pixel[3] > 0]
            self.assertTrue(visible)
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))

    def test_cells_reassemble_to_each_processed_master_byte_for_byte(self) -> None:
        build = build_assets(_synthetic_sheet(), _synthetic_floor())
        for spec in VIEWS:
            rebuilt = reassemble_outputs(build.outputs, spec)
            expected = build.masters[spec.index]
            for expected_pixel, rebuilt_pixel in zip(
                expected.get_flattened_data(),
                rebuilt.get_flattened_data(),
            ):
                self.assertEqual(expected_pixel[3], rebuilt_pixel[3])
                if expected_pixel[3] > 0:
                    self.assertEqual(expected_pixel, rebuilt_pixel)

    def test_each_view_keeps_service_and_drain_in_physical_cell_windows(self) -> None:
        expected = {
            0: ((64, 0), (0, 32)),
            1: ((0, 0), (64, 32)),
            2: ((0, 32), (64, 0)),
            3: ((64, 32), (0, 0)),
        }
        self.assertEqual(expected, {spec.index: spec.windows for spec in VIEWS})

    def test_approved_sheet_conforms_when_present(self) -> None:
        if not SOURCE.exists():
            self.skipTest("approved B2 barrel-bay source is not present")
        build = build_assets(
            Image.open(SOURCE).convert("RGBA"),
            Image.open(
                Path(__file__).resolve().parents[3]
                / "Assets/_Project/Art/Environment/env-floor.png"
            ).convert("RGBA"),
        )
        self.assertEqual(8, len(build.outputs))

    def test_rejects_wrong_source_or_floor_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected B2 barrel-bay source size"):
            build_assets(Image.new("RGBA", (512, 512)), _synthetic_floor())
        with self.assertRaisesRegex(ValueError, "unexpected base floor size"):
            build_assets(_synthetic_sheet(), Image.new("RGBA", (64, 32)))


if __name__ == "__main__":
    unittest.main()
