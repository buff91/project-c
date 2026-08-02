using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class FloorFoundationPresentationTests
    {
        [TestCase(1, 0, 0, 1, 2, 3)]
        [TestCase(0, 1, -1, 0, 3, 2)]
        [TestCase(-1, 0, 0, -1, 2, 3)]
        [TestCase(0, -1, 1, 0, 3, 2)]
        public void Collect_Rectangle_EmitsOnlyTwoCurrentFrontChains(
            int frontAx,
            int frontAy,
            int frontBx,
            int frontBy,
            int expectedRightFaces,
            int expectedLeftFaces)
        {
            HashSet<GridPos> room = Rectangle(width: 3, height: 2);
            var frontA = new Vector2Int(frontAx, frontAy);
            var frontB = new Vector2Int(frontBx, frontBy);

            FoundationCell[] cells = FloorFoundationPresentation.Collect(
                room,
                _ => true,
                room.Contains,
                frontA,
                frontB);

            Assert.AreEqual(expectedRightFaces, Count(cells, FoundationFaces.ScreenRight));
            Assert.AreEqual(expectedLeftFaces, Count(cells, FoundationFaces.ScreenLeft));
            Assert.AreEqual(1, cells.Count(cell => cell.Faces == FoundationFaces.Both));
            Assert.AreEqual(expectedRightFaces + expectedLeftFaces - 1, cells.Length);

            foreach (FoundationCell cell in cells)
            {
                if ((cell.Faces & FoundationFaces.ScreenRight) != 0)
                    Assert.IsFalse(room.Contains(Offset(cell.Position, frontA)));
                if ((cell.Faces & FoundationFaces.ScreenLeft) != 0)
                    Assert.IsFalse(room.Contains(Offset(cell.Position, frontB)));
            }
        }

        [Test]
        public void Collect_PlanarNeighborOutsideCandidates_SuppressesOpening()
        {
            var source = new GridPos(0, 0, -2);
            var planar = new HashSet<GridPos>
            {
                source,
                source.East
            };

            FoundationCell[] cells = FloorFoundationPresentation.Collect(
                new[] { source },
                _ => true,
                planar.Contains,
                Vector2Int.right,
                Vector2Int.up);

            Assert.AreEqual(1, cells.Length);
            Assert.AreEqual(FoundationFaces.ScreenLeft, cells[0].Faces);
        }

        [Test]
        public void Collect_HiddenSourceCell_DoesNotEmit()
        {
            HashSet<GridPos> room = Rectangle(width: 2, height: 1);
            GridPos hidden = new GridPos(1, 0);

            FoundationCell[] cells = FloorFoundationPresentation.Collect(
                room,
                position => position != hidden,
                room.Contains,
                Vector2Int.right,
                Vector2Int.up);

            Assert.IsFalse(cells.Any(cell => cell.Position == hidden));
            Assert.AreEqual(1, cells.Length);
            Assert.AreEqual(FoundationFaces.ScreenLeft, cells[0].Faces);
        }

        [Test]
        public void Collect_RibPhase_DependsOnlyOnWorldPosition()
        {
            var source = new GridPos(-7, 11, -2);
            var planar = new HashSet<GridPos> { source };

            FoundationCell q0 = FloorFoundationPresentation.Collect(
                planar,
                _ => true,
                planar.Contains,
                Vector2Int.right,
                Vector2Int.up).Single();
            FoundationCell q2 = FloorFoundationPresentation.Collect(
                planar,
                _ => true,
                planar.Contains,
                Vector2Int.left,
                Vector2Int.down).Single();

            Assert.AreEqual(q0.Position, q2.Position);
            Assert.AreEqual(q0.RibPhase, q2.RibPhase);
        }

        [Test]
        public void CollectSupports_UsesWorldCorners_AndRejectsHiddenOrInvalidCells()
        {
            HashSet<GridPos> room = Rectangle(width: 3, height: 2);
            GridPos hidden = new GridPos(2, 1);
            GridPos invalid = new GridPos(0, 0);

            FoundationSupport[] supports = FloorFoundationPresentation.CollectSupports(
                room,
                position => position != hidden,
                position => position != invalid,
                room.Contains);

            Assert.AreEqual(2, supports.Length);
            Assert.IsTrue(supports.Any(support =>
                support.Position == new GridPos(0, 1) &&
                support.Corner == FoundationCorner.NorthWest));
            Assert.IsTrue(supports.Any(support =>
                support.Position == new GridPos(2, 0) &&
                support.Corner == FoundationCorner.SouthEast));
        }

        private static int Count(IEnumerable<FoundationCell> cells, FoundationFaces face) =>
            cells.Count(cell => (cell.Faces & face) != 0);

        private static GridPos Offset(GridPos position, Vector2Int direction) =>
            position.Offset(direction.x, direction.y);

        private static HashSet<GridPos> Rectangle(int width, int height)
        {
            var result = new HashSet<GridPos>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result.Add(new GridPos(x, y));
            return result;
        }
    }
}
