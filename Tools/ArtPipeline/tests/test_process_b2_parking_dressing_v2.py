from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_parking_dressing_v2 import (
    CANVAS_SIZE,
    SPECS,
    VIEW_COUNT,
    build_outputs,
    build_prop_overlay,
    neutralize_floor_source,
)
from torchstone_palette import load_gpl, load_gpl_entries, lock_rgba_to_palette


def _base_floor() -> Image.Image:
    image = Image.new("RGBA", CANVAS_SIZE, (5, 7, 12, 0))
    pixels = image.load()
    for y in range(CANVAS_SIZE[1]):
        half_width = min(y * 2, (CANVAS_SIZE[1] - 1 - y) * 2)
        for x in range(CANVAS_SIZE[0] // 2 - half_width, CANVAS_SIZE[0] // 2 + half_width + 1):
            if 0 <= x < CANVAS_SIZE[0]:
                pixels[x, y] = (59, 63, 69, 255)
    return image


def _assert_two_by_two_clusters(test: unittest.TestCase, image: Image.Image) -> None:
    for top in range(0, image.height, 2):
        for left in range(0, image.width, 2):
            block = {
                image.getpixel((x, y))
                for y in range(top, top + 2)
                for x in range(left, left + 2)
            }
            test.assertEqual(
                1,
                len(block),
                f"non-native cluster at {(left, top)}: {block}",
            )


class B2ParkingDressingV2ProcessorTests(unittest.TestCase):
    def test_outputs_keep_four_world_views_and_floor_contract(self) -> None:
        base_floor = _base_floor()
        outputs = build_outputs(base_floor)

        expected_names = {spec.output_name for spec in SPECS}
        expected_names.update(
            f"{spec.output_name}-view-{view}"
            for spec in SPECS
            for view in range(VIEW_COUNT)
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

        for spec in SPECS:
            views = [
                outputs[f"{spec.output_name}-view-{view}"].tobytes()
                for view in range(VIEW_COUNT)
            ]
            self.assertEqual(VIEW_COUNT, len(set(views)), "world views must stay distinct")
            self.assertEqual(views[0], outputs[spec.output_name].tobytes())

    def test_native_overlays_meet_approved_silhouette_bounds(self) -> None:
        expected = {
            "env-floor-b2-parking-stop": (80, 20),
            "env-floor-b2-fallen-sign": (76, 18),
        }
        palette = set(load_gpl())
        signal_names = {"sig-torch", "sig-gold", "sig-hazard", "sig-warning"}
        signal_colors = {
            rgb for name, rgb in load_gpl_entries() if name in signal_names
        }

        for spec in SPECS:
            for view in range(VIEW_COUNT):
                overlay = build_prop_overlay(spec, view)
                bounds = overlay.getchannel("A").getbbox()
                self.assertIsNotNone(bounds)
                left, top, right, bottom = bounds
                self.assertEqual(expected[spec.output_name], (right - left, bottom - top))
                self.assertEqual(64, (left + right) // 2)
                self.assertEqual(51, bottom - 1)
                _assert_two_by_two_clusters(self, overlay)

                visible = [pixel for pixel in overlay.get_flattened_data() if pixel[3] > 0]
                colors = {pixel[:3] for pixel in visible}
                self.assertTrue(colors.issubset(palette))
                self.assertGreaterEqual(len(colors), 8)
                self.assertLessEqual(len(colors), 12)
                signal_count = sum(pixel[:3] in signal_colors for pixel in visible)
                self.assertLessEqual(signal_count / len(visible), 0.05)

    def test_quarter_views_alternate_screen_axis_without_ai_redraws(self) -> None:
        for spec in SPECS:
            slopes = []
            for view in range(VIEW_COUNT):
                overlay = build_prop_overlay(spec, view)
                alpha = overlay.getchannel("A")
                left, top, right, bottom = alpha.getbbox()
                midpoint = (left + right) // 2
                left_y = [
                    y
                    for y in range(top, bottom)
                    for x in range(left, midpoint)
                    if alpha.getpixel((x, y))
                ]
                right_y = [
                    y
                    for y in range(top, bottom)
                    for x in range(midpoint, right)
                    if alpha.getpixel((x, y))
                ]
                slopes.append(sum(right_y) / len(right_y) - sum(left_y) / len(left_y))

            self.assertGreater(slopes[0], 0)
            self.assertLess(slopes[1], 0)
            self.assertGreater(slopes[2], 0)
            self.assertLess(slopes[3], 0)

    def test_build_is_byte_deterministic_and_preserves_floor_outside_overlay(self) -> None:
        base_floor = _base_floor()
        first = build_outputs(base_floor)
        second = build_outputs(base_floor)
        neutral = lock_rgba_to_palette(neutralize_floor_source(base_floor))

        self.assertEqual(first.keys(), second.keys())
        for spec in SPECS:
            for view in range(VIEW_COUNT):
                name = f"{spec.output_name}-view-{view}"
                self.assertEqual(first[name].tobytes(), second[name].tobytes())
                overlay_alpha = build_prop_overlay(spec, view).getchannel("A")
                for y in range(CANVAS_SIZE[1]):
                    for x in range(CANVAS_SIZE[0]):
                        if overlay_alpha.getpixel((x, y)) == 0:
                            self.assertEqual(
                                neutral.getpixel((x, y)),
                                first[name].getpixel((x, y)),
                            )

    def test_neutral_floor_keeps_alpha_and_removes_warm_chroma(self) -> None:
        floor = Image.new("RGBA", CANVAS_SIZE, (112, 97, 82, 255))
        floor.putpixel((0, 0), (5, 7, 12, 0))

        neutral = neutralize_floor_source(floor)

        self.assertEqual(0, neutral.getpixel((0, 0))[3])
        visible = [pixel for pixel in neutral.get_flattened_data() if pixel[3] > 0]
        self.assertTrue(visible)
        self.assertFalse(
            any(
                red > blue * 1.15 and green > blue * 1.05
                for red, green, blue, _ in visible
            ),
            "base floor must not enter runtime Wood/rust remapping",
        )

    def test_rejects_invalid_view_and_wrong_floor_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "invalid B2 dressing view"):
            build_prop_overlay(SPECS[0], VIEW_COUNT)
        with self.assertRaisesRegex(ValueError, "unexpected base floor size"):
            build_outputs(Image.new("RGBA", (64, 32)))


if __name__ == "__main__":
    unittest.main()
