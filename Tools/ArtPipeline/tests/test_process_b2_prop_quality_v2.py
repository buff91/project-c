from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_prop_quality_v2 import (
    DIRECTIONS,
    PALETTE,
    PROP_SIZE,
    SERVICE_MASTER_SIZE,
    WALL_SIZE,
    build_assets,
    reassemble_service_outputs,
)
from torchstone_palette import load_gpl


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _row_width(image: Image.Image, y: int) -> int:
    xs = [x for x in range(image.width) if image.getpixel((x, y))[3] > 0]
    return 0 if not xs else max(xs) - min(xs) + 1


class B2PropQualityV2ProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.build = build_assets()

    def test_outputs_keep_native_palette_alpha_and_cluster_contract(self) -> None:
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
            self.assertEqual(
                PROP_SIZE if name == "prop-explosive-barrel" else WALL_SIZE,
                image.size,
                name,
            )
            self.assertTrue(set(image.getchannel("A").get_flattened_data()).issubset({0, 255}))
            visible = [pixel for pixel in _pixels(image) if pixel[3] > 0]
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette), name)
            pixels = image.load()
            for y in range(0, image.height, 2):
                for x in range(0, image.width, 2):
                    self.assertEqual(
                        1,
                        len(
                            {
                                pixels[x + dx, y + dy]
                                for dy in range(2)
                                for dx in range(2)
                            }
                        ),
                        f"{name} broken cluster at {(x, y)}",
                    )

    def test_build_is_byte_deterministic(self) -> None:
        rebuilt = build_assets()
        for name, image in self.build.outputs.items():
            self.assertEqual(image.tobytes(), rebuilt.outputs[name].tobytes(), name)
        for direction, master in self.build.service_masters.items():
            self.assertEqual(master.tobytes(), rebuilt.service_masters[direction].tobytes())

    def test_wall_shell_has_strong_frame_bevel_and_recessed_material_density(self) -> None:
        frame_colors = {
            PALETTE["grey-3"][:3],
            PALETTE["grey-4"][:3],
            PALETTE["grey-5"][:3],
        }
        depth_colors = {
            PALETTE["dark-void"][:3],
            PALETTE["dark-cool"][:3],
            PALETTE["grey-1"][:3],
        }
        for direction in DIRECTIONS:
            wall = self.build.outputs[f"env-wall-{direction.name}"]
            visible = [pixel for pixel in _pixels(wall) if pixel[3] > 0]
            frame_count = sum(1 for pixel in visible if pixel[:3] in frame_colors)
            depth_count = sum(1 for pixel in visible if pixel[:3] in depth_colors)
            self.assertGreater(frame_count / len(visible), 0.18)
            self.assertGreater(depth_count / len(visible), 0.35)
            self.assertGreaterEqual(len({pixel[:3] for pixel in visible}), 10)

    def test_service_cells_reassemble_and_have_three_distinct_functions(self) -> None:
        for direction in DIRECTIONS:
            master = self.build.service_masters[direction.name]
            self.assertEqual(SERVICE_MASTER_SIZE, master.size)
            self.assertEqual(
                master.tobytes(),
                reassemble_service_outputs(self.build.outputs, direction).tobytes(),
            )
            cells = [
                self.build.outputs[
                    f"env-wall-b2-service-segment-{segment}-{direction.name}"
                ]
                for segment in range(3)
            ]
            self.assertEqual(3, len({cell.tobytes() for cell in cells}))

            # Hose/reel orange mass and its amber lower service box stay in seg0.
            hose_colors = {
                PALETTE["rust-3"][:3],
                PALETTE["rust-4"][:3],
                PALETTE["sig-hazard"][:3],
            }
            hose_counts = [
                sum(1 for pixel in _pixels(cell) if pixel[3] and pixel[:3] in hose_colors)
                for cell in cells
            ]
            self.assertGreater(hose_counts[0], hose_counts[1] * 2)
            self.assertGreater(hose_counts[0], hose_counts[2] * 2)
            lower_amber = sum(
                1
                for y in range(72, 104)
                for x in range(WALL_SIZE[0])
                if cells[0].getpixel((x, y))[:3] == PALETTE["sig-hazard"][:3]
            )
            self.assertGreater(lower_amber, 0)

    def test_kiosk_is_narrow_flush_and_stops_above_wall_foot(self) -> None:
        for direction in DIRECTIONS:
            base = self.build.outputs[f"env-wall-{direction.name}"]
            kiosk = self.build.outputs[f"env-wall-cabinet-{direction.name}"]
            self.assertEqual(base.getchannel("A").tobytes(), kiosk.getchannel("A").tobytes())
            changed = []
            for y in range(WALL_SIZE[1]):
                for x in range(WALL_SIZE[0]):
                    if base.getpixel((x, y)) != kiosk.getpixel((x, y)):
                        changed.append((x, y))
            self.assertTrue(changed)
            self.assertLessEqual(max(x for x, _ in changed) - min(x for x, _ in changed) + 1, 34)

            for x, y in changed:
                logical_x = x // 2
                top = ((31 - logical_x) // 2 if direction.slope < 0 else logical_x // 2) * 2
                self.assertLess(y - top, 72)

    def test_fuel_cell_has_cylindrical_not_crate_silhouette(self) -> None:
        cell = self.build.outputs["prop-explosive-barrel"]
        bounds = cell.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        left, top, right, bottom = bounds
        self.assertGreaterEqual(right - left, 52)
        self.assertLessEqual(right - left, 56)
        self.assertGreaterEqual(bottom - top, 82)
        self.assertLessEqual(bottom - top, 86)
        self.assertGreaterEqual(bottom, 116)
        self.assertLessEqual(bottom, 120)
        self.assertLessEqual(abs((left + right) - PROP_SIZE[0]), 4)

        shoulder = _row_width(cell, 54)
        middle = _row_width(cell, 78)
        bottom_ring = _row_width(cell, 114)
        self.assertLess(shoulder, middle)
        self.assertLess(bottom_ring, middle)

        # Runtime owns grounding; there must be no detached baked shadow pixels.
        for y in range(bottom, PROP_SIZE[1]):
            self.assertFalse(any(cell.getpixel((x, y))[3] for x in range(PROP_SIZE[0])))


if __name__ == "__main__":
    unittest.main()
