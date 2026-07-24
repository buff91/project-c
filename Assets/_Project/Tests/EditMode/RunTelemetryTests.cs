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
            telemetry.RecordFloorEntered(-1);
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
            telemetry.RecordItemUsed(ItemKind.Potion);
            telemetry.RecordItemCrafted(ItemKind.Bomb);
            telemetry.RecordFall(player: true, intentional: true, fallenFloorCount: 1);
            telemetry.RecordStatus(StatusKind.Burn);
            telemetry.RecordOilIgnition(3);
            telemetry.RecordRest(3);
            telemetry.RecordSecretRoomFound();

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
