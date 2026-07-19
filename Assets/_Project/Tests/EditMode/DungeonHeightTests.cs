using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonHeightModelTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(-1, -1, 3)]
        [TestCase(-3, -1, 1)]
        [TestCase(-4, -1, 0)]
        [TestCase(-8, -2, 0)]
        public void Elevation_IsSplitIntoFloorAndLocalHeight(
            int elevation,
            int expectedFloor,
            int expectedLocalHeight)
        {
            var model = new DungeonHeightModel(4);

            Assert.AreEqual(expectedFloor, model.FloorIndex(elevation));
            Assert.AreEqual(expectedLocalHeight, model.LocalHeight(elevation));
            Assert.AreEqual(elevation, model.Elevation(expectedFloor, expectedLocalHeight));
        }
    }

    public class DungeonGeneratorTests
    {
        [Test]
        public void Generate_CreatesThreeFloorsWithInternalHeightAndLinks()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 7);

            Assert.AreEqual(3, dungeon.Floors.Count);
            Assert.AreEqual(0, dungeon.TopFloorIndex);
            Assert.AreEqual(-2, dungeon.BottomFloorIndex);
            Assert.IsTrue(map.HasLinks);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                int[] localHeights = map.All()
                    .Where(pair => dungeon.Height.FloorIndex(pair.Key.elevation) == floor.FloorIndex)
                    .Select(pair => dungeon.Height.LocalHeight(pair.Key.elevation))
                    .Distinct()
                    .ToArray();

                CollectionAssert.Contains(localHeights, 0);
                CollectionAssert.Contains(localHeights, 1);
                Assert.AreEqual(2, floor.Doors.Count);
                Assert.IsTrue(floor.Doors.All(door => map.Get(door).kind == TileKind.DoorClosed));
            }
        }

        [Test]
        public void PathFinder_TraversesInternalStairsAndDungeonFloorLinks()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 11);
            GridPos bottomEntry = dungeon.Floors[2].Entry;

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            var path = GridPathfinder.FindPath(map, dungeon.Entry, bottomEntry);

            Assert.Greater(path.Count, 0);
            Assert.AreEqual(dungeon.Entry, path[0]);
            Assert.AreEqual(bottomEntry, path[path.Count - 1]);
            Assert.IsTrue(path.Any(pos => pos == dungeon.Floors[0].DownStairs.Value));
            Assert.IsTrue(path.Any(pos => pos == dungeon.Floors[1].UpStairs.Value));
            Assert.IsTrue(path.Any(pos => pos == dungeon.Floors[1].DownStairs.Value));
            Assert.IsTrue(path.Any(pos => pos == dungeon.Floors[2].UpStairs.Value));
        }

        [Test]
        public void GeneratedHoles_HaveSolidLandingOnLowerFloor()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 17);
            int minimumElevation = dungeon.Height.Elevation(dungeon.BottomFloorIndex);

            foreach (DungeonFloorInfo floor in dungeon.Floors.Where(floor => floor.Hole.HasValue))
            {
                GridPos? landing = map.FindLandingBelow(floor.Hole.Value, minimumElevation);

                Assert.IsTrue(landing.HasValue, $"{floor.FloorIndex}층 Hole 아래에 착지점이 필요합니다.");
                Assert.Less(dungeon.Height.FloorIndex(landing.Value.elevation), floor.FloorIndex);
            }
        }

        [Test]
        public void Clear_RemovesDungeonLinksAlongWithTiles()
        {
            var map = new GridMap();
            DungeonGenerator.Generate(map, 11, 11, 2, 4);

            map.Clear();

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.HasLinks);
        }

        [Test]
        public void ClosedDoor_BlocksAnotherRoomUntilOpened()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 5);
            DungeonFloorInfo floor = dungeon.Floors[0];

            Assert.AreEqual(0, GridPathfinder.FindPath(map, floor.Entry, floor.EnemySpawn).Count);

            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            Assert.Greater(GridPathfinder.FindPath(map, floor.Entry, floor.EnemySpawn).Count, 0);
        }

        [Test]
        public void DoorState_ChangesWalkAndSightRules()
        {
            var closed = new TileData(TileKind.DoorClosed);
            var open = new TileData(TileKind.DoorOpen);

            Assert.IsTrue(closed.IsSolidGround);
            Assert.IsFalse(closed.IsWalkable);
            Assert.IsTrue(closed.BlocksSight);
            Assert.IsTrue(closed.CanOpen);
            Assert.IsFalse(closed.CanClose);
            Assert.IsTrue(open.IsWalkable);
            Assert.IsFalse(open.BlocksSight);
            Assert.IsTrue(open.CanClose);
        }
    }

    public class GridVisibilityTests
    {
        [Test]
        public void ClosedDoor_IsVisibleButHidesTilesBehindIt()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++)
                map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.DoorClosed);

            var visible = GridVisibility.Compute(map, new GridPos(0, 0, 0), 0, 0, 8);

            Assert.IsTrue(visible.Contains(new GridPos(2, 0, 0)));
            Assert.IsFalse(visible.Contains(new GridPos(3, 0, 0)));
            Assert.IsFalse(visible.Contains(new GridPos(4, 0, 0)));
        }

        [Test]
        public void OpenDoor_RevealsConnectedTilesWithinRadius()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++)
                map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.DoorOpen);

            var visible = GridVisibility.Compute(map, new GridPos(0, 0, 0), 0, 0, 8);

            Assert.IsTrue(visible.Contains(new GridPos(4, 0, 0)));
        }

        [Test]
        public void Radius_LimitsVisibilityEvenWithoutBlockers()
        {
            var map = new GridMap();
            for (int x = 0; x < 6; x++)
                map.Set(new GridPos(x, 0, 0), TileKind.Floor);

            var visible = GridVisibility.Compute(map, new GridPos(0, 0, 0), 0, 0, 2);

            Assert.IsTrue(visible.Contains(new GridPos(2, 0, 0)));
            Assert.IsFalse(visible.Contains(new GridPos(3, 0, 0)));
        }
    }
}
