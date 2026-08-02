from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_prop_quality_v3 import SOURCE, build_assets as build_v3_assets
import process_b2_prop_quality_v1 as legacy_v1
from process_b2_prop_quality_v4 import (
    DIRECTIONS,
    MATERIAL_SOURCE,
    PALETTE,
    PROP_SIZE,
    SERVICE_MASTER_SIZE,
    SIGNAL_AND_HIGHLIGHT_COLORS,
    WALL_SIZE,
    build_assets,
    reassemble_service_outputs,
)
from process_environment_sprites import build_canonical_wall_outputs
from torchstone_palette import load_gpl


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _mean_luminance(image: Image.Image) -> float:
    visible = [pixel for pixel in _pixels(image) if pixel[3] > 0]
    return sum(
        red * 0.2126 + green * 0.7152 + blue * 0.0722
        for red, green, blue, _ in visible
    ) / len(visible)


def _boundary_count(image: Image.Image) -> int:
    pixels = image.load()
    count = 0
    for y in range(image.height):
        for x in range(image.width):
            pixel = pixels[x, y]
            if pixel[3] == 0:
                continue
            if x + 1 < image.width:
                right = pixels[x + 1, y]
                if right[3] and right[:3] != pixel[:3]:
                    count += 1
            if y + 1 < image.height:
                below = pixels[x, y + 1]
                if below[3] and below[:3] != pixel[:3]:
                    count += 1
    return count


def _connected_component_count(image: Image.Image) -> int:
    alpha = image.getchannel("A").load()
    remaining = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if alpha[x, y] > 0
    }
    count = 0
    while remaining:
        count += 1
        pending = [remaining.pop()]
        while pending:
            x, y = pending.pop()
            for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    pending.append(neighbor)
    return count


