from __future__ import annotations

import contextlib
import copy
import io
import json
import sqlite3
import sys
import tempfile
import unittest
import unittest.mock
from pathlib import Path

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from art_review import (
    ASSET_TYPES,
    BatchRegistry,
    RecipeRegistry,
    ReviewError,
    ReviewStore,
    WorkflowTypeRegistry,
    derive_asset_type,
    image_metrics,
)
from art_asset import (
    detect_border_color,
    keep_largest_alpha_component,
    remove_chroma_key,
)
from art_recipe_tool import parse_assignment, set_nested
from art_runner import (
    approve_candidate,
    build_parser,
    decide_candidate_shot,
    process_action,
    publish_candidate,
    reject_candidate,
    resolve_batch_jobs,
    render_draft_frame,
    review_sheet,
)
from comfy_batch import ComfyError, final_output_node_ids, multipart_body
from art_slack_bot import (
    allowed_user,
    animation_action_value,
    animation_timing_blocks,
    candidate_blocks,
    find_candidate_from_text,
    find_feedback_target,
    modal_view,
    parse_animation_action,
    parse_shot_action,
    recipe_list_text,
    recipes_by_asset_type,
    slack_help_text,
    shot_blocks,
)


class RecipeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.registry = RecipeRegistry()

    def test_registry_loads_project_recipes(self) -> None:
        recipes = self.registry.load_all()
        self.assertIn("actor-slinger-idle-v1", recipes)
        self.assertIn("actor-concept-sdxl-v1", recipes)
        self.assertIn("environment-hospital-style-v1", recipes)
        self.assertIn("actor-slinger-animation-walk-v1", recipes)
        self.assertIn("actor-slinger-animation-v2", recipes)
        self.assertIn("actor-slinger-animation-v5", recipes)
        self.assertIn("fx-impact-suite-v1", recipes)
        self.assertIn("fx-impact-suite-v2", recipes)

    def test_every_recipe_declares_a_known_asset_type(self) -> None:
        known = {type_id for type_id, _ in ASSET_TYPES}
        for recipe_id, recipe in self.registry.load_all().items():
            with self.subTest(recipe=recipe_id):
                self.assertIn(
                    "asset_type",
                    recipe.purpose,
                    "레시피는 에셋 타입을 파생에 맡기지 말고 명시한다",
                )
                self.assertIn(recipe.asset_type, known)

    def test_asset_type_splits_actor_recipes_by_intent(self) -> None:
        """같은 actor 카테고리라도 고르는 목적이 다르면 다른 타입이다."""
        recipes = self.registry.load_all()
        self.assertEqual("concept", recipes["actor-concept-sdxl-v1"].asset_type)
        self.assertEqual(
            "character", recipes["actor-slinger-idle-v1"].asset_type
        )
        self.assertEqual(
            "animation", recipes["actor-slinger-animation-v5"].asset_type
        )
        self.assertEqual(
            "environment",
            recipes["environment-hospital-style-v1"].asset_type,
        )
        self.assertEqual("effect", recipes["fx-impact-suite-v1"].asset_type)

    def test_derive_asset_type_covers_recipes_without_declaration(self) -> None:
        self.assertEqual("concept", derive_asset_type("actor", "concept"))
        self.assertEqual("concept", derive_asset_type("effect", "concept"))
        self.assertEqual("character", derive_asset_type("actor", "gameplay"))
        self.assertEqual(
            "animation", derive_asset_type("actor", "animation-source")
        )
        self.assertEqual(
            "effect", derive_asset_type("effect", "animation-source")
        )
        self.assertEqual("prop", derive_asset_type("item", "gameplay"))
        self.assertEqual("ui", derive_asset_type("ui", "gameplay"))

    def test_unknown_asset_type_is_rejected(self) -> None:
        from art_review import Recipe

        document = dict(self.registry.get("actor-slinger-idle-v1").document)
        document["purpose"] = dict(document["purpose"])
        document["purpose"]["asset_type"] = "괴상한타입"
        with self.assertRaisesRegex(ReviewError, "asset_type"):
            Recipe.from_document(document, path=Path("memory.yaml"))

    def test_slack_groups_recipes_by_asset_type(self) -> None:
        groups = recipes_by_asset_type(self.registry)
        labels = [label for _type_id, label, _recipes in groups]
        self.assertEqual(["컨셉", "배경", "캐릭터", "애니메이션", "이펙트"], labels)
        listed = recipe_list_text(self.registry)
        self.assertIn("*애니메이션*", listed)
        self.assertIn("actor-slinger-animation-v5", listed)

        element = modal_view(self.registry)["blocks"][0]["element"]
        self.assertNotIn(
            "options",
            element,
            "드롭다운은 평평한 목록이 아니라 타입별 그룹이어야 한다",
        )
        self.assertEqual(
            ["컨셉", "배경", "캐릭터", "애니메이션", "이펙트"],
            [group["label"]["text"] for group in element["option_groups"]],
        )

    def test_every_recipe_satisfies_its_workflow_type_contract(self) -> None:
        types = WorkflowTypeRegistry().load_all()
        for recipe_id, recipe in self.registry.load_all().items():
            with self.subTest(recipe=recipe_id):
                self.assertIn(recipe.workflow_type, types)
                recipe.validate_workflow_type()

    def test_unknown_workflow_type_is_rejected(self) -> None:
        recipe = self._mutated_recipe(
            lambda document: document["pipeline"].__setitem__(
                "type", "sd15-magic"
            )
        )
        with self.assertRaisesRegex(ReviewError, "Unknown workflow type"):
            recipe.validate_workflow_type()

    def test_missing_required_binding_is_rejected(self) -> None:
        """타입만 맞고 바인딩이 없으면 ComfyUI가 조용히 기본값을 쓴다."""
        recipe = self._mutated_recipe(
            lambda document: document["pipeline"]["bindings"].pop("seed")
        )
        with self.assertRaisesRegex(ReviewError, "requires bindings seed"):
            recipe.validate_workflow_type()

    def test_missing_required_upload_is_rejected(self) -> None:
        recipe = self._mutated_recipe(
            lambda document: document["pipeline"]["uploads"].pop("6.image")
        )
        with self.assertRaisesRegex(ReviewError, "requires upload 6.image"):
            recipe.validate_workflow_type()

    def test_shot_level_upload_satisfies_the_contract(self) -> None:
        """포즈 가이드처럼 샷마다 다른 입력은 샷이 채워도 계약이 성립한다."""
        recipe = self.registry.get("actor-slinger-animation-v5")
        self.assertNotIn("6.image", recipe.pipeline.get("uploads", {}))
        self.assertTrue(
            all("6.image" in (shot.uploads or {}) for shot in recipe.shots)
        )
        recipe.validate_workflow_type()

    def test_workflow_type_registry_describes_capabilities(self) -> None:
        openpose = WorkflowTypeRegistry().get("sd15-img2img-openpose")
        self.assertTrue(openpose.supports_denoise)
        self.assertTrue(openpose.supports_controlnet)
        txt2img = WorkflowTypeRegistry().get("sdxl-txt2img")
        self.assertFalse(txt2img.supports_denoise)
        self.assertFalse(txt2img.supports_controlnet)
        self.assertEqual((), txt2img.required_uploads)

    def _mutated_recipe(self, mutate):
        from art_review import Recipe

        document = copy.deepcopy(
            self.registry.get("actor-slinger-idle-v1").document
        )
        mutate(document)
        return Recipe.from_document(document, path=Path("memory.yaml"))

    def test_style_sampler_batch_covers_each_art_purpose(self) -> None:
        plan = BatchRegistry().get("style-sampler")
        plan.validate_recipes(self.registry)
        self.assertEqual(
            {
                "actor-concept",
                "actor-runtime",
                "environment",
                "effect",
                "animation",
            },
            {item.id for item in plan.items},
        )

    def test_cli_candidate_count_is_bounded(self) -> None:
        parser = build_parser()
        self.assertEqual(
            12,
            parser.parse_args(
                ["submit", "actor-slinger-idle-v1", "--count", "12"]
            ).count,
        )
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                parser.parse_args(
                    ["submit", "actor-slinger-idle-v1", "--count", "13"]
                )

    def test_slinger_assignments_are_reproducible(self) -> None:
        recipe = self.registry.get("actor-slinger-idle-v1")
        values = {
            assignment.split("=", 1)[0]: json.loads(
                assignment.split("=", 1)[1]
            )
            for assignment in recipe.assignments(12345)
        }
        self.assertEqual(12345, values["9.seed"])
        self.assertEqual(24, values["9.steps"])
        self.assertEqual("pixelartV3.safetensors", values["2.lora_name"])
        self.assertEqual(
            "control_v11p_sd15_openpose_fp16.safetensors",
            values["7.control_net_name"],
        )
        self.assertIn("visible curved sling", values["3.text"])

    def test_yaml_preserves_projection_label(self) -> None:
        recipe = self.registry.get("environment-hospital-style-v1")
        self.assertEqual(
            "2:1",
            recipe.document["quality_gates"]["preserve_projection"],
        )

    def test_animation_recipe_expands_pose_locked_shots(self) -> None:
        recipe = self.registry.get("actor-slinger-animation-walk-v1")
        self.assertEqual(9, len(recipe.shots))
        attack = next(
            shot for shot in recipe.shots if shot.id == "attack-impact"
        )
        values = {
            assignment.split("=", 1)[0]: json.loads(
                assignment.split("=", 1)[1]
            )
            for assignment in recipe.assignments(900, attack)
        }
        self.assertEqual(900, values["9.seed"])
        self.assertIn("attack release", values["3.text"])
        uploads = recipe.uploads(attack)
        self.assertTrue(
            any(
                "actor-slinger-attack-impact.png" in upload
                for upload in uploads
            )
        )

    def test_animation_v2_matches_runtime_tag_contract(self) -> None:
        recipe = self.registry.get("actor-slinger-animation-v2")
        self.assertEqual(10, len(recipe.shots))
        self.assertEqual(
            {"idle", "walk", "attack", "hit", "fall", "death"},
            {
                clip["tag"]
                for clip in recipe.animation["draft"]["clips"]
            },
        )
        walk = next(
            clip
            for clip in recipe.animation["draft"]["clips"]
            if clip["tag"] == "walk"
        )
        self.assertTrue(walk["loop"])
        self.assertEqual(
            [
                "walk-contact-a",
                "walk-pass",
                "walk-contact-b",
                "walk-pass",
            ],
            walk["frames"],
        )

    def test_animation_v5_locks_identity_inputs_across_shots(self) -> None:
        recipe = self.registry.get("actor-slinger-animation-v5")
        self.assertEqual(
            {0},
            {shot.seed_offset for shot in recipe.shots},
        )
        self.assertIn(
            "actor-slinger-runtime-source-512-v1.png",
            recipe.pipeline["uploads"]["5.image"],
        )
        self.assertEqual(0.60, recipe.generation["denoise"])

    def test_one_shot_variation_drops_parent_animation_contract(self) -> None:
        recipe = self.registry.get("actor-slinger-animation-v2")
        isolated = recipe.only_shot("walk-contact-a")
        self.assertEqual({}, isolated.animation)

    def test_effect_recipe_expands_all_runtime_slots(self) -> None:
        recipe = self.registry.get("fx-impact-suite-v1")
        self.assertEqual(
            {
                "fx-impact-physical",
                "fx-impact-fire",
                "fx-impact-frost",
                "fx-impact-heavy",
                "fx-status-burn",
                "fx-status-freeze",
            },
            {shot.id for shot in recipe.shots},
        )
        heavy = next(
            shot for shot in recipe.shots if shot.id == "fx-impact-heavy"
        )
        self.assertEqual("fx-impact-heavy", heavy.slot)
        self.assertEqual((32, 32), heavy.output_canvas)
        values = {
            assignment.split("=", 1)[0]: json.loads(
                assignment.split("=", 1)[1]
            )
            for assignment in recipe.assignments(123, heavy)
        }
        self.assertIn("heavy shockwave", values["3.text"])

    def test_effect_recipe_can_isolate_one_shot(self) -> None:
        recipe = self.registry.get("fx-impact-suite-v1")
        isolated = recipe.only_shot("fx-impact-fire")
        self.assertEqual(1, len(isolated.shots))
        self.assertEqual("fx-impact-fire", isolated.shots[0].id)
        self.assertEqual("fx-impact-fire", isolated.slot)

    def test_review_sheet_keeps_all_shots_visible(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first = root / "first.png"
            second = root / "second.png"
            Image.new("RGBA", (8, 8), (255, 0, 0, 255)).save(first)
            Image.new("RGBA", (8, 8), (0, 255, 0, 255)).save(second)
            destination = root / "sheet.png"
            review_sheet(
                [("first", first), ("second", second)],
                destination,
                columns=2,
                cell_size=(64, 64),
            )
            with Image.open(destination) as sheet:
                self.assertEqual((128, 64), sheet.size)

    def test_fx_draft_frame_preserves_canvas_and_scales_alpha(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.png"
            image = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
            for x in range(8, 16):
                for y in range(8, 16):
                    image.putpixel((x, y), (255, 100, 20, 255))
            image.save(source)
            destination = root / "frame.png"
            render_draft_frame(
                source,
                destination,
                scale=0.5,
                opacity=0.5,
            )
            with Image.open(destination) as frame:
                self.assertEqual((24, 24), frame.size)
                alpha = frame.convert("RGBA").getchannel("A")
                self.assertEqual((10, 10, 14, 14), alpha.getbbox())
                self.assertEqual(128, alpha.getextrema()[1])

    def test_recipe_clone_overrides_nested_values(self) -> None:
        keys, value = parse_assignment("generation.steps=32")
        document = {"generation": {"steps": 24}}
        set_nested(document, keys, value)
        self.assertEqual(32, document["generation"]["steps"])

    def test_auto_chroma_key_and_detached_cleanup(self) -> None:
        image = Image.new("RGBA", (16, 16), (180, 40, 150, 255))
        for x in range(6, 10):
            for y in range(4, 13):
                image.putpixel((x, y), (80, 60, 40, 255))
        image.putpixel((1, 1), (20, 20, 20, 255))
        key = detect_border_color(image)
        self.assertEqual((180, 40, 150), key)
        cleaned = remove_chroma_key(image, key, 8)
        cleaned = keep_largest_alpha_component(cleaned, 80)
        self.assertEqual(0, cleaned.getpixel((1, 1))[3])
        self.assertEqual(255, cleaned.getpixel((7, 7))[3])

    def test_comfy_final_outputs_only_use_save_nodes(self) -> None:
        prompt = {
            "2": {"class_type": "PreviewImage", "inputs": {}},
            "10": {"class_type": "SaveImage", "inputs": {}},
        }
        self.assertEqual(("10",), final_output_node_ids(prompt))

    def test_multipart_rejects_header_injection(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "safe.png"
            source.write_bytes(b"png")
            with self.assertRaisesRegex(ComfyError, "CR or LF"):
                multipart_body(
                    {"bad\r\nheader": "value"},
                    "image",
                    source,
                )


class StoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.store = ReviewStore(self.root / "review.sqlite3")
        self.recipe = RecipeRegistry().get("actor-slinger-idle-v1")
        self.job_id = self.store.create_job(
            self.recipe,
            requested_by="test",
            candidate_count=2,
            base_seed=100,
        )

    def tearDown(self) -> None:
        self.temp.cleanup()

    def add_candidate(self) -> str:
        image_path = self.root / "candidate.png"
        image = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
        for x in range(2, 6):
            for y in range(1, 7):
                image.putpixel((x, y), (120, 80, 40, 255))
        image.save(image_path)
        return self.store.add_candidate(
            job_id=self.job_id,
            ordinal=1,
            seed=100,
            raw_path=image_path,
            metrics=image_metrics(image_path),
        )

    def test_job_claim_is_atomic(self) -> None:
        claimed = self.store.claim_job()
        self.assertIsNotNone(claimed)
        self.assertEqual(self.job_id, claimed["id"])
        self.assertIsNone(self.store.claim_job())

    def test_legacy_database_adds_batch_columns_before_index(self) -> None:
        legacy_path = self.root / "legacy.sqlite3"
        with sqlite3.connect(legacy_path) as connection:
            connection.execute(
                """
                CREATE TABLE jobs (
                    id TEXT PRIMARY KEY,
                    recipe_id TEXT NOT NULL,
                    recipe_path TEXT NOT NULL,
                    recipe_hash TEXT NOT NULL,
                    recipe_json TEXT NOT NULL,
                    status TEXT NOT NULL,
                    requested_by TEXT NOT NULL,
                    candidate_count INTEGER NOT NULL,
                    base_seed INTEGER NOT NULL,
                    notes TEXT NOT NULL DEFAULT '',
                    parent_candidate_id TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    error TEXT
                )
                """
            )
        ReviewStore(legacy_path)
        with sqlite3.connect(legacy_path) as connection:
            columns = {
                row[1]
                for row in connection.execute("PRAGMA table_info(jobs)")
            }
            indexes = {
                row[1]
                for row in connection.execute("PRAGMA index_list(jobs)")
            }
        self.assertIn("batch_id", columns)
        self.assertIn("batch_item_id", columns)
        self.assertIn("idx_jobs_batch", indexes)

    def test_allowlist_fails_closed_when_unconfigured(self) -> None:
        with unittest.mock.patch.dict(
            "os.environ",
            {"SLACK_ART_ALLOWED_USERS": ""},
        ):
            self.assertFalse(allowed_user("U123"))
        with unittest.mock.patch.dict(
            "os.environ",
            {"SLACK_ART_ALLOWED_USERS": "U123, U456"},
        ):
            self.assertTrue(allowed_user("U123"))
            self.assertFalse(allowed_user("U789"))

    def test_stale_running_rows_are_requeued(self) -> None:
        claimed = self.store.claim_job()
        self.assertEqual(0, self.store.recover_stale_running())
        with self.store.connect() as connection:
            connection.execute(
                "UPDATE jobs SET updated_at = '2000-01-01T00:00:00+00:00' "
                "WHERE id = ?",
                (claimed["id"],),
            )
        self.assertEqual(1, self.store.recover_stale_running())
        self.assertEqual("queued", self.store.get_job(claimed["id"])["status"])

    def test_requeued_job_can_replace_its_candidates(self) -> None:
        first = self.add_candidate()
        self.assertEqual(first, self.add_candidate())

    def test_outbox_claim_is_atomic_and_retries(self) -> None:
        outbox_id = self.store.enqueue_outbox("job_ready", {"job_id": "x"})
        rows = self.store.claim_outbox()
        self.assertEqual([outbox_id], [row["id"] for row in rows])
        self.assertEqual([], self.store.claim_outbox())
        self.store.finish_outbox(outbox_id, error="boom", retry=True)
        retried = self.store.claim_outbox()
        self.assertEqual([outbox_id], [row["id"] for row in retried])
        self.assertEqual(1, retried[0]["attempts"])

    def test_publish_rejects_traversal_slot(self) -> None:
        import copy as copy_module

        from art_review import Recipe

        document = copy_module.deepcopy(self.recipe.document)
        document["purpose"]["slot"] = "../../../../evil"
        bad_recipe = Recipe.from_document(document, path=self.recipe.path)
        job_id = self.store.create_job(
            bad_recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=7,
        )
        image_path = self.root / "traversal.png"
        Image.new("RGBA", (8, 8), (120, 80, 40, 255)).save(image_path)
        candidate_id = self.store.add_candidate(
            job_id=job_id,
            ordinal=1,
            seed=7,
            raw_path=image_path,
            metrics=image_metrics(image_path),
        )
        approve_candidate(
            self.store,
            candidate_id,
            user_id="test",
            event_key="traversal-approve",
        )
        self.store.set_candidate_status(
            candidate_id,
            "prepared",
            aseprite_path=image_path,
        )
        with self.assertRaisesRegex(ReviewError, "Invalid slot"):
            publish_candidate(self.store, candidate_id)

    def test_feedback_is_idempotent(self) -> None:
        candidate_id = self.add_candidate()
        first = self.store.add_feedback(
            event_key="event-1",
            user_id="U1",
            source="reaction",
            label="style-fit",
            candidate_id=candidate_id,
        )
        second = self.store.add_feedback(
            event_key="event-1",
            user_id="U1",
            source="reaction",
            label="style-fit",
            candidate_id=candidate_id,
        )
        self.assertTrue(first)
        self.assertFalse(second)
        self.assertEqual(1, len(self.store.pending_feedback()))

    def test_approval_records_resolved_feedback(self) -> None:
        candidate_id = self.add_candidate()
        approve_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="button-1",
        )
        candidate = self.store.get_candidate(candidate_id)
        self.assertEqual("approved", candidate["status"])
        snapshot = Path(candidate["approved_snapshot_path"])
        self.assertTrue(snapshot.is_dir())
        self.assertTrue((snapshot / "raw.png").is_file())
        self.assertTrue((snapshot / "approval.json").is_file())
        self.assertTrue((snapshot / "recipe.json").is_file())
        self.assertEqual([], self.store.pending_feedback())

    def test_batch_rotates_expensive_single_shots(self) -> None:
        plan, first_jobs = resolve_batch_jobs(
            self.store,
            "style-sampler",
            batch_dir=BatchRegistry().directory,
            recipe_dir=RecipeRegistry().directory,
        )
        first_effect = next(
            recipe for item, recipe, *_ in first_jobs if item == "effect"
        )
        first_animation = next(
            recipe for item, recipe, *_ in first_jobs if item == "animation"
        )
        self.assertEqual("fx-impact-physical", first_effect.shots[0].id)
        self.assertEqual("idle", first_animation.shots[0].id)
        batch_id, job_ids = self.store.create_batch_run(
            plan,
            requested_by="test",
            jobs=first_jobs,
        )
        self.assertEqual(5, len(job_ids))
        run = self.store.get_batch_run(batch_id)
        self.assertEqual("style-sampler", run["plan_id"])
        self.assertEqual(5, len(run["jobs"]))

        _, second_jobs = resolve_batch_jobs(
            self.store,
            "style-sampler",
            batch_dir=BatchRegistry().directory,
            recipe_dir=RecipeRegistry().directory,
        )
        second_effect = next(
            recipe for item, recipe, *_ in second_jobs if item == "effect"
        )
        second_animation = next(
            recipe for item, recipe, *_ in second_jobs if item == "animation"
        )
        self.assertEqual("fx-impact-fire", second_effect.shots[0].id)
        self.assertEqual("walk-contact-a", second_animation.shots[0].id)

    def test_only_queued_jobs_cancel_and_only_failed_jobs_retry(self) -> None:
        self.store.cancel_job(self.job_id)
        self.assertEqual("cancelled", self.store.get_job(self.job_id)["status"])
        with self.assertRaisesRegex(ReviewError, "cannot be cancelled"):
            self.store.cancel_job(self.job_id)
        with self.store.connect() as connection:
            connection.execute(
                "UPDATE jobs SET status = 'failed' WHERE id = ?",
                (self.job_id,),
            )
        self.store.retry_job(self.job_id)
        self.assertEqual("queued", self.store.get_job(self.job_id)["status"])
        with self.assertRaisesRegex(ReviewError, "cannot be retried"):
            self.store.retry_job(self.job_id)

    def test_apply_request_is_separate_from_approval_and_claimable(self) -> None:
        candidate_id = self.add_candidate()
        with self.assertRaisesRegex(ReviewError, "explicit approval"):
            self.store.create_apply_request(
                candidate_id,
                requested_by="test",
            )
        approve_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="apply-approve",
        )
        request_id = self.store.create_apply_request(
            candidate_id,
            requested_by="test",
            intent="투석 약탈자 런타임 교체",
        )
        self.assertEqual(
            request_id,
            self.store.create_apply_request(
                candidate_id,
                requested_by="test",
            ),
        )
        claimed = self.store.claim_apply_request()
        self.assertEqual(request_id, claimed["id"])
        context = self.store.apply_context(request_id)
        self.assertEqual(candidate_id, context["candidate"]["id"])
        self.assertEqual("planning", context["request"]["status"])
        self.store.set_apply_request_status(
            request_id,
            "needs_input",
            plan={"question": "교체 대상?"},
        )
        self.assertEqual(
            "needs_input",
            self.store.get_apply_request(request_id)["status"],
        )
        self.assertEqual(
            request_id,
            self.store.create_apply_request(
                candidate_id,
                requested_by="test",
                intent="기존 actor-slinger 슬롯 교체",
            ),
        )
        resumed = self.store.get_apply_request(request_id)
        self.assertEqual("queued", resumed["status"])
        self.assertEqual("기존 actor-slinger 슬롯 교체", resumed["intent"])

    def test_apply_intent_policy_and_rejection_cancellation(self) -> None:
        candidate_id = self.add_candidate()
        approve_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="intent-approve",
        )
        request_id = self.store.create_apply_request(
            candidate_id,
            requested_by="test",
            intent="first",
        )
        self.assertEqual(
            request_id,
            self.store.create_apply_request(
                candidate_id,
                requested_by="test",
                intent="latest",
            ),
        )
        self.assertEqual(
            "latest",
            self.store.get_apply_request(request_id)["intent"],
        )
        self.store.claim_apply_request(request_id)
        with self.assertRaisesRegex(ReviewError, "already planning"):
            self.store.create_apply_request(
                candidate_id,
                requested_by="test",
                intent="too late",
            )
        reject_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="intent-reject",
        )
        self.assertEqual(
            "cancelled",
            self.store.get_apply_request(request_id)["status"],
        )

    def test_publish_rejects_multi_shot_handoff_manifest(self) -> None:
        candidate_id = self.add_candidate()
        approve_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="handoff-approve",
        )
        handoff = self.root / "aseprite-handoff.json"
        handoff.write_text('{"shots": []}', encoding="utf-8")
        self.store.set_candidate_status(
            candidate_id,
            "prepared",
            aseprite_path=handoff,
        )
        with self.assertRaisesRegex(ReviewError, "finalized first"):
            publish_candidate(self.store, candidate_id)

    def test_slack_thread_maps_to_candidate(self) -> None:
        candidate_id = self.add_candidate()
        self.store.map_slack_message(
            message_ts="123.456",
            channel_id="C1",
            kind="candidate-root",
            job_id=self.job_id,
            candidate_id=candidate_id,
        )
        job_id, mapped_candidate = find_candidate_from_text(
            self.store,
            "123.456",
            "슬링을 조금 짧게",
        )
        self.assertEqual(self.job_id, job_id)
        self.assertEqual(candidate_id, mapped_candidate)

    def test_publish_requires_explicit_approval(self) -> None:
        candidate_id = self.add_candidate()
        self.store.set_candidate_status(candidate_id, "prepared")
        with self.assertRaisesRegex(ReviewError, "explicit approval"):
            publish_candidate(self.store, candidate_id)

    def test_candidate_card_has_review_controls(self) -> None:
        candidate_id = self.add_candidate()
        candidate = self.store.get_candidate(candidate_id)
        blocks = candidate_blocks(self.recipe, candidate)
        self.assertIn("검토 대기", blocks[0]["text"]["text"])
        self.assertIn("지금 할 일", blocks[1]["text"]["text"])
        self.assertNotIn("Checkpoint", blocks[1]["text"]["text"])
        action_ids = {
            element["action_id"]
            for block in blocks
            if block["type"] == "actions"
            for element in block["elements"]
        }
        self.assertEqual(
            {
                "art_candidate_approve",
                "art_candidate_reject",
                "art_candidate_variation",
                "art_candidate_prepare",
            },
            action_ids,
        )
        approve_candidate(
            self.store,
            candidate_id,
            user_id="U1",
            event_key="button-card-approve",
        )
        candidate = self.store.get_candidate(candidate_id)
        approved_blocks = candidate_blocks(
            self.recipe,
            candidate,
            approved=self.store.candidate_is_approved(candidate_id),
        )
        approved_actions = {
            element["action_id"]
            for block in approved_blocks
            if block["type"] == "actions"
            for element in block["elements"]
        }
        self.assertIn("art_candidate_apply", approved_actions)

    def test_candidate_card_carries_job_identity(self) -> None:
        """후보 카드가 유일한 완료 알림이므로 작업 ID와 묶음 위치를 직접 진다."""
        candidate_id = self.add_candidate()
        candidate = self.store.get_candidate(candidate_id)
        blocks = candidate_blocks(
            self.recipe,
            candidate,
            job_id=self.job_id,
            batch_position=(2, 3),
        )
        self.assertIn("(2/3)", blocks[1]["text"]["text"])
        context = blocks[2]["elements"][0]["text"]
        self.assertIn(f"작업 `{self.job_id}`", context)

    def test_candidate_card_omits_position_for_single_candidate(self) -> None:
        candidate_id = self.add_candidate()
        candidate = self.store.get_candidate(candidate_id)
        blocks = candidate_blocks(
            self.recipe,
            candidate,
            job_id=self.job_id,
            batch_position=(1, 1),
        )
        self.assertNotIn("(1/1)", blocks[1]["text"]["text"])

    def test_shot_card_has_scoped_review_controls(self) -> None:
        candidate_id = self.add_candidate()
        candidate = self.store.get_candidate(candidate_id)
        recipe = RecipeRegistry().get("fx-impact-suite-v1")
        shot = next(
            value for value in recipe.shots
            if value.id == "fx-impact-fire"
        )
        blocks = shot_blocks(recipe, candidate, shot)
        self.assertIn("검토 대기", blocks[0]["text"]["text"])
        self.assertIn("지금 할 일", blocks[1]["text"]["text"])
        action_ids = {
            element["action_id"]
            for block in blocks
            if block["type"] == "actions"
            for element in block["elements"]
        }
        self.assertEqual(
            {
                "art_shot_approve",
                "art_shot_reject",
                "art_shot_variation",
            },
            action_ids,
        )
        action = next(
            element
            for block in blocks
            if block["type"] == "actions"
            for element in block["elements"]
            if element["action_id"] == "art_shot_variation"
        )
        self.assertEqual(
            (candidate_id, "fx-impact-fire"),
            parse_shot_action(action),
        )

    def test_manual_shot_decision_updates_card_state(self) -> None:
        recipe = RecipeRegistry().get("fx-impact-suite-v2")
        job_id = self.store.create_job(
            recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=250,
        )
        image_path = self.root / "fx-decision.png"
        Image.new("RGBA", (8, 8), (255, 0, 255, 255)).save(image_path)
        candidate_id = self.store.add_candidate(
            job_id=job_id,
            ordinal=1,
            seed=250,
            raw_path=image_path,
            metrics=image_metrics(image_path),
        )
        decide_candidate_shot(
            self.store,
            candidate_id,
            "fx-impact-fire",
            "approve",
            user_id="cli",
            event_key="cli-shot-approve-1",
        )
        self.assertEqual(
            "approve",
            self.store.shot_decision(candidate_id, "fx-impact-fire"),
        )
        self.assertEqual([], self.store.pending_feedback())
        outbox = self.store.pending_outbox()
        self.assertEqual("shot_status", outbox[-1]["kind"])

    def test_slack_help_covers_generation_and_review_triggers(self) -> None:
        help_text = slack_help_text()
        for command in (
            "/art run <recipe-id> [count]",
            "/art shot <recipe-id> <shot-id> [count]",
            "/art approve <candidate-id>",
            "/art animation <candidate-id> [timing-scale]",
            "/art shot-variation <candidate-id> <shot-id> [count]",
            "/art batches",
            "/art queue",
            "/art apply <candidate-id> confirm",
        ):
            self.assertIn(command, help_text)

    def test_animation_card_and_timing_payload_are_scoped(self) -> None:
        candidate_id = self.add_candidate()
        candidate = self.store.get_candidate(candidate_id)
        recipe = RecipeRegistry().get("actor-slinger-animation-v2")
        blocks = candidate_blocks(recipe, candidate)
        action_ids = {
            element["action_id"]
            for block in blocks
            if block["type"] == "actions"
            for element in block["elements"]
        }
        self.assertIn("art_candidate_animation", action_ids)
        value = animation_action_value(candidate_id, 0.85)
        self.assertEqual(
            (candidate_id, 0.85),
            parse_animation_action({"value": value}),
        )
        timing = animation_timing_blocks(candidate_id, 1.0)
        timing_actions = {
            element["action_id"]
            for block in timing
            if block["type"] == "actions"
            for element in block["elements"]
        }
        self.assertEqual(
            {
                "art_animation_timing_fast",
                "art_animation_timing_normal",
                "art_animation_timing_slow",
            },
            timing_actions,
        )

    def test_thread_feedback_can_target_one_shot(self) -> None:
        recipe = RecipeRegistry().get("fx-impact-suite-v1")
        job_id = self.store.create_job(
            recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=200,
        )
        image_path = self.root / "fx.png"
        Image.new("RGBA", (8, 8), (255, 0, 255, 255)).save(image_path)
        candidate_id = self.store.add_candidate(
            job_id=job_id,
            ordinal=1,
            seed=200,
            raw_path=image_path,
            metrics=image_metrics(image_path),
        )
        self.store.map_slack_message(
            message_ts="shot-root",
            channel_id="C1",
            kind="candidate-root",
            job_id=job_id,
            candidate_id=candidate_id,
        )
        self.assertEqual(
            (job_id, candidate_id, "fx-impact-fire"),
            find_feedback_target(
                self.store,
                "shot-root",
                "[fx-impact-fire] 완전한 링을 제거해줘",
            ),
        )

    def test_shot_variation_creates_single_shot_job(self) -> None:
        recipe = RecipeRegistry().get("fx-impact-suite-v2")
        job_id = self.store.create_job(
            recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=300,
        )
        image_path = self.root / "fx-parent.png"
        Image.new("RGBA", (8, 8), (255, 0, 255, 255)).save(image_path)
        candidate_id = self.store.add_candidate(
            job_id=job_id,
            ordinal=1,
            seed=300,
            raw_path=image_path,
            metrics=image_metrics(image_path),
        )
        self.store.enqueue_action(
            "shot_variation",
            requested_by="slack:U1",
            candidate_id=candidate_id,
            payload={"count": 2, "shot_id": "fx-impact-fire"},
        )
        action = self.store.claim_action()
        self.assertIsNotNone(action)
        process_action(self.store, action)
        child = next(
            job
            for job in self.store.list_jobs(limit=10)
            if job["parent_candidate_id"] == candidate_id
        )
        child_recipe = json.loads(child["recipe_json"])
        self.assertEqual(2, child["candidate_count"])
        self.assertEqual(
            ["fx-impact-fire"],
            [
                item["id"]
                for item in child_recipe["effect_variants"]["variants"]
            ],
        )


if __name__ == "__main__":
    unittest.main()
