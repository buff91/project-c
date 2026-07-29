from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from generate_openpose_guides import (  # noqa: E402
    BODY_18_BONES,
    BODY_18_COLORS,
    POSES,
    render_pose,
)


class OpenPoseGuideTests(unittest.TestCase):
    def test_body_18_topology_uses_standard_limb_order(self) -> None:
        self.assertEqual(("neck", "rs", 0), BODY_18_BONES[0])
        self.assertEqual(("neck", "ls", 1), BODY_18_BONES[1])
        self.assertEqual(("neck", "rh", 6), BODY_18_BONES[6])
        self.assertEqual(("neck", "lh", 9), BODY_18_BONES[9])
        self.assertEqual(("neck", "head", 12), BODY_18_BONES[12])
        self.assertEqual(17, len(BODY_18_BONES))
        self.assertEqual(18, len(BODY_18_COLORS))

    def test_death_map_renders_one_body_18_control_image(self) -> None:
        image = render_pose(POSES["death"])
        self.assertEqual((512, 512), image.size)
        self.assertEqual("RGB", image.mode)
        self.assertNotEqual((0, 0, 0), image.getpixel((128, 343)))


if __name__ == "__main__":
    unittest.main()
