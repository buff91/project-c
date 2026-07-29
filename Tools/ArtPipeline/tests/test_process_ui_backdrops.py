from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_ui_backdrops_v1 import RUNTIME_SIZE, build_main_menu_backdrop
from torchstone_palette import load_gpl, lock_rgba_to_palette


class UiBackdropProcessorTests(unittest.TestCase):
    def test_main_menu_backdrop_is_runtime_size_and_palette_locked(self) -> None:
        source = Image.new("RGB", (64, 64), (123, 87, 45))

        backdrop = build_main_menu_backdrop(source)

        self.assertEqual(RUNTIME_SIZE, backdrop.size)
        self.assertTrue(
            set(backdrop.get_flattened_data()).issubset(set(load_gpl()))
        )

    def test_rgba_palette_lock_preserves_alpha(self) -> None:
        source = Image.new("RGBA", (2, 1))
        source.putdata(((123, 87, 45, 255), (20, 30, 40, 17)))

        locked = lock_rgba_to_palette(source)

        self.assertEqual(
            (255, 17),
            tuple(locked.getchannel("A").get_flattened_data()),
        )
        self.assertTrue(
            {pixel[:3] for pixel in locked.get_flattened_data()}.issubset(
                set(load_gpl())
            )
        )


if __name__ == "__main__":
    unittest.main()
