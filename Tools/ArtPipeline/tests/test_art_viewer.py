#!/usr/bin/env python3
"""로컬 리뷰 뷰어 규칙 — 순서·이미지 경로·판정 위임·이스케이프."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any

from PIL import Image


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import art_viewer  # noqa: E402
from art_review import (  # noqa: E402
    RecipeRegistry,
    ReviewError,
    ReviewStore,
    image_metrics,
)


def write_png(path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    image = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    for x in range(2, 6):
        for y in range(1, 7):
            image.putpixel((x, y), (120, 80, 40, 255))
    image.save(path)
    return path


class RecordingActions:
    """CLI 판정 함수 자리에 들어가는 기록용 어댑터."""

    def __init__(self, store: ReviewStore) -> None:
        self.store = store
        self.calls: list[tuple[Any, ...]] = []

    def build(self) -> art_viewer.ViewerActions:
        return art_viewer.ViewerActions(
            approve=self.approve,
            reject=self.reject,
            shot_decision=self.shot_decision,
            enqueue=self.enqueue,
        )

    def approve(self, candidate_id: str) -> None:
        self.calls.append(("approve", candidate_id))
        self.store.set_candidate_status(candidate_id, "approved")

    def reject(self, candidate_id: str) -> None:
        self.calls.append(("reject", candidate_id))
        self.store.set_candidate_status(candidate_id, "rejected")

    def shot_decision(
        self,
        candidate_id: str,
        shot_id: str,
        decision: str,
    ) -> None:
        self.calls.append(("shot", candidate_id, shot_id, decision))

    def enqueue(
        self,
        kind: str,
        candidate_id: str,
        payload: dict[str, Any],
    ) -> str:
        self.calls.append(("enqueue", kind, candidate_id, payload))
        return "ACT-TEST"


class ViewerTests(unittest.TestCase):
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
            notes="테스트 노트",
        )
        self.candidates = [
            self.add_candidate(ordinal) for ordinal in (1, 2)
        ]
        self.actions = RecordingActions(self.store)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def add_candidate(self, ordinal: int, job_id: str | None = None) -> str:
        job = job_id or self.job_id
        raw = write_png(self.root / job / f"C{ordinal:02d}" / "raw.png")
        return self.store.add_candidate(
            job_id=job,
            ordinal=ordinal,
            seed=100 + ordinal,
            raw_path=raw,
            metrics=image_metrics(raw),
        )

    def add_shot_manifest(self, candidate_id: str) -> Path:
        directory = self.root / self.job_id / "C01"
        write_png(directory / "shots" / "idle" / "raw.png")
        write_png(directory / "shots" / "idle" / "game-preview.png")
        write_png(directory / "shots" / "walk-contact-a" / "raw.png")
        manifest = directory / "shot-manifest.json"
        manifest.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "recipe_id": self.recipe.id,
                    "candidate_seed": 101,
                    "shots": [
                        {
                            "id": "idle",
                            "label": "대기",
                            "raw_path": str(
                                directory / "shots" / "idle" / "raw.png"
                            ),
                            "game_preview_path": str(
                                directory
                                / "shots"
                                / "idle"
                                / "game-preview.png"
                            ),
                        },
                        {
                            "id": "walk-contact-a",
                            "label": "걷기 A",
                            "raw_path": str(
                                directory
                                / "shots"
                                / "walk-contact-a"
                                / "raw.png"
                            ),
                        },
                    ],
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        return manifest

    def test_index_numbers_follow_the_recent_candidate_order(self) -> None:
        newer_job = self.store.create_job(
            self.recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=200,
        )
        newest = self.add_candidate(1, job_id=newer_job)
        views = art_viewer.build_index(self.store)
        self.assertEqual(
            [newest, *self.candidates],
            [view.id for view in views],
        )
        self.assertEqual([1, 2, 3], [view.index for view in views])

    def test_index_reports_missing_source_instead_of_raising(self) -> None:
        (self.root / self.job_id / "C02" / "raw.png").unlink()
        views = {view.id: view for view in art_viewer.build_index(self.store)}
        self.assertTrue(views[self.candidates[0]].has_image)
        self.assertFalse(views[self.candidates[1]].has_image)

    def test_shot_views_carry_labels_previews_and_decisions(self) -> None:
        self.add_shot_manifest(self.candidates[0])
        self.store.add_feedback(
            event_key="test:shot",
            user_id="test",
            source="shot-command",
            label="shot:idle:approve",
            candidate_id=self.candidates[0],
        )
        views = {view.id: view for view in art_viewer.build_index(self.store)}
        shots = views[self.candidates[0]].shots
        self.assertEqual(["idle", "walk-contact-a"], [s.id for s in shots])
        self.assertEqual("대기", shots[0].label)
        self.assertEqual("approve", shots[0].decision)
        self.assertTrue(shots[0].has_preview)
        self.assertIsNone(shots[1].decision)
        self.assertFalse(shots[1].has_preview)

    def test_image_path_resolves_candidate_and_shot_sources(self) -> None:
        self.add_shot_manifest(self.candidates[0])
        candidate = self.candidates[0]
        self.assertEqual(
            "raw.png",
            art_viewer.image_path(self.store, candidate, "raw").name,
        )
        self.assertEqual(
            "raw.png",
            art_viewer.image_path(
                self.store, candidate, "raw", "idle"
            ).name,
        )
        self.assertEqual(
            "game-preview.png",
            art_viewer.image_path(
                self.store, candidate, "preview", "idle"
            ).name,
        )

    def test_image_path_rejects_shot_ids_that_escape_the_folder(self) -> None:
        outside = write_png(self.root / "secret.png")
        self.assertTrue(outside.is_file())
        for shot_id in ("../..", "..", "a/b", "", "."):
            with self.assertRaises(ReviewError):
                art_viewer.image_path(
                    self.store,
                    self.candidates[0],
                    "raw",
                    shot_id,
                )

    def test_image_path_rejects_missing_files(self) -> None:
        with self.assertRaisesRegex(ReviewError, "missing"):
            art_viewer.image_path(
                self.store,
                self.candidates[0],
                "raw",
                "no-such-shot",
            )

    def test_dispatch_approve_uses_the_shared_decision_path(self) -> None:
        result = art_viewer.dispatch_action(
            self.store,
            self.actions.build(),
            {"action": "approve", "candidate_id": self.candidates[0]},
        )
        self.assertEqual(
            ("approve", self.candidates[0]),
            self.actions.calls[0],
        )
        self.assertEqual("approved", result["status"])
        self.assertEqual("채택", result["status_label"])
        self.assertIsNone(result["queued_action_id"])

    def test_dispatch_queues_variation_with_a_bounded_count(self) -> None:
        result = art_viewer.dispatch_action(
            self.store,
            self.actions.build(),
            {
                "action": "variation",
                "candidate_id": self.candidates[0],
                "count": 6,
            },
        )
        kind, candidate_id, payload = self.actions.calls[0][1:]
        self.assertEqual("variation", kind)
        self.assertEqual(self.candidates[0], candidate_id)
        self.assertEqual(6, payload["count"])
        self.assertEqual("ACT-TEST", result["queued_action_id"])

    def test_dispatch_rejects_out_of_range_arguments(self) -> None:
        for payload in (
            {"action": "variation", "count": 13},
            {"action": "variation", "count": 0},
            {"action": "animation_draft", "timing_scale": 3.0},
        ):
            with self.assertRaises(ReviewError):
                art_viewer.dispatch_action(
                    self.store,
                    self.actions.build(),
                    {"candidate_id": self.candidates[0], **payload},
                )
        self.assertEqual([], self.actions.calls)

    def test_dispatch_rejects_unknown_actions_and_candidates(self) -> None:
        with self.assertRaisesRegex(ReviewError, "Unknown action"):
            art_viewer.dispatch_action(
                self.store,
                self.actions.build(),
                {"action": "publish", "candidate_id": self.candidates[0]},
            )
        with self.assertRaises(ReviewError):
            art_viewer.dispatch_action(
                self.store,
                self.actions.build(),
                {"action": "approve", "candidate_id": "ART-nope-C01"},
            )

    def test_dispatch_shot_actions_need_a_shot_id(self) -> None:
        with self.assertRaisesRegex(ReviewError, "shot_id"):
            art_viewer.dispatch_action(
                self.store,
                self.actions.build(),
                {"action": "shot_approve", "candidate_id": self.candidates[0]},
            )
        art_viewer.dispatch_action(
            self.store,
            self.actions.build(),
            {
                "action": "shot_reject",
                "candidate_id": self.candidates[0],
                "shot_id": "idle",
            },
        )
        self.assertEqual(
            ("shot", self.candidates[0], "idle", "reject"),
            self.actions.calls[0],
        )

    def test_page_escapes_untrusted_text(self) -> None:
        job_id = self.store.create_job(
            self.recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=300,
            notes="<script>alert(1)</script>",
        )
        self.add_candidate(1, job_id=job_id)
        page = art_viewer.render_page(art_viewer.build_index(self.store))
        self.assertNotIn("<script>alert(1)</script>", page)
        self.assertIn("&lt;script&gt;alert(1)&lt;/script&gt;", page)

    def test_page_shows_the_alias_number_used_by_the_cli(self) -> None:
        page = art_viewer.render_page(art_viewer.build_index(self.store))
        self.assertIn(">^1</span>", page)
        self.assertIn(">^2</span>", page)

    def test_progress_strip_lists_queued_and_running_jobs(self) -> None:
        queued = self.store.create_job(
            self.recipe,
            requested_by="test",
            candidate_count=1,
            base_seed=400,
        )
        self.store.claim_job()
        self.store.set_job_progress(
            self.job_id,
            {"stage": "generating", "units_total": 2, "units_done": 1},
        )
        jobs = art_viewer.running_jobs(self.store)
        self.assertEqual([self.job_id, queued], [job["id"] for job in jobs])
        self.assertEqual("running", jobs[0]["status"])
        self.assertEqual(50, jobs[0]["percent"])
        self.assertEqual("queued", jobs[1]["status"])
        self.assertIsNone(jobs[1]["percent"])

    def test_progress_strip_ignores_finished_jobs(self) -> None:
        self.store.claim_job()
        self.store.set_job_status(self.job_id, "awaiting_review")
        self.assertEqual([], art_viewer.running_jobs(self.store))

    def test_page_carries_the_progress_strip(self) -> None:
        page = art_viewer.render_page(art_viewer.build_index(self.store))
        self.assertIn('<section id="progress"', page)
        self.assertIn('fetch("/progress")', page)

    def test_host_guard_only_accepts_localhost(self) -> None:
        self.assertTrue(art_viewer.host_is_local("127.0.0.1:8787", 8787))
        self.assertTrue(art_viewer.host_is_local("localhost:8787", 8787))
        self.assertTrue(art_viewer.host_is_local("localhost", 8787))
        self.assertTrue(art_viewer.host_is_local("[::1]:8787", 8787))
        self.assertFalse(art_viewer.host_is_local("localhost:9999", 8787))
        self.assertFalse(art_viewer.host_is_local("evil.example:8787", 8787))
        self.assertFalse(art_viewer.host_is_local("", 8787))
        self.assertFalse(art_viewer.host_is_local(None, 8787))


if __name__ == "__main__":
    unittest.main()
