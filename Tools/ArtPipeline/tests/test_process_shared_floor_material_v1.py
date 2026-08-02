from __future__ import annotations

import sys
import unittest
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = TOOLS_DIR.parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_shared_floor_material_v1 import (
    CANVAS_SIZE,
    OUTER_BAND_PIXELS,
    SOURCE,
    WEAR_MASSES,
    WEAR_RATIO,
    build_floor,
    canonical_diamond_mask,
    encode_png,
    extract_material,
    floor_source_colors,
    outer_band_mask,
    screen_to_material,
)
from torchstone_palette import load_gpl


CURRENT_FLOOR = ROOT / "Assets/_Project/Art/Environment/env-floor.png"
MAGENTA = (255, 0, 255, 255)


def _pixels(image: Image.Image):
    return (
        image.get_flattened_data()
        if hasattr(image, "get_flattened_data")
        else image.getdata()
    )


def _striped_source() -> Image.Image:
    source = Image.new("RGBA", (320, 320), MAGENTA)
    draw = ImageDraw.Draw(source)
    draw.rectangle((32, 32, 287, 287), fill=(90, 94, 99, 255))
    draw.rectangle((32, 32, 116, 287), fill=(28, 31, 35, 255))
    draw.rectangle((203, 32, 287, 287), fill=(185, 190, 194, 255))
    return source


def _source_tone_counts(floor: Image.Image) -> Counter[str]:
    colors = floor_source_colors()
    by_color = {value: role for role, value in colors.items()}
    return Counter(
        by_color[pixel]
        for pixel in _pixels(floor)
        if pixel[3] > 0
    )


def _color_components(
    image: Image.Image,
    color: tuple[int, int, int, int],
) -> list[set[tuple[int, int]]]:
    remaining = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if image.getpixel((x, y)) == color
    }
    components: list[set[tuple[int, int]]] = []
    while remaining:
        start = min(remaining, key=lambda point: (point[1], point[0]))
        remaining.remove(start)
        pending = [start]
        component = {start}
        while pending:
            x, y = pending.pop()
            for offset_y in (-1, 0, 1):
                for offset_x in (-1, 0, 1):
                    if offset_x == 0 and offset_y == 0:
                        continue
                    neighbor = (x + offset_x, y + offset_y)
                    if neighbor not in remaining:
                        continue
                    remaining.remove(neighbor)
                    component.add(neighbor)
                    pending.append(neighbor)
        components.append(component)
    return components


class SharedFloorMaterialProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = Image.open(SOURCE).convert("RGBA")
        cls.floor = build_floor(cls.source)

    def test_checked_in_source_conforms_to_canonical_geometry_and_palette(self) -> None:
        floor = self.floor
        alpha = floor.getchannel("A")
        current_floor = Image.open(CURRENT_FLOOR).convert("RGBA")
        current_alpha = current_floor.getchannel("A")
        visible = [pixel for pixel in _pixels(floor) if pixel[3] > 0]

        self.assertEqual(CANVAS_SIZE, floor.size)
        self.assertEqual(floor.tobytes(), current_floor.tobytes())
        self.assertEqual(current_alpha.tobytes(), alpha.tobytes())
        self.assertEqual(4098, len(visible))
        self.assertEqual({0, 255}, set(_pixels(alpha)))
        for point in ((64, 0), (127, 32), (64, 63), (0, 32)):
            self.assertEqual(255, alpha.getpixel(point))

        allowed = set(floor_source_colors().values())
        self.assertEqual(allowed, set(visible))
        self.assertTrue({pixel[:3] for pixel in visible}.issubset(set(load_gpl())))
        # All legal colors are neutral greys.  Warm/rust/signal colors therefore
        # cannot leak from the ImageGen source into this shared gameplay surface.
        self.assertTrue(all(max(pixel[:3]) - min(pixel[:3]) <= 13 for pixel in visible))

    def test_raw_wear_budget_collapses_to_one_runtime_stone_role(self) -> None:
        counts = _source_tone_counts(self.floor)
        visible_count = sum(counts.values())
        ratios = {role: count / visible_count for role, count in counts.items()}

        self.assertAlmostEqual(WEAR_RATIO, ratios["wear"], delta=0.002)
        self.assertAlmostEqual(1.0 - WEAR_RATIO, ratios["mid"], delta=0.002)

        # Mirror PrototypeEnvironmentSprites.ToneMapEnvironmentPixel thresholds:
        # both raw tones map to Stone; none become Outline, StoneShadow or StoneLight.
        def luminance(pixel: tuple[int, int, int, int]) -> float:
            red, green, blue, _ = pixel
            return (red * 0.2126 + green * 0.7152 + blue * 0.0722) / 255.0

        visible_luminances = {
            luminance(pixel)
            for pixel in _pixels(self.floor)
            if pixel[3] > 0
        }
        self.assertTrue(
            all(0.28 <= value < 0.50 for value in visible_luminances),
            visible_luminances,
        )

    def test_outer_three_pixel_band_is_unbroken_midtone(self) -> None:
        band = outer_band_mask(canonical_diamond_mask(), OUTER_BAND_PIXELS)
        mid = floor_source_colors()["mid"]
        band_points = [
            (x, y)
            for y in range(CANVAS_SIZE[1])
            for x in range(CANVAS_SIZE[0])
            if band.getpixel((x, y)) > 0
        ]

        self.assertGreater(len(band_points), 0)
        self.assertTrue(all(self.floor.getpixel(point) == mid for point in band_points))

    def test_non_midtone_detail_uses_complete_two_pixel_clusters_without_speckles(self) -> None:
        mid = floor_source_colors()["mid"]
        pixels = self.floor.load()
        for y in range(CANVAS_SIZE[1]):
            for x in range(CANVAS_SIZE[0]):
                pixel = pixels[x, y]
                if pixel[3] == 0 or pixel == mid:
                    continue
                left = x // 2 * 2
                top = y // 2 * 2
                self.assertTrue(
                    all(
                        pixels[left + dx, top + dy] == pixel
                        for dy in range(2)
                        for dx in range(2)
                    )
                )

        # The midtone edge and every authored 2x2 detail pixel have a same-color
        # eight-neighbour, which is the pipeline's no-isolated-1px definition.
        for y in range(CANVAS_SIZE[1]):
            for x in range(CANVAS_SIZE[0]):
                pixel = pixels[x, y]
                if pixel[3] == 0:
                    continue
                neighbors = [
                    pixels[nx, ny]
                    for ny in range(max(0, y - 1), min(CANVAS_SIZE[1], y + 2))
                    for nx in range(max(0, x - 1), min(CANVAS_SIZE[0], x + 2))
                    if (nx, ny) != (x, y)
                ]
                self.assertIn(pixel, neighbors, f"isolated color at {(x, y)}")

    def test_checked_in_source_consolidates_wear_into_at_most_three_broad_masses(self) -> None:
        colors = floor_source_colors()
        wear_components = _color_components(self.floor, colors["wear"])

        self.assertGreaterEqual(len(wear_components), 1)
        self.assertLessEqual(len(wear_components), WEAR_MASSES)
        # At least twelve complete 2x2 blocks: each mark reads as a material
        # mass at gameplay scale, never as a tiny decorative island.
        self.assertTrue(
            all(len(component) >= 48 for component in wear_components)
        )

    def test_source_broad_values_control_projected_cluster_placement(self) -> None:
        floor = build_floor(_striped_source())
        colors = floor_source_colors()

        def mean_material_u(color: tuple[int, int, int, int]) -> float:
            values = [
                screen_to_material(x, y)[0]
                for y in range(CANVAS_SIZE[1])
                for x in range(CANVAS_SIZE[0])
                if floor.getpixel((x, y)) == color
            ]
            return sum(values) / len(values)

        self.assertLess(mean_material_u(colors["wear"]), 0.35)

    def test_build_and_png_encoding_are_byte_deterministic(self) -> None:
        first = build_floor(self.source)
        second = build_floor(self.source.copy())
        self.assertEqual(first.tobytes(), second.tobytes())
        self.assertEqual(encode_png(first), encode_png(second))

    def test_rejects_missing_or_non_square_material(self) -> None:
        with self.assertRaisesRegex(ValueError, "no non-chroma material"):
            extract_material(Image.new("RGBA", (128, 128), MAGENTA))

        source = Image.new("RGBA", (200, 200), MAGENTA)
        ImageDraw.Draw(source).rectangle((20, 70, 179, 129), fill=(70, 75, 80, 255))
        with self.assertRaisesRegex(ValueError, "must be square"):
            extract_material(source)


if __name__ == "__main__":
    unittest.main()
