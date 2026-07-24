using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class SmithyRulesTests
    {
        private static HeroArchetype Knight => HeroRoster.ById("knight");

        [Test]
        public void DefaultMeta_LeavesHeroStatsUnchanged()
        {
            var meta = new MetaSaveData();
            HeroArchetype hero = Knight;

            Assert.AreEqual(hero.Attack, SmithyRules.EffectiveAttack(hero, meta));
            Assert.AreEqual(hero.MaxHp, SmithyRules.EffectiveMaxHp(hero, meta));
            Assert.AreEqual(hero.RangedDamage, SmithyRules.EffectiveRangedDamage(hero, meta));
        }

        [Test]
        public void CostForTier_EscalatesAndClampsToRange()
        {
            SmithyUpgrade weapon = SmithyRules.ById("weapon");
            Assert.AreEqual(60, weapon.CostForTier(1));
            Assert.AreEqual(130, weapon.CostForTier(2));
            Assert.AreEqual(0, weapon.CostForTier(0), "범위를 벗어난 티어는 0.");
            Assert.AreEqual(0, weapon.CostForTier(weapon.MaxTier + 1));
        }

        [Test]
        public void TryPurchase_InsufficientGold_DoesNotChangeState()
        {
            var meta = new MetaSaveData { gold = 10 };

            SmithyPurchaseResult result = SmithyRules.TryPurchase(meta, "weapon");

            Assert.AreEqual(SmithyPurchaseResult.InsufficientGold, result);
            Assert.AreEqual(10, meta.gold);
            Assert.AreEqual(0, meta.GetSmithyTier("weapon"));
        }

        [Test]
        public void TryPurchase_SpendsGoldAndRaisesTierAndStat()
        {
            var meta = new MetaSaveData { gold = 200 };
            HeroArchetype hero = Knight;

            Assert.AreEqual(SmithyPurchaseResult.Success, SmithyRules.TryPurchase(meta, "weapon"));
            Assert.AreEqual(1, meta.GetSmithyTier("weapon"));
            Assert.AreEqual(200 - 60, meta.gold);
            Assert.AreEqual(hero.Attack + 1, SmithyRules.EffectiveAttack(hero, meta));

            Assert.AreEqual(SmithyPurchaseResult.Success, SmithyRules.TryPurchase(meta, "weapon"));
            Assert.AreEqual(2, meta.GetSmithyTier("weapon"));
            Assert.AreEqual(200 - 60 - 130, meta.gold);
            Assert.AreEqual(hero.Attack + 2, SmithyRules.EffectiveAttack(hero, meta));
        }

        [Test]
        public void TryPurchase_AtMaxTier_ReturnsMaxedOut()
        {
            var meta = new MetaSaveData { gold = 10000 };
            SmithyUpgrade weapon = SmithyRules.ById("weapon");
            for (int i = 0; i < weapon.MaxTier; i++)
                Assert.AreEqual(SmithyPurchaseResult.Success, SmithyRules.TryPurchase(meta, "weapon"));

            Assert.IsTrue(SmithyRules.IsMaxed(meta, "weapon"));
            Assert.AreEqual(0, SmithyRules.NextTierCost(meta, "weapon"));
            Assert.AreEqual(SmithyPurchaseResult.MaxedOut, SmithyRules.TryPurchase(meta, "weapon"));
        }

        [Test]
        public void TryPurchase_UnknownUpgrade_IsRejected()
        {
            var meta = new MetaSaveData { gold = 10000 };
            Assert.AreEqual(SmithyPurchaseResult.UnknownUpgrade, SmithyRules.TryPurchase(meta, "nonsense"));
            Assert.AreEqual(10000, meta.gold);
        }

        [Test]
        public void ArmorAndToolTracks_StackOntoTheirOwnStats()
        {
            var meta = new MetaSaveData { gold = 10000 };
            HeroArchetype hero = Knight;
            SmithyRules.TryPurchase(meta, "armor");
            SmithyRules.TryPurchase(meta, "tools");

            Assert.AreEqual(hero.MaxHp + 2, SmithyRules.EffectiveMaxHp(hero, meta));
            Assert.AreEqual(hero.RangedDamage + 1, SmithyRules.EffectiveRangedDamage(hero, meta));
            Assert.AreEqual(hero.Attack, SmithyRules.EffectiveAttack(hero, meta), "무기 티어 0 이면 근접은 그대로.");
        }
    }
}
