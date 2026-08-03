from __future__ import annotations

from collections import Counter
import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from build_actor_knight_directional_v1 import (
    ACTOR_PALETTE_NAMES,
    BASE_SOUTH,
    DIRECTIONS,
    actor_palette,
    assert_frame_contract,
    build_frames,
    build_manifest_payload,
)


def _bounds(image):
    return image.convert("RGBA").getchannel("A").getbbox()


class DirectionalKnightProcessorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.frames = build_frames()

    def test_builds_complete_directional_state_contract(self) -> None:
        expected = {
            f"{state}-{direction}"
            for state in ("idle", "walk", "attack", "hit", "fall", "death")
            for direction in DIRECTIONS
        }
        tags = {frame.tag for frame in self.frames}
        self.assertEqual(expected, tags)
        self.assertEqual(80, len(self.frames))

        counts = Counter(frame.tag.split("-", 1)[0] for frame in self.frames)
        self.assertEqual(
            {
                "idle": 16,
                "walk": 12,
                "attack": 12,
                "hit": 12,
                "fall": 8,
                "death": 20,
            },
            counts,
        )

    def test_frames_are_grounded_clustered_and_palette_locked(self) -> None:
        colors, _ = actor_palette()
        self.assertEqual(len(ACTOR_PALETTE_NAMES), len(colors))
        for frame in self.frames:
            with self.subTest(tag=frame.tag, index=frame.index):
                assert_frame_contract(frame.image, set(colors))

    def test_manifest_keeps_approved_catalog_frame_outside_directional_tags(self) -> None:
        payload = build_manifest_payload(self.frames, save_frames=False)
        self.assertEqual(
            [{"source": str(BASE_SOUTH), "duration_ms": 180}],
            payload["leading_frames"],
        )
        self.assertEqual(24, len(payload["clips"]))
        self.assertTrue(all(
            clip["tag"] != "catalog" for clip in payload["clips"]
        ))
        self.assertTrue(all(
            clip["loop"] == clip["tag"].startswith(("idle-", "walk-"))
            for clip in payload["clips"]
        ))

    def test_each_multiframe_clip_contains_visible_motion(self) -> None:
        grouped: dict[str, list] = {}
        for frame in self.frames:
            grouped.setdefault(frame.tag, []).append(frame.image.tobytes())
        for tag, images in grouped.items():
            if len(images) < 2:
                continue
            with self.subTest(tag=tag):
                self.assertGreater(len(set(images)), 1)

    def test_canonical_pose_scale_and_grounding_stay_consistent(self) -> None:
        grouped: dict[str, list] = {}
        for frame in self.frames:
            grouped.setdefault(frame.tag, []).append(frame.image)

        idle_bounds = [_bounds(grouped[f"idle-{direction}"][0]) for direction in DIRECTIONS]
        self.assertTrue(all(bounds is not None for bounds in idle_bounds))
        widths = [bounds[2] - bounds[0] for bounds in idle_bounds]
        heights = [bounds[3] - bounds[1] for bounds in idle_bounds]
        self.assertLessEqual(max(widths) - min(widths), 8)
        self.assertGreaterEqual(min(heights), 112)

        for state in ("idle", "attack", "hit"):
            for direction in DIRECTIONS:
                for image in grouped[f"{state}-{direction}"]:
                    bounds = _bounds(image)
                    with self.subTest(state=state, direction=direction):
                        self.assertIsNotNone(bounds)
                        self.assertEqual(124, bounds[3])
                        self.assertGreaterEqual(bounds[3] - bounds[1], 110)

    def test_east_west_canonical_motion_is_pixel_mirrored(self) -> None:
        grouped: dict[str, list] = {}
        for frame in self.frames:
            grouped.setdefault(frame.tag, []).append(frame.image)

        for state in ("idle", "attack", "hit", "fall", "death"):
            east = grouped[f"{state}-east"]
            west = grouped[f"{state}-west"]
            self.assertEqual(len(east), len(west))
            for index, (east_image, west_image) in enumerate(zip(east, west, strict=True)):
                with self.subTest(state=state, index=index):
                    self.assertEqual(
                        east_image.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
                        west_image.tobytes(),
                    )

    def test_final_death_pose_preserves_corpse_volume(self) -> None:
        grouped: dict[str, list] = {}
        for frame in self.frames:
            grouped.setdefault(frame.tag, []).append(frame.image)

        for direction in DIRECTIONS:
            idle = grouped[f"idle-{direction}"][0]
            corpse = grouped[f"death-{direction}"][-1]
            idle_pixels = sum(idle.getchannel("A").get_flattened_data()) // 255
            corpse_pixels = sum(corpse.getchannel("A").get_flattened_data()) // 255
            bounds = _bounds(corpse)
            with self.subTest(direction=direction):
                self.assertIsNotNone(bounds)
                self.assertGreaterEqual(bounds[2] - bounds[0], 80)
                self.assertGreaterEqual(bounds[3] - bounds[1], 56)
                self.assertGreaterEqual(corpse_pixels / idle_pixels, 0.72)


if __name__ == "__main__":
    unittest.main()