class B2PropQualityV4ProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.sheet = Image.open(SOURCE).convert("RGBA")
        cls.material_sheet = Image.open(MATERIAL_SOURCE).convert("RGBA")
        cls.v3 = build_v3_assets(cls.sheet)
        cls.v4 = build_assets(cls.sheet, cls.material_sheet)

    def test_outputs_keep_slot_size_palette_and_hard_alpha_contract(self) -> None:
        self.assertTrue(set(self.v3.outputs).issubset(self.v4.outputs))
        self.assertEqual(
            {
                "env-wall-window-rising-right",
                "env-wall-window-rising-left",
            },
            set(self.v4.outputs) - set(self.v3.outputs),
        )
        palette = set(load_gpl())
        for name, image in self.v4.outputs.items():
            self.assertEqual(
                PROP_SIZE if name == "prop-explosive-barrel" else WALL_SIZE,
                image.size,
                name,
            )
            self.assertTrue(set(image.getchannel("A").get_flattened_data()).issubset({0, 255}))
            visible = [pixel for pixel in _pixels(image) if pixel[3] > 0]
            self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette), name)

    @patch("process_b2_prop_quality_v4.main")
    def test_legacy_v1_cli_delegates_without_writing_live_art(self, current_main) -> None:
        legacy_v1.main()

        current_main.assert_called_once_with()

    def test_legacy_environment_writer_reuses_canonical_wall_slots(self) -> None:
        outputs = build_canonical_wall_outputs()
        expected_names = {
            "env-wall-rising-right",
            "env-wall-rising-left",
            "env-wall-torch-rising-right",
            "env-wall-torch-rising-left",
        }
        self.assertEqual(expected_names, set(outputs))
        for name, image in outputs.items():
            self.assertEqual(self.v4.outputs[name].tobytes(), image.tobytes(), name)

    def test_direct_final_conform_retains_more_material_boundaries_than_v3(self) -> None:
        minimum_gain = {
            # Compare the authored center, not the twelve-pixel joinable band:
            # v4 deliberately removes the sheet's false full-height end-cap
            # there so repeating cells read as one wall.
            "env-wall-b2-service-segment-0-rising-right": 1.15,
            "env-wall-cabinet-rising-right": 1.20,
            "prop-explosive-barrel": 1.20,
        }
        for name, gain in minimum_gain.items():
            v3 = self.v3.outputs[name]
            v4 = self.v4.outputs[name]
            if name.startswith("env-wall"):
                v3 = v3.crop((12, 0, v3.width - 12, v3.height))
                v4 = v4.crop((12, 0, v4.width - 12, v4.height))
            self.assertGreater(
                _boundary_count(v4),
                _boundary_count(v3) * gain,
                name,
            )

    def test_joinable_side_is_one_common_low_contrast_post(self) -> None:
        structural_colors = {
            PALETTE["dark-void"][:3],
            PALETTE["dark-cool"][:3],
            PALETTE["grey-1"][:3],
            PALETTE["grey-2"][:3],
            PALETTE["grey-3"][:3],
        }
        wall_names = (
            "env-wall",
            "env-wall-torch",
            "env-wall-pipes",
            "env-wall-window",
            "env-wall-cabinet",
        )
        canonical = self.v4.outputs[
            "env-wall-rising-right"
        ].crop((0, 0, 12, WALL_SIZE[1]))
        for name in wall_names:
            right = self.v4.outputs[f"{name}-rising-right"]
            left = self.v4.outputs[f"{name}-rising-left"]
            right_band = right.crop((0, 0, 12, WALL_SIZE[1]))
            left_band = left.crop((WALL_SIZE[0] - 12, 0, WALL_SIZE[0], WALL_SIZE[1]))
            self.assertEqual(canonical.tobytes(), right_band.tobytes(), name)
            self.assertEqual(
                right_band.tobytes(),
                left_band.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
                name,
            )
            visible_colors = {
                pixel[:3]
                for pixel in _pixels(right_band)
                if pixel[3] > 0
            }
            self.assertTrue(visible_colors.issubset(structural_colors), name)

    def test_wall_cap_and_plinth_are_continuous_face_relative_bands(self) -> None:
        expected_bands = {
            0: PALETTE["dark-void"][:3],
            1: PALETTE["dark-void"][:3],
            2: PALETTE["grey-3"][:3],
            3: PALETTE["grey-3"][:3],
            4: PALETTE["grey-2"][:3],
            5: PALETTE["grey-2"][:3],
            6: PALETTE["dark-cool"][:3],
            7: PALETTE["dark-cool"][:3],
            74: PALETTE["grey-2"][:3],
            75: PALETTE["grey-2"][:3],
            76: PALETTE["dark-cool"][:3],
            77: PALETTE["dark-cool"][:3],
            78: PALETTE["dark-cool"][:3],
            79: PALETTE["dark-cool"][:3],
            80: PALETTE["dark-void"][:3],
            81: PALETTE["dark-void"][:3],
        }
        for name, image in self.v4.outputs.items():
            if name == "prop-explosive-barrel":
                continue
            direction = next(
                direction
                for direction in DIRECTIONS
                if name.endswith(direction.name)
            )
            alpha = image.getchannel("A")
            pixels = image.load()
            for x in range(image.width):
                bounds = alpha.crop((x, 0, x + 1, alpha.height)).getbbox()
                self.assertIsNotNone(bounds)
                top = bounds[1]
                for vertical, color in expected_bands.items():
                    self.assertEqual(
                        color,
                        pixels[x, top + vertical][:3],
                        (name, x, vertical),
                    )

    def test_quiet_material_variants_change_only_the_center_body(self) -> None:
        signal_colors = {
            color[:3]
            for name, color in PALETTE.items()
            if name.startswith("sig-")
        }
        for direction in DIRECTIONS:
            base = self.v4.outputs[f"env-wall-{direction.name}"]
            display = self.v4.outputs[f"env-wall-window-{direction.name}"]
            self.assertEqual(
                base.getchannel("A").tobytes(),
                display.getchannel("A").tobytes(),
                direction.name,
            )

            base_pixels = base.load()
            display_pixels = display.load()
            alpha = base.getchannel("A")
            changed = 0
            body_count = 0
            base_luminance = []
            display_luminance = []
            display_chroma = []
            for x in range(base.width):
                bounds = alpha.crop((x, 0, x + 1, alpha.height)).getbbox()
                self.assertIsNotNone(bounds)
                top = bounds[1]
                for y in range(base.height):
                    if base_pixels[x, y][3] == 0:
                        continue
                    vertical = y - top
                    in_body = 12 <= x < 52 and 8 <= vertical <= 73
                    if base_pixels[x, y] != display_pixels[x, y]:
                        self.assertTrue(in_body, (direction.name, x, vertical))
                        changed += 1
                    if not in_body:
                        continue
                    body_count += 1
                    for pixel, samples in (
                        (base_pixels[x, y], base_luminance),
                        (display_pixels[x, y], display_luminance),
                    ):
                        red, green, blue, _ = pixel
                        samples.append(red * 0.2126 + green * 0.7152 + blue * 0.0722)
                    red, green, blue, _ = display_pixels[x, y]
                    display_chroma.append(max(red, green, blue) - min(red, green, blue))
                    self.assertNotIn(display_pixels[x, y][:3], signal_colors)

            changed_fraction = changed / body_count
            self.assertGreaterEqual(changed_fraction, 0.08, direction.name)
            self.assertLessEqual(changed_fraction, 0.65, direction.name)
            self.assertLessEqual(
                abs(
                    sum(base_luminance) / len(base_luminance) -
                    sum(display_luminance) / len(display_luminance)
                ),
                6.0,
                direction.name,
            )
            high_chroma = sum(value > 32 for value in display_chroma)
            self.assertLessEqual(high_chroma / len(display_chroma), 0.05)

    def test_wall_midtones_are_lifted_into_grey_material_ramps(self) -> None:
        grey_mid = {
            PALETTE["grey-1"][:3],
            PALETTE["grey-2"][:3],
            PALETTE["grey-3"][:3],
        }
        for name in (
            "env-wall-rising-right",
            "env-wall-b2-service-segment-1-rising-right",
            "env-wall-pipes-rising-right",
        ):
            v3 = self.v3.outputs[name]
            v4 = self.v4.outputs[name]
            v3_grey = sum(1 for pixel in _pixels(v3) if pixel[3] and pixel[:3] in grey_mid)
            v4_grey = sum(1 for pixel in _pixels(v4) if pixel[3] and pixel[:3] in grey_mid)
            self.assertGreater(v4_grey, v3_grey * 2, name)
            self.assertGreater(_mean_luminance(v4), _mean_luminance(v3) + 7.0, name)

    def test_hose_amber_and_terminal_ivory_are_preserved(self) -> None:
        hose_name = "env-wall-b2-service-segment-0-rising-right"
        amber = {
            PALETTE["rust-3"][:3],
            PALETTE["rust-4"][:3],
            PALETTE["sig-gold-deep"][:3],
            PALETTE["sig-hazard"][:3],
            PALETTE["sig-torch"][:3],
        }
        v3_amber = sum(
            1 for pixel in _pixels(self.v3.outputs[hose_name])
            if pixel[3] and pixel[:3] in amber
        )
        v4_amber = sum(
            1 for pixel in _pixels(self.v4.outputs[hose_name])
            if pixel[3] and pixel[:3] in amber
        )
        self.assertGreater(v4_amber, v3_amber * 3)

        terminal_name = "env-wall-cabinet-rising-right"
        ivory = {
            PALETTE["grey-4"][:3],
            PALETTE["grey-5"][:3],
            PALETTE["grey-6"][:3],
            PALETTE["pc-stone-lit"][:3],
        }
        v3_ivory = sum(
            1 for pixel in _pixels(self.v3.outputs[terminal_name])
            if pixel[3] and pixel[:3] in ivory
        )
        v4_ivory = sum(
            1 for pixel in _pixels(self.v4.outputs[terminal_name])
            if pixel[3] and pixel[:3] in ivory
        )
        self.assertGreater(v4_ivory, max(80, v3_ivory * 2))

    def test_cleanup_removes_alpha_singletons_but_keeps_intentional_signals(self) -> None:
        offsets = (
            (-1, -1), (0, -1), (1, -1),
            (-1, 0), (1, 0),
            (-1, 1), (0, 1), (1, 1),
        )
        signal_pixels = 0
        for image in self.v4.outputs.values():
            pixels = image.load()
            for y in range(image.height):
                for x in range(image.width):
                    pixel = pixels[x, y]
                    if pixel[3] == 0:
                        continue
                    neighbors = [
                        pixels[x + dx, y + dy]
                        for dx, dy in offsets
                        if 0 <= x + dx < image.width
                        and 0 <= y + dy < image.height
                        and pixels[x + dx, y + dy][3] > 0
                    ]
                    self.assertTrue(neighbors, f"isolated alpha at {(x, y)}")
                    if pixel in SIGNAL_AND_HIGHLIGHT_COLORS:
                        signal_pixels += 1
        self.assertGreater(signal_pixels, 24)

    def test_fuel_cell_preserves_direct_resolution_cylinder_and_handle(self) -> None:
        fuel = self.v4.outputs["prop-explosive-barrel"]
        bounds = fuel.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        left, top, right, bottom = bounds
        self.assertGreaterEqual(right - left, 54)
        self.assertLessEqual(right - left, 60)
        self.assertGreaterEqual(bottom - top, 92)
        self.assertLessEqual(bottom - top, 98)
        self.assertGreaterEqual(bottom, 116)
        self.assertLessEqual(bottom, 120)
        self.assertLessEqual(abs((left + right) - PROP_SIZE[0]), 4)

        # Transparent gap under the top handle remains after direct conform.
        handle_region = fuel.crop((48, top, 80, min(bottom, top + 28)))
        alphas = list(handle_region.getchannel("A").get_flattened_data())
        self.assertIn(0, alphas)
        self.assertIn(255, alphas)
        handle_amber = sum(
            1
            for pixel in _pixels(handle_region)
            if pixel[3] and pixel[:3] in {
                PALETTE["sig-torch"][:3],
                PALETTE["sig-hazard"][:3],
                PALETTE["sig-gold-deep"][:3],
            }
        )
        self.assertLess(handle_amber, 8)

    def test_service_masters_reassemble_and_left_is_exact_mirror(self) -> None:
        for direction in DIRECTIONS:
            self.assertEqual(SERVICE_MASTER_SIZE, self.v4.service_masters[direction.name].size)
            self.assertEqual(
                self.v4.service_masters[direction.name].tobytes(),
                reassemble_service_outputs(self.v4.outputs, direction).tobytes(),
            )
        right_suffix = "-rising-right"
        for right_name in sorted(
            name for name in self.v4.outputs if name.endswith(right_suffix)
        ):
            left_name = right_name[:-len(right_suffix)] + "-rising-left"
            self.assertIn(left_name, self.v4.outputs)
            right = self.v4.outputs[right_name]
            left = self.v4.outputs[left_name]
            self.assertEqual(
                right.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
                left.tobytes(),
                right_name,
            )

    def test_wall_alpha_uses_one_exact_isometric_cell_contract(self) -> None:
        for direction in DIRECTIONS:
            expected = self.v4.outputs[f"env-wall-{direction.name}"].getchannel("A")
            for name, image in self.v4.outputs.items():
                if name == "prop-explosive-barrel" or not name.endswith(direction.name):
                    continue
                self.assertEqual(expected.tobytes(), image.getchannel("A").tobytes(), name)

            # The pivot columns land on the common y=96 foot datum, allowing
            # one rounding row on either side of the half-pixel isometric slope.
            for x in (31, 32):
                bounds = expected.crop((x, 0, x + 1, expected.height)).getbbox()
                self.assertIsNotNone(bounds)
                self.assertGreaterEqual(bounds[3], 96)
                self.assertLessEqual(bounds[3], 98)

    def test_service_master_is_connected_and_seams_share_at_least_78_rows(self) -> None:
        for direction in DIRECTIONS:
            master = self.v4.service_masters[direction.name]
            self.assertEqual(1, _connected_component_count(master), direction.name)
            alpha = master.getchannel("A").load()
            for seam_x in (64, 128):
                contact_rows = sum(
                    1
                    for y in range(master.height)
                    if alpha[seam_x - 1, y] and alpha[seam_x, y]
                )
                self.assertGreaterEqual(contact_rows, 78, (direction.name, seam_x))

    def test_build_is_byte_deterministic(self) -> None:
        rebuilt = build_assets(self.sheet, self.material_sheet)
        for name, image in self.v4.outputs.items():
            self.assertEqual(image.tobytes(), rebuilt.outputs[name].tobytes(), name)
        for direction, master in self.v4.service_masters.items():
            self.assertEqual(master.tobytes(), rebuilt.service_masters[direction].tobytes())


if __name__ == "__main__":
    unittest.main()
