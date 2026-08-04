using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>Hole 셀의 개구부 묶음 계약 — Unity 없이 shim에서도 검증한다.</summary>
    public class HoleOpeningRulesTests
    {
        [Test]
        public void GroupOpenings_EmptyInput_ReturnsNoOpenings()
        {
            Assert.IsEmpty(HoleOpeningRules.GroupOpenings(new List<GridPos>()));
            Assert.IsEmpty(HoleOpeningRules.GroupOpenings(null));
        }

        [Test]
        public void GroupOpenings_AdjacentCells_FormSingleOpening()
        {
            var tiles = new List<GridPos>
            {
                new GridPos(3, 3, 2),
                new GridPos(4, 3, 2),
                new GridPos(4, 4, 2)
            };

            List<List<GridPos>> openings = HoleOpeningRules.GroupOpenings(tiles);

            Assert.AreEqual(1, openings.Count);
            CollectionAssert.AreEqual(tiles, openings[0]);
        }

        [Test]
        public void GroupOpenings_SeparatedClusters_FormDistinctOpenings()
        {
            var tiles = new List<GridPos>
            {
                new GridPos(3, 3, 2),
                new GridPos(4, 3, 2),
                new GridPos(9, 9, 2),
                new GridPos(9, 8, 2)
            };

            List<List<GridPos>> openings = HoleOpeningRules.GroupOpenings(tiles);

            Assert.AreEqual(2, openings.Count);
            CollectionAssert.AreEqual(
                new[] { new GridPos(3, 3, 2), new GridPos(4, 3, 2) }, openings[0]);
            CollectionAssert.AreEqual(
                new[] { new GridPos(9, 9, 2), new GridPos(9, 8, 2) }, openings[1]);
        }

        [Test]
        public void GroupOpenings_DiagonalOrDifferentElevation_DoesNotConnect()
        {
            var tiles = new List<GridPos>
            {
                new GridPos(3, 3, 2),
                new GridPos(4, 4, 2),
                new GridPos(3, 4, 5)
            };

            Assert.AreEqual(3, HoleOpeningRules.GroupOpenings(tiles).Count);
        }

        [Test]
        public void GroupOpenings_InterleavedInputOrder_StillMergesByAdjacency()
        {
            var tiles = new List<GridPos>
            {
                new GridPos(3, 3, 2),
                new GridPos(9, 9, 2),
                new GridPos(4, 3, 2),
                new GridPos(9, 8, 2)
            };

            List<List<GridPos>> openings = HoleOpeningRules.GroupOpenings(tiles);

            Assert.AreEqual(2, openings.Count);
            CollectionAssert.AreEquivalent(
                new[] { new GridPos(3, 3, 2), new GridPos(4, 3, 2) }, openings[0]);
            CollectionAssert.AreEquivalent(
                new[] { new GridPos(9, 9, 2), new GridPos(9, 8, 2) }, openings[1]);
        }

        [Test]
        public void OpeningContaining_ReturnsOnlyThatOpening()
        {
            var tiles = new List<GridPos>
            {
                new GridPos(3, 3, 2),
                new GridPos(4, 3, 2),
                new GridPos(9, 9, 2)
            };

            CollectionAssert.AreEqual(
                new[] { new GridPos(9, 9, 2) },
                HoleOpeningRules.OpeningContaining(tiles, new GridPos(9, 9, 2)));
        }

        [Test]
        public void OpeningContaining_UnknownCell_ReturnsEmpty()
        {
            var tiles = new List<GridPos> { new GridPos(3, 3, 2) };

            Assert.IsEmpty(HoleOpeningRules.OpeningContaining(tiles, new GridPos(7, 7, 2)));
        }
    }
}
