using NUnit.Framework;
using ProjectC.Core;
using System.Linq;

namespace ProjectC.Tests
{
    public class VerticalTraversalRulesTests
    {
        [TestCase(TileKind.StairsUp)]
        [TestCase(TileKind.StairsDown)]
        public void TryGetAutomaticFloorDestination_LinkedFloorStairs_ReturnsDestination(
            TileKind kind)
        {
            var map = new GridMap();
            var entrance = new GridPos(2, 3, 0);
            var destination = new GridPos(2, 3, -4);
            map.Set(entrance, kind);
            map.Set(destination, kind == TileKind.StairsDown
                ? TileKind.StairsUp
                : TileKind.StairsDown);
            map.Connect(entrance, destination);

            Assert.IsTrue(VerticalTraversalRules.TryGetAutomaticFloorDestination(
                map, entrance, out GridPos result));
            Assert.AreEqual(destination, result);
        }

        [TestCase(TileKind.Ladder)]
        [TestCase(TileKind.Stairs)]
        public void TryGetAutomaticFloorDestination_LocalConnector_DoesNotAutoActivate(
            TileKind kind)
        {
            var map = new GridMap();
            var entrance = new GridPos(2, 3, 0);
            var destination = new GridPos(2, 4, 1);
            map.Set(entrance, kind);
            map.Set(destination, kind);
            map.Connect(entrance, destination);

            Assert.IsFalse(VerticalTraversalRules.TryGetAutomaticFloorDestination(
                map, entrance, out _),
                kind == TileKind.Ladder
                    ? "사다리 첫 입력은 발판 부착에서 끝나고, 그 위에서 두 번째 상호작용으로 올라야 한다"
                    : "같은 층의 발판 계단은 링크 자동 이동 대상이 아니다");
        }

        [Test]
        public void TryGetAutomaticFloorDestination_UnlinkedExit_DoesNotInventDestination()
        {
            var map = new GridMap();
            var exit = new GridPos(2, 3, -8);
            map.Set(exit, TileKind.StairsDown);

            Assert.IsFalse(VerticalTraversalRules.TryGetAutomaticFloorDestination(
                map, exit, out _));
        }

        [Test]
        public void LadderScaleY_OneStepLadder_IsShorterThanFullPlaceholderSprite()
        {
            float scale = VerticalTraversalRules.LadderScaleY(
                elevationDelta: 1,
                elevationStep: 0.25f,
                tileHeight: 0.5f,
                spriteWorldHeight: 1.125f);

            Assert.That(scale, Is.EqualTo(0.3777778f).Within(0.0001f));
            Assert.Less(scale, 0.5f);
        }

        [Test]
        public void GeneratedLocalStairs_AreReachableAfterOpeningTheirRooms(
            [Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(
                map, 13, 13, floorCount: 3, elevationsPerFloor: 4, seed: seed);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                GridPos stairs = map.All()
                    .Where(pair =>
                        pair.Value.kind == TileKind.Stairs &&
                        dungeon.Height.FloorIndex(pair.Key.elevation) == floor.FloorIndex)
                    .Select(pair => pair.Key)
                    .Single();

                Assert.IsTrue(StairTopology.TryGetHigherLanding(
                    map, stairs, out GridPos higherLanding));
                Assert.Greater(
                    GridPathfinder.FindPath(map, floor.Entry, stairs).Count,
                    0,
                    $"seed {seed}, floor {floor.FloorIndex}: 계단 입구에 갈 수 없습니다.");
                Assert.Greater(
                    GridPathfinder.FindPath(map, floor.Entry, higherLanding).Count,
                    0,
                    $"seed {seed}, floor {floor.FloorIndex}: 계단 위 착지면에 갈 수 없습니다.");
            }
        }
    }
}
