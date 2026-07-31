using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonDressingPlacementRulesTests
    {
        [Test]
        public void SelectSafePositions_UsesSeparatedPerimeterTilesAndSkipsReserved()
        {
            var map = FilledRoom(9, 9);
            var entry = new GridPos(4, 4);
            var reservedEdge = new GridPos(0, 4);
            var first = new GridPos(8, 4);
            var tooClose = new GridPos(8, 5);
            var second = new GridPos(4, 0);

            IReadOnlyList<GridPos> selected =
                DungeonDressingPlacementRules.SelectSafePositions(
                    map,
                    entry,
                    new[]
                    {
                        entry,
                        new GridPos(5, 4),
                        reservedEdge,
                        new GridPos(6, 4), // 방 중앙이라 제외
                        first,
                        tooClose,
                        second
                    },
                    new HashSet<GridPos> { reservedEdge },
                    2);

            CollectionAssert.AreEqual(new[] { first, second }, selected);
        }

        [Test]
        public void SelectSafePositions_SkipsDisconnectedAndDifferentElevationTiles()
        {
            var map = new GridMap();
            var entry = new GridPos(0, 0);
            var reachable = new GridPos(2, 0);
            var disconnected = new GridPos(8, 8);
            var raised = new GridPos(1, 0, 1);
            map.Set(entry, TileKind.Floor);
            map.Set(new GridPos(1, 0), TileKind.Floor);
            map.Set(reachable, TileKind.Floor);
            map.Set(disconnected, TileKind.Floor);
            map.Set(raised, TileKind.Floor);

            IReadOnlyList<GridPos> selected =
                DungeonDressingPlacementRules.SelectSafePositions(
                    map,
                    entry,
                    new[] { disconnected, raised, reachable },
                    null,
                    2);

            CollectionAssert.AreEqual(new[] { reachable }, selected);
        }

        [Test]
        public void SelectSafePositions_StopsAtRequestedCountWithoutMutatingMap()
        {
            var map = FilledRoom(7, 7);
            int before = map.Count;

            IReadOnlyList<GridPos> selected =
                DungeonDressingPlacementRules.SelectSafePositions(
                    map,
                    new GridPos(3, 3),
                    new[] { new GridPos(0, 3), new GridPos(6, 3), new GridPos(3, 0) },
                    null,
                    1);

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(before, map.Count);
        }

        private static GridMap FilledRoom(int width, int height)
        {
            var map = new GridMap();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map.Set(new GridPos(x, y), TileKind.Floor);
            return map;
        }
    }
}
