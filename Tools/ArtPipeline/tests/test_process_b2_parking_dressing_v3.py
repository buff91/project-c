from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_parking_dressing_v3 import (
    CANVAS_SIZE,
    SPECS,
    VIEW_COUNT,
    build_outputs,
    build_prop_overlay,
)
from torchstone_palette import load_gpl, load_gpl_entries


def _base_floor() -> Image.Image:
    image = Image.new("RGBA", CANVAS_SIZE, (5, 7, 12, 0))
    draw_color = (59, 63, 69, 255)
    pixels = image.load()
    for y in range(CANVAS_SIZE[1]):
        radius = min(y * 2, (CANVAS_SIZE[1] - 1 - y) * 2)
        for x in range(CANVAS_SIZE[0] // 2 - radius, CANVAS_SIZE[0] // 2 + radius + 1):
            if 0 <= x < CANVAS_SIZE[0]:
                pixels[x, y] = draw_color
    return image


def _assert_two_by_two(test: unittest.TestCase, image: Image.Image) -> None:
    for top in range(0, image.height, 2):
        for left in range(0, image.width, 2):
            block = {
                image.getpixel((x, y))
                for y in range(top, top + 2)
                for x in range(left, left + 2)
            }
            test.assertEqual(1, len(block), f"broken 2x2 cluster at {(left, top)}")


class B2ParkingDressingV3Tests(unittest.TestCase):
    def test_outputs_are_quarantined_runtime_compatible_floor_tiles(self) -> None:
        outputs = build_outputs(_base_floor())
        expected = {
            f"{spec.output_name}-view-{view}"
            for spec in SPECS
            for view in range(VIEW_COUNT)
        }
        self.assertEqual(expected, set(outputs))
        palette = set(load_gpl())
        for image in outputs.values():
            self.assertEqual(CANVAS_SIZE, image.size)
            self.assertTrue(set(image.getchannel("A").get_flattened_data()).issubset({0, 255}))
            visible = [pixel[:3] for pixel in image.get_flattened_data() if pixel[3]]
            self.assertTrue(set(visible).issubset(palette))

    def test_v3_overlays_have_stronger_volume_and_exact_grounding(self) -> None:
        expected_size = {
            "env-floor-b2-parking-stop": (88, 28),
            "env-floor-b2-fallen-sign": (84, 26),
        }
        palette_entries = dict(load_gpl_entries())
        signal_colors = {
            palette_entries["sig-hazard"],
            palette_entries["sig-warning"],
            palette_entries["sig-gold"],
            palette_entries["sig-torch"],
        }
        volume_colors = {
            palette_entries["grey-1"],
            palette_entries["grey-2"],
            palette_entries["grey-3"],
            palette_entries["grey-4"],
        }

        for spec in SPECS:
            for view in range(VIEW_COUNT):
                overlay = build_prop_overlay(spec, view)
                left, top, right, bottom = overlay.getchannel("A").getbbox()
                self.assertEqual(expected_size[spec.output_name], (right - left, bottom - top))
                self.assertEqual(64, (left + right) // 2)
                self.assertEqual(51, bottom - 1)
                _assert_two_by_two(self, overlay)

                visible = [pixel[:3] for pixel in overlay.get_flattened_data() if pixel[3]]
                self.assertGreaterEqual(len(set(visible) & volume_colors), 4)
                signal_ratio = sum(color in signal_colors for color in visible) / len(visible)
                self.assertGreaterEqual(signal_ratio, 0.01)
                self.assertLessEqual(signal_ratio, 0.05)

    def test_views_rotate_axis_and_keep_world_fixed_wear_distinct(self) -> None:
        for spec in SPECS:
            payloads = []
            slopes = []
            for view in range(VIEW_COUNT):
                overlay = build_prop_overlay(spec, view)
                payloads.append(overlay.tobytes())
                alpha = overlay.getchannel("A")
                left, top, right, bottom = alpha.getbbox()
                middle = (left + right) // 2
                left_y = [
                    y for y in range(top, bottom) for x in range(left, middle)
                    if alpha.getpixel((x, y))
                ]
                right_y = [
                    y for y in range(top, bottom) for x in range(middle, right)
                    if alpha.getpixel((x, y))
                ]
                slopes.append(sum(right_y) / len(right_y) - sum(left_y) / len(left_y))
            self.assertEqual(VIEW_COUNT, len(set(payloads)))
            self.assertGreater(slopes[0], 0)
            self.assertLess(slopes[1], 0)
            self.assertGreater(slopes[2], 0)
            self.assertLess(slopes[3], 0)

    def test_generation_is_byte_deterministic(self) -> None:
        first = build_outputs(_base_floor())
        second = build_outputs(_base_floor())
        self.assertEqual(first.keys(), second.keys())
        for name in first:
            self.assertEqual(first[name].tobytes(), second[name].tobytes())

    def test_rejects_invalid_view(self) -> None:
        with self.assertRaisesRegex(ValueError, "invalid B2 dressing view"):
            build_prop_overlay(SPECS[0], VIEW_COUNT)


if __name__ == "__main__":
    unittest.main()
