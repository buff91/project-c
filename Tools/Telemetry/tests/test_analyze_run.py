#!/usr/bin/env python3
"""리포트 분석기 — 소스에서 읽는 계약과 빈 값 처리."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import analyze_run  # noqa: E402


def fixture_report(**overrides) -> dict:
    report = {
        "schemaVersion": 6,
        "outcomeLabel": "Death",
        "dungeonId": "forgotten-catacombs",
        "seed": 1,
        "deepestFloorLabel": "4F",
        "deepestProgressIndex": 6,
        "totalTurns": 480,
        "elapsedSeconds": 1520.0,
        "kills": 14,
        "bossKills": 0,
        "meleeAttacks": 96,
        "rangedAttacks": 18,
        "burnApplications": 7,
        "barrelPushes": 0,
        "secretRoomsFound": 1,
        "enemyFalls": 3,
    }
    report.update(overrides)
    return report


class CoreContractTests(unittest.TestCase):
    """임계값은 Core 소스가 소유한다 — 여기서 값을 베끼면 조정 다음 판에 거짓이 된다."""

    def test_metric_fields_cover_every_unlock_condition(self) -> None:
        fields = analyze_run.metric_fields()
        for condition in analyze_run.unlock_conditions():
            self.assertIn(
                condition["metric"],
                fields,
                f"{condition['item']} 의 지표가 BountyRules.Measure 에 없다 — "
                "리포트가 조용히 '?'를 낸다",
            )

    def test_unlock_conditions_are_read_from_source(self) -> None:
        conditions = {c["item"]: c for c in analyze_run.unlock_conditions()}
        self.assertIn("FrostBomb", conditions)
        self.assertEqual("BurnApplications", conditions["FrostBomb"]["metric"])
        self.assertGreater(conditions["FrostBomb"]["threshold"], 0)
        self.assertTrue(conditions["FrostBomb"]["text"])

    def test_ranged_tiers_are_read_from_source(self) -> None:
        tiers = analyze_run.ranged_tiers()
        self.assertEqual(2, len(tiers))
        for tier in tiers:
            for key in ("range", "capacity", "recharge"):
                self.assertGreater(tier[key], 0, f"{tier['name']}.{key}")
        # 상위 티어는 더 자주 쏜다 — 이 관계가 뒤집히면 티어 설계가 깨진 것이다.
        self.assertLess(tiers[1]["recharge"], tiers[0]["recharge"])
        self.assertGreater(tiers[1]["capacity"], tiers[0]["capacity"])

    def test_missing_core_source_fails_loudly(self) -> None:
        with self.assertRaises(analyze_run.AnalysisError):
            analyze_run.read_core("NoSuchRules.cs")


class RenderTests(unittest.TestCase):
    def test_all_sections_render(self) -> None:
        text = analyze_run.analyze(fixture_report(), Path("run-x.json"))
        for heading in (
            "## 런 요약",
            "## 원거리",
            "## 해금 조건 진척",
            "## 의뢰 지표",
            "## 기둥 신호",
        ):
            self.assertIn(heading, text)

    def test_cheated_run_is_marked(self) -> None:
        clean = analyze_run.analyze(fixture_report(), Path("run-x.json"))
        cheated = analyze_run.analyze(
            fixture_report(cheatsUsed=True), Path("run-x.json")
        )
        self.assertNotIn("cheatsUsed", clean)
        self.assertIn("cheatsUsed=true", cheated)

    def test_zero_shots_and_zero_turns_do_not_divide_by_zero(self) -> None:
        text = analyze_run.analyze(
            fixture_report(rangedAttacks=0, meleeAttacks=0, totalTurns=0),
            Path("run-x.json"),
        )
        self.assertIn("## 원거리", text)

    def test_untouched_metric_is_called_out(self) -> None:
        text = analyze_run.analyze(
            fixture_report(barrelPushes=0), Path("run-x.json")
        )
        self.assertIn("아예 안 일어난다", text)

    def test_old_report_without_bands_still_renders(self) -> None:
        report = fixture_report()
        report.pop("deepestProgressIndex")
        text = analyze_run.analyze(report, Path("run-old.json"))
        self.assertIn("## 런 요약", text)
        self.assertNotIn("## 구간 곡선", text)


class AggregateTests(unittest.TestCase):
    """수치 조정은 한 판으로 하지 않는다 — 집계가 그 판단을 강제한다."""

    @staticmethod
    def runs(*reports) -> list:
        return [(Path(f"run-{i}.json"), r) for i, r in enumerate(reports)]

    def test_median_is_used_not_the_last_run(self) -> None:
        text = analyze_run.aggregate(
            self.runs(
                fixture_report(rangedAttacks=4),
                fixture_report(rangedAttacks=10),
                fixture_report(rangedAttacks=100),
            )
        )
        self.assertIn("10발", text, "중앙값이어야 한 판의 극단이 끌고 가지 않는다")

    def test_cheated_runs_are_excluded_and_counted(self) -> None:
        text = analyze_run.aggregate(
            self.runs(
                fixture_report(),
                fixture_report(cheatsUsed=True),
            )
        )
        self.assertIn("판 1건", text)
        self.assertIn("치트 1건 제외", text)

    def test_all_cheated_is_refused(self) -> None:
        with self.assertRaisesRegex(analyze_run.AnalysisError, "치트"):
            analyze_run.aggregate(self.runs(fixture_report(cheatsUsed=True)))

    def test_small_sample_blocks_number_setting(self) -> None:
        thin = analyze_run.aggregate(self.runs(fixture_report()))
        self.assertIn("수치는 확정하지 않는다", thin)
        thick = analyze_run.aggregate(
            self.runs(fixture_report(), fixture_report(), fixture_report())
        )
        self.assertNotIn("수치는 확정하지 않는다", thick)

    def test_always_zero_metrics_are_named(self) -> None:
        text = analyze_run.aggregate(
            self.runs(fixture_report(barrelPushes=0), fixture_report(barrelPushes=0))
        )
        self.assertIn("BarrelPushes", text.split("## 의뢰 지표")[1])

    def test_median_helper(self) -> None:
        self.assertEqual(0.0, analyze_run.median([]))
        self.assertEqual(2, analyze_run.median([3, 1, 2]))
        self.assertEqual(2.5, analyze_run.median([1, 2, 3, 4]))


class ReportDiscoveryTests(unittest.TestCase):
    def test_explicit_directory_is_scanned(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("run-b.json", "run-a.json"):
                (root / name).write_text(
                    json.dumps(fixture_report()), encoding="utf-8"
                )
            found = analyze_run.find_reports(root)
            self.assertEqual(["run-a.json", "run-b.json"], [p.name for p in found])

    def test_empty_directory_explains_how_a_report_appears(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(analyze_run.AnalysisError, "run-\\*.json"):
                analyze_run.find_reports(Path(directory))

    def test_unreadable_report_fails_loudly(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            broken = Path(directory) / "run-broken.json"
            broken.write_text("{not json", encoding="utf-8")
            with self.assertRaises(analyze_run.AnalysisError):
                analyze_run.load_report(broken)


if __name__ == "__main__":
    unittest.main()
