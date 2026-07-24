using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class SecretRoomRulesTests
    {
        [Test]
        public void TenFloorDungeon_GeneratesExactlyThreeRewardedSecretRooms(
            [Range(1, 30)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 13, 13, 10, 4, seed);
            DungeonFloorInfo[] secrets = dungeon.Floors.Where(floor => floor.HasSecretRoom).ToArray();

            Assert.AreEqual(3, secrets.Length, $"seed {seed}: 첫 던전의 숨은 방 수가 다릅니다.");
            foreach (DungeonFloorInfo floor in secrets)
            {
                Assert.AreEqual(TileKind.SecretDoor, map.Get(floor.SecretDoor.Value).kind);
                Assert.IsTrue(map.Get(floor.SecretDoor.Value).BlocksSight);
                Assert.IsFalse(map.Get(floor.SecretDoor.Value).IsWalkable);
                Assert.Greater(floor.SecretRoomTiles.Count, 0);
                Assert.IsTrue(floor.SecretReward.HasValue);
                CollectionAssert.Contains(floor.SecretRoomTiles.ToList(), floor.SecretReward.Value);

                ItemSpawn reward = floor.Items.Single(item => item.Position == floor.SecretReward.Value);
                Assert.IsTrue(
                    reward.Kind == ItemKind.Gemstone || reward.Kind == ItemKind.Relic,
                    $"seed {seed}: 비밀 보상이 고급 전리품이 아닙니다.");
            }
        }

        [Test]
        public void SecretRoom_IsInaccessibleAndHiddenUntilDoorIsRevealed(
            [Range(1, 20)] int seed)
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 13, 13, 10, 4, seed);
            DungeonFloorInfo floor = dungeon.Floors.First(candidate => candidate.HasSecretRoom);

            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);

            GridPos reward = floor.SecretReward.Value;
            Assert.AreEqual(0, GridPathfinder.FindPath(map, floor.Entry, reward).Count);

            var visibleBefore = GridVisibility.Compute(
                map,
                floor.SecretDoor.Value.South,
                floor.SecretDoor.Value.elevation,
                floor.SecretDoor.Value.elevation,
                8);
            Assert.IsTrue(visibleBefore.Contains(floor.SecretDoor.Value));
            Assert.IsFalse(visibleBefore.Contains(reward));

            Assert.IsTrue(SecretRoomRules.TryReveal(map, floor.SecretDoor.Value));
            Assert.AreEqual(TileKind.SecretPassage, map.Get(floor.SecretDoor.Value).kind);
            Assert.IsFalse(map.Get(floor.SecretDoor.Value).CanClose);
            Assert.Greater(GridPathfinder.FindPath(map, floor.Entry, reward).Count, 0);
        }

        [Test]
        public void Investigation_RequiresCardinalAdjacencyOnSameElevation()
        {
            var door = new GridPos(3, 3, 0);

            Assert.IsTrue(SecretRoomRules.CanInvestigate(new GridPos(3, 2, 0), door));
            Assert.IsFalse(SecretRoomRules.CanInvestigate(new GridPos(2, 2, 0), door));
            Assert.IsFalse(SecretRoomRules.CanInvestigate(new GridPos(3, 2, 1), door));
        }

        [Test]
        public void Blast_RevealsSecretDoorButDoesNotTouchOrdinaryWall()
        {
            var map = new GridMap();
            var secret = new GridPos(2, 1, 0);
            var wall = new GridPos(3, 1, 0);
            map.Set(secret, TileKind.SecretDoor);
            map.Set(wall, TileKind.Wall);

            var revealed = SecretRoomRules.RevealInBlast(map, new GridPos(1, 1, 0));

            CollectionAssert.AreEqual(new[] { secret }, revealed);
            Assert.AreEqual(TileKind.SecretPassage, map.Get(secret).kind);
            Assert.AreEqual(TileKind.Wall, map.Get(wall).kind);
        }

        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(7, 2)]
        [TestCase(8, 3)]
        [TestCase(10, 3)]
        public void DesiredCount_ScalesWithRunLength(int floorCount, int expected)
        {
            Assert.AreEqual(expected, SecretRoomRules.DesiredCount(floorCount));
        }
    }
}
