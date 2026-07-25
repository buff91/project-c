using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class TravelRulesTests
    {
        private static readonly HashSet<string> NoEnemies = new HashSet<string>();

        [Test]
        public void AllowedSteps_NoEnemyInSight_AllowsFullPath()
        {
            Assert.AreEqual(7, TravelRules.AllowedSteps(enemyInSight: false, pathSteps: 7));
        }

        [Test]
        public void AllowedSteps_EnemyInSight_AllowsSingleStep()
        {
            Assert.AreEqual(1, TravelRules.AllowedSteps(enemyInSight: true, pathSteps: 7));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AllowedSteps_EmptyPath_AllowsNothing(bool enemyInSight)
        {
            Assert.AreEqual(0, TravelRules.AllowedSteps(enemyInSight, pathSteps: 0));
        }

        [Test]
        public void Evaluate_NewVisibleEnemy_InterruptsAsEnemySighted()
        {
            TravelInterrupt result = TravelRules.Evaluate(
                NoEnemies,
                new[] { ("goblin", true, true) },
                newItemSighted: false,
                tookDamage: false);

            Assert.AreEqual(TravelInterrupt.EnemySighted, result);
        }

        [Test]
        public void Evaluate_AlreadyVisibleEnemy_DoesNotInterrupt()
        {
            var previouslyVisible = new HashSet<string> { "goblin" };

            TravelInterrupt result = TravelRules.Evaluate(
                previouslyVisible,
                new[] { ("goblin", true, true) },
                newItemSighted: false,
                tookDamage: false);

            Assert.AreEqual(TravelInterrupt.None, result);
        }

        [Test]
        public void Evaluate_DeadOrInvisibleEnemy_DoesNotInterrupt()
        {
            TravelInterrupt result = TravelRules.Evaluate(
                NoEnemies,
                new[] { ("corpse", true, false), ("lurker", false, true) },
                newItemSighted: false,
                tookDamage: false);

            Assert.AreEqual(TravelInterrupt.None, result);
        }

        [Test]
        public void Evaluate_NewItem_InterruptsAsItemSighted()
        {
            TravelInterrupt result = TravelRules.Evaluate(
                NoEnemies,
                new (string, bool, bool)[0],
                newItemSighted: true,
                tookDamage: false);

            Assert.AreEqual(TravelInterrupt.ItemSighted, result);
        }

        [Test]
        public void Evaluate_DamageOutranksEnemyAndItem()
        {
            TravelInterrupt result = TravelRules.Evaluate(
                NoEnemies,
                new[] { ("goblin", true, true) },
                newItemSighted: true,
                tookDamage: true);

            Assert.AreEqual(TravelInterrupt.PlayerDamaged, result);
        }

        [Test]
        public void Evaluate_EnemyOutranksItem()
        {
            TravelInterrupt result = TravelRules.Evaluate(
                NoEnemies,
                new[] { ("goblin", true, true) },
                newItemSighted: true,
                tookDamage: false);

            Assert.AreEqual(TravelInterrupt.EnemySighted, result);
        }
    }

    public class RunSummaryTests
    {
        [Test]
        public void RecordFloor_KeepsFurthestProgress_NotLowestElevation()
        {
            // 하강 던전: 진행이 깊어질수록 층 인덱스가 작아진다.
            var summary = new RunSummary(0);
            summary.RecordFloor(-2, 2);
            summary.RecordFloor(-1, 1); // 되돌아왔다 — 도달 층을 되돌리지 않는다

            Assert.AreEqual(-2, summary.DeepestFloorIndex);
            Assert.AreEqual(2, summary.FurthestProgressIndex);
        }

        [Test]
        public void RecordFloor_Ascending_TracksProgressNotMinimum()
        {
            // 상승 던전: 진행이 깊어질수록 층 인덱스가 **커진다**. 최솟값을 쓰면
            // 시작 층이 영원히 "도달 층"으로 남는다 — 그 결함을 고정한다.
            var summary = new RunSummary(0);
            summary.RecordFloor(1, 1);
            summary.RecordFloor(5, 5);
            summary.RecordFloor(3, 3); // 되돌아왔다

            Assert.AreEqual(5, summary.DeepestFloorIndex);
            Assert.AreEqual(5, summary.FurthestProgressIndex);
        }

        [Test]
        public void RecordFloor_NonMonotonicPath_UsesProgressOrder()
        {
            // 올라갔다 떨어지는 경로: 고도로는 순서를 알 수 없다.
            var summary = new RunSummary(0);
            summary.RecordFloor(3, 1);
            summary.RecordFloor(1, 2); // 떨어졌지만 나중에 방문했다
            summary.RecordFloor(6, 3);

            Assert.AreEqual(6, summary.DeepestFloorIndex);
            Assert.AreEqual(3, summary.FurthestProgressIndex);
        }

        [Test]
        public void EndInDefeat_KeepsFirstCause()
        {
            var summary = new RunSummary();
            summary.EndInDefeat("Goblin B2-1");
            summary.EndInDefeat("Burn");

            Assert.IsTrue(summary.Ended);
            Assert.IsFalse(summary.Victory);
            Assert.AreEqual("Goblin B2-1", summary.CauseOfDeath);
        }

        [Test]
        public void EndInVictory_AfterDefeat_DoesNotOverride()
        {
            var summary = new RunSummary();
            summary.EndInDefeat("Fall");
            summary.EndInVictory();

            Assert.IsFalse(summary.Victory);
        }

        [Test]
        public void RestoreConstructor_KeepsKillsAndDeepestFloor()
        {
            var summary = new RunSummary(startFloorIndex: -2, kills: 7);

            Assert.AreEqual(7, summary.Kills);
            Assert.AreEqual(-2, summary.DeepestFloorIndex);
            Assert.IsFalse(summary.Ended);
        }

        [Test]
        public void EndInExtraction_MarksExtracted_NotVictory_AndKeepsGold()
        {
            var summary = new RunSummary(-2, kills: 3);
            summary.EndInExtraction(85);

            Assert.IsTrue(summary.Ended);
            Assert.IsTrue(summary.Extracted);
            Assert.IsFalse(summary.Victory);
            Assert.AreEqual(85, summary.GoldBanked);

            summary.EndInDefeat("Fall"); // 이미 끝난 판은 덮어쓰지 못한다
            Assert.IsTrue(summary.Extracted);
        }

        [TestCase("Goblin B2-1", "약탈자")]
        [TestCase("Skeleton B3-2", "낡은 경비 드론")]
        [TestCase("Slime B1-1", "누출 오염 슬러지")]
        [TestCase("Burn", "화상")]
        [TestCase("Fall", "낙하")]
        [TestCase("Crush", "낙하 충돌")]
        [TestCase("Bomb", "폭발")]
        [TestCase("", "알 수 없음")]
        [TestCase("Something", "Something")]
        public void FormatCause_TranslatesKnownSources(string source, string expected)
        {
            Assert.AreEqual(expected, RunSummary.FormatCause(source));
        }
    }
}
