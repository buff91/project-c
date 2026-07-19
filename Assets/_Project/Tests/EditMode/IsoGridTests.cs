using NUnit.Framework;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class IsoGridTests
    {
        private IsoGrid MakeGrid() => new IsoGrid(1.0f, 0.5f, 0.25f);

        [Test]
        public void GridToWorld_Origin_IsZeroAtElevationZero()
        {
            var iso = MakeGrid();
            Vector2 w = iso.GridToWorld(new GridPos(0, 0, 0));
            Assert.AreEqual(0f, w.x, 1e-4f);
            Assert.AreEqual(0f, w.y, 1e-4f);
        }

        [Test]
        public void GridToWorld_Elevation_RaisesY()
        {
            var iso = MakeGrid();
            Vector2 ground = iso.GridToWorld(new GridPos(2, 3, 0));
            Vector2 raised = iso.GridToWorld(new GridPos(2, 3, 1));
            Assert.AreEqual(ground.y + 0.25f, raised.y, 1e-4f);
            Assert.AreEqual(ground.x, raised.x, 1e-4f); // elevation 은 x 에 영향 없음
        }

        [Test]
        public void WorldToGrid_IsInverseOf_GridToWorld()
        {
            var iso = MakeGrid();
            for (int e = 0; e <= 2; e++)
            for (int x = -5; x <= 5; x++)
            for (int y = -5; y <= 5; y++)
            {
                var original = new GridPos(x, y, e);
                Vector2 world = iso.GridToWorld(original);
                GridPos back = iso.WorldToGrid(world, e);
                Assert.AreEqual(original, back, $"round-trip 실패: {original} → {world} → {back}");
            }
        }

        [Test]
        public void SortingOrder_HigherElevation_DrawsInFront()
        {
            var iso = MakeGrid();
            int low = iso.SortingOrder(new GridPos(0, 0, 0));
            int high = iso.SortingOrder(new GridPos(0, 0, 1));
            Assert.Greater(high, low);
        }

        [Test]
        public void SortingOrder_LargerXPlusY_DrawsInFront_SameElevation()
        {
            var iso = MakeGrid();
            int back = iso.SortingOrder(new GridPos(0, 0, 0));
            int front = iso.SortingOrder(new GridPos(2, 1, 0));
            Assert.Greater(front, back);
        }

        [Test]
        public void WorldToGrid_RoundTripsAtEveryViewRotation()
        {
            var iso = MakeGrid();
            iso.viewPivotX = 3.5f;
            iso.viewPivotY = 3.5f;

            for (int rotation = 0; rotation < 4; rotation++)
            {
                iso.SetViewRotation(rotation);
                for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                {
                    var original = new GridPos(x, y, 1);
                    Vector2 world = iso.GridToWorld(original);
                    Assert.AreEqual(original, iso.WorldToGrid(world, 1));
                }
            }
        }

        [Test]
        public void FourViewRotations_ReturnToOriginalProjection()
        {
            var iso = MakeGrid();
            iso.viewPivotX = 3.5f;
            iso.viewPivotY = 3.5f;
            var pos = new GridPos(1, 6, 0);
            Vector2 original = iso.GridToWorld(pos);

            for (int i = 0; i < 4; i++) iso.RotateView(1);

            Assert.AreEqual(0, iso.viewQuarterTurns);
            Assert.AreEqual(original, iso.GridToWorld(pos));
        }

        [Test]
        public void SortingDepth_ChangesWithViewRotation()
        {
            var iso = MakeGrid();
            iso.viewPivotX = 3.5f;
            iso.viewPivotY = 3.5f;
            var first = new GridPos(1, 1, 0);
            var second = new GridPos(6, 1, 0);

            iso.SetViewRotation(0);
            Assert.Greater(iso.SortingOrder(second), iso.SortingOrder(first));

            iso.SetViewRotation(2);
            Assert.Less(iso.SortingOrder(second), iso.SortingOrder(first));
        }
    }

    public class GridPosTests
    {
        [Test]
        public void Equality_And_HashCode_MatchByValue()
        {
            var a = new GridPos(1, 2, 3);
            var b = new GridPos(1, 2, 3);
            var c = new GridPos(1, 2, 0);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Offset_KeepsElevation()
        {
            var p = new GridPos(1, 1, 2).Offset(3, -1);
            Assert.AreEqual(new GridPos(4, 0, 2), p);
        }
    }

    public class GridMapTests
    {
        [Test]
        public void FindLandingBelow_ReturnsFirstSolidGround()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 3), TileKind.Empty);
            map.Set(new GridPos(0, 0, 1), TileKind.Floor);   // 착지 지점
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);

            GridPos? landing = map.FindLandingBelow(new GridPos(0, 0, 3), minElevation: 0);
            Assert.IsTrue(landing.HasValue);
            Assert.AreEqual(new GridPos(0, 0, 1), landing.Value);
        }

        [Test]
        public void FindLandingBelow_NoGround_ReturnsNull()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 5), TileKind.Empty);
            GridPos? landing = map.FindLandingBelow(new GridPos(0, 0, 5), minElevation: 0);
            Assert.IsFalse(landing.HasValue);
        }

        [Test]
        public void Walkable_And_SolidGround_Flags()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 0), TileKind.Wall);
            map.Set(new GridPos(2, 0, 0), TileKind.Hole);

            Assert.IsTrue(map.IsWalkable(new GridPos(0, 0, 0)));
            Assert.IsFalse(map.IsWalkable(new GridPos(1, 0, 0)));
            Assert.IsFalse(map.IsWalkable(new GridPos(3, 0, 0))); // 없는 칸
            Assert.IsTrue(map.IsSolidGround(new GridPos(0, 0, 0)));
            Assert.IsFalse(map.IsSolidGround(new GridPos(2, 0, 0)));
        }
    }

    public class GridPathfinderTests
    {
        [Test]
        public void FindPath_RoutesAroundHole()
        {
            var map = new GridMap();
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 3; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            map.Set(new GridPos(1, 1, 0), TileKind.Hole);

            var path = GridPathfinder.FindPath(map, new GridPos(0, 1, 0), new GridPos(3, 1, 0));

            Assert.Greater(path.Count, 0);
            Assert.AreEqual(new GridPos(0, 1, 0), path[0]);
            Assert.AreEqual(new GridPos(3, 1, 0), path[path.Count - 1]);
            Assert.IsFalse(path.Contains(new GridPos(1, 1, 0)));
        }

        [Test]
        public void FindPath_ChangesElevationOnlyThroughStairs()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 0), TileKind.Stairs);
            map.Set(new GridPos(2, 0, 1), TileKind.Floor);

            var path = GridPathfinder.FindPath(map, new GridPos(0, 0, 0), new GridPos(2, 0, 1));

            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPos(0, 0, 0),
                    new GridPos(1, 0, 0),
                    new GridPos(2, 0, 1)
                },
                path);
        }

        [Test]
        public void FindPath_RejectsDirectHeightChangeWithoutStairs()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 1), TileKind.Floor);

            var path = GridPathfinder.FindPath(map, new GridPos(0, 0, 0), new GridPos(1, 0, 1));

            Assert.AreEqual(0, path.Count);
        }
    }
}
