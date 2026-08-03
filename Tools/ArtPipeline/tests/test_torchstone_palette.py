from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from torchstone_palette import (
    IDENTITY_PREFIXES,
    load_gpl_entries,
    lock_rgba_to_named_palette,
    lock_rgba_to_palette,
    lock_to_palette,
)


SKIN_1 = (138, 90, 68)  # .gpl skin-1 — 아이덴티티 램프의 대표값
FABRIC_2 = (113, 97, 80)  # 기존 재료 램프 중 skin-1과 가장 가까운 이웃


class IdentityLockTests(unittest.TestCase):
    def test_gpl_still_carries_identity_entries(self) -> None:
        names = [name for name, _ in load_gpl_entries()]
        self.assertIn("skin-1", names)
        self.assertTrue(any(name.startswith("hair-") for name in names))

    def test_default_lock_excludes_identity_ramps(self) -> None:
        image = Image.new("RGB", (4, 4), SKIN_1)

        locked = lock_to_palette(image)

        identity_values = {
            rgb
            for name, rgb in load_gpl_entries()
            if name.startswith(IDENTITY_PREFIXES)
        }
        self.assertNotIn(locked.getpixel((0, 0)), identity_values)

    def test_opt_in_lock_keeps_identity_ramps(self) -> None:
        image = Image.new("RGB", (4, 4), SKIN_1)

        locked = lock_to_palette(image, include_identity=True)

        self.assertEqual(SKIN_1, locked.getpixel((0, 0)))

    def test_material_colors_survive_both_modes(self) -> None:
        image = Image.new("RGB", (4, 4), FABRIC_2)

        for include_identity in (False, True):
            locked = lock_to_palette(image, include_identity=include_identity)
            self.assertEqual(FABRIC_2, locked.getpixel((0, 0)))

    def test_rgba_lock_forwards_identity_flag(self) -> None:
        image = Image.new("RGBA", (4, 4), (*SKIN_1, 255))

        default = lock_rgba_to_palette(image)
        opted = lock_rgba_to_palette(image, include_identity=True)

        self.assertNotEqual(SKIN_1, default.getpixel((0, 0))[:3])
        self.assertEqual((*SKIN_1, 255), opted.getpixel((0, 0)))

    def test_named_subset_lock_preserves_alpha_and_uses_only_requested_colors(self) -> None:
        image = Image.new("RGBA", (2, 1))
        image.putdata(((*SKIN_1, 255), (240, 73, 42, 0)))

        locked = lock_rgba_to_named_palette(
            image,
            ("fabric-2", "sig-warning"),
        )

        self.assertEqual(FABRIC_2, locked.getpixel((0, 0))[:3])
        self.assertEqual(0, locked.getpixel((1, 0))[3])

    def test_named_subset_lock_rejects_unknown_entry(self) -> None:
        with self.assertRaisesRegex(ValueError, "missing-entry"):
            lock_rgba_to_named_palette(
                Image.new("RGBA", (1, 1)),
                ("missing-entry",),
            )


if __name__ == "__main__":
    unittest.main()
