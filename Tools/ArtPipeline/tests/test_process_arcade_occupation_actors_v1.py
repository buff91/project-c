from __future__ import annotations

import sys
import unittest
from collections import Counter
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from process_arcade_occupation_actors_v1 import (
    ACTOR_PALETTE_NAMES,
    CANVAS_SIZE,
    GROUND_ROW,
    PIXEL_CLUSTER,
    SPECS,
    build_actor,
    extract_subject,
)
from torchstone_palette import load_gpl_entries


class ArcadeOccupationActorConformTests(unittest.TestCase):
    @staticmethod
    def _visible_palette_counts(actor: Image.Image) -> Counter[str]:
        names = {color: name for name, color in load_gpl_entries()}
        return Counter(
            names[pixel[:3]]
            for pixel in actor.get_flattened_data()
            if pixel[3] != 0
        )

    def test_sources_conform_to_canvas_palette_clusters_and_ground(self) -> None:
        allowed = {
            color
            for name, color in load_gpl_entries()
            if name in ACTOR_PALETTE_NAMES
        }
        self.assertEqual(len(ACTOR_PALETTE_NAMES), len(allowed))

        for spec in SPECS:
            with self.subTest(spec=spec.output_name):
                self.assertTrue(spec.source.exists(), spec.source)
                actor = build_actor(Image.open(spec.source).convert("RGBA"), spec)
                self.assertEqual(CANVAS_SIZE, actor.size)
                bounds = actor.getchannel("A").getbbox()
                self.assertIsNotNone(bounds)
                assert bounds is not None
                self.assertEqual(GROUND_ROW + 1, bounds[3])
                self.assertLessEqual(bounds[2] - bounds[0], spec.visible_max[0])
                self.assertLessEqual(bounds[3] - bounds[1], spec.visible_max[1])
                self.assertGreaterEqual(bounds[2] - bounds[0], 36)
                self.assertGreaterEqual(bounds[3] - bounds[1], 30)

                alpha = set(actor.getchannel("A").get_flattened_data())
                self.assertLessEqual(alpha, {0, 255})
                visible = {
                    pixel[:3]
                    for pixel in actor.get_flattened_data()
                    if pixel[3] != 0
                }
                self.assertLessEqual(visible, allowed)

                pixels = actor.load()
                for y in range(0, actor.height, PIXEL_CLUSTER):
                    for x in range(0, actor.width, PIXEL_CLUSTER):
                        block = {
                            pixels[x + dx, y + dy]
                            for dx in range(PIXEL_CLUSTER)
                            for dy in range(PIXEL_CLUSTER)
                        }
                        self.assertEqual(1, len(block), f"non-clustered block {(x, y)}")

    def test_every_actor_keeps_a_readable_hostile_signal_after_downsampling(self) -> None:
        entries = dict(load_gpl_entries())
        hostile = {entries["sig-warning"], entries["sig-warning-deep"]}
        for spec in SPECS:
            with self.subTest(spec=spec.output_name):
                actor = build_actor(Image.open(spec.source).convert("RGBA"), spec)
                count = sum(
                    pixel[:3] in hostile and pixel[3] != 0
                    for pixel in actor.get_flattened_data()
                )
                self.assertGreaterEqual(count, spec.signal_length * PIXEL_CLUSTER ** 2)

    def test_roster_uses_cool_cyberpunk_materials_with_local_tech_accents(self) -> None:
        warm = {
            "dark-warm",
            *(f"fabric-{index}" for index in range(1, 6)),
            *(f"rust-{index}" for index in range(1, 5)),
            *(f"sludge-{index}" for index in range(1, 5)),
        }
        cold = {
            "dark-void",
            "dark-cool",
            *(f"grey-{index}" for index in range(1, 7)),
            *(f"anomaly-{index}" for index in range(1, 5)),
        }
        tech = {
            *(f"anomaly-{index}" for index in range(1, 5)),
            "sig-neon-cyan",
            "sig-neon-magenta",
            "sig-ice",
            "sig-teal-item",
        }
        bright_neon = {"sig-neon-cyan", "sig-neon-magenta"}

        for spec in SPECS:
            with self.subTest(spec=spec.output_name):
                actor = build_actor(Image.open(spec.source).convert("RGBA"), spec)
                counts = self._visible_palette_counts(actor)
                total = sum(counts.values())
                ratio = lambda names: sum(counts[name] for name in names) / total

                self.assertLessEqual(ratio(warm), 0.15)
                self.assertGreaterEqual(ratio(cold), 0.80)
                self.assertGreaterEqual(ratio(tech), 0.012)
                self.assertGreater(ratio(bright_neon), 0.0)
                self.assertLessEqual(ratio(bright_neon), 0.03)

    def test_legacy_slime_slot_is_a_pursuit_drone_without_sludge_colors(self) -> None:
        pursuit = next(spec for spec in SPECS if spec.output_name == "actor-slime")
        self.assertEqual(
            "project-c-corporate-pursuit-drone-source-v2.png",
            pursuit.source.name,
        )
        actor = build_actor(Image.open(pursuit.source).convert("RGBA"), pursuit)
        counts = self._visible_palette_counts(actor)
        self.assertEqual(0, sum(counts[f"sludge-{index}"] for index in range(1, 5)))

    def test_empty_source_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "contains no visible subject"):
            build_actor(Image.new("RGBA", (32, 32)), SPECS[0])

    def test_generated_magenta_variation_is_removed_without_losing_signals(self) -> None:
        source = Image.new("RGBA", (24, 24), (242, 12, 238, 255))
        pixels = source.load()
        for y in range(7, 19):
            for x in range(8, 16):
                pixels[x, y] = (72, 78, 82, 255)
        pixels[10, 10] = (240, 73, 42, 255)
        pixels[13, 10] = (56, 153, 166, 255)
        pixels[0, 12] = (249, 5, 211, 255)

        subject = extract_subject(source)

        self.assertEqual((8, 12), subject.size)
        self.assertEqual((0, 0, 8, 12), subject.getchannel("A").getbbox())
        visible = {
            pixel[:3]
            for pixel in subject.get_flattened_data()
            if pixel[3] != 0
        }
        self.assertIn((240, 73, 42), visible)
        self.assertIn((56, 153, 166), visible)

    def test_existing_transparent_source_keeps_dark_materials(self) -> None:
        source = Image.new("RGBA", (12, 12), (0, 0, 0, 0))
        for y in range(3, 10):
            for x in range(4, 8):
                source.putpixel((x, y), (18, 20, 24, 255))

        subject = extract_subject(source)

        self.assertEqual((4, 7), subject.size)
        self.assertEqual({255}, set(subject.getchannel("A").get_flattened_data()))


if __name__ == "__main__":
    unittest.main()
