using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class EnemyPresentationRulesTests
    {
        [Test]
        public void ShouldShowFeedback_VisibleEnemyOnActiveFloor_IsShown()
        {
            Assert.IsTrue(EnemyPresentationRules.ShouldShowFeedback(
                debugAll: false,
                enemyFloorIndex: 0,
                activeFloorIndex: 0,
                tileVisible: true));
        }

        [Test]
        public void ShouldShowFeedback_HiddenEnemy_IsNotShown()
        {
            Assert.IsFalse(EnemyPresentationRules.ShouldShowFeedback(
                debugAll: false,
                enemyFloorIndex: 0,
                activeFloorIndex: 0,
                tileVisible: false));
        }

        [Test]
        public void ShouldShowFeedback_EnemyOnOtherFloor_IsNotShown()
        {
            Assert.IsFalse(EnemyPresentationRules.ShouldShowFeedback(
                debugAll: false,
                enemyFloorIndex: -1,
                activeFloorIndex: 0,
                tileVisible: true));
        }

        [Test]
        public void ShouldShowFeedback_DebugAll_ShowsHiddenEnemy()
        {
            Assert.IsTrue(EnemyPresentationRules.ShouldShowFeedback(
                debugAll: true,
                enemyFloorIndex: -1,
                activeFloorIndex: 0,
                tileVisible: false));
        }

        [Test]
        public void IsCorpseExpired_RemainsBeforeLifetime_ThenExpires()
        {
            Assert.IsFalse(EnemyPresentationRules.IsCorpseExpired(
                currentTurn: 7,
                deathTurn: 5,
                lifetimeTurns: 3));
            Assert.IsTrue(EnemyPresentationRules.IsCorpseExpired(
                currentTurn: 8,
                deathTurn: 5,
                lifetimeTurns: 3));
        }

        [Test]
        public void CorpseAlpha_FadesEachTurn()
        {
            float fresh = EnemyPresentationRules.CorpseAlpha(5, 5, 3);
            float old = EnemyPresentationRules.CorpseAlpha(7, 5, 3);

            Assert.Greater(fresh, old);
            Assert.Greater(old, 0f);
        }
    }
}
