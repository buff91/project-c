using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class FiringPositionTests
    {
        private static GridMap Flat(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void AlreadyInPosition_ReturnsTrueWithEmptyPath()
        {
            GridMap map = Flat(9);

            bool ok = CombatRules.FindFiringPosition(
                map, new GridPos(1, 1, 0), new GridPos(3, 1, 0), 4, out List<GridPos> path);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void OutOfRange_FindsClosestFiringTile()
        {
            GridMap map = Flat(12);

            bool ok = CombatRules.FindFiringPosition(
                map, new GridPos(0, 0, 0), new GridPos(10, 0, 0), 4, out List<GridPos> path);

            Assert.IsTrue(ok);
            Assert.Greater(path.Count, 1);
            GridPos firing = path[path.Count - 1];
            Assert.IsTrue(CombatRules.CanFireFrom(map, firing, new GridPos(10, 0, 0), 4));
        }

        [Test]
        public void BlockedLineOfSight_ExcludesBlindCandidates()
        {
            GridMap map = Flat(9);
            // 타깃을 벽 고리로 감싸고 남쪽(4,3) 한 칸만 연다 — 사선은 남쪽 수직뿐.
            var target = new GridPos(4, 4, 0);
            for (int x = 3; x <= 5; x++)
            for (int y = 3; y <= 5; y++)
            {
                if ((x == 4 && y == 4) || (x == 4 && y == 3)) continue;
                map.Set(new GridPos(x, y, 0), TileKind.Wall);
            }

            bool ok = CombatRules.FindFiringPosition(
                map, new GridPos(0, 0, 0), target, 3, out List<GridPos> path);

            Assert.IsTrue(ok);
            GridPos firing = path[path.Count - 1];
            Assert.AreEqual(4, firing.x, "남쪽 개구부를 통한 수직 사선만 열려 있다");
            Assert.Less(firing.y, 4);
            Assert.IsTrue(CombatRules.HasLineOfSight(map, firing, target));
        }

        [Test]
        public void ElevationMismatch_ClimbsStairsToTargetLevel()
        {
            GridMap map = Flat(9);
            // (4,4)~(6,4)를 한 단 위 플랫폼으로, (3,4)에 계단.
            map.Set(new GridPos(3, 4, 0), TileKind.Stairs);
            for (int x = 4; x <= 6; x++)
            {
                map.Remove(new GridPos(x, 4, 0));
                map.Set(new GridPos(x, 4, 1), TileKind.Floor);
            }
            var target = new GridPos(6, 4, 1);

            bool ok = CombatRules.FindFiringPosition(
                map, new GridPos(0, 4, 0), target, 2, out List<GridPos> path);

            Assert.IsTrue(ok, "계단으로 같은 높이에 올라 사선을 잡아야 한다");
            GridPos firing = path[path.Count - 1];
            Assert.AreEqual(1, firing.elevation);
            Assert.IsTrue(CombatRules.CanFireFrom(map, firing, target, 2));
        }

        [Test]
        public void UnreachableTarget_ReturnsFalse()
        {
            GridMap map = Flat(9);
            // 타깃 주변을 전부 벽으로 밀봉 (사거리 2 다이아몬드 밖까지).
            for (int x = 2; x <= 6; x++)
            for (int y = 2; y <= 6; y++)
            {
                if (x == 4 && y == 4) continue;
                map.Set(new GridPos(x, y, 0), TileKind.Wall);
            }

            bool ok = CombatRules.FindFiringPosition(
                map, new GridPos(0, 0, 0), new GridPos(4, 4, 0), 2, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void TieBreak_IsDeterministic()
        {
            GridMap map = Flat(9);
            var shooter = new GridPos(4, 0, 0);
            var target = new GridPos(4, 8, 0);

            CombatRules.FindFiringPosition(map, shooter, target, 3, out List<GridPos> first);
            CombatRules.FindFiringPosition(map, shooter, target, 3, out List<GridPos> second);

            CollectionAssert.AreEqual(first, second);
        }

        [TestCase(0, 0, 0, 10, 0, 0, 4, RangedBlockReason.OutOfRange)]
        [TestCase(0, 0, 0, 2, 0, 1, 4, RangedBlockReason.ElevationMismatch)]
        [TestCase(0, 0, 0, 3, 0, 0, 4, RangedBlockReason.None)]
        public void DiagnoseRanged_ReportsFirstFailure(
            int sx, int sy, int se, int tx, int ty, int te, int range, RangedBlockReason expected)
        {
            GridMap map = Flat(12);
            if (te != 0) map.Set(new GridPos(tx, ty, te), TileKind.Floor);

            Assert.AreEqual(expected, CombatRules.DiagnoseRanged(
                map, new GridPos(sx, sy, se), new GridPos(tx, ty, te), range));
        }

        [Test]
        public void DiagnoseRanged_WallBetween_ReportsBlocked()
        {
            GridMap map = Flat(9);
            map.Set(new GridPos(2, 0, 0), TileKind.Wall);

            Assert.AreEqual(RangedBlockReason.Blocked, CombatRules.DiagnoseRanged(
                map, new GridPos(0, 0, 0), new GridPos(4, 0, 0), 6));
        }
    }

    public class HubLayoutTests
    {
        [Test]
        public void Build_AllTilesWalkable_SingleFloor_NoStairsOrHoles()
        {
            var map = new GridMap();
            DungeonLayout hub = HubLayout.Build(map);

            Assert.AreEqual(1, hub.Floors.Count);
            Assert.AreEqual(0, hub.Floors[0].EnemySpawns.Count);
            Assert.IsFalse(hub.Floors[0].UpStairs.HasValue);
            Assert.IsFalse(hub.Floors[0].DownStairs.HasValue);
            Assert.IsFalse(hub.Floors[0].Hole.HasValue);
            for (int x = 0; x < HubLayout.Width; x++)
            for (int y = 0; y < HubLayout.Height; y++)
                Assert.IsTrue(map.IsWalkable(new GridPos(x, y, 0)), $"({x},{y})");
        }

        [Test]
        public void PointsOfInterest_AreDistinct_AndReachableFromEntry()
        {
            var map = new GridMap();
            HubLayout.Build(map);

            var pois = new List<GridPos>
            {
                HubLayout.Portal, HubLayout.Merchant, HubLayout.Stash, HubLayout.Campfire
            };
            pois.AddRange(HubLayout.HeroPositions);

            var seen = new HashSet<GridPos> { HubLayout.Entry };
            foreach (GridPos poi in pois)
            {
                Assert.IsTrue(seen.Add(poi), $"POI 겹침: {poi}");
                Assert.Greater(
                    GridPathfinder.FindPath(map, HubLayout.Entry, poi).Count, 0,
                    $"입구에서 {poi} 도달 불가");
            }
        }
    }
}
