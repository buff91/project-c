from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_band_floors_v1 import (
    BASE_VALUE_TOLERANCE,
    SHEET_SIZE,
    SPECS,
    SPRITE_SIZE,
    build_outputs,
)
from torchstone_palette import load_gpl


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
    return sheet


class BandFloorProcessorTests(unittest.TestCase):
    def _base_floor(self, fill: tuple[int, int, int, int]) -> Image.Image:
        return Image.new("RGBA", SPRITE_SIZE, fill)

    def test_outputs_have_contract_names_sizes_hard_alpha_and_shared_palette(self) -> None:
        outputs = build_outputs(_sheet((96, 92, 88, 255)), self._base_floor((96, 92, 88, 255)))

        self.assertEqual({spec.output_name for spec in SPECS}, set(outputs))
        palette = set(load_gpl())
        for image in outputs.values():
            self.assertEqual(SPRITE_SIZE, image.size)
            self.assertTrue(
                set(image.getchannel("A").get_flattened_data()).issubset({0, 255})
            )
            visible = {p[:3] for p in image.get_flattened_data() if p[3] > 0}
            self.assertTrue(visible.issubset(palette))

    def test_rejects_band_tile_that_drifts_from_shared_floor_value(self) -> None:
        # §1-c 게이트 회귀 — 석재 기본색(명도)이 깊이에 따라 달라지면 마감이 거부돼야 한다.
        bright = _sheet((214, 208, 196, 255))
        with self.assertRaisesRegex(ValueError, "base color gate"):
            build_outputs(bright, self._base_floor((60, 58, 55, 255)))
        self.assertLess(BASE_VALUE_TOLERANCE, 0.2)

    def test_suppresses_reserved_teal_outside_boss_tiles(self) -> None:
        # §1-c 회귀 — 틸은 Hole/출구 예약이라 mid/deep 산출물에 남으면 안 된다.
        sheet = _sheet((96, 92, 88, 255))
        draw = ImageDraw.Draw(sheet)
        for index in range(6):
            left = index % 3 * 512
            top = index // 3 * 512
            draw.ellipse(
                (left + 236, top + 240, left + 290, top + 272),
                fill=(79, 167, 160, 255),  # anomaly-3 틸 웅덩이
            )

        outputs = build_outputs(sheet, self._base_floor((96, 92, 88, 255)))
        teal_family = {
            (55, 106, 103), (79, 167, 160), (154, 223, 232),
            (198, 244, 247), (56, 153, 166), (61, 225, 232),
        }
        for name in ("env-floor-mid", "env-floor-deep",
                     "env-floor-mid-raised", "env-floor-deep-raised"):
            visible = {p[:3] for p in outputs[name].get_flattened_data() if p[3] > 0}
            self.assertFalse(visible & teal_family, f"{name} kept reserved teal")
        boss_visible = {
            p[:3] for p in outputs["env-floor-boss"].get_flattened_data() if p[3] > 0
        }
        self.assertTrue(boss_visible & teal_family, "boss lost its anomaly seam")

    def test_rejects_wrong_sheet_size(self) -> None:
        with self.assertRaisesRegex(ValueError, "unexpected band floor"):
            build_outputs(
                Image.new("RGBA", (1024, 1024)),
                Image.new("RGBA", SPRITE_SIZE),
            )


if __name__ == "__main__":
    unittest.main()
