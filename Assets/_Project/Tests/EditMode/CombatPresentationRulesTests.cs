using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class CombatPresentationRulesTests
    {
        [TestCase("Melee", CombatImpactKind.Physical)]
        [TestCase("Goblin B1-1", CombatImpactKind.Physical)]
        [TestCase("Bomb", CombatImpactKind.Fire)]
        [TestCase("Burn", CombatImpactKind.Fire)]
        [TestCase("FrostBomb", CombatImpactKind.Frost)]
        [TestCase("Freeze", CombatImpactKind.Frost)]
        [TestCase("Fall", CombatImpactKind.Heavy)]
        [TestCase("Crush", CombatImpactKind.Heavy)]
        public void ImpactForSource_SeparatesReadableCombatFlavors(
            string source,
            CombatImpactKind expected)
        {
            Assert.AreEqual(expected, CombatPresentationRules.ImpactForSource(source));
        }

        [Test]
        public void ImpactProfiles_HeavyReadsStrongerThanPhysical()
        {
            Assert.Greater(
                CombatPresentationRules.ShakeStrength(CombatImpactKind.Heavy),
                CombatPresentationRules.ShakeStrength(CombatImpactKind.Physical));
            Assert.Greater(
                CombatPresentationRules.BurstRayCount(CombatImpactKind.Heavy),
                CombatPresentationRules.BurstRayCount(CombatImpactKind.Physical));
        }

        [Test]
        public void StatusApply_ReturnsAppliedRefreshedAndCancelledForPresentation()
        {
            var statuses = new StatusEffects();

            Assert.AreEqual(
                StatusApplyResult.Applied,
                statuses.Apply(StatusKind.Burn, 2));
            Assert.AreEqual(
                StatusApplyResult.Refreshed,
                statuses.Apply(StatusKind.Burn, 3));
            Assert.AreEqual(
                StatusApplyResult.CancelledOpposite,
                statuses.Apply(StatusKind.Freeze, 2));
            Assert.IsFalse(statuses.Has(StatusKind.Burn));
            Assert.IsFalse(statuses.Has(StatusKind.Freeze));
        }

        [Test]
        public void StatusCue_ExplainsRefreshAndElementCancellation()
        {
            Assert.AreEqual(
                "BURN +",
                CombatPresentationRules.StatusCue(
                    StatusKind.Burn,
                    StatusApplyResult.Refreshed));
            Assert.AreEqual(
                "QUENCHED",
                CombatPresentationRules.StatusCue(
                    StatusKind.Freeze,
                    StatusApplyResult.CancelledOpposite));
            Assert.AreEqual(
                "POISON",
                CombatPresentationRules.StatusCue(
                    StatusKind.Poison,
                    StatusApplyResult.Applied));
        }
    }
}
