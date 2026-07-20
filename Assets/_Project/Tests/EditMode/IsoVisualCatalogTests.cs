using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class IsoVisualCatalogTests
    {
        private IsoVisualCatalog _catalog;
        private readonly List<Sprite> _sprites = new List<Sprite>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Sprite sprite in _sprites)
                Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void DoorFor_PrefersMatchingDirection()
        {
            Sprite legacy = MakeSprite();
            Sprite risingRight = MakeSprite();
            Sprite risingLeft = MakeSprite();
            _catalog.doorClosed = legacy;
            _catalog.doorClosedRisingRight = risingRight;
            _catalog.doorClosedRisingLeft = risingLeft;

            Assert.AreSame(risingRight, _catalog.DoorFor(TileKind.DoorClosed, true));
            Assert.AreSame(risingLeft, _catalog.DoorFor(TileKind.DoorClosed, false));
        }

        [Test]
        public void DoorFor_MissingDirection_FallsBackToLegacySprite()
        {
            Sprite legacy = MakeSprite();
            _catalog.doorOpen = legacy;

            Assert.AreSame(legacy, _catalog.DoorFor(TileKind.DoorOpen, true));
            Assert.AreSame(legacy, _catalog.DoorFor(TileKind.DoorOpen, false));
        }

        [Test]
        public void StairsFor_UsesDirectionPerStairKind_ThenSharedFallback()
        {
            Sprite shared = MakeSprite();
            Sprite upRight = MakeSprite();
            Sprite downLeft = MakeSprite();
            _catalog.stairs = shared;
            _catalog.stairsUpRisingRight = upRight;
            _catalog.stairsDownRisingLeft = downLeft;

            Assert.AreSame(upRight, _catalog.StairsFor(TileKind.StairsUp, true));
            Assert.AreSame(downLeft, _catalog.StairsFor(TileKind.StairsDown, false));
            Assert.AreSame(shared, _catalog.StairsFor(TileKind.StairsUp, false));
        }

        [Test]
        public void RearWallFor_MissingTorchVariant_FallsBackToSameDirectionWall()
        {
            Sprite wallRight = MakeSprite();
            Sprite wallLeft = MakeSprite();
            Sprite torchRight = MakeSprite();
            _catalog.rearWallRisingRight = wallRight;
            _catalog.rearWallRisingLeft = wallLeft;
            _catalog.rearWallTorchRisingRight = torchRight;

            Assert.AreSame(torchRight, _catalog.RearWallFor(true, true));
            Assert.AreSame(wallLeft, _catalog.RearWallFor(true, false));
            Assert.AreSame(wallRight, _catalog.RearWallFor(false, true));
        }

        private Sprite MakeSprite()
        {
            Sprite sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _sprites.Add(sprite);
            return sprite;
        }
    }
}
