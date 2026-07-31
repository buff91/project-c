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
                "forgotten-catacombs", 5, 0, DateTime.UtcNow);

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
                "forgotten-catacombs", 9, 0, DateTime.UtcNow);

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
                "forgotten-catacombs", 9, -1, DateTime.UtcNow);

            // 아케이드 타워: B2(진행 0) → 1F(진행 2) → 5F(진행 6) → 8F(진행 9, 보스).
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

        [Test]
        public void FloorLabels_UseDungeonDirection_ForAscendingAndInwardRuns()
        {
            RunTelemetry tower = RunTelemetry.Begin(
                DungeonCatalog.DefaultId, 9, 0, DateTime.UtcNow);
            tower.RecordFloorEntered(9, 9);

            Assert.AreEqual("B2", tower.floors[0].floorLabel);
            Assert.AreEqual("8F", tower.floors[1].floorLabel);
            Assert.AreEqual("8F", tower.currentFloorLabel);
            Assert.AreEqual("8F", tower.deepestFloorLabel);
            StringAssert.Contains("8F (최고 도달 8F)", tower.FormatCompactSummary());
            StringAssert.Contains("- B2 ", tower.FormatDetailedSummary());

            RunTelemetry vault = RunTelemetry.Begin(
                "flooded-vault", 9, 0, DateTime.UtcNow);
            vault.RecordFloorEntered(-9, 9);

            Assert.AreEqual("1구역", vault.floors[0].floorLabel);
            Assert.AreEqual("10구역", vault.floors[1].floorLabel);
            StringAssert.Contains("10구역 (최고 도달 10구역)", vault.FormatCompactSummary());
            StringAssert.Contains("- 1구역 ", vault.FormatDetailedSummary());
        }

        [Test]
        public void LegacyMissingFloorLabels_UseCatalogOrNeutralSectionFallback()
        {
            var legacy = new RunTelemetry
            {
                dungeonId = DungeonCatalog.DefaultId,
                currentFloorIndex = 9,
                deepestFloorIndex = 9,
                currentProgressIndex = 9,
                deepestProgressIndex = 9
            };
            legacy.floors.Add(new RunFloorTelemetry
            {
                floorIndex = 9,
                progressIndex = 9,
                visits = 1
            });

            StringAssert.Contains("8F (최고 도달 8F)", legacy.FormatCompactSummary(),
                "구 리포트도 현재 상승 던전 규칙으로 읽혀야 한다");

            legacy.dungeonId = "removed-dungeon";
            StringAssert.Contains("10구역 (최고 도달 10구역)", legacy.FormatCompactSummary(),
                "불명 ID를 기본 던전으로 오인해 B/F 라벨을 붙이면 안 된다");
        }

        [Test]
        public void FreezeFloorLabels_BackfillsLegacyReportOnceAndKeepsThatInterpretation()
        {
            var legacy = new RunTelemetry
            {
                schemaVersion = 5,
                dungeonId = DungeonCatalog.DefaultId,
                currentFloorIndex = 9,
                deepestFloorIndex = 9,
                currentProgressIndex = 9,
                deepestProgressIndex = 9
            };
            legacy.floors.Add(new RunFloorTelemetry
            {
                floorIndex = 0,
                progressIndex = 0,
                visits = 1
            });
            legacy.floors.Add(new RunFloorTelemetry
            {
                floorIndex = 9,
                progressIndex = 9,
                visits = 1
            });

            Assert.IsTrue(legacy.FreezeFloorLabels());
            Assert.AreEqual(RunTelemetry.CurrentSchemaVersion, legacy.schemaVersion);
            Assert.AreEqual("B2", legacy.floors[0].floorLabel);
            Assert.AreEqual("8F", legacy.floors[1].floorLabel);
            Assert.AreEqual("8F", legacy.currentFloorLabel);
            Assert.AreEqual("8F", legacy.deepestFloorLabel);

            // 최초 백필 뒤에는 카탈로그 해석을 바꿔도 저장 문자열이 우선한다.
            legacy.dungeonId = "flooded-vault";
            Assert.IsFalse(legacy.FreezeFloorLabels());
            StringAssert.Contains("8F (최고 도달 8F)", legacy.FormatCompactSummary());
        }

        [Test]
        public void FreezeFloorLabels_RepairsEarlyV6DataThatWasStampedWithoutLabels()
        {
            var incompleteV6 = new RunTelemetry
            {
                schemaVersion = RunTelemetry.CurrentSchemaVersion,
                dungeonId = "removed-dungeon",
                currentFloorIndex = -3,
                deepestFloorIndex = -3,
                currentProgressIndex = 3,
                deepestProgressIndex = 3
            };
            incompleteV6.floors.Add(new RunFloorTelemetry
            {
                floorIndex = -3,
                progressIndex = 3,
                visits = 1
            });

            Assert.IsTrue(incompleteV6.FreezeFloorLabels());
            Assert.AreEqual("4구역", incompleteV6.floors[0].floorLabel);
            Assert.AreEqual("4구역", incompleteV6.currentFloorLabel);
            Assert.AreEqual("4구역", incompleteV6.deepestFloorLabel);
        }

        [Test]
        public void FreezeFloorLabels_V1ToV4_ReconstructsProgressFromLegacyFloorIndex()
        {
            var legacyV4 = new RunTelemetry
            {
                schemaVersion = 4,
                dungeonId = DungeonCatalog.DefaultId,
                currentFloorIndex = -4,
                deepestFloorIndex = -9
            };
            legacyV4.floors.Add(new RunFloorTelemetry { floorIndex = 0, visits = 1 });
            legacyV4.floors.Add(new RunFloorTelemetry { floorIndex = -4, visits = 1 });
            legacyV4.floors.Add(new RunFloorTelemetry { floorIndex = -9, visits = 1 });

            Assert.IsTrue(legacyV4.FreezeFloorLabels());

            CollectionAssert.AreEqual(
                new[] { 0, 4, 9 },
                legacyV4.floors.ConvertAll(floor => floor.progressIndex));
            CollectionAssert.AreEqual(
                new[] { "B2", "3F", "8F" },
                legacyV4.floors.ConvertAll(floor => floor.floorLabel));
            Assert.AreEqual(4, legacyV4.currentProgressIndex);
            Assert.AreEqual(9, legacyV4.deepestProgressIndex);
            Assert.AreEqual("3F", legacyV4.currentFloorLabel);
            Assert.AreEqual("8F", legacyV4.deepestFloorLabel);
        }

        [Test]
        public void FreezeFloorLabels_DoesNotRewriteFutureSchema()
        {
            var future = new RunTelemetry
            {
                schemaVersion = RunTelemetry.CurrentSchemaVersion + 1,
                dungeonId = DungeonCatalog.DefaultId,
                currentFloorIndex = 9,
                deepestFloorIndex = 9,
                currentProgressIndex = 9,
                deepestProgressIndex = 9
            };
            future.floors.Add(new RunFloorTelemetry
            {
                floorIndex = 9,
                progressIndex = 9
            });

            Assert.IsFalse(future.FreezeFloorLabels());
            Assert.AreEqual(RunTelemetry.CurrentSchemaVersion + 1, future.schemaVersion);
            Assert.IsNull(future.currentFloorLabel);
            Assert.IsNull(future.floors[0].floorLabel);
        }

        /// <summary>층 목록은 고도가 아니라 방문 순서로 정렬된다(상승·비단조 공용).</summary>
        [Test]
        public void Floors_AreOrderedByProgress_NotByElevation()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                "forgotten-catacombs", 9, -1, DateTime.UtcNow);

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
                "forgotten-catacombs", 3, 0, DateTime.UtcNow);
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

        [Test]
        public void RunSaveJson_RoundTripsFrozenFloorLabels()
        {
            RunTelemetry telemetry = RunTelemetry.Begin(
                DungeonCatalog.DefaultId,
                77,
                0,
                DateTime.UtcNow);
            telemetry.RecordFloorEntered(9, 9);
            var save = new RunSaveData { telemetry = telemetry };

            string json = JsonUtility.ToJson(save);
            RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

            Assert.AreEqual(RunTelemetry.CurrentSchemaVersion, restored.telemetry.schemaVersion);
            Assert.AreEqual("B2", restored.telemetry.floors[0].floorLabel);
            Assert.AreEqual("8F", restored.telemetry.floors[1].floorLabel);
            Assert.AreEqual("8F", restored.telemetry.currentFloorLabel);
            Assert.AreEqual("8F", restored.telemetry.deepestFloorLabel);

            // 카탈로그 해석이 달라져도 저장된 당시 라벨이 우선한다.
            restored.telemetry.dungeonId = "flooded-vault";
            StringAssert.Contains("8F (최고 도달 8F)",
                restored.telemetry.FormatCompactSummary());
        }

        [Test]
        public void RunSaveJson_RoundTripsRangedChargeRhythm()
        {
            var save = new RunSaveData
            {
                rangedCharges = new RangedChargeState(1) { turnsSinceGain = 3 }
            };

            string json = JsonUtility.ToJson(save);
            RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

            Assert.IsNotNull(restored.rangedCharges);
            Assert.AreEqual(1, restored.rangedCharges.charges);
            Assert.AreEqual(3, restored.rangedCharges.turnsSinceGain);
        }

        [Test]
        public void LegacyRunSaveJson_WithoutRangedCharges_MigratesToRestorePolicy()
        {
            const string json =
                "{\"schemaVersion\":1,\"dungeonId\":\"forgotten-catacombs\"}";
            RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

            Assert.IsNotNull(
                restored.rangedCharges,
                "JsonUtility는 누락된 중첩 필드도 빈 객체로 만들므로 마이그레이션이 필요하다");
            Assert.IsTrue(SaveMigration.Migrate(
                restored,
                ItemCatalog.ChargesPerItem,
                SaveMigration.HasSerializedRangedCharges(json)));
            Assert.IsNull(
                restored.rangedCharges,
                "v2 변환 뒤에는 Restore의 만충 호환 경로를 타야 한다");
        }
    }
}
