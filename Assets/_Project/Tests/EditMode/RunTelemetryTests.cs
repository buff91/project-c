using System;
using NUnit.Framework;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Tests
{
    public class RunTelemetryTests
    {
        [Test]
        public void BeginAndFloorTransitions_AccumulateTimeTurnsAndVisits()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs",
                "knight",
                1977,
                0,
                new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc));

            telemetry.RecordElapsed(61.5f, 0);
            telemetry.RecordTurn(0);
            telemetry.RecordFloorEntered(-1, 1);
            telemetry.RecordElapsed(30f, -1);
            telemetry.RecordTurn(-1);
            telemetry.RecordTurn(-1);

            Assert.AreEqual(91.5f, telemetry.elapsedSeconds);
            Assert.AreEqual(3, telemetry.totalTurns);
            Assert.AreEqual(-1, telemetry.deepestFloorIndex);
            Assert.AreEqual(2, telemetry.floors.Count);
            Assert.AreEqual(1, telemetry.floors[0].visits);
            Assert.AreEqual(2, telemetry.floors[1].turns);
            Assert.AreEqual("01:31", RunTelemetry.FormatDuration(telemetry.elapsedSeconds));
        }

        [Test]
        public void CombatItemsAndMechanics_AggregateForPlaytestReport()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs",
                "ranger",
                11,
                0,
                DateTime.UtcNow);

            telemetry.RecordDamageTaken("Goblin B1-1", 2, false, 0);
            telemetry.RecordDamageTaken("Goblin B1-2", 3, true, 0);
            telemetry.RecordDamageDealt("Melee", 4, 0);
            telemetry.RecordKill(0, boss: false);
            telemetry.RecordItemCollected(ItemKind.Potion, 0);
            telemetry.RecordItemUsed(ItemKind.Potion, 0);
            telemetry.RecordItemCrafted(ItemKind.Bomb, 0);
            telemetry.RecordFall(player: true, intentional: true, fallenFloorCount: 1);
            telemetry.RecordStatus(StatusKind.Burn);
            telemetry.RecordOilIgnition(3);
            telemetry.RecordRest(3, 0);
            telemetry.RecordSecretRoomFound(0);

            Assert.AreEqual(5, telemetry.totalDamageTaken);
            Assert.AreEqual(1, telemetry.damageSources[0].fatalHits);
            Assert.AreEqual("Goblin", telemetry.damageSources[0].source);
            Assert.AreEqual(1, telemetry.kills);
            Assert.AreEqual(1, telemetry.itemsCollected);
            Assert.AreEqual(1, telemetry.itemsUsed);
            Assert.AreEqual(1, telemetry.itemsCrafted);
            Assert.AreEqual(1, telemetry.intentionalFalls);
            Assert.AreEqual(3, telemetry.oilIgnitedTiles);
            Assert.AreEqual(1, telemetry.restSitesUsed);
            Assert.AreEqual(3, telemetry.healingFromRest);
            Assert.AreEqual(1, telemetry.secretRoomsFound);
            StringAssert.Contains("Goblin 5", telemetry.FormatCompactSummary());
            StringAssert.Contains("휴식 1회/+3 HP", telemetry.FormatCompactSummary());
            StringAssert.Contains("숨은 방 1", telemetry.FormatCompactSummary());
        }

        [Test]
        public void PerFloorCounters_TrackItemsRestAndSecretsWhereTheyHappened()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs", "knight", 5, 0, DateTime.UtcNow);

            telemetry.RecordItemUsed(ItemKind.Potion, 0);
            telemetry.RecordItemUsed(ItemKind.Bomb, -4);
            telemetry.RecordItemCrafted(ItemKind.Bomb, -4);
            telemetry.RecordRest(4, -3);
            telemetry.RecordSecretRoomFound(-6);

            RunFloorTelemetry first = telemetry.floors.Find(f => f.floorIndex == 0);
            RunFloorTelemetry fifth = telemetry.floors.Find(f => f.floorIndex == -4);
            RunFloorTelemetry rest = telemetry.floors.Find(f => f.floorIndex == -3);
            RunFloorTelemetry secret = telemetry.floors.Find(f => f.floorIndex == -6);

            Assert.AreEqual(1, first.itemsUsed);
            Assert.AreEqual(1, fifth.itemsUsed);
            Assert.AreEqual(1, fifth.itemsCrafted);
            Assert.AreEqual(1, rest.restSitesUsed);
            Assert.AreEqual(4, rest.healingFromRest);
            Assert.AreEqual(1, secret.secretRoomsFound);
            // 전체 합계도 그대로 유지된다(층별은 추가 축이지 대체가 아니다).
            Assert.AreEqual(2, telemetry.itemsUsed);
            Assert.AreEqual(1, telemetry.restSitesUsed);
            Assert.AreEqual(1, telemetry.secretRoomsFound);
        }

        [Test]
        public void RefreshBands_RollsFloorsIntoDepthBands_InShallowToDeepOrder()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs", "knight", 9, 0, DateTime.UtcNow);

            // B1·B2(Shallow) · B5(Mid) · B10(Boss). B7~B9(Deep)는 방문하지 않는다.
            // 구간은 진행 지수로 묶이므로 층에 들어설 때 진행 지수를 함께 기록한다.
            telemetry.RecordTurn(0);
            telemetry.RecordDamageTaken("Goblin", 3, false, 0);
            telemetry.RecordFloorEntered(-1, 1);
            telemetry.RecordTurn(-1);
            telemetry.RecordElapsed(12f, -1);
            telemetry.RecordFloorEntered(-4, 4);
            telemetry.RecordKill(-4, boss: false);
            telemetry.RecordItemUsed(ItemKind.Potion, -4);
            telemetry.RecordFloorEntered(-9, 9);
            telemetry.RecordDamageTaken("Skeleton", 7, false, -9);
            telemetry.RecordKill(-9, boss: true);

            telemetry.RefreshBands();

            Assert.AreEqual(3, telemetry.bands.Count, "방문하지 않은 구간은 리포트에 넣지 않는다");
            Assert.AreEqual("Shallow", telemetry.bands[0].band);
            Assert.AreEqual("1~3번째", telemetry.bands[0].floorRange);
            Assert.AreEqual(2, telemetry.bands[0].floors);
            Assert.AreEqual(2, telemetry.bands[0].turns);
            Assert.AreEqual(3, telemetry.bands[0].damageTaken);
            Assert.AreEqual(12f, telemetry.bands[0].elapsedSeconds);

            Assert.AreEqual("Mid", telemetry.bands[1].band);
            Assert.AreEqual("4~6번째", telemetry.bands[1].floorRange);
            Assert.AreEqual(1, telemetry.bands[1].kills);
            Assert.AreEqual(1, telemetry.bands[1].itemsUsed);

            Assert.AreEqual("Boss", telemetry.bands[2].band);
            Assert.AreEqual("10번째+", telemetry.bands[2].floorRange);
            Assert.AreEqual(7, telemetry.bands[2].damageTaken);
            Assert.AreEqual(1, telemetry.bands[2].kills);

            StringAssert.Contains("중반 4~6번째", telemetry.FormatBandSummary());
            StringAssert.Contains("구간별:", telemetry.FormatDetailedSummary());
        }

        /// <summary>
        /// 회귀 방지: 구간 롤업은 진행 지수로 묶는다. 예전에는 floorIndex 부호로 역산해서
        /// 상승 던전(양수 floorIndex)의 모든 층이 첫 구간(Shallow)으로 뭉개졌다.
        /// </summary>
        [Test]
        public void RefreshBands_AscendingDungeon_UsesProgressNotFloorSign()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "derelict-hospital", "knight", 9, -1, DateTime.UtcNow);

            // 폐병원: B2(진행 0) → 1F(진행 2) → 5F(진행 6) → 8F(진행 9, 보스).
            telemetry.RecordTurn(-1);
            telemetry.RecordFloorEntered(1, 2);
            telemetry.RecordTurn(1);
            telemetry.RecordFloorEntered(5, 6);
            telemetry.RecordKill(5, boss: false);
            telemetry.RecordFloorEntered(8, 9);
            telemetry.RecordKill(8, boss: true);

            telemetry.RefreshBands();

            Assert.AreEqual(3, telemetry.bands.Count,
                "고도가 전부 양수여도 진행 지수대로 세 구간에 흩어져야 한다");
            Assert.AreEqual("Shallow", telemetry.bands[0].band);
            Assert.AreEqual(2, telemetry.bands[0].floors, "진행 0·2가 첫 구간");
            Assert.AreEqual("Deep", telemetry.bands[1].band);
            Assert.AreEqual("Boss", telemetry.bands[2].band);
            Assert.AreEqual(1, telemetry.bands[2].kills);
            Assert.AreEqual(9, telemetry.deepestProgressIndex);
        }

        /// <summary>층 목록은 고도가 아니라 방문 순서로 정렬된다(상승·비단조 공용).</summary>
        [Test]
        public void Floors_AreOrderedByProgress_NotByElevation()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "derelict-hospital", "knight", 9, -1, DateTime.UtcNow);

            telemetry.RecordFloorEntered(3, 1);
            telemetry.RecordFloorEntered(1, 2);   // 내려갔다 — 고도는 낮지만 나중에 방문했다
            telemetry.RecordFloorEntered(6, 3);

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3 },
                telemetry.floors.ConvertAll(f => f.progressIndex));
            CollectionAssert.AreEqual(
                new[] { -1, 3, 1, 6 },
                telemetry.floors.ConvertAll(f => f.floorIndex));
        }

        [Test]
        public void RefreshBands_IsDerived_AndIdempotent()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs", "ranger", 3, 0, DateTime.UtcNow);
            telemetry.RecordTurn(0);
            telemetry.RecordTurn(0);

            telemetry.RefreshBands();
            telemetry.RefreshBands();
            telemetry.End(RunTelemetryOutcome.Extraction, "", DateTime.UtcNow);

            Assert.AreEqual(1, telemetry.bands.Count, "여러 번 불러도 구간이 늘어나지 않는다");
            Assert.AreEqual(2, telemetry.bands[0].turns);
        }

        [Test]
        public void End_IsIdempotentAndPreservesFirstOutcome()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs",
                "knight",
                1,
                0,
                DateTime.UtcNow);

            telemetry.End(RunTelemetryOutcome.Defeat, "Burn", DateTime.UtcNow);
            telemetry.End(RunTelemetryOutcome.Victory, "", DateTime.UtcNow);
            telemetry.RecordTurn(0);

            Assert.AreEqual(RunTelemetryOutcome.Defeat, telemetry.outcome);
            Assert.AreEqual("Burn", telemetry.endCause);
            Assert.AreEqual(0, telemetry.totalTurns);
            Assert.IsTrue(telemetry.Ended);
        }

        [Test]
        public void RunSaveJson_RoundTripsNestedTelemetry()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs",
                "alchemist",
                77,
                -2,
                DateTime.UtcNow);
            telemetry.RecordTurn(-2);
            telemetry.RecordItemCollected(ItemKind.FrostShard, -2);
            var save = new RunSaveData
            {
                currentFloorIndex = -2,
                usedRestFloorIndices = new System.Collections.Generic.List<int> { -3, -6 },
                telemetry = telemetry
            };

            string json = JsonUtility.ToJson(save);
            RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

            Assert.IsNotNull(restored.telemetry);
            Assert.AreEqual(1, restored.telemetry.totalTurns);
            Assert.AreEqual(-2, restored.telemetry.currentFloorIndex);
            Assert.AreEqual(ItemKind.FrostShard.ToString(), restored.telemetry.items[0].itemId);
            CollectionAssert.AreEqual(new[] { -3, -6 }, restored.usedRestFloorIndices);
        }
    }
}
