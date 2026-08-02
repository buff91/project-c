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

        [TestCase(0, 0, 0, 0)]
        [TestCase(3, 0, 0, 3)]
        [TestCase(-4, -1, 1, 0)]
        [TestCase(-3, -1, 1, 1)]
        [TestCase(-12, -3, 3, 0)]
        public void VisualContext_SeparatesProgressElevationAndLocalHeight(
            int elevation,
            int expectedFloor,
            int progressIndex,
            int expectedLocalHeight)
        {
            DungeonVisualContext context = DungeonVisualContext.From(
                new DungeonHeightModel(4),
                elevation,
                progressIndex);

            Assert.AreEqual(expectedFloor, context.FloorIndex);
            Assert.AreEqual(progressIndex, context.ProgressIndex);
            Assert.AreEqual(elevation, context.Elevation);
            Assert.AreEqual(expectedLocalHeight, context.LocalHeight);
            Assert.AreEqual(expectedLocalHeight > 0, context.IsRaised);
        }

        /// <summary>
        /// 회귀 방지: 진행 지수는 elevation/floorIndex 에서 파생되지 않는다.
        /// 예전 구현은 <c>Max(0, -floorIndex)</c>로 역산해서 상승 던전(양수 floorIndex)에서
        /// 전부 0으로 붕괴했고, 비단조 경로에서는 애초에 성립하지 않았다(GDD §5.1).
        /// </summary>
        [Test]
        public void VisualContext_ProgressIsIndependentOfElevationSign()
        {
            var height = new DungeonHeightModel(4);

            // 지상 8층(양수 elevation)이 진행 지수 7 — 파생이었다면 0으로 뭉갰다.
            DungeonVisualContext ascending = DungeonVisualContext.From(height, height.Elevation(7, 0), 7);
            Assert.AreEqual(7, ascending.ProgressIndex);
            Assert.AreEqual(DungeonDepthBand.Deep, ascending.DepthBand);

            // 같은 고도라도 진행 지수가 다르면 다른 구간이다.
            DungeonVisualContext sameHeightEarlier = DungeonVisualContext.From(height, height.Elevation(7, 0), 1);
            Assert.AreEqual(DungeonDepthBand.Shallow, sameHeightEarlier.DepthBand);
            Assert.AreNotEqual(ascending, sameHeightEarlier);

            // 비단조 경로: 내려갔다 올라온 층은 고도로 순서를 알 수 없다.
            DungeonVisualContext revisitedLow = DungeonVisualContext.From(height, height.Elevation(-1, 0), 9);
            Assert.AreEqual(DungeonDepthBand.Boss, revisitedLow.DepthBand);
        }

        [TestCase(0, DungeonDepthBand.Shallow)]
        [TestCase(2, DungeonDepthBand.Shallow)]
        [TestCase(3, DungeonDepthBand.Mid)]
        [TestCase(5, DungeonDepthBand.Mid)]
        [TestCase(6, DungeonDepthBand.Deep)]
        [TestCase(8, DungeonDepthBand.Deep)]
        [TestCase(9, DungeonDepthBand.Boss)]
        public void DepthBand_UsesDungeonProgress_NotElevationOrLocalHeight(
            int depthIndex,
            DungeonDepthBand expected)
        {
            var height = new DungeonHeightModel(4);
            int floorIndex = -depthIndex;
            DungeonVisualContext flat = DungeonVisualContext.From(
                height,
                height.Elevation(floorIndex, 0),
                depthIndex);
            DungeonVisualContext raised = DungeonVisualContext.From(
                height,
                height.Elevation(floorIndex, 1),
                depthIndex);

            Assert.AreEqual(expected, flat.DepthBand);
            Assert.AreEqual(expected, raised.DepthBand);
        }
    }

    public class DungeonGeneratorTests
    {
        [Test]
        public void FirstDungeon_UsesOneTraversableElevationPerFloor()
        {
            DungeonDefinition definition = DungeonCatalog.ById(DungeonCatalog.DefaultId);
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(
                map,
                13,
                13,
                definition.FloorCount,
                elevationsPerFloor: 4,
                seed: definition.Seed,
                direction: definition.Direction,
                firstBuildingFloor: definition.FirstBuildingFloor,
                region: definition.Region,
                usesLocalElevation: definition.UsesLocalElevation);

            Assert.AreEqual(4, dungeon.Height.ElevationsPerFloor,
                "평탄화는 층간 stride를 줄이는 마이그레이션이 아니다");

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                int[] localHeights = map.All()
                    .Where(pair => dungeon.Height.FloorIndex(pair.Key.elevation) == floor.FloorIndex)
                    .Select(pair => dungeon.Height.LocalHeight(pair.Key.elevation))
                    .Distinct()
                    .ToArray();

                CollectionAssert.AreEqual(new[] { 0 }, localHeights,
                    $"{floor.FloorIndex}층의 모든 타일은 기준 elevation에 있어야 한다");
            }

            DungeonFloorInfo arena = dungeon.Floors.Single(
                floor => DungeonBossArenaRules.IsArenaFloor(
                    floor.ProgressIndex, dungeon.Floors.Count));
            Assert.IsTrue(arena.Landmark.HasValue, "평탄화해도 보스 제단은 유지해야 한다");
            Assert.AreEqual(0, dungeon.Height.LocalHeight(arena.Landmark.Value.elevation));

            Assert.IsFalse(map.All().Any(pair => pair.Value.kind == TileKind.Stairs),
                "첫 던전에는 같은 층 ±1 이동용 계단을 생성하지 않는다");

            foreach (var pair in map.All())
            foreach (GridPos linked in map.LinksFrom(pair.Key))
            {
                bool sameFloor = dungeon.Height.FloorIndex(pair.Key.elevation) ==
                                 dungeon.Height.FloorIndex(linked.elevation);
                Assert.IsFalse(
                    sameFloor && pair.Key.elevation != linked.elevation,
                    $"첫 던전에 층내 높이 링크가 남았다: {pair.Key} -> {linked}");
            }
        }

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
                // 기본 복도 문 2개 + 확률적 분기 방 문. (seed 변형 생성)
                Assert.GreaterOrEqual(floor.Doors.Count, 2);
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
        public void Generate_AddsOneRecognizableLadderLinkPerDungeonFloor()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 23);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                GridPos[] ladderTiles = map.All()
                    .Where(pair =>
                        pair.Value.kind == TileKind.Ladder &&
                        dungeon.Height.FloorIndex(pair.Key.elevation) == floor.FloorIndex)
                    .Select(pair => pair.Key)
                    .ToArray();

                Assert.AreEqual(2, ladderTiles.Length);
                Assert.AreEqual(1, System.Math.Abs(
                    ladderTiles[0].elevation - ladderTiles[1].elevation));
                CollectionAssert.Contains(map.LinksFrom(ladderTiles[0]), ladderTiles[1]);
                CollectionAssert.Contains(map.LinksFrom(ladderTiles[1]), ladderTiles[0]);
                Assert.IsTrue(map.Get(ladderTiles[0]).IsSolidGround);
                Assert.IsTrue(map.Get(ladderTiles[1]).IsWalkable);
            }
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
