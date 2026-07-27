from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from art_review import ReviewError, SlotCatalog  # noqa: E402
from art_compose import (  # noqa: E402
    MethodRegistry,
    Subject,
    SubjectRegistry,
    SubjectSetRegistry,
    resolve,
    resolve_by_id,
    targets_for_method,
)


class ComposeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.methods = MethodRegistry()
        self.subjects = SubjectRegistry()
        self.sets = SubjectSetRegistry()

    def test_every_method_and_target_resolves_and_validates(self) -> None:
        combinations = 0
        for method in self.methods.load_all().values():
            for target_id, _name in targets_for_method(method):
                with self.subTest(method=method.id, target=target_id):
                    recipe = resolve_by_id(method.id, target_id)
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

    def test_guide_variants_let_methods_pick_the_identity_anchor(self) -> None:
        """경로는 대상이 알고, 어느 변형을 쓸지는 방법이 고른다."""
        idle = resolve_by_id("character-idle-v1", "actor-slinger")
        action = resolve_by_id(
            "character-action-keyframes-v5", "actor-slinger"
        )
        self.assertIn(
            "actor-slinger-style-source.png",
            idle.pipeline["uploads"]["5.image"],
        )
        self.assertIn(
            "actor-slinger-runtime-source-512-v1.png",
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

    def test_composition_records_where_it_came_from(self) -> None:
        recipe = resolve_by_id("character-idle-v1", "actor-slinger")
        self.assertEqual(
            {
                "method": "character-idle-v1",
                "subjects": ["actor-slinger"],
            },
            recipe.document["composed_from"],
        )

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
