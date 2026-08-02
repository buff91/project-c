from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_prop_quality_v3 import (
    ALPHA_CUTOFF,
    DIRECTIONS,
    PALETTE,
    PROP_SIZE,
    SERVICE_MASTER_SIZE,
    SOURCE,
    SOURCE_CROP_BY_KEY,
    SOURCE_CROPS,
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


class B2PropQualityV3ProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if not SOURCE.exists():
            raise FileNotFoundError(SOURCE)
        cls.sheet = Image.open(SOURCE).convert("RGBA")
        cls.build = build_assets(cls.sheet)

    def test_source_reference_size_and_measured_crop_bboxes_are_exact(self) -> None:
        self.assertEqual((1672, 941), self.sheet.size)
        self.assertEqual(
            {
                "wall-1-base": (87, 54, 312, 568),
                "wall-2-hose": (399, 49, 628, 554),
                "wall-3-vent": (713, 51, 940, 557),
                "wall-4-quiet": (1021, 54, 1248, 565),
                "wall-5-terminal": (1312, 51, 1591, 567),
                "fuel-cell": (286, 594, 421, 832),
            },
            {spec.key: spec.bbox for spec in SOURCE_CROPS},
        )
        for key, spec in SOURCE_CROP_BY_KEY.items():
            crop = self.sheet.crop(spec.bbox)
            alpha = crop.getchannel("A").point(
                lambda value: 255 if value >= ALPHA_CUTOFF else 0
            )
            self.assertEqual((0, 0, crop.width, crop.height), alpha.getbbox(), key)

    def test_outputs_have_exact_slots_native_sizes_palette_alpha_and_clusters(self) -> None:
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
            self.assertTrue(visible, name)
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

    def test_rising_left_is_exact_mirror_of_rising_right_per_source_slot(self) -> None:
        slot_prefixes = (
            "env-wall-",
            "env-wall-torch-",
            "env-wall-pipes-",
            "env-wall-cabinet-",
        )
        for prefix in slot_prefixes:
            right = self.build.outputs[f"{prefix}rising-right"]
            left = self.build.outputs[f"{prefix}rising-left"]
            self.assertEqual(
                right.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
                left.tobytes(),
                prefix,
            )
        for segment in range(3):
            right = self.build.outputs[
                f"env-wall-b2-service-segment-{segment}-rising-right"
            ]
            left = self.build.outputs[
                f"env-wall-b2-service-segment-{segment}-rising-left"
            ]
            self.assertEqual(
                right.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
                left.tobytes(),
                f"service segment {segment}",
            )

    def test_service_mapping_reassembles_and_keeps_hose_quiet_vent_roles(self) -> None:
        for direction in DIRECTIONS:
            master = self.build.service_masters[direction.name]
            self.assertEqual(SERVICE_MASTER_SIZE, master.size)
            self.assertEqual(
                master.tobytes(),
                reassemble_service_outputs(self.build.outputs, direction).tobytes(),
            )
            hose = self.build.outputs[
                f"env-wall-b2-service-segment-0-{direction.name}"
            ]
            quiet = self.build.outputs[
                f"env-wall-b2-service-segment-1-{direction.name}"
            ]
            vent = self.build.outputs[
                f"env-wall-b2-service-segment-2-{direction.name}"
            ]
            self.assertEqual(3, len({hose.tobytes(), quiet.tobytes(), vent.tobytes()}))

            warm = {
                PALETTE["rust-2"][:3],
                PALETTE["rust-3"][:3],
                PALETTE["rust-4"][:3],
                PALETTE["sig-hazard"][:3],
                PALETTE["sig-torch"][:3],
            }
            warm_counts = [
                sum(1 for pixel in _pixels(image) if pixel[3] and pixel[:3] in warm)
                for image in (hose, quiet, vent)
            ]
            self.assertGreater(warm_counts[0], warm_counts[1] * 2)
            self.assertGreater(warm_counts[0], warm_counts[2])

    def test_terminal_hose_and_cylinder_survive_native_reduction(self) -> None:
        hose = self.build.outputs["env-wall-b2-service-segment-0-rising-right"]
        terminal = self.build.outputs["env-wall-cabinet-rising-right"]
        fuel = self.build.outputs["prop-explosive-barrel"]

        hose_warm = sum(
            1
            for pixel in _pixels(hose)
            if pixel[3] and pixel[:3] in {
                PALETTE["rust-1"][:3],
                PALETTE["rust-2"][:3],
                PALETTE["rust-3"][:3],
                PALETTE["rust-4"][:3],
                PALETTE["sig-gold-deep"][:3],
                PALETTE["sig-warning-deep"][:3],
            }
        )
        self.assertGreater(hose_warm, 80)

        terminal_light = sum(
            1
            for pixel in _pixels(terminal)
            if pixel[3] and pixel[:3] in {
                PALETTE["grey-2"][:3],
                PALETTE["grey-3"][:3],
                PALETTE["stone-mid"][:3],
                PALETTE["fabric-2"][:3],
            }
        )
        terminal_dark = sum(
            1
            for pixel in _pixels(terminal)
            if pixel[3] and pixel[:3] in {
                PALETTE["dark-void"][:3],
                PALETTE["dark-cool"][:3],
            }
        )
        self.assertGreater(terminal_light, 120)
        self.assertGreater(terminal_dark, 48)

        bounds = fuel.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        left, top, right, bottom = bounds
        self.assertGreaterEqual(right - left, 48)
        self.assertLessEqual(right - left, 58)
        self.assertGreaterEqual(bottom - top, 78)
        self.assertLessEqual(bottom - top, 88)
        self.assertGreaterEqual(bottom, 114)
        self.assertLessEqual(bottom, 120)
        self.assertLessEqual(abs((left + right) - PROP_SIZE[0]), 4)
        fuel_warm = sum(
            1
            for pixel in _pixels(fuel)
            if pixel[3] and pixel[:3] in {
                PALETTE["sig-gold-deep"][:3],
                PALETTE["sig-hazard"][:3],
                PALETTE["sig-warning"][:3],
                PALETTE["rust-1"][:3],
                PALETTE["rust-2"][:3],
            }
        )
        self.assertGreater(fuel_warm, 180)

    def test_build_is_byte_deterministic(self) -> None:
        rebuilt = build_assets(self.sheet)
        for name, image in self.build.outputs.items():
            self.assertEqual(image.tobytes(), rebuilt.outputs[name].tobytes(), name)
        for direction, master in self.build.service_masters.items():
            self.assertEqual(master.tobytes(), rebuilt.service_masters[direction].tobytes())

    def test_rejects_wrong_sheet_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected B2 production-sheet size"):
            build_assets(Image.new("RGBA", (512, 512)))


if __name__ == "__main__":
    unittest.main()
