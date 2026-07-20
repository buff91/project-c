using System;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// seed 변형 던전 생성의 불변식 검증.
    /// 알고리즘은 어떤 난수 결과에서도 아래 성질을 지켜야 한다:
    /// 재현성, 전 타일 도달 가능성, 문 게이트, 구멍의 한 층 착지, 샤프트 규칙.
    /// </summary>
    public class ProceduralDungeonTests
    {
        [Test]
        public void SameSeed_ReproducesIdenticalDungeon()
        {
            Assert.AreEqual(Dump(11, 13, 3, seed: 23), Dump(11, 13, 3, seed: 23));
        }

        [Test]
        public void DifferentSeeds_VaryTheLayout()
        {
            int distinct = Enumerable.Range(1, 8)
                .Select(seed => Dump(11, 11, 3, seed))
                .Distinct()
                .Count();

            Assert.Greater(distinct, 1, "seed가 달라도 던전이 변하지 않습니다.");
        }

        [Test]
        public void AnySeed_EveryWalkableTileIsReachableFromEntry([Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            foreach (var pair in map.All().Where(pair => pair.Value.IsWalkable).ToList())
            {
                Assert.Greater(
                    GridPathfinder.FindPath(map, dungeon.Entry, pair.Key).Count,
                    0,
                    $"seed {seed}: {pair.Key} ({pair.Value.kind}) 에 도달할 수 없습니다.");
            }
        }

        [Test]
        public void AnySeed_ClosedDoorsGateEveryNorthRoom([Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                Assert.AreEqual(
                    0,
                    GridPathfinder.FindPath(map, floor.Entry, floor.EnemySpawn).Count,
                    $"seed {seed}: {floor.FloorIndex}층 북쪽 방이 문 없이 열려 있습니다.");
            }
        }

        [Test]
        public void AnySeed_HolesDropExactlyOneFloorOntoWalkableGround([Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed);
            int bottomElevation = dungeon.Height.Elevation(dungeon.BottomFloorIndex);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (floor.FloorIndex == dungeon.BottomFloorIndex)
                {
                    Assert.IsFalse(floor.Hole.HasValue, "바닥층에는 구멍이 없어야 합니다.");
                    continue;
                }

                Assert.IsTrue(floor.Hole.HasValue, $"seed {seed}: {floor.FloorIndex}층에 구멍이 없습니다.");
                GridPos? landing = map.FindLandingBelow(floor.Hole.Value, bottomElevation);
                Assert.IsTrue(landing.HasValue, $"seed {seed}: 구멍 아래 착지점이 없습니다.");
                Assert.AreEqual(
                    floor.FloorIndex - 1,
                    dungeon.Height.FloorIndex(landing.Value.elevation),
                    $"seed {seed}: 구멍이 정확히 한 층 아래에 착지해야 합니다.");
                Assert.IsTrue(
                    map.Get(landing.Value).IsWalkable,
                    $"seed {seed}: 착지 칸 {landing.Value} ({map.Get(landing.Value).kind}) 이 걷기 불가입니다.");
            }
        }

        [Test]
        public void AnySeed_StairShaftsShareXYAndAvoidOverlap([Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed);

            for (int i = 0; i < dungeon.Floors.Count - 1; i++)
            {
                GridPos down = dungeon.Floors[i].DownStairs.Value;
                GridPos up = dungeon.Floors[i + 1].UpStairs.Value;
                Assert.AreEqual(down.x, up.x, "층간 계단은 같은 x 샤프트를 써야 합니다.");
                Assert.AreEqual(down.y, up.y, "층간 계단은 같은 y 샤프트를 써야 합니다.");
                CollectionAssert.Contains(map.LinksFrom(down), up);
            }

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (!floor.UpStairs.HasValue || !floor.DownStairs.HasValue) continue;
                Assert.AreNotEqual(
                    (floor.UpStairs.Value.x, floor.UpStairs.Value.y),
                    (floor.DownStairs.Value.x, floor.DownStairs.Value.y),
                    "중간층의 Up/Down 계단이 한 칸에 겹치면 안 됩니다.");
            }
        }

        [Test]
        public void MinimumSize_NineByNine_StillSatisfiesInvariants([Range(1, 10)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 9, 9, 2, 4, seed);

            // 문을 닫은 상태에서 북쪽 방이 게이트되는지 (동쪽 방-북쪽 방 인접 우회 방지).
            foreach (DungeonFloorInfo floor in dungeon.Floors)
                Assert.AreEqual(0, GridPathfinder.FindPath(map, floor.Entry, floor.EnemySpawn).Count);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
                Assert.Greater(GridPathfinder.FindPath(map, dungeon.Entry, floor.EnemySpawn).Count, 0);
        }

        private static string Dump(int width, int height, int floorCount, int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, width, height, floorCount, 4, seed);

            var tiles = map.All()
                .Select(pair => $"{pair.Key}:{pair.Value.kind}")
                .OrderBy(entry => entry, StringComparer.Ordinal);
            var floors = dungeon.Floors.Select(floor =>
                $"{floor.FloorIndex}|{floor.Entry}|{floor.UpStairs}|{floor.DownStairs}|" +
                $"{floor.Hole}|{floor.EnemySpawn}|{string.Join(",", floor.Doors)}");

            return string.Join(";", tiles) + "#" + string.Join(";", floors);
        }
    }
}
