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
        public void TileFor_MapsLadderSeparatelyFromStairs()
        {
            Sprite stairs = MakeSprite();
            Sprite ladder = MakeSprite();
            _catalog.stairs = stairs;
            _catalog.ladder = ladder;

            Assert.AreSame(stairs, _catalog.TileFor(TileKind.Stairs, 0));
            Assert.AreSame(ladder, _catalog.TileFor(TileKind.Ladder, 0));
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

        [Test]
        public void HeroFor_UsesDistinctRoleSprite_AndFallsBackToPlayer()
        {
            Sprite fallback = MakeSprite();
            Sprite knight = MakeSprite();
            Sprite ranger = MakeSprite();
            Sprite alchemist = MakeSprite();
            _catalog.player = fallback;
            _catalog.knight = knight;
            _catalog.ranger = ranger;
            _catalog.alchemist = alchemist;

            Assert.AreSame(knight, _catalog.HeroFor("knight"));
            Assert.AreSame(ranger, _catalog.HeroFor("ranger"));
            Assert.AreSame(alchemist, _catalog.HeroFor("alchemist"));

            _catalog.ranger = null;
            Assert.AreSame(fallback, _catalog.HeroFor("ranger"));
        }

        [Test]
        public void ItemFor_MapsCraftingMaterialsWithoutRuntimeArtFallback()
        {
            Sprite herb = MakeSprite();
            Sprite powder = MakeSprite();
            Sprite shard = MakeSprite();
            _catalog.herb = herb;
            _catalog.blastPowder = powder;
            _catalog.frostShard = shard;

            Assert.AreSame(herb, _catalog.ItemFor(ItemKind.Herb));
            Assert.AreSame(powder, _catalog.ItemFor(ItemKind.BlastPowder));
            Assert.AreSame(shard, _catalog.ItemFor(ItemKind.FrostShard));
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
