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
        public void RecordFloor_KeepsDeepestFloor()
        {
            var summary = new RunSummary(0);
            summary.RecordFloor(-2);
            summary.RecordFloor(-1);

            Assert.AreEqual(-2, summary.DeepestFloorIndex);
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

        [TestCase("Goblin B2-1", "고블린")]
        [TestCase("Skeleton B3-2", "해골")]
        [TestCase("Slime B1-1", "슬라임")]
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
