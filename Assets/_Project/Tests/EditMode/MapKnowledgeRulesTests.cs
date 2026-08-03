using System;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public sealed class MapKnowledgeRulesTests
    {
        private static readonly GridPos Position = new GridPos(2, 3, 0);

        [TestCase(TileKind.Floor)]
        [TestCase(TileKind.WeakFloor)]
        [TestCase(TileKind.Stairs)]
        [TestCase(TileKind.StairsUp)]
        [TestCase(TileKind.StairsDown)]
        [TestCase(TileKind.Ladder)]
        [TestCase(TileKind.SecretPassage)]
        [TestCase(TileKind.WindowBroken)]
        public void TryGetSilhouette_WalkableAndSpecialSurfaces_CollapseToFloor(TileKind kind)
        {
            AssertSilhouette(kind, MapSilhouetteKind.Floor);
        }

        [TestCase(TileKind.Wall)]
        [TestCase(TileKind.Window)]
        public void TryGetSilhouette_BlockingTopology_CollapsesToBarrier(TileKind kind)
        {
            AssertSilhouette(kind, MapSilhouetteKind.Barrier);
        }

        [TestCase(TileKind.DoorClosed)]
        [TestCase(TileKind.DoorOpen)]
        public void TryGetSilhouette_DoorState_CollapsesToSameDoorCategory(TileKind kind)
        {
            AssertSilhouette(kind, MapSilhouetteKind.Door);
        }

        [TestCase(TileKind.Empty)]
        [TestCase(TileKind.Hole)]
        public void TryGetSilhouette_OpenVoid_CollapsesToGap(TileKind kind)
        {
            AssertSilhouette(kind, MapSilhouetteKind.Gap);
        }

        [Test]
        public void TryGetSilhouette_ElementFlags_DoNotLeakThroughCategory()
        {
            var tile = new TileData(TileKind.WeakFloor)
            {
                oiled = true,
                wet = true
            };

            Assert.IsTrue(MapKnowledgeRules.TryGetSilhouette(
                Floor(), 0, Position, tile, out MapSilhouetteKind silhouette));
            Assert.AreEqual(MapSilhouetteKind.Floor, silhouette);
        }

        [Test]
        public void TryGetSilhouette_HiddenSecretRoomFootprint_IsExcluded()
        {
            DungeonFloorInfo floor = Floor(secretRoomTiles: new[] { Position });

            Assert.IsTrue(MapKnowledgeRules.IsHiddenSecretRoomTile(floor, Position));
            Assert.IsFalse(MapKnowledgeRules.TryGetSilhouette(
                floor,
                0,
                Position,
                new TileData(TileKind.Floor),
                out _));
        }

        [Test]
        public void TryGetSilhouette_SecretDoorCoordinate_IsExcludedEvenIfListedWithSecretRoom()
        {
            DungeonFloorInfo floor = Floor(
                secretDoor: Position,
                secretRoomTiles: new[] { Position });

            Assert.IsFalse(MapKnowledgeRules.TryGetSilhouette(
                floor,
                0,
                Position,
                new TileData(TileKind.SecretDoor),
                out _));
            Assert.AreEqual(
                MapSilhouetteKind.Barrier,
                MapKnowledgeRules.SilhouetteFor(TileKind.SecretDoor),
                "종류 축약은 Barrier지만 돌출 좌표 자체는 mapped 집합에서 빠져야 한다.");
        }

        [TestCase(TileKind.StairsUp, false)]
        [TestCase(TileKind.StairsDown, false)]
        [TestCase(TileKind.SecretDoor, false)]
        [TestCase(TileKind.Floor, true)]
        [TestCase(TileKind.WeakFloor, true)]
        [TestCase(TileKind.Ladder, true)]
        [TestCase(TileKind.DoorClosed, true)]
        public void CanAutoTravelThroughUnknown_HiddenAutomaticTransitionsAreBlocked(
            TileKind kind,
            bool expected)
        {
            Assert.AreEqual(expected, MapKnowledgeRules.CanAutoTravelThroughUnknown(kind));
        }

        [Test]
        public void TryGetSilhouette_OtherFloorOrMissingTile_IsExcluded()
        {
            DungeonFloorInfo floor = Floor();

            Assert.IsFalse(MapKnowledgeRules.TryGetSilhouette(
                floor,
                -1,
                Position,
                new TileData(TileKind.Floor),
                out _));
            Assert.IsFalse(MapKnowledgeRules.TryGetSilhouette(
                floor,
                0,
                Position,
                null,
                out _));
        }

        [Test]
        public void IsHiddenSecretRoomTile_OrdinaryOrMissingFloor_ReturnsFalse()
        {
            Assert.IsFalse(MapKnowledgeRules.IsHiddenSecretRoomTile(Floor(), Position));
            Assert.IsFalse(MapKnowledgeRules.IsHiddenSecretRoomTile(null, Position));
        }

        private static void AssertSilhouette(TileKind kind, MapSilhouetteKind expected)
        {
            Assert.IsTrue(MapKnowledgeRules.TryGetSilhouette(
                Floor(),
                0,
                Position,
                new TileData(kind),
                out MapSilhouetteKind actual));
            Assert.AreEqual(expected, actual);
        }

        private static DungeonFloorInfo Floor(
            GridPos? secretDoor = null,
            GridPos[] secretRoomTiles = null)
        {
            return new DungeonFloorInfo(
                floorIndex: 0,
                progressIndex: 0,
                entry: new GridPos(0, 0, 0),
                upStairs: null,
                downStairs: null,
                holeTiles: Array.Empty<GridPos>(),
                restSite: null,
                enemySpawns: Array.Empty<GridPos>(),
                items: Array.Empty<ItemSpawn>(),
                doors: Array.Empty<GridPos>(),
                secretDoor: secretDoor,
                secretRoomTiles: secretRoomTiles);
        }
    }
}
