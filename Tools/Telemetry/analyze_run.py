#!/usr/bin/env python3
"""플레이테스트 리포트 한 건을 막힌 밸런스 항목의 숫자로 바꾼다.

`docs/ROADMAP.md`「콘텐츠 & 밸런스」의 미완 항목들은 코드가 아니라 **수치**를 기다린다.
이 도구는 게임이 이미 남기는 `RunTelemetry` JSON을 읽어 그 판단에 필요한 값만 뽑는다.

**임계값을 여기에 베끼지 않는다.** 해금 조건·원거리 충전·의뢰 지표 매핑은 전부
`Assets/_Project/Scripts/Core`에서 읽는다 — 게임에서 숫자를 고치면 이 리포트가 같이
따라와야 하고, 복사본을 두면 조정한 다음 판에 거짓을 말한다.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
CORE_DIR = PROJECT_ROOT / "Assets/_Project/Scripts/Core"
DEFAULT_REPORT_ROOTS = (
    Path.home()
    / "Library/Application Support/buff/project-c/development-profile/telemetry",
    Path.home()
    / "Library/Application Support/DefaultCompany/project-c"
    / "development-profile/telemetry",
)


class AnalysisError(RuntimeError):
    """소스에서 계약을 못 읽었거나 리포트가 없다 — 조용히 추정하지 않는다."""


# ── Core 소스에서 살아 있는 계약을 읽는다 ────────────────────────────


def read_core(name: str) -> str:
    path = CORE_DIR / name
    if not path.is_file():
        raise AnalysisError(f"Core 소스를 찾을 수 없다: {path}")
    return path.read_text(encoding="utf-8")


def metric_fields() -> dict[str, str]:
    """`BountyMetric` → 텔레메트리 필드. 게임의 Measure 스위치를 그대로 읽는다."""
    source = read_core("BountyRules.cs")
    pairs = re.findall(
        r"case\s+BountyMetric\.(\w+)\s*:\s*return\s+telemetry\.(\w+)\s*;",
        source,
    )
    if not pairs:
        raise AnalysisError(
            "BountyRules.Measure 의 지표 매핑을 읽지 못했다 — 스위치 형태가 바뀌었다"
        )
    return dict(pairs)


def unlock_conditions() -> list[dict[str, Any]]:
    """도구 해금 조건 5종(지표·임계값·표시 문구)."""
    source = read_core("ItemUnlockRules.cs")
    rows = re.findall(
        r"new ItemUnlockCondition\(\s*"
        r"ItemKind\.(\w+)\s*,\s*BountyMetric\.(\w+)\s*,\s*(\d+)\s*,\s*"
        r'"([^"]*)"',
        source,
    )
    if not rows:
        raise AnalysisError("ItemUnlockRules.Conditions 를 읽지 못했다")
    return [
        {"item": item, "metric": metric, "threshold": int(value), "text": text}
        for item, metric, value, text in rows
    ]


def ranged_tiers() -> list[dict[str, Any]]:
    """원거리 2티어의 사거리·용량·재충전 턴."""
    baseline = read_core("RangedWeaponRules.cs")

    def const(name: str) -> int:
        match = re.search(rf"const int {name} = (\d+);", baseline)
        if match is None:
            raise AnalysisError(
                f"RangedWeaponRules.Baseline.{name} 을 읽지 못했다"
            )
        return int(match.group(1))

    tiers = [
        {
            "name": "내장 이미터(기본)",
            "range": const("Range"),
            "capacity": const("Capacity"),
            "recharge": const("RechargeTurns"),
        }
    ]

    equipment = read_core("Equipment.cs")
    arc = re.search(
        r'"arc-caster".*?rangedRange:\s*(\d+),\s*'
        r"rangedCapacity:\s*(\d+),\s*rangedRechargeTurns:\s*(\d+)",
        equipment,
        re.S,
    )
    if arc is None:
        raise AnalysisError("Equipment.cs 의 아크 캐스터 수치를 읽지 못했다")
    tiers.append(
        {
            "name": "아크 캐스터",
            "range": int(arc.group(1)),
            "capacity": int(arc.group(2)),
            "recharge": int(arc.group(3)),
        }
    )
    return tiers


# ── 리포트 찾기 ────────────────────────────────────────────────────


def find_reports(explicit: Path | None) -> list[Path]:
    if explicit is not None:
        if explicit.is_dir():
            found = sorted(explicit.glob("run-*.json"))
        elif explicit.is_file():
            found = [explicit]
        else:
            raise AnalysisError(f"리포트를 찾을 수 없다: {explicit}")
        if not found:
            raise AnalysisError(f"{explicit} 아래에 run-*.json 이 없다")
        return found

    for root in DEFAULT_REPORT_ROOTS:
        found = sorted(root.glob("run-*.json"))
        if found:
            return found
    raise AnalysisError(
        "리포트가 아직 없다 — 한 판을 끝내야(승리·생환·사망·포기) 생긴다.\n"
        "  찾아본 곳:\n    "
        + "\n    ".join(str(root) for root in DEFAULT_REPORT_ROOTS)
    )


def load_report(path: Path) -> dict[str, Any]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise AnalysisError(f"리포트를 읽을 수 없다 {path}: {exc}") from exc
    if not isinstance(document, dict):
        raise AnalysisError(f"리포트 형식이 아니다: {path}")
    return document


def get(report: dict[str, Any], field: str, default: Any = 0) -> Any:
    value = report.get(field, default)
    return default if value is None else value


# ── 절별 출력 ──────────────────────────────────────────────────────


def line(label: str, value: Any, note: str = "") -> str:
    text = f"  {label:<22} {value}"
    return f"{text}   {note}" if note else text


def section_summary(report: dict[str, Any]) -> list[str]:
    turns = get(report, "totalTurns")
    seconds = float(get(report, "elapsedSeconds", 0.0))
    out = [
        "## 런 요약",
        line("결과", get(report, "outcomeLabel", "?")),
        line("종료 사유", get(report, "endCause", "") or "—"),
        line("던전 · 시드", f"{get(report, 'dungeonId', '?')} · {get(report, 'seed')}"),
        line(
            "도달",
            f"{get(report, 'deepestFloorLabel', '?')} "
            f"(진행 지수 {get(report, 'deepestProgressIndex')})",
        ),
        line("턴 · 시간", f"{turns}턴 · {seconds / 60:.1f}분"),
        line("처치 · 보스", f"{get(report, 'kills')} · {get(report, 'bossKills')}"),
    ]
    if get(report, "cheatsUsed", False):
        out.append(
            "  ⚠ cheatsUsed=true — 밸런스 표본으로 쓰지 않는다(치트가 섞인 판)."
        )
    return out


def section_ranged(report: dict[str, Any]) -> list[str]:
    ranged = get(report, "rangedAttacks")
    melee = get(report, "meleeAttacks")
    turns = get(report, "totalTurns")
    out = ["", "## 원거리 — 조정 축은 판당 사격 횟수 하나"]
    out.append(line("사격 · 근접", f"{ranged} · {melee}"))
    if ranged + melee > 0:
        out.append(
            line("사격 비중", f"{ranged / (ranged + melee) * 100:.0f}%")
        )
    if ranged > 0 and turns > 0:
        out.append(line("실측 사격 간격", f"{turns / ranged:.1f}턴/발"))

    for tier in ranged_tiers():
        if turns <= 0:
            continue
        # 재충전만으로 걸리는 상한. 실제 제약은 교전 횟수·사선이므로 기대치가 아니라
        # "재충전이 병목이었나"를 가르는 선으로만 읽는다.
        ceiling = tier["capacity"] + turns // tier["recharge"]
        used = ranged / ceiling if ceiling else 0
        out.append(
            line(
                tier["name"],
                f"재충전 상한 {ceiling}발 (용량 {tier['capacity']} + "
                f"{turns}턴/{tier['recharge']}턴)",
                f"실제 {ranged}발 = {used * 100:.0f}%",
            )
        )
    out.append(
        "  판정: 상한에 붙어 있으면 재충전이 병목이다 → 늘려서 사격을 아끼게 한다.\n"
        "        한참 못 미치면 병목은 재충전이 아니다 → 사거리·피해·사선을 먼저 본다."
    )
    return out


def section_unlocks(report: dict[str, Any]) -> list[str]:
    fields = metric_fields()
    out = ["", "## 해금 조건 진척 (한 판 기준)"]
    for condition in unlock_conditions():
        field = fields.get(condition["metric"])
        if field is None:
            out.append(
                line(condition["item"], "?", f"지표 {condition['metric']} 매핑 없음")
            )
            continue
        value = get(report, field)
        threshold = condition["threshold"]
        ratio = value / threshold if threshold else 0
        if value >= threshold:
            verdict = "이번 판에 달성"
        elif ratio == 0:
            verdict = "0 — 이 축이 판에서 아예 안 일어난다"
        else:
            verdict = f"이 페이스면 {1 / ratio:.1f}판"
        out.append(
            line(
                condition["item"],
                f"{value}/{threshold}",
                f"{condition['text']} → {verdict}",
            )
        )
    return out


def section_bounty_metrics(report: dict[str, Any]) -> list[str]:
    fields = metric_fields()
    out = ["", "## 의뢰 지표 — 0이면 그 지표로는 의뢰를 만들 수 없다"]
    for metric, field in sorted(fields.items()):
        value = get(report, field)
        out.append(line(metric, value, "" if value else "← 0"))
    return out


def section_bands(report: dict[str, Any]) -> list[str]:
    bands = get(report, "bands", []) or []
    if not bands:
        return []
    out = ["", "## 구간 곡선", "  구간         층  턴    받은피해  준피해  처치  아이템"]
    for band in bands:
        out.append(
            f"  {str(band.get('label', '?')):<11} "
            f"{band.get('floors', 0):>2}  "
            f"{band.get('turns', 0):>4}  "
            f"{band.get('damageTaken', 0):>8}  "
            f"{band.get('damageDealt', 0):>6}  "
            f"{band.get('kills', 0):>4}  "
            f"{band.get('itemsCollected', 0):>6}"
        )
    return out


def section_damage(report: dict[str, Any]) -> list[str]:
    sources = get(report, "damageSources", []) or []
    if not sources:
        return []
    ordered = sorted(
        sources, key=lambda item: item.get("damageTaken", 0), reverse=True
    )
    out = ["", "## 무엇에 맞았나 (치명타 = 죽은 원인)"]
    for source in ordered[:8]:
        fatal = source.get("fatalHits", 0)
        out.append(
            line(
                source.get("source", "?"),
                f"{source.get('damageTaken', 0)} 피해 / "
                f"{source.get('incomingHits', 0)}회",
                "☠ 치명타" if fatal else "",
            )
        )
    return out


def section_pillars(report: dict[str, Any]) -> list[str]:
    return [
        "",
        "## 기둥 신호",
        line(
            "① 입체",
            f"플레이어 낙하 {get(report, 'playerFalls')} "
            f"(의도 {get(report, 'intentionalFalls')}) · "
            f"적 낙하 {get(report, 'enemyFalls')} · "
            f"총 {get(report, 'floorsFallen')}층",
        ),
        line(
            "② 상호작용",
            f"화상 {get(report, 'burnApplications')} · "
            f"빙결 {get(report, 'freezeApplications')} · "
            f"기름 {get(report, 'oilIgnitedTiles')} · "
            f"결빙 {get(report, 'waterFrozenTiles')} · "
            f"증발 {get(report, 'waterEvaporatedTiles')}",
        ),
        line(
            "④ 파밍",
            f"획득 {get(report, 'itemsCollected')} · "
            f"사용 {get(report, 'itemsUsed')} · "
            f"조합 {get(report, 'itemsCrafted')} · "
            f"숨은 방 {get(report, 'secretRoomsFound')}",
        ),
        line(
            "회복 경제",
            f"휴식 {get(report, 'restSitesUsed')}회 / "
            f"{get(report, 'healingFromRest')} 회복 · "
            f"굶주림 {get(report, 'starvingTurns')}턴 "
            f"({get(report, 'starvationDamage')} 피해)",
        ),
    ]


def median(values: list[float]) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return float(ordered[middle])
    return (ordered[middle - 1] + ordered[middle]) / 2


def aggregate(reports: list[tuple[Path, dict[str, Any]]]) -> str:
    """여러 판을 묶어 중앙값으로 본다.

    한 판은 표본 1이다 — 배경 부하가 아니라 플레이 방식만으로도 사격 횟수가 두 배씩
    흔들린다. 수치를 확정하려면 판이 여러 개여야 하고, 이 절이 그 판단을 강제한다.
    """
    clean = [(path, doc) for path, doc in reports if not get(doc, "cheatsUsed", False)]
    cheated = len(reports) - len(clean)
    if not clean:
        raise AnalysisError(
            f"치트가 섞이지 않은 리포트가 없다(전체 {len(reports)}건) — "
            "밸런스 표본으로 쓸 수 없다"
        )

    def values(field: str) -> list[float]:
        return [float(get(doc, field, 0)) for _, doc in clean]

    def med(field: str) -> float:
        return median(values(field))

    count = len(clean)
    out = [f"# 집계 — 판 {count}건"]
    if cheated:
        out.append(f"  (치트 {cheated}건 제외)")
    if count < 3:
        out.append(
            "  ⚠ 표본이 적다 — 방향은 읽어도 **수치는 확정하지 않는다**. 3판 이상 권장."
        )

    outcomes: dict[str, int] = {}
    for _, doc in clean:
        label = str(get(doc, "outcomeLabel", "?"))
        outcomes[label] = outcomes.get(label, 0) + 1
    out.append("")
    out.append("## 결과 분포")
    for label, times in sorted(outcomes.items(), key=lambda kv: -kv[1]):
        out.append(line(label, f"{times}판"))
    out.append(line("턴(중앙값)", f"{med('totalTurns'):.0f}"))
    out.append(
        line(
            "도달 진행지수",
            f"중앙 {med('deepestProgressIndex'):.0f} · "
            f"최고 {max(values('deepestProgressIndex')):.0f}",
        )
    )

    out.append("")
    out.append("## 원거리")
    shots = med("rangedAttacks")
    turns = med("totalTurns")
    out.append(line("사격(중앙값)", f"{shots:.0f}발", f"근접 {med('meleeAttacks'):.0f}"))
    if shots > 0:
        out.append(line("실측 사격 간격", f"{turns / shots:.1f}턴/발"))
    else:
        out.append(line("실측 사격 간격", "— (사격 0)"))
    for tier in ranged_tiers():
        ceiling = tier["capacity"] + turns // tier["recharge"]
        if ceiling <= 0:
            continue
        out.append(
            line(
                tier["name"],
                f"재충전 상한 {ceiling:.0f}발",
                f"실제 {shots:.0f}발 = {shots / ceiling * 100:.0f}%",
            )
        )

    fields = metric_fields()
    out.append("")
    out.append("## 해금 조건 (판당 중앙값)")
    for condition in unlock_conditions():
        field = fields.get(condition["metric"])
        if field is None:
            continue
        value = med(field)
        threshold = condition["threshold"]
        ratio = value / threshold if threshold else 0
        if ratio >= 1:
            verdict = "매 판 달성 — 조건이 느슨하다"
        elif ratio == 0:
            verdict = "0 — 이 축이 판에서 일어나지 않는다"
        else:
            verdict = f"{1 / ratio:.1f}판 필요"
        out.append(
            line(condition["item"], f"{value:.1f}/{threshold}", verdict)
        )

    dead = [
        metric
        for metric, field in sorted(fields.items())
        if med(field) == 0
    ]
    out.append("")
    out.append("## 의뢰 지표")
    out.append(
        line("항상 0", ", ".join(dead) if dead else "없음", "← 의뢰를 만들 수 없다")
    )

    fatal: dict[str, int] = {}
    for _, doc in clean:
        for source in get(doc, "damageSources", []) or []:
            hits = int(source.get("fatalHits", 0))
            if hits:
                name = str(source.get("source", "?"))
                fatal[name] = fatal.get(name, 0) + hits
    if fatal:
        out.append("")
        out.append("## 무엇에 죽었나")
        for name, hits in sorted(fatal.items(), key=lambda kv: -kv[1]):
            out.append(line(name, f"{hits}회"))
    return "\n".join(out)


def analyze(report: dict[str, Any], path: Path) -> str:
    blocks = [f"# {path.name}  (스키마 v{get(report, 'schemaVersion', '?')})"]
    blocks += section_summary(report)
    blocks += section_ranged(report)
    blocks += section_unlocks(report)
    blocks += section_bounty_metrics(report)
    blocks += section_bands(report)
    blocks += section_damage(report)
    blocks += section_pillars(report)
    return "\n".join(blocks)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "report",
        nargs="?",
        type=Path,
        help="run-*.json 경로 또는 폴더 (생략하면 개발 프로필에서 찾는다)",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="찾은 리포트를 전부 분석한다 (기본은 가장 최근 것 하나)",
    )
    parser.add_argument(
        "--aggregate",
        action="store_true",
        help="여러 판을 중앙값으로 묶는다 — 수치 조정은 이 값으로 판단한다",
    )
    parser.add_argument("--json", action="store_true", help="원본 리포트를 그대로 낸다")
    args = parser.parse_args()

    try:
        reports = find_reports(args.report)
        if args.aggregate:
            print(aggregate([(path, load_report(path)) for path in reports]))
            return 0
        if not args.all:
            reports = reports[-1:]
        for index, path in enumerate(reports):
            report = load_report(path)
            if args.json:
                print(json.dumps(report, ensure_ascii=False, indent=2))
                continue
            if index:
                print()
            print(analyze(report, path))
    except AnalysisError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
