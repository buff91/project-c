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

            DungeonVisualContext context = DungeonVisualContext.Preview();
            Assert.AreSame(stairs, _catalog.TileFor(TileKind.Stairs, context));
            Assert.AreSame(ladder, _catalog.TileFor(TileKind.Ladder, context));
        }

        [Test]
        public void TileFor_SelectsDepthBandAndLocalHeightIndependently()
        {
            Sprite shallow = MakeSprite();
            Sprite shallowRaised = MakeSprite();
            Sprite mid = MakeSprite();
            Sprite midRaised = MakeSprite();
            Sprite deep = MakeSprite();
            Sprite boss = MakeSprite();
            _catalog.floor = shallow;
            _catalog.raisedFloor = shallowRaised;
            _catalog.midFloor = mid;
            _catalog.midRaisedFloor = midRaised;
            _catalog.deepFloor = deep;
            _catalog.bossFloor = boss;
            var height = new DungeonHeightModel(4);

            Assert.AreSame(
                shallow,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: 0)));
            Assert.AreSame(
                shallowRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: 1)));
            Assert.AreSame(
                mid,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -12)));
            Assert.AreSame(
                midRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -11)));
            Assert.AreSame(
                deep,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -24)));
            Assert.AreSame(
                boss,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -36)));
        }

        [Test]
        public void DungeonSurfaceFor_UsesOneCommonToneAcrossDepths_AndHeightOnlyChangesValue()
        {
            var height = new DungeonHeightModel(4);
            DungeonVisualContext b1 = DungeonVisualContext.From(height, height.Elevation(0, 0));
            DungeonVisualContext b4 = DungeonVisualContext.From(height, height.Elevation(-3, 0));
            DungeonVisualContext b7 = DungeonVisualContext.From(height, height.Elevation(-6, 0));
            DungeonVisualContext b10 = DungeonVisualContext.From(height, height.Elevation(-9, 0));
            DungeonVisualContext raised = DungeonVisualContext.From(height, height.Elevation(-6, 1));

            Assert.AreEqual(_catalog.dungeonStone, _catalog.DungeonSurfaceFor(b1));
            Assert.AreEqual(_catalog.dungeonStone, _catalog.DungeonSurfaceFor(b4));
            Assert.AreEqual(_catalog.dungeonStone, _catalog.DungeonSurfaceFor(b7));
            Assert.AreEqual(_catalog.dungeonStone, _catalog.DungeonSurfaceFor(b10));
            Assert.AreNotEqual(_catalog.dungeonStone, _catalog.DungeonSurfaceFor(raised));
        }

        [Test]
        public void DungeonPalette_DefaultRolesUseTorchstoneTokens()
        {
            Assert.AreEqual(new Color32(5, 7, 12, 255), _catalog.dungeonVoid);
            Assert.AreEqual(new Color32(10, 13, 19, 255), _catalog.dungeonSeam);
            Assert.AreEqual(new Color32(74, 64, 56, 255), _catalog.dungeonStone);
            Assert.AreEqual(new Color32(152, 134, 111, 255), _catalog.dungeonStoneLight);
            Assert.AreEqual(new Color32(207, 192, 174, 255), _catalog.dungeonWallLight);
            Assert.AreEqual(new Color32(255, 189, 65, 255), _catalog.dungeonAmber);
            Assert.AreEqual(new Color32(255, 213, 84, 255), _catalog.dungeonAmberCore);
            Assert.AreEqual(new Color32(79, 167, 160, 255), _catalog.dungeonMagic);
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

        [Test]
        public void ImpactFx_MapsEachKind_AndDefaultsToPhysical()
        {
            Sprite physical = MakeSprite();
            Sprite fire = MakeSprite();
            Sprite frost = MakeSprite();
            Sprite heavy = MakeSprite();
            _catalog.fxImpactPhysical = physical;
            _catalog.fxImpactFire = fire;
            _catalog.fxImpactFrost = frost;
            _catalog.fxImpactHeavy = heavy;

            Assert.AreSame(fire, _catalog.ImpactFx(CombatImpactKind.Fire));
            Assert.AreSame(frost, _catalog.ImpactFx(CombatImpactKind.Frost));
            Assert.AreSame(heavy, _catalog.ImpactFx(CombatImpactKind.Heavy));
            Assert.AreSame(physical, _catalog.ImpactFx(CombatImpactKind.Physical));
        }

        [Test]
        public void StatusFx_MapsBurnAndFreeze()
        {
            Sprite burn = MakeSprite();
            Sprite freeze = MakeSprite();
            _catalog.fxStatusBurn = burn;
            _catalog.fxStatusFreeze = freeze;

            Assert.AreSame(burn, _catalog.StatusFx(StatusKind.Burn));
            Assert.AreSame(freeze, _catalog.StatusFx(StatusKind.Freeze));
        }

        [Test]
        public void Fx_ReturnsNullWhenUnassigned_ForProceduralFallback()
        {
            Assert.IsNull(_catalog.ImpactFx(CombatImpactKind.Fire));
            Assert.IsNull(_catalog.StatusFx(StatusKind.Burn));
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
