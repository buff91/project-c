from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_prop_quality_v1 import (
    DIRECTIONS,
    PALETTE,
    PROP_SIZE,
    SERVICE_MASTER_SIZE,
    WALL_SIZE,
    build_assets,
    reassemble_service_outputs,
)
from torchstone_palette import load_gpl, load_gpl_entries


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _assert_two_by_two_clusters(test: unittest.TestCase, image: Image.Image) -> None:
    pixels = image.load()
    for y in range(0, image.height, 2):
        for x in range(0, image.width, 2):
            block = {
                pixels[x + dx, y + dy]
                for dy in range(2)
                for dx in range(2)
            }
            test.assertEqual(1, len(block), f"broken 2x2 cluster at {(x, y)}")


class B2PropQualityProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.build = build_assets()

    def test_outputs_are_exact_native_palette_locked_hard_alpha_set(self) -> None:
        expected = {"prop-explosive-barrel"}
        for direction in DIRECTIONS:
            expected.update(
                {
                    f"env-wall-{direction.name}",
                    f"env-wall-torch-{direction.name}",
                    f"env-wall-pipes-{direction.name}",
                    f"env-wall-cabinet-{direction.name}",
                }
            )
            expected.update(
                f"env-wall-b2-service-segment-{segment}-{direction.name}"
                for segment in range(3)
            )
        self.assertEqual(expected, set(self.build.outputs))

        palette = set(load_gpl())
        for name, image in self.build.outputs.items():
            expected_size = PROP_SIZE if name == "prop-explosive-barrel" else WALL_SIZE
            self.assertEqual(expected_size, image.size, name)
            self.assertTrue(
                set(image.getchannel("A").get_flattened_data()).issubset({0, 255}),
                name,
            )
            visible = [pixel for pixel in _pixels(image) if pixel[3] > 0]
            self.assertTrue(visible, name)
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette), name)
            _assert_two_by_two_clusters(self, image)

    def test_build_is_byte_deterministic(self) -> None:
        rebuilt = build_assets()
        self.assertEqual(set(self.build.outputs), set(rebuilt.outputs))
        for name, image in self.build.outputs.items():
            self.assertEqual(image.tobytes(), rebuilt.outputs[name].tobytes(), name)
        for direction, master in self.build.service_masters.items():
            self.assertEqual(
                master.tobytes(),
                rebuilt.service_masters[direction].tobytes(),
                direction,
            )

    def test_wall_variants_are_flush_and_keep_base_silhouette(self) -> None:
        for direction in DIRECTIONS:
            base_name = f"env-wall-{direction.name}"
            base = self.build.outputs[base_name]
            base_alpha = base.getchannel("A").tobytes()
            for variant in ("torch", "pipes", "cabinet"):
                image = self.build.outputs[f"env-wall-{variant}-{direction.name}"]
                self.assertEqual(base_alpha, image.getchannel("A").tobytes())

            # The kiosk is a shallow wall object: its lower floor-contact band is
            # byte-identical to the quiet wall, not an invented plinth/footprint.
            kiosk = self.build.outputs[f"env-wall-cabinet-{direction.name}"]
            for x in range(0, WALL_SIZE[0], 2):
                top = ((WALL_SIZE[0] - 2 - x) // 4) * 2 if direction.slope < 0 else (x // 4) * 2
                lower = min(WALL_SIZE[1], top + 72)
                for y in range(lower, min(WALL_SIZE[1], top + 82)):
                    self.assertEqual(base.getpixel((x, y)), kiosk.getpixel((x, y)))

    def test_service_segments_reassemble_shared_master_byte_for_byte(self) -> None:
        for direction in DIRECTIONS:
            master = self.build.service_masters[direction.name]
            self.assertEqual(SERVICE_MASTER_SIZE, master.size)
            rebuilt = reassemble_service_outputs(self.build.outputs, direction)
            self.assertEqual(master.tobytes(), rebuilt.tobytes())
            _assert_two_by_two_clusters(self, master)

    def test_service_hose_lives_on_segment_zero_and_quiet_cells_stay_subordinate(self) -> None:
        hose_colors = {
            PALETTE["rust-2"][:3],
            PALETTE["rust-4"][:3],
            PALETTE["sig-hazard"][:3],
        }
        for direction in DIRECTIONS:
            segments = [
                self.build.outputs[
                    f"env-wall-b2-service-segment-{segment}-{direction.name}"
                ]
                for segment in range(3)
            ]
            hose_counts = [
                sum(
                    1
                    for red, green, blue, alpha in _pixels(image)
                    if alpha and (red, green, blue) in hose_colors
                )
                for image in segments
            ]
            self.assertGreater(hose_counts[0], hose_counts[1] * 2)
            self.assertGreater(hose_counts[0], hose_counts[2] * 2)

    def test_fuel_cell_is_compact_rotation_neutral_and_signal_restrained(self) -> None:
        cell = self.build.outputs["prop-explosive-barrel"]
        bounds = cell.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        left, top, right, bottom = bounds
        self.assertGreaterEqual(right - left, 50)
        self.assertLessEqual(right - left, 56)
        self.assertGreaterEqual(bottom - top, 82)
        self.assertLessEqual(bottom - top, 86)
        self.assertGreaterEqual(bottom, 116)
        self.assertLessEqual(bottom, 120)
        self.assertLessEqual(abs((left + right) - PROP_SIZE[0]), 4)

        visible = [pixel for pixel in _pixels(cell) if pixel[3] > 0]
        signals = {
            PALETTE["sig-hazard"][:3],
            PALETTE["sig-warning"][:3],
        }
        signal_count = sum(1 for pixel in visible if pixel[:3] in signals)
        self.assertGreater(signal_count, 0)
        self.assertLess(signal_count / len(visible), 0.05)
        forbidden = {
            PALETTE["sig-neon-cyan"][:3],
            PALETTE["sig-neon-magenta"][:3],
        }
        self.assertFalse(any(pixel[:3] in forbidden for pixel in visible))

    def test_quiet_wall_uses_broad_value_groups_without_signal_noise(self) -> None:
        signal_colors = {
            rgb
            for name, rgb in load_gpl_entries()
            if name.startswith("sig-")
        }
        for direction in DIRECTIONS:
            wall = self.build.outputs[f"env-wall-{direction.name}"]
            visible = [pixel for pixel in _pixels(wall) if pixel[3] > 0]
            colors = {pixel[:3] for pixel in visible}
            self.assertGreaterEqual(len(colors), 8)
            self.assertLessEqual(len(colors), 14)
            self.assertTrue(colors.isdisjoint(signal_colors))

            # At least sixty percent of opaque pixels belong to the four broad
            # structural greys/darks, leaving effects and silhouettes room to read.
            quiet = {
                PALETTE["dark-cool"][:3],
                PALETTE["grey-1"][:3],
                PALETTE["grey-2"][:3],
                PALETTE["grey-3"][:3],
            }
            quiet_count = sum(1 for pixel in visible if pixel[:3] in quiet)
            self.assertGreaterEqual(quiet_count / len(visible), 0.60)


if __name__ == "__main__":
    unittest.main()
