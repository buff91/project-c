from __future__ import annotations

from collections import Counter
import math
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image, ImageChops


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import build_arcade_enemy_directional_v1 as builder
from build_arcade_enemy_directional_v1 import (
    DIRECTIONS,
    FRAME_COUNTS,
    SPECS,
    STATES,
    allowed_colors,
    assert_frame_contract,
    build_frames,
    build_manifest_payload,
    palette_entries,
    write_motion_preview,
)


def _opaque_count(image: Image.Image) -> int:
    return sum(value != 0 for value in image.getchannel("A").get_flattened_data())


def _working_alpha(image: Image.Image) -> Image.Image:
    return image.resize(builder.WORKING_SIZE, Image.Resampling.NEAREST).getchannel("A")


def _alpha_change_count(first: Image.Image, second: Image.Image) -> int:
    return sum(
        left != right
        for left, right in zip(
            _working_alpha(first).get_flattened_data(),
            _working_alpha(second).get_flattened_data(),
            strict=True,
        )
    )


class ArcadeEnemyDirectionalBuilderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.frames = {spec.asset_name: build_frames(spec) for spec in SPECS}

    def test_every_enemy_builds_the_complete_24_tag_contract(self) -> None:
        expected_tags = {
            f"{state}-{direction}"
            for state in STATES
            for direction in DIRECTIONS
        }
        for spec in SPECS:
            with self.subTest(actor=spec.asset_name):
                frames = self.frames[spec.asset_name]
                self.assertEqual(80, len(frames))
                self.assertEqual(expected_tags, {frame.tag for frame in frames})
                counts = Counter(frame.tag for frame in frames)
                for tag in expected_tags:
                    state = tag.split("-", 1)[0]
                    self.assertEqual(FRAME_COUNTS[state], counts[tag], tag)

    def test_all_frames_are_grounded_clustered_and_palette_locked(self) -> None:
        colors = allowed_colors()
        for spec in SPECS:
            for frame in self.frames[spec.asset_name]:
                with self.subTest(actor=spec.asset_name, tag=frame.tag, index=frame.index):
                    assert_frame_contract(frame.image, colors)

    def test_directional_roster_keeps_the_cool_cyberpunk_color_contract(self) -> None:
        entries = palette_entries()
        names = {color: name for name, color in entries.items()}
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

        for spec in SPECS:
            counts: Counter[str] = Counter()
            for frame in self.frames[spec.asset_name]:
                counts.update(
                    names[pixel[:3]]
                    for pixel in frame.image.get_flattened_data()
                    if pixel[3] != 0
                )
            total = sum(counts.values())
            ratio = lambda selected: sum(counts[name] for name in selected) / total
            with self.subTest(actor=spec.asset_name):
                self.assertLessEqual(ratio(warm), 0.15)
                self.assertGreaterEqual(ratio(cold), 0.80)
                self.assertGreaterEqual(ratio(tech), 0.012)

    def test_legacy_slime_slot_uses_the_quadruped_hound_motion_profile(self) -> None:
        pursuit = next(spec for spec in SPECS if spec.asset_name == "actor-slime")
        self.assertEqual("slime", pursuit.actor_key)
        self.assertEqual("hound", pursuit.motion)

    def test_approved_static_frame_is_preserved_as_south_idle_and_catalog_lead(self) -> None:
        for spec in SPECS:
            with self.subTest(actor=spec.asset_name):
                base = Image.open(spec.runtime_path).convert("RGBA")
                south_idle = next(
                    frame.image
                    for frame in self.frames[spec.asset_name]
                    if frame.tag == "idle-south" and frame.index == 0
                )
                self.assertEqual(base.tobytes(), south_idle.tobytes())
                payload = build_manifest_payload(
                    spec,
                    self.frames[spec.asset_name],
                    save_frames=False,
                )
                self.assertEqual(
                    [{"source": str(spec.runtime_path), "duration_ms": 180}],
                    payload["leading_frames"],
                )

    def test_manifest_loops_only_idle_and_walk(self) -> None:
        for spec in SPECS:
            payload = build_manifest_payload(
                spec,
                self.frames[spec.asset_name],
                save_frames=False,
            )
            self.assertEqual(24, len(payload["clips"]))
            self.assertEqual([96, 128], payload["canvas"])
            for clip in payload["clips"]:
                state = clip["tag"].split("-", 1)[0]
                self.assertEqual(state in {"idle", "walk"}, clip["loop"])
                self.assertEqual(FRAME_COUNTS[state], len(clip["frames"]))

    def test_west_is_a_byte_exact_mirror_of_east_for_v1_screen_space_handedness(self) -> None:
        for spec in SPECS:
            lookup = {
                (frame.tag, frame.index): frame.image
                for frame in self.frames[spec.asset_name]
            }
            for state in STATES:
                for index in range(FRAME_COUNTS[state]):
                    east = lookup[(f"{state}-east", index)]
                    west = lookup[(f"{state}-west", index)]
                    expected = east.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    with self.subTest(actor=spec.asset_name, state=state, index=index):
                        self.assertEqual(expected.tobytes(), west.tobytes())

    def test_north_and_profile_views_are_not_relabelled_south_frames(self) -> None:
        for spec in SPECS:
            lookup = {
                frame.tag: frame.image
                for frame in self.frames[spec.asset_name]
                if frame.index == 0 and frame.tag.startswith("idle-")
            }
            south = lookup["idle-south"]
            for direction in ("north", "east"):
                diff = ImageChops.difference(south, lookup[f"idle-{direction}"])
                changed = sum(
                    pixel != (0, 0, 0, 0)
                    for pixel in diff.get_flattened_data()
                )
                with self.subTest(actor=spec.asset_name, direction=direction):
                    self.assertGreater(changed, 120)

    def test_north_alpha_is_not_a_mirrored_south_silhouette(self) -> None:
        for spec in SPECS:
            lookup = {
                frame.tag: frame.image
                for frame in self.frames[spec.asset_name]
                if frame.index == 0 and frame.tag.startswith("idle-")
            }
            mirrored_south = lookup["idle-south"].transpose(
                Image.Transpose.FLIP_LEFT_RIGHT
            )
            north_change = _alpha_change_count(
                mirrored_south,
                lookup["idle-north"],
            )
            profile_change = _alpha_change_count(
                lookup["idle-south"],
                lookup["idle-east"],
            )
            with self.subTest(actor=spec.asset_name):
                self.assertGreaterEqual(north_change, 24)
                self.assertGreaterEqual(profile_change, 72)

    def test_walk_attack_and_hit_keep_a_static_core_while_local_parts_move(self) -> None:
        key_frames = {"walk": 0, "attack": 1, "hit": 0}
        for spec in SPECS:
            lookup = {
                (frame.tag, frame.index): frame.image
                for frame in self.frames[spec.asset_name]
            }
            for direction in ("north", "east", "south"):
                base = lookup[(f"idle-{direction}", 0)]
                base_alpha = _working_alpha(base)
                bounds = base_alpha.getbbox()
                assert bounds is not None
                width = bounds[2] - bounds[0]
                height = bounds[3] - bounds[1]
                core = (
                    bounds[0] + math.floor(width * 0.38),
                    bounds[1] + math.floor(height * 0.30),
                    bounds[0] + math.ceil(width * 0.62),
                    bounds[1] + math.ceil(height * 0.62),
                )
                core_opaque = sum(
                    value != 0
                    for value in base_alpha.crop(core).get_flattened_data()
                )
                self.assertGreater(core_opaque, 24)
                for state, index in key_frames.items():
                    pose = lookup[(f"{state}-{direction}", index)]
                    pose_alpha = _working_alpha(pose)
                    # A whole-body affine transform cannot leave this central
                    # multi-pixel body block fixed while changing only shell
                    # pixels; this is the authored local-articulation contract.
                    with self.subTest(
                        actor=spec.asset_name,
                        direction=direction,
                        state=state,
                    ):
                        self.assertEqual(
                            base_alpha.crop(core).tobytes(),
                            pose_alpha.crop(core).tobytes(),
                        )
                        self.assertGreaterEqual(
                            _alpha_change_count(base, pose),
                            12,
                            "관절 브리지를 남겨도 외곽 키포즈는 48 runtime px 이상 보여야 한다",
                        )

    def test_local_key_pose_offsets_follow_each_facing_axis(self) -> None:
        phases = {"walk": -1, "attack": 1, "hit": 1}
        for spec in SPECS:
            base = next(
                frame.image
                for frame in self.frames[spec.asset_name]
                if frame.tag == "idle-south" and frame.index == 0
            )
            for state, phase in phases.items():
                poses = {
                    direction: builder._articulated_key_pose(
                        base,
                        spec,
                        state,
                        phase,
                        direction,
                    ).getchannel("A").tobytes()
                    for direction in ("north", "east", "south")
                }
                with self.subTest(actor=spec.asset_name, state=state):
                    self.assertEqual(3, len(set(poses.values())))

    def test_alive_states_keep_hostile_iff_and_state_motion_is_visible(self) -> None:
        entries = palette_entries()
        hostile = {entries["sig-warning"], entries["sig-warning-deep"]}
        for spec in SPECS:
            frames = self.frames[spec.asset_name]
            for frame in frames:
                if frame.tag.startswith("death-") and frame.index == 4:
                    continue
                count = sum(
                    pixel[:3] in hostile and pixel[3] != 0
                    for pixel in frame.image.get_flattened_data()
                )
                with self.subTest(actor=spec.asset_name, tag=frame.tag, index=frame.index):
                    self.assertEqual(count, spec.signal_length * 4)

            for direction in DIRECTIONS:
                final_death = next(
                    frame.image
                    for frame in frames
                    if frame.tag == f"death-{direction}" and frame.index == 4
                )
                signal_count = sum(
                    pixel[:3] in hostile and pixel[3] != 0
                    for pixel in final_death.get_flattened_data()
                )
                with self.subTest(actor=spec.asset_name, direction=direction):
                    self.assertEqual(0, signal_count, "사망 마지막 프레임은 IFF 소등")

            for state in ("walk", "attack", "hit", "fall", "death"):
                south = [
                    frame.image.tobytes()
                    for frame in frames
                    if frame.tag == f"{state}-south"
                ]
                with self.subTest(actor=spec.asset_name, state=state):
                    self.assertGreaterEqual(len(set(south)), 2)

    def test_collapse_keeps_role_volume_and_machine_proportions(self) -> None:
        for spec in SPECS:
            frames = self.frames[spec.asset_name]
            idle = next(
                frame.image
                for frame in frames
                if frame.tag == "idle-south" and frame.index == 0
            )
            corpse = next(
                frame.image
                for frame in frames
                if frame.tag == "death-south" and frame.index == 4
            )
            with self.subTest(actor=spec.asset_name):
                self.assertGreaterEqual(_opaque_count(corpse), _opaque_count(idle) * 0.52)

            if spec.motion in {"hound", "drone"}:
                for frame in frames:
                    if frame.tag.startswith(("idle-", "walk-", "attack-", "hit-")):
                        bounds = frame.image.getchannel("A").getbbox()
                        assert bounds is not None
                        with self.subTest(actor=spec.asset_name, tag=frame.tag):
                            self.assertLessEqual(bounds[3] - bounds[1], 60)

    def test_motion_preview_contains_every_authored_state_frame(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output = Path(temp_dir) / "enemy-motion.gif"
            with patch.object(builder, "MOTION_PREVIEW", output):
                write_motion_preview(self.frames)

            with Image.open(output) as preview:
                self.assertEqual(sum(FRAME_COUNTS.values()), preview.n_frames)
                self.assertEqual(
                    (112 + 96 * len(DIRECTIONS), 24 + 128 * len(SPECS)),
                    preview.size,
                )
                self.assertEqual(140, preview.info["duration"])


if __name__ == "__main__":
    unittest.main()
