using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonPropPlacementRulesTests
    {
        [Test]
        public void TrySelectSafePosition_SkipsEntryAdjacentReservedAndRouteTiles()
        {
            var map = new GridMap();
            var entry = new GridPos(0, 0);
            var adjacent = new GridPos(1, 0);
            var routeTile = new GridPos(2, 0);
            var reserved = new GridPos(3, 0);
            var safe = new GridPos(4, 0);
            map.Set(entry, TileKind.Floor);
            map.Set(adjacent, TileKind.Floor);
            map.Set(routeTile, TileKind.StairsUp);
            map.Set(reserved, TileKind.Floor);
            map.Set(safe, TileKind.Floor);

            bool found = DungeonPropPlacementRules.TrySelectSafePosition(
                map,
                entry,
                new[] { entry, adjacent, routeTile, reserved, safe },
                new HashSet<GridPos> { reserved },
                out GridPos selected);

            Assert.IsTrue(found);
            Assert.AreEqual(safe, selected);
        }

        [Test]
        public void TrySelectSafePosition_ReturnsFalseInsteadOfUsingEntryAsFallback()
        {
            var map = new GridMap();
            var entry = new GridPos(0, 0);
            map.Set(entry, TileKind.Floor);
            map.Set(new GridPos(1, 0), TileKind.Floor);

            bool found = DungeonPropPlacementRules.TrySelectSafePosition(
                map,
                entry,
                map.All().Select(pair => pair.Key),
                null,
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TrySelectSafePosition_SkipsSameScreenColumnAtAnyQuarterTurn()
        {
            var map = new GridMap();
            var entry = new GridPos(1, 1);
            var centeredBehind = new GridPos(0, 0);
            var safe = new GridPos(3, 1);
            map.Set(entry, TileKind.Floor);
            map.Set(new GridPos(1, 0), TileKind.Floor);
            map.Set(centeredBehind, TileKind.Floor);
            map.Set(new GridPos(2, 1), TileKind.Floor);
            map.Set(safe, TileKind.Floor);

            bool found = DungeonPropPlacementRules.TrySelectSafePosition(
                map,
                entry,
                new[] { centeredBehind, safe },
                null,
                out GridPos selected);

            Assert.IsTrue(found);
            Assert.AreEqual(safe, selected);
        }

        [Test]
        public void GeneratedStartingFloors_AlwaysOfferSeparatedSafePropPosition(
            [Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(
                map,
                11,
                11,
                3,
                4,
                seed,
                DungeonProgressDirection.Ascend);
            DungeonFloorInfo start = dungeon.Floors[0];
            var reserved = new HashSet<GridPos>(start.EnemySpawns);
            foreach (ItemSpawn item in start.Items)
                reserved.Add(item.Position);
            if (start.UpStairs.HasValue) reserved.Add(start.UpStairs.Value);
            if (start.DownStairs.HasValue) reserved.Add(start.DownStairs.Value);
            if (start.RestSite.HasValue) reserved.Add(start.RestSite.Value);
            if (start.ExtractionPoint.HasValue) reserved.Add(start.ExtractionPoint.Value);
            if (start.RescueNpc.HasValue) reserved.Add(start.RescueNpc.Value);
            if (start.Landmark.HasValue) reserved.Add(start.Landmark.Value);

            List<GridPos> candidates = map.All()
                .Where(pair =>
                    dungeon.Height.FloorIndex(pair.Key.elevation) == start.FloorIndex)
                .Select(pair => pair.Key)
                .OrderBy(pos => pos.ManhattanTo(start.Entry))
                .ThenBy(pos => pos.x)
                .ThenBy(pos => pos.y)
                .ThenBy(pos => pos.elevation)
                .ToList();

            Assert.IsTrue(
                DungeonPropPlacementRules.TrySelectSafePosition(
                    map,
                    start.Entry,
                    candidates,
                    reserved,
                    out GridPos selected),
                $"seed {seed}: 시작층에 안전한 폭발통 좌표가 없습니다.");
            Assert.AreNotEqual(start.Entry, selected);
            Assert.GreaterOrEqual(
                selected.ManhattanTo(start.Entry),
                DungeonPropPlacementRules.MinimumEntryDistance);
            Assert.AreNotEqual(
                System.Math.Abs(selected.x - start.Entry.x),
                System.Math.Abs(selected.y - start.Entry.y),
                "폭발통이 입구와 같은 화면 세로축에 놓였습니다.");
            Assert.IsFalse(reserved.Contains(selected));
            Assert.AreEqual(TileKind.Floor, map.Get(selected).kind);
        }
    }
}
