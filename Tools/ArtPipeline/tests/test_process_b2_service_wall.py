from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_service_wall_v1 import (
    ALPHA_CUTOFF,
    CELL_SIZE,
    DIRECTIONS,
    MASTER_SIZE,
    SHEET_SIZE,
    SOURCE,
    SPRITE_SIZE,
    build_assets,
    envelope_lines,
    reassemble_outputs,
)
from torchstone_palette import load_gpl


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _synthetic_sheet() -> Image.Image:
    sheet = Image.new("RGBA", SHEET_SIZE, (255, 0, 255, 255))
    for row in range(2):
        slope = -0.28 if row == 0 else 0.28
        for column in range(3):
            cell = Image.new("RGBA", CELL_SIZE, (255, 0, 255, 255))
            draw = ImageDraw.Draw(cell)
            left = 34
            right = 478
            center = (left + right) * 0.5
            top_center = 118
            bottom_center = 394
            points = (
                (left, round(top_center + slope * (left - center))),
                (right, round(top_center + slope * (right - center))),
                (right, round(bottom_center + slope * (right - center))),
                (left, round(bottom_center + slope * (left - center))),
            )
            draw.polygon(points, fill=(43, 49, 57, 255))
            for offset in (-62, -45, 24, 64):
                y_left = round(top_center + offset + slope * (left - center))
                y_right = round(top_center + offset + slope * (right - center))
                draw.line((left, y_left, right, y_right), fill=(92, 94, 91, 255), width=8)
            accent_x = (112, 256, 430)[column]
            accent_y = round(250 + slope * (accent_x - center))
            accent = ((35, 213, 224), (247, 165, 42), (226, 38, 171))[column]
            draw.rectangle(
                (accent_x - 10, accent_y - 8, accent_x + 10, accent_y + 8),
                fill=(*accent, 255),
            )
            sheet.alpha_composite(cell, (column * 512, row * 512))
    return sheet


class B2ServiceWallProcessorTests(unittest.TestCase):
    def test_outputs_are_six_palette_locked_hard_alpha_wall_cells(self) -> None:
        build = build_assets(_synthetic_sheet())
        expected = {
            f"env-wall-b2-service-segment-{segment}-{direction.name}"
            for direction in DIRECTIONS
            for segment in range(3)
        }
        self.assertEqual(expected, set(build.outputs))
        palette = set(load_gpl())
        for image in build.outputs.values():
            self.assertEqual(SPRITE_SIZE, image.size)
            self.assertTrue(set(image.getchannel("A").getdata()).issubset({0, 255}))
            visible = [pixel for pixel in _pixels(image) if pixel[3] > 0]
            self.assertTrue(visible)
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))

    def test_segments_reassemble_to_processed_master_byte_for_byte(self) -> None:
        build = build_assets(_synthetic_sheet())
        for direction in DIRECTIONS:
            rebuilt = reassemble_outputs(build.outputs, direction)
            self.assertEqual(
                build.masters[direction.name].tobytes(),
                rebuilt.tobytes(),
            )

    def test_master_envelopes_follow_isometric_slope_and_pivot_footline(self) -> None:
        build = build_assets(_synthetic_sheet())
        for direction in DIRECTIONS:
            master = build.masters[direction.name]
            (top_slope, _), (bottom_slope, bottom_intercept) = envelope_lines(master)
            self.assertAlmostEqual(direction.target_slope, top_slope, delta=0.06)
            self.assertAlmostEqual(direction.target_slope, bottom_slope, delta=0.06)
            for segment, (left, top) in enumerate(direction.windows):
                center_x = left + SPRITE_SIZE[0] * 0.5
                foot_y = bottom_slope * center_x + bottom_intercept
                self.assertAlmostEqual(
                    top + 96,
                    foot_y,
                    delta=2.0,
                    msg=f"{direction.name} segment {segment} footline",
                )

    def test_exterior_chroma_is_removed_but_authored_signal_survives(self) -> None:
        build = build_assets(_synthetic_sheet())
        palette = set(load_gpl())
        for master in build.masters.values():
            visible = [pixel for pixel in _pixels(master) if pixel[3] >= ALPHA_CUTOFF]
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))
            self.assertLess(len(visible), MASTER_SIZE[0] * MASTER_SIZE[1])
            self.assertTrue(any(max(pixel[:3]) - min(pixel[:3]) > 40 for pixel in visible))

    def test_approved_sheet_conforms_when_present(self) -> None:
        if not SOURCE.exists():
            self.skipTest("approved B2 service-wall source is not present")
        build = build_assets(Image.open(SOURCE).convert("RGBA"))
        for direction in DIRECTIONS:
            rebuilt = reassemble_outputs(build.outputs, direction)
            self.assertEqual(build.masters[direction.name].tobytes(), rebuilt.tobytes())

    def test_rejects_wrong_sheet_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected B2 service-wall source size"):
            build_assets(Image.new("RGBA", (512, 512)))


if __name__ == "__main__":
    unittest.main()
