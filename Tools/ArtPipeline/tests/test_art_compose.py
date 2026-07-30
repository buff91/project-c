from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from art_review import ReviewError, SlotCatalog  # noqa: E402
from art_compose import (  # noqa: E402
    MethodRegistry,
    StyleRegistry,
    Subject,
    SubjectRegistry,
    SubjectSetRegistry,
    WorldRegistry,
    resolve,
    resolve_by_id,
    targets_for_method,
)


class ComposeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.styles = StyleRegistry()
        self.worlds = WorldRegistry()
        self.methods = MethodRegistry()
        self.subjects = SubjectRegistry()
        self.sets = SubjectSetRegistry()

    def test_every_method_and_target_resolves_and_validates(self) -> None:
        combinations = 0
        for method in self.methods.load_all().values():
            for target_id, _name in targets_for_method(method):
                with self.subTest(method=method.id, target=target_id):
                    recipe = resolve_by_id(method.id, target_id)
                    if method.requires_source_candidate:
                        recipe = recipe.with_source_image(
                            Path(
                                "docs/art-direction/comfyui/guides/"
                                "actor-slinger-style-source.png"
                            )
                        )
                    recipe.validate_files()
                    combinations += 1
        self.assertGreaterEqual(combinations, 10)

    def test_subject_name_falls_back_to_the_game_roster(self) -> None:
        """대상이 이름을 다시 적지 않아도 게임의 이름으로 불린다."""
        slinger = self.subjects.get("actor-slinger")
        self.assertNotIn("name", slinger.document)
        self.assertEqual("투석 약탈자", slinger.name)
        self.assertEqual(
            "투석 약탈자", SlotCatalog().describe("actor-slinger")[0]
        )
        warden = self.subjects.get("actor-grave-warden")
        self.assertEqual("감시자", warden.name)

    def test_approved_source_becomes_the_identity_anchor(self) -> None:
        """합성 방법은 고정 원화 대신 승인 후보를 style_source로 받는다."""
        source = (
            Path(__file__).resolve().parents[3]
            / "docs/art-direction/comfyui/guides/"
            "actor-slinger-runtime-source-512-v1.png"
        )
        idle = resolve_by_id(
            "character-idle-v1", "actor-slinger"
        ).with_source_image(source)
        action = resolve_by_id(
            "character-action-keyframes-v5", "actor-slinger"
        ).with_source_image(source)
        self.assertIn(
            "actor-slinger-runtime-source-512-v1.png",
            idle.pipeline["uploads"]["5.image"],
        )
        self.assertEqual(
            idle.pipeline["uploads"]["5.image"],
            action.pipeline["uploads"]["5.image"],
        )

    def test_unknown_guide_variant_is_rejected(self) -> None:
        slinger = self.subjects.get("actor-slinger")
        with self.assertRaisesRegex(ReviewError, "no variant"):
            slinger.guides({"style_source": "nope"})

    def test_pose_guides_are_attached_per_shot(self) -> None:
        recipe = resolve_by_id(
            "character-action-keyframes-v5", "actor-slinger"
        )
        self.assertEqual(10, len(recipe.shots))
        for shot in recipe.shots:
            with self.subTest(shot=shot.id):
                self.assertIn("6.image", shot.uploads or {})
        # 포즈 id 와 파일명이 늘 같지는 않다 — 대상이 실제 경로를 소유한다.
        release = next(s for s in recipe.shots if s.id == "attack-release")
        self.assertIn("attack-impact", release.uploads["6.image"])

    def test_a_set_keeps_each_member_distinct(self) -> None:
        """묶음 본문에 첫 멤버를 구우면 나머지가 전부 그걸 닮는다."""
        recipe = resolve_by_id("effect-keyframes-v2", "fx-impact-suite")
        members = self.sets.get("fx-impact-suite").member_ids
        self.assertEqual(len(members), len(recipe.shots))
        first = self.subjects.get(members[0])
        self.assertNotIn(first.identity, recipe.prompt["positive"])
        suffixes = {shot.prompt_suffix for shot in recipe.shots}
        self.assertEqual(len(members), len(suffixes))
        slots = {shot.slot for shot in recipe.shots}
        self.assertEqual(len(members), len(slots))

    def test_set_members_keep_their_own_canvas(self) -> None:
        recipe = resolve_by_id("effect-keyframes-v2", "fx-impact-suite")
        canvases = {shot.id: shot.output_canvas for shot in recipe.shots}
        self.assertEqual((24, 24), canvases["fx-impact-physical"])
        self.assertEqual((32, 32), canvases["fx-impact-heavy"])

    def test_a_method_also_serves_one_member_alone(self) -> None:
        """예전에는 6장 세트를 통째로 돌려야 했다."""
        recipe = resolve_by_id("effect-keyframes-v2", "fx-status-burn")
        self.assertEqual("fx-status-burn", recipe.slot)
        self.assertEqual(1, len(recipe.shots))

    def test_method_rejects_a_mismatched_asset_type(self) -> None:
        method = self.methods.get("character-idle-v1")
        with self.assertRaisesRegex(ReviewError, "applies to"):
            resolve(method, [self.subjects.get("fx-impact-fire")])

    def test_targets_are_scoped_to_what_the_method_accepts(self) -> None:
        character = dict(targets_for_method(
            self.methods.get("character-idle-v1")
        ))
        self.assertIn("actor-slinger", character)
        self.assertNotIn("fx-impact-fire", character)
        self.assertNotIn("fx-impact-suite", character)

        effect = dict(targets_for_method(
            self.methods.get("effect-keyframes-v2")
        ))
        self.assertIn("fx-impact-suite", effect)
        self.assertIn("fx-impact-fire", effect)
        self.assertNotIn("actor-slinger", effect)

    def test_concept_is_a_method_applied_to_a_real_character(self) -> None:
        """컨셉은 가짜 제작 대상이 아니라 캐릭터에 적용하는 제작 단계다."""
        concept_targets = dict(targets_for_method(
            self.methods.get("concept-sdxl-v1")
        ))
        self.assertIn("actor-slinger", concept_targets)
        self.assertIn("actor-grave-warden", concept_targets)
        self.assertIn("actor-knight", concept_targets)
        self.assertNotIn("actor-concept", concept_targets)

        recipe = resolve_by_id(
            "concept-sdxl-v1",
            "actor-grave-warden",
            style_id="chunky-isometric-pixel-v1",
            world_id="arcade-tower-v1",
        )
        self.assertEqual("actor-grave-warden", recipe.slot)
        self.assertEqual("concept-only", recipe.output["promotion"])
        self.assertIn("sensor mast", recipe.prompt["positive"])

    def test_character_production_requires_pose_and_approved_source(self) -> None:
        idle_targets = dict(targets_for_method(
            self.methods.get("character-idle-v1")
        ))
        self.assertIn("actor-slinger", idle_targets)
        self.assertIn("actor-grave-warden", idle_targets)
        self.assertIn("actor-knight", idle_targets)
        self.assertTrue(
            self.methods.get("character-idle-v1").requires_source_candidate
        )

        recipe = resolve_by_id("character-idle-v1", "actor-knight")
        self.assertNotIn("5.image", recipe.pipeline.get("uploads", {}))
        self.assertIn("6.image", recipe.pipeline["uploads"])

    def test_survivor_runtime_methods_keep_subject_gates_separate(self) -> None:
        base = resolve_by_id("character-runtime-base-v2", "actor-knight")
        self.assertFalse(
            self.methods.get(
                "character-runtime-base-v2"
            ).requires_source_candidate
        )
        self.assertIn("5.image", base.pipeline["uploads"])
        self.assertIn("6.image", base.pipeline["uploads"])
        self.assertIn(
            "compact-medical-pack",
            base.document["quality_gates"]["silhouette_tags"],
        )
        self.assertNotIn(
            "visible-sling",
            base.document["quality_gates"]["silhouette_tags"],
        )
        self.assertIn(
            "permanent-signature-weapon",
            base.document["quality_gates"]["reject_if"],
        )

        action = resolve_by_id(
            "character-action-keyframes-v6", "actor-knight"
        )
        self.assertEqual(11, len(action.shots))
        self.assertEqual(0.56, action.generation["denoise"])
        self.assertNotIn("5.image", action.pipeline.get("uploads", {}))
        self.assertIn("6.image", action.pipeline["shots"][0]["uploads"])
        self.assertNotIn("male", action.prompt["positive"])
        self.assertNotIn("sling", action.prompt["positive"])

    def test_composition_records_where_it_came_from(self) -> None:
        recipe = resolve_by_id("character-idle-v1", "actor-slinger")
        self.assertEqual(
            {
                "method": "character-idle-v1",
                "subjects": ["actor-slinger"],
            },
            recipe.document["composed_from"],
        )

    def test_style_world_subject_and_method_are_separate(self) -> None:
        recipe = resolve_by_id(
            "character-idle-v1",
            "actor-slinger",
            style_id="chunky-isometric-pixel-v1",
            world_id="arcade-tower-v1",
        )
        self.assertEqual(
            "chunky-isometric-pixel-v1",
            recipe.document["composed_from"]["style"],
        )
        self.assertEqual(
            "arcade-tower-v1",
            recipe.document["composed_from"]["world"],
        )
        self.assertEqual(
            "청키 아이소메트릭 픽셀",
            recipe.document["art_style"]["name"],
        )
        self.assertEqual(
            "폐 아케이드 복합타워 네온 미궁 (v0.3.3)",
            recipe.document["world"]["name"],
        )
        self.assertIn("deliberate hard-edged", recipe.prompt["positive"])
        self.assertIn("cracked concrete", recipe.prompt["positive"])
        self.assertIn("photorealism", recipe.prompt["negative"])
        self.assertIn("medieval fantasy", recipe.prompt["negative"])

    def test_environment_subject_owns_its_slot_canvas_and_pivot(self) -> None:
        ladder = resolve_by_id(
            "environment-concept-sdxl-v1",
            "env-ladder",
            style_id="chunky-isometric-pixel-v1",
            world_id="arcade-tower-v1",
        )
        self.assertEqual("env-ladder", ladder.slot)
        self.assertEqual((64, 112), ladder.canvas)
        self.assertEqual([0.5, 0.08], ladder.output["pivot"])
        self.assertEqual("concept-only", ladder.output["promotion"])

        floor = resolve_by_id(
            "environment-static-refine-v1",
            "env-floor-mid",
        )
        self.assertEqual((128, 64), floor.canvas)
        self.assertEqual("aseprite", floor.output["promotion"])
        floor.validate_slot_registration()

    def test_environment_loop_builds_four_idle_keyframes(self) -> None:
        recipe = resolve_by_id(
            "environment-idle-keyframes-v1",
            "prop-campfire",
            style_id="chunky-isometric-pixel-v1",
            world_id="arcade-tower-v1",
        )
        self.assertEqual("environment", recipe.purpose["category"])
        self.assertEqual((128, 128), recipe.canvas)
        self.assertEqual(
            ["pulse-low", "pulse-rise", "pulse-high", "pulse-fall"],
            [shot.id for shot in recipe.shots],
        )
        clip = recipe.animation["draft"]["clips"][0]
        self.assertEqual("idle", clip["tag"])
        self.assertTrue(clip["loop"])

    def test_composed_recipes_respect_the_slot_registry(self) -> None:
        """합성본도 미등록 슬롯에 승격할 수 없다."""
        broken = copy.deepcopy(
            self.subjects.get("actor-slinger").document
        )
        broken["slot"] = "actor-does-not-exist"
        subject = Subject(path=Path("memory.yaml"), document=broken)
        method = self.methods.get("character-idle-v1")
        with self.assertRaisesRegex(ReviewError, "unregistered slot"):
            resolve(method, [subject]).validate_slot_registration()


if __name__ == "__main__":
    unittest.main()
