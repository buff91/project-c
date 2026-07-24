using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonRestRulesTests
    {
        [Test]
        public void TenFloorDungeon_PlacesRestSitesOnlyAtB4AndB7()
        {
            int[] depths = Enumerable.Range(0, 10)
                .Where(depth => DungeonRestRules.ShouldPlace(depth, 10))
                .ToArray();

            CollectionAssert.AreEqual(new[] { 3, 6 }, depths);
        }

        [TestCase(8, 8, 0)]
        [TestCase(7, 8, 1)]
        [TestCase(5, 8, 2)]
        [TestCase(2, 8, 3)]
        [TestCase(1, 8, 4)]
        [TestCase(0, 8, 0)]
        public void HealingAmount_RecoversHalfMissingHpRoundedUp(
            int hp,
            int maxHp,
            int expected)
        {
            Assert.AreEqual(expected, DungeonRestRules.HealingAmount(hp, maxHp));
        }

        [Test]
        public void AnySeed_RestSitesAreReachableFloorPropsWithoutSpawnOverlap(
            [Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 13, 13, 10, 4, seed);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                int depth = -floor.FloorIndex;
                bool expected = depth == 3 || depth == 6;
                Assert.AreEqual(
                    expected,
                    floor.RestSite.HasValue,
                    $"seed {seed}: {floor.FloorIndex}층 휴식처 배치가 잘못됐습니다.");
                if (!floor.RestSite.HasValue) continue;

                GridPos rest = floor.RestSite.Value;
                Assert.AreEqual(TileKind.Floor, map.Get(rest).kind);
                Assert.IsFalse(map.Get(rest).wet, "휴식처가 물 웅덩이와 겹칩니다.");
                CollectionAssert.DoesNotContain(floor.EnemySpawns.ToList(), rest);
                CollectionAssert.DoesNotContain(
                    floor.Items.Select(item => item.Position).ToList(),
                    rest);
                Assert.Greater(
                    GridPathfinder.FindPath(map, dungeon.Entry, rest).Count,
                    0,
                    $"seed {seed}: 휴식처 {rest}에 도달할 수 없습니다.");
            }
        }
    }
}
