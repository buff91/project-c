using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonLootRulesTests
    {
        [Test]
        public void FacilityPool_PreservesLegacyTwentyThreeSlotDistribution()
        {
            ItemKind[] expected =
            {
                ItemKind.Potion, ItemKind.Potion, ItemKind.Potion,
                ItemKind.Bomb, ItemKind.Bomb, ItemKind.Bomb,
                ItemKind.FrostBomb,
                ItemKind.OilFlask,
                ItemKind.ThrowingKnife,
                ItemKind.RecallScroll,
                ItemKind.CannedFood, ItemKind.CannedFood, ItemKind.CannedFood,
                ItemKind.CannedFood, ItemKind.CannedFood,
                ItemKind.CoinPouch, ItemKind.CoinPouch,
                ItemKind.Gemstone,
                ItemKind.Relic,
                ItemKind.Herb, ItemKind.Herb,
                ItemKind.BlastPowder,
                ItemKind.FrostShard
            };

            ItemKind[] actual = Enumerable.Range(0, DungeonLootRules.RollCount)
                .Select(roll => DungeonLootRules.Resolve(
                    DungeonRegionProfile.Facility, progressIndex: 2, roll: roll))
                .ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void FloodedPool_FavorsColdToolsAndExcludesOil()
        {
            ItemKind[] flooded = Enumerable.Range(0, DungeonLootRules.RollCount)
                .Select(roll => DungeonLootRules.Resolve(
                    DungeonRegionProfile.Flooded, progressIndex: 2, roll: roll))
                .ToArray();
            ItemKind[] facility = Enumerable.Range(0, DungeonLootRules.RollCount)
                .Select(roll => DungeonLootRules.Resolve(
                    DungeonRegionProfile.Facility, progressIndex: 2, roll: roll))
                .ToArray();

            Assert.IsFalse(flooded.Contains(ItemKind.OilFlask));
            Assert.Greater(
                flooded.Count(IsColdReward),
                facility.Count(IsColdReward));
        }

        [Test]
        public void RelicSlot_IsDowngradedOnOpeningFloors()
        {
            Assert.AreEqual(
                ItemKind.CoinPouch,
                DungeonLootRules.Resolve(DungeonRegionProfile.Flooded, 1, 18));
            Assert.AreEqual(
                ItemKind.Relic,
                DungeonLootRules.Resolve(DungeonRegionProfile.Flooded, 2, 18));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                DungeonLootRules.Resolve(DungeonRegionProfile.Flooded, 0, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                DungeonLootRules.Resolve(
                    DungeonRegionProfile.Flooded, 0, DungeonLootRules.RollCount));
        }

        private static bool IsColdReward(ItemKind kind) =>
            kind == ItemKind.FrostBomb || kind == ItemKind.FrostShard;
    }
}
