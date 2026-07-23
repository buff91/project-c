using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class WorldInputRulesTests
    {
        [Test]
        public void IsMapTile_ExistingWalkableTile_ReturnsTrue()
        {
            var map = new GridMap();
            var target = new GridPos(2, 3, 0);
            map.Set(target, TileKind.Floor);

            Assert.IsTrue(WorldInputRules.IsMapTile(map, target));
        }

        [Test]
        public void IsMapTile_ExistingWall_ReturnsTrue()
        {
            var map = new GridMap();
            var target = new GridPos(2, 3, 0);
            map.Set(target, TileKind.Wall);

            Assert.IsTrue(WorldInputRules.IsMapTile(map, target),
                "벽은 이동 불가지만 맵 안의 상호작용 대상이다.");
        }

        [Test]
        public void IsMapTile_VoidCoordinate_ReturnsFalse()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 3, 0), TileKind.Floor);

            Assert.IsFalse(WorldInputRules.IsMapTile(map, new GridPos(20, 30, 0)));
        }

        [Test]
        public void IsMapTile_SameColumnAtMissingElevation_ReturnsFalse()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 3, 0), TileKind.Floor);

            Assert.IsFalse(WorldInputRules.IsMapTile(map, new GridPos(2, 3, 1)));
        }

        [Test]
        public void IsMapTile_NullMap_ReturnsFalse()
        {
            Assert.IsFalse(WorldInputRules.IsMapTile(null, new GridPos(0, 0, 0)));
        }

        [Test]
        public void TryPickProjectedTile_OverlappingFloors_PrefersActiveLayer()
        {
            var active = new GridPos(4, 5, 0);
            var lowerPreview = new GridPos(8, 9, -4);
            var candidates = new List<WorldInputCandidate>
            {
                new WorldInputCandidate(
                    lowerPreview, 0f, 0f, layerPriority: 1, sortingOrder: 99999),
                new WorldInputCandidate(
                    active, 0f, 0f, layerPriority: 2, sortingOrder: 10)
            };

            Assert.IsTrue(WorldInputRules.TryPickProjectedTile(
                candidates, 0f, 0f, 1f, 0.5f, out GridPos picked));
            Assert.AreEqual(active, picked);
        }

        [Test]
        public void TryPickProjectedTile_SameLayer_PrefersFrontmostRenderer()
        {
            var back = new GridPos(4, 4, 0);
            var front = new GridPos(5, 5, 1);
            var candidates = new List<WorldInputCandidate>
            {
                new WorldInputCandidate(back, 0f, 0f, 2, 10),
                new WorldInputCandidate(front, 0f, 0f, 2, 20)
            };

            Assert.IsTrue(WorldInputRules.TryPickProjectedTile(
                candidates, 0f, 0f, 1f, 0.5f, out GridPos picked));
            Assert.AreEqual(front, picked);
        }

        [Test]
        public void TryPickProjectedTile_OutsideVisibleDiamond_ReturnsFalse()
        {
            var candidates = new[]
            {
                new WorldInputCandidate(new GridPos(2, 3, 0), 0f, 0f, 2, 10)
            };

            Assert.IsFalse(WorldInputRules.TryPickProjectedTile(
                candidates, 0.6f, 0f, 1f, 0.5f, out _));
        }

        [Test]
        public void TryPickProjectedTile_OnlyPreviewVisible_CanPickPreview()
        {
            var preview = new GridPos(2, 3, -4);
            var candidates = new[]
            {
                new WorldInputCandidate(preview, 1f, 2f, 1, -100)
            };

            Assert.IsTrue(WorldInputRules.TryPickProjectedTile(
                candidates, 1f, 2f, 1f, 0.5f, out GridPos picked));
            Assert.AreEqual(preview, picked);
        }
    }
}
