from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = TOOLS_DIR.parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_band_floors_v1 import (
    BASE_FLOOR,
    CONTRAST_RATIOS,
    COOL_FLOOR_COLOR_NAMES,
    OUTPUT,
    PIXEL_CLUSTER,
    SHARED_RATIOS,
    SHEET_SIZE,
    SOURCE,
    SPECS,
    SPRITE_SIZE,
    build_outputs,
)
from process_shared_floor_material_v1 import (
    OUTER_BAND_PIXELS,
    canonical_diamond_mask,
    encode_png,
    floor_source_colors,
    outer_band_mask,
)
from torchstone_palette import load_gpl_entries


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _sheet(fill: tuple[int, int, int, int]) -> Image.Image:
    sheet = Image.new("RGBA", SHEET_SIZE, (255, 0, 255, 255))
    draw = ImageDraw.Draw(sheet)
    for index in range(6):
        left = index % 3 * 512
        top = index // 3 * 512
        draw.polygon(
            [
                (left + 256, top + 128),
                (left + 500, top + 256),
                (left + 256, top + 384),
                (left + 12, top + 256),
            ],
            fill=fill,
        )
        # A long, dark generated crack is deliberately supplied.  The
        # processor must reduce it to separated 2x2 accents rather than copy it.
        draw.line(
            (left + 112, top + 256, left + 400, top + 256),
            fill=(18, 12, 8, 255),
            width=24,
        )
    return sheet


def _base_floor() -> Image.Image:
    colors = floor_source_colors()
    mask = canonical_diamond_mask()
    floor = Image.new("RGBA", SPRITE_SIZE, (0, 0, 0, 0))
    floor.paste(colors["mid"], mask=mask)
    # Include the second legal source grey so toggled subtle details exercise
    # both directions without changing the canonical mask.
    draw = ImageDraw.Draw(floor)
    draw.rectangle((62, 30, 65, 33), fill=colors["wear"])
    floor.putalpha(mask)
    return floor


def _changed_points(base: Image.Image, output: Image.Image) -> set[tuple[int, int]]:
    return {
        (x, y)
        for y in range(output.height)
        for x in range(output.width)
        if output.getpixel((x, y)) != base.getpixel((x, y))
    }


def _runtime_role(pixel: tuple[int, int, int, int]) -> str:
    red, green, blue, _ = pixel
    luminance = (red * 0.2126 + green * 0.7152 + blue * 0.0722) / 255
    if luminance < 0.16:
        return "outline"
    if luminance < 0.28:
        return "shadow"
    if luminance < 0.50:
        return "stone"
    return "light"


class BandFloorProcessorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.base = _base_floor()
        self.outputs = build_outputs(_sheet((160, 122, 78, 255)), self.base)

    def test_outputs_keep_names_canonical_geometry_and_cool_named_palette(self) -> None:
        self.assertEqual({spec.output_name for spec in SPECS}, set(self.outputs))
        canonical = canonical_diamond_mask()
        entries = dict(load_gpl_entries())
        allowed = {entries[name] for name in COOL_FLOOR_COLOR_NAMES}

        for name, image in self.outputs.items():
            self.assertEqual(SPRITE_SIZE, image.size, name)
            self.assertEqual(canonical.tobytes(), image.getchannel("A").tobytes(), name)
            self.assertEqual({0, 255}, set(_pixels(image.getchannel("A"))), name)
            visible = {pixel[:3] for pixel in _pixels(image) if pixel[3] > 0}
            self.assertTrue(visible.issubset(allowed), f"{name}: {visible - allowed}")

    def test_outputs_preserve_outer_band_and_exact_shared_pixel_budgets(self) -> None:
        mask = canonical_diamond_mask()
        protected = outer_band_mask(mask, OUTER_BAND_PIXELS)
        visible_count = sum(value > 0 for value in _pixels(mask))

        for spec in SPECS:
            image = self.outputs[spec.output_name]
            changed = _changed_points(self.base, image)
            expected_blocks = round(
                visible_count * (1.0 - SHARED_RATIOS[spec.band])
                / (PIXEL_CLUSTER ** 2)
            )
            self.assertEqual(expected_blocks * 4, len(changed), spec.output_name)
            self.assertTrue(
                all(protected.getpixel(point) == 0 for point in changed),
                spec.output_name,
            )

            exact_ratio = 1.0 - len(changed) / visible_count
            self.assertAlmostEqual(
                SHARED_RATIOS[spec.band],
                exact_ratio,
                delta=4 / visible_count,
                msg=spec.output_name,
            )

    def test_contrast_is_sparse_separated_2x2_and_never_maps_to_outline(self) -> None:
        colors = dict(load_gpl_entries())
        shadow = (*colors["grey-2"], 255)
        visible_count = sum(
            pixel[3] > 0 for pixel in _pixels(self.base)
        )

        for spec in SPECS:
            image = self.outputs[spec.output_name]
            shadow_points = {
                (x, y)
                for y in range(image.height)
                for x in range(image.width)
                if image.getpixel((x, y)) == shadow
            }
            expected_blocks = round(
                visible_count * CONTRAST_RATIOS[spec.band]
                / (PIXEL_CLUSTER ** 2)
            )
            self.assertEqual(expected_blocks * 4, len(shadow_points), spec.output_name)

            blocks = set()
            for x, y in shadow_points:
                block = (x // 2, y // 2)
                blocks.add(block)
                left, top = block[0] * 2, block[1] * 2
                self.assertTrue(
                    all(
                        image.getpixel((left + dx, top + dy)) == shadow
                        for dy in range(2)
                        for dx in range(2)
                    ),
                    f"{spec.output_name}: incomplete block at {block}",
                )
            for block in blocks:
                self.assertFalse(
                    any(
                        other != block
                        and abs(block[0] - other[0]) <= 1
                        and abs(block[1] - other[1]) <= 1
                        for other in blocks
                    ),
                    f"{spec.output_name}: adjacent contrast blocks near {block}",
                )

            roles = {
                _runtime_role(pixel)
                for pixel in _pixels(image)
                if pixel[3] > 0
            }
            self.assertNotIn("outline", roles, spec.output_name)
            self.assertNotIn("light", roles, spec.output_name)

    def test_flat_and_raised_outputs_share_the_same_authored_top(self) -> None:
        for band in ("mid", "deep", "boss"):
            flat = self.outputs[f"env-floor-{band}"]
            raised = self.outputs[f"env-floor-{band}-raised"]
            self.assertEqual(flat.tobytes(), raised.tobytes(), band)

    def test_generated_warm_or_teal_source_colors_cannot_leak(self) -> None:
        visible = {
            pixel[:3]
            for image in self.outputs.values()
            for pixel in _pixels(image)
            if pixel[3] > 0
        }
        self.assertFalse(
            any(red > blue * 1.12 and green > blue * 1.04 for red, green, blue in visible)
        )
        self.assertFalse(
            any(green > red * 1.10 and blue > red * 1.10 for red, green, blue in visible)
        )

    def test_build_is_pixel_and_png_byte_deterministic(self) -> None:
        sheet = _sheet((160, 122, 78, 255))
        first = build_outputs(sheet, self.base)
        second = build_outputs(sheet.copy(), self.base.copy())
        for name in first:
            self.assertEqual(first[name].tobytes(), second[name].tobytes(), name)
            self.assertEqual(encode_png(first[name]), encode_png(second[name]), name)

    def test_rejects_noncanonical_or_off_palette_shared_floor(self) -> None:
        rectangular = Image.new("RGBA", SPRITE_SIZE, (107, 113, 120, 255))
        with self.assertRaisesRegex(ValueError, "canonical diamond"):
            build_outputs(_sheet((80, 80, 80, 255)), rectangular)

        off_palette = _base_floor()
        off_palette.putpixel((64, 32), (120, 80, 50, 255))
        with self.assertRaisesRegex(ValueError, "grey-3/grey-4"):
            build_outputs(_sheet((80, 80, 80, 255)), off_palette)

    def test_rejects_wrong_sheet_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected band floor"):
            build_outputs(Image.new("RGBA", (1024, 1024)), self.base)


class CheckedInBandFloorContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = Image.open(SOURCE).convert("RGBA")
        cls.base = Image.open(BASE_FLOOR).convert("RGBA")
        cls.generated = build_outputs(cls.source, cls.base)

    def test_checked_in_source_builds_all_canonical_repeat_safe_outputs(self) -> None:
        canonical = canonical_diamond_mask()
        allowed = {
            rgb
            for name, rgb in load_gpl_entries()
            if name in COOL_FLOOR_COLOR_NAMES
        }
        for spec in SPECS:
            image = self.generated[spec.output_name]
            self.assertEqual(canonical.tobytes(), image.getchannel("A").tobytes())
            self.assertTrue(
                {pixel[:3] for pixel in _pixels(image) if pixel[3] > 0}.issubset(allowed)
            )

    def test_checked_in_outputs_match_the_processor_byte_for_byte(self) -> None:
        # This intentionally fails when the adopted source or processor changes
        # until the six runtime PNGs are republished by the pipeline.
        for spec in SPECS:
            path = OUTPUT / f"{spec.output_name}.png"
            self.assertTrue(path.exists(), path)
            self.assertEqual(
                encode_png(self.generated[spec.output_name]),
                path.read_bytes(),
                f"stale generated asset: {path}",
            )


if __name__ == "__main__":
    unittest.main()
