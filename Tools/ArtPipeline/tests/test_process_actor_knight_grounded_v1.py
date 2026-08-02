from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_actor_knight_grounded_v1 import (
    ACTOR_PALETTE_NAMES,
    CANVAS_SIZE,
    GROUND_ROW,
    PIXEL_CLUSTER,
    SOURCE,
    build_actor,
    extract_subject,
)
from torchstone_palette import load_gpl_entries


def _flat_data(image: Image.Image):
    if hasattr(image, "get_flattened_data"):
        return image.get_flattened_data()
    return image.getdata()


def _synthetic_source() -> Image.Image:
    source = Image.new("RGBA", (120, 160), (255, 0, 255, 255))
    draw = ImageDraw.Draw(source)
    draw.ellipse((42, 18, 78, 54), fill=(224, 185, 79, 255))
    draw.polygon(((40, 50), (80, 50), (92, 126), (28, 126)), fill=(84, 91, 97, 255))
    draw.rectangle((31, 126, 53, 146), fill=(44, 49, 56, 255))
    draw.rectangle((67, 126, 89, 146), fill=(44, 49, 56, 255))
    # Enclosed key background must also disappear; it is not an authored accent.
    draw.rectangle((58, 72, 61, 75), fill=(255, 0, 255, 255))
    return source


class GroundedActorProcessorTests(unittest.TestCase):
    def test_output_is_palette_locked_hard_alpha_and_foot_locked(self) -> None:
        actor = build_actor(_synthetic_source())
        self.assertEqual(CANVAS_SIZE, actor.size)
        self.assertTrue(set(_flat_data(actor.getchannel("A"))).issubset({0, 255}))

        bounds = actor.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        self.assertEqual(GROUND_ROW + 1, bounds[3])
        self.assertGreaterEqual(bounds[0], 0)
        self.assertLessEqual(bounds[2], CANVAS_SIZE[0])

        entries = dict(load_gpl_entries())
        palette = {entries[name] for name in ACTOR_PALETTE_NAMES}
        visible = [pixel for pixel in _flat_data(actor) if pixel[3] > 0]
        self.assertTrue({pixel[:3] for pixel in visible}.issubset(palette))
        self.assertLessEqual(len({pixel[:3] for pixel in visible}), 24)

        # The 2x cluster contract keeps actor pixels at the same screen density
        # as the 128-regime B2 environment assets.
        for y in range(0, CANVAS_SIZE[1], PIXEL_CLUSTER):
            for x in range(0, CANVAS_SIZE[0], PIXEL_CLUSTER):
                block = {
                    actor.getpixel((x + dx, y + dy))
                    for dy in range(PIXEL_CLUSTER)
                    for dx in range(PIXEL_CLUSTER)
                }
                self.assertEqual(1, len(block))

    def test_chroma_removal_clears_enclosed_key_background(self) -> None:
        subject = extract_subject(_synthetic_source())
        visible = [pixel for pixel in _flat_data(subject) if pixel[3] > 0]
        self.assertNotIn((255, 0, 255, 255), visible)

    def test_rejects_empty_chroma_source(self) -> None:
        with self.assertRaisesRegex(ValueError, "contains no visible subject"):
            extract_subject(Image.new("RGBA", (32, 32), (255, 0, 255, 255)))

    def test_approved_source_conforms_when_present(self) -> None:
        if not SOURCE.exists():
            self.skipTest("approved grounded expeditioner source is not present")
        actor = build_actor(Image.open(SOURCE).convert("RGBA"))
        bounds = actor.getchannel("A").getbbox()
        self.assertIsNotNone(bounds)
        self.assertEqual(GROUND_ROW + 1, bounds[3])
        self.assertGreaterEqual(bounds[2] - bounds[0], 36)
        self.assertGreaterEqual(bounds[3] - bounds[1], 100)


if __name__ == "__main__":
    unittest.main()
