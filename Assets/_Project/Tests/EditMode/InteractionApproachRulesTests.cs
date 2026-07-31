using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class InteractionApproachRulesTests
    {
        [Test]
        public void FindPathToAdjacent_DetoursAroundOccupiedShortestRoute()
        {
            GridMap map = FilledFloor(0, 3, 0, 2);
            var start = new GridPos(0, 1);
            var target = new GridPos(4, 1);
            var occupied = new GridPos(1, 1);

            List<GridPos> path = InteractionApproachRules.FindPathToAdjacent(
                map,
                start,
                target,
                pos => pos == occupied);

            Assert.AreEqual(target.West, path[path.Count - 1]);
            Assert.IsTrue(
                InteractionApproachRules.IsAdjacent(path[path.Count - 1], target),
                "접근 경로의 끝은 대상 칸이 아니라 대상과 같은 높이의 인접 칸이어야 한다");
            Assert.AreNotEqual(target, path[path.Count - 1]);
            CollectionAssert.DoesNotContain(path, occupied);
            Assert.AreEqual(6, path.Count, "막힌 직선 대신 위나 아래의 한 칸 긴 우회로");
        }

        [Test]
        public void FindPathToAdjacent_AllAdjacentCellsOccupied_ReturnsEmpty()
        {
            GridMap map = FilledFloor(0, 4, 0, 4);
            var start = new GridPos(0, 0);
            var target = new GridPos(2, 2);
            map.Set(target, TileKind.Wall);
            var occupied = new HashSet<GridPos>
            {
                target.North,
                target.East,
                target.South,
                target.West
            };

            List<GridPos> path = InteractionApproachRules.FindPathToAdjacent(
                map,
                start,
                target,
                occupied.Contains);

            Assert.IsEmpty(path);
        }

        [Test]
        public void FindPathToAdjacent_AlreadyAdjacent_ReturnsStartOnly()
        {
            GridMap map = FilledFloor(0, 2, 0, 2);
            var target = new GridPos(1, 1);
            GridPos start = target.North;
            map.Set(target, TileKind.Wall);

            List<GridPos> path = InteractionApproachRules.FindPathToAdjacent(
                map,
                start,
                target);

            CollectionAssert.AreEqual(new[] { start }, path);
        }

        [Test]
        public void FindPathToAdjacent_EqualLengthCandidates_UsesStableCardinalOrder()
        {
            GridMap map = FilledFloor(0, 4, 0, 4);
            var start = new GridPos(0, 0);
            var target = new GridPos(2, 2);
            map.Set(target, TileKind.Wall);

            List<GridPos> first = InteractionApproachRules.FindPathToAdjacent(
                map,
                start,
                target);
            List<GridPos> second = InteractionApproachRules.FindPathToAdjacent(
                map,
                start,
                target);

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(
                target.South,
                first[first.Count - 1],
                "남쪽과 서쪽이 동률이면 북→동→남→서 후보 순서에서 남쪽을 먼저 고른다");
        }

        [Test]
        public void IsAdjacent_RequiresCardinalNeighborOnSameElevation()
        {
            var target = new GridPos(2, 2, 4);

            Assert.IsTrue(InteractionApproachRules.IsAdjacent(target.North, target));
            Assert.IsTrue(InteractionApproachRules.IsAdjacent(target.West, target));
            Assert.IsFalse(InteractionApproachRules.IsAdjacent(target, target));
            Assert.IsFalse(InteractionApproachRules.IsAdjacent(
                new GridPos(2, 2, 5), target));
            Assert.IsFalse(InteractionApproachRules.IsAdjacent(
                new GridPos(3, 3, 4), target));
        }

        private static GridMap FilledFloor(
            int minX,
            int maxX,
            int minY,
            int maxY)
        {
            var map = new GridMap();
            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                map.Set(new GridPos(x, y), TileKind.Floor);
            return map;
        }
    }
}
