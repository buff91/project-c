from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_b2_macro_floor_v1 import (
    BASE_FLOOR,
    ROLE_COORDS,
    SOURCE,
    SPRITE_SIZE,
    VIEWS,
    build_assets,
    reassemble_outputs,
    rotate_grid_coord,
    rotate_topdown,
)
from torchstone_palette import load_gpl


def _synthetic_floor() -> Image.Image:
    floor = Image.new("RGBA", SPRITE_SIZE, (5, 7, 12, 0))
    draw = ImageDraw.Draw(floor)
    draw.polygon(
        ((64, 0), (127, 31), (64, 63), (0, 31)),
        fill=(107, 113, 120, 255),
    )
    draw.line(((64, 0), (127, 31), (64, 63), (0, 31), (64, 0)), fill=(44, 49, 56, 255))
    return floor


def _synthetic_source(size: tuple[int, int] = (320, 320)) -> Image.Image:
    source = Image.new("RGBA", size, (255, 0, 255, 255))
    draw = ImageDraw.Draw(source)
    right = size[0] - 33
    bottom = size[1] - 33
    draw.rectangle((32, 32, right, bottom), fill=(59, 63, 69, 255))
    # Both marks cross the future cell boundaries; neither belongs to one cell.
    draw.rectangle((size[0] // 2 - 5, 44, size[0] // 2 + 5, bottom - 12), fill=(148, 155, 161, 255))
    draw.line((48, bottom - 42, right - 20, 60), fill=(33, 28, 26, 255), width=5)
    return source


def _pixel_for_coord(coord: tuple[int, int], size: int) -> tuple[int, int]:
    half = size // 2
    return coord[0] * half + half // 2, coord[1] * half + half // 2


class B2MacroFloorProcessorTests(unittest.TestCase):
    def test_outputs_are_sixteen_palette_locked_hard_alpha_floor_cells(self) -> None:
        build = build_assets(_synthetic_source(), _synthetic_floor())
        expected = {
            f"env-floor-b2-macro-role-{role}-view-{view}"
            for role in range(4)
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

    def test_cells_reassemble_to_every_processed_master_on_visible_pixels(self) -> None:
        build = build_assets(_synthetic_source(), _synthetic_floor())
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

    def test_physical_roles_map_to_the_expected_window_in_each_view(self) -> None:
        expected = {
            0: ((64, 0), (128, 32), (0, 32), (64, 64)),
            1: ((0, 32), (64, 0), (64, 64), (128, 32)),
            2: ((64, 64), (0, 32), (128, 32), (64, 0)),
            3: ((128, 32), (64, 64), (64, 0), (0, 32)),
        }
        self.assertEqual(expected, {spec.index: spec.role_windows for spec in VIEWS})

    def test_full_topdown_rotation_matches_physical_role_mapping(self) -> None:
        size = 8
        topdown = Image.new("RGBA", (size, size), (5, 7, 12, 255))
        pixels = topdown.load()
        role_colors = (
            (44, 49, 56, 255),
            (84, 91, 97, 255),
            (122, 62, 28, 255),
            (148, 155, 161, 255),
        )
        for role, coord in enumerate(ROLE_COORDS):
            left = coord[0] * (size // 2)
            top = coord[1] * (size // 2)
            for py in range(top, top + size // 2):
                for px in range(left, left + size // 2):
                    pixels[px, py] = role_colors[role]

        for view in range(4):
            rotated = rotate_topdown(topdown, view)
            for role, coord in enumerate(ROLE_COORDS):
                sample = _pixel_for_coord(rotate_grid_coord(coord, view), size)
                self.assertEqual(role_colors[role], rotated.getpixel(sample))

    def test_approved_source_conforms_when_present(self) -> None:
        if not SOURCE.exists():
            self.skipTest("approved B2 macro-floor source is not present")
        if not BASE_FLOOR.exists():
            self.skipTest("canonical environment floor is not present")
        build = build_assets(
            Image.open(SOURCE).convert("RGBA"),
            Image.open(BASE_FLOOR).convert("RGBA"),
        )
        self.assertEqual(16, len(build.outputs))

    def test_rejects_empty_non_square_or_wrong_floor_sources(self) -> None:
        with self.assertRaisesRegex(ValueError, "contains no visible patch"):
            build_assets(
                Image.new("RGBA", (128, 128), (255, 0, 255, 255)),
                _synthetic_floor(),
            )

        non_square = Image.new("RGBA", (320, 240), (255, 0, 255, 255))
        ImageDraw.Draw(non_square).rectangle(
            (32, 32, 287, 207),
            fill=(59, 63, 69, 255),
        )
        with self.assertRaisesRegex(ValueError, "must be square"):
            build_assets(non_square, _synthetic_floor())

        with self.assertRaisesRegex(ValueError, "unexpected base floor size"):
            build_assets(_synthetic_source(), Image.new("RGBA", (64, 32)))


if __name__ == "__main__":
    unittest.main()
