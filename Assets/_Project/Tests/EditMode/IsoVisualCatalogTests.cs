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
                    DungeonVisualContext.From(height, elevation: 0, progressIndex: 0)));
            Assert.AreSame(
                shallowRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: 1, progressIndex: 0)));
            Assert.AreSame(
                mid,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -12, progressIndex: 3)));
            Assert.AreSame(
                midRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -11, progressIndex: 3)));
            Assert.AreSame(
                deep,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -24, progressIndex: 6)));
            Assert.AreSame(
                boss,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -36, progressIndex: 9)));
        }

        [Test]
        public void TileFor_DeepAndBossRaisedSlots_TakePriorityOverSharedRaised()
        {
            // FloorFor의 deepRaisedFloor/bossRaisedFloor 분기는 그동안 어서션이 없었다 —
            // 밴드 바닥 발주(배치 1) 전에 선택 규칙을 고정한다.
            Sprite shared = MakeSprite();
            Sprite sharedRaised = MakeSprite();
            Sprite deepRaised = MakeSprite();
            Sprite bossRaised = MakeSprite();
            _catalog.floor = shared;
            _catalog.raisedFloor = sharedRaised;
            _catalog.deepRaisedFloor = deepRaised;
            _catalog.bossRaisedFloor = bossRaised;
            var height = new DungeonHeightModel(4);

            Assert.AreSame(
                deepRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -23, progressIndex: 6)));
            Assert.AreSame(
                bossRaised,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -35, progressIndex: 9)));

            // 밴드 raised 슬롯이 비면 평면 밴드 → 공용 바닥 순으로 내려간다.
            _catalog.deepRaisedFloor = null;
            _catalog.bossRaisedFloor = null;
            Assert.AreSame(
                shared,
                _catalog.TileFor(
                    TileKind.Floor,
                    DungeonVisualContext.From(height, elevation: -23, progressIndex: 6)));
        }

        [Test]
        public void DungeonSurfaceFor_UsesOneCommonToneAcrossDepths_AndHeightOnlyChangesValue()
        {
            var height = new DungeonHeightModel(4);
            DungeonVisualContext b1 = DungeonVisualContext.From(height, height.Elevation(0, 0), 0);
            DungeonVisualContext b4 = DungeonVisualContext.From(height, height.Elevation(-3, 0), 3);
            DungeonVisualContext b7 = DungeonVisualContext.From(height, height.Elevation(-6, 0), 6);
            DungeonVisualContext b10 = DungeonVisualContext.From(height, height.Elevation(-9, 0), 9);
            DungeonVisualContext raised = DungeonVisualContext.From(height, height.Elevation(-6, 1), 6);

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
        public void SurvivorSprite_UsesTheSurvivorSlot_AndFallsBackToPlayer()
        {
            Sprite fallback = MakeSprite();
            Sprite knight = MakeSprite();
            _catalog.player = fallback;
            // 직업이 사라져 원정자 스프라이트는 하나다. knight 슬롯을 그대로 쓰고,
            // 비어 있으면 공용 player 로 떨어진다.
            _catalog.knight = knight;
            Assert.AreSame(knight, _catalog.SurvivorSprite);

            _catalog.knight = null;
            Assert.AreSame(fallback, _catalog.SurvivorSprite);
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

        [Test]
        public void TileFor_Ladder_RendersAsFloor_LadderSlotBelongsToLandmark()
        {
            // ladder 슬롯의 주인은 세워진 사다리 랜드마크다 — 타일 경로가 ladder를 반환하면
            // 랜드마크와 이중 표시가 된다. 발밑은 밴드 규칙을 따르는 일반 바닥이어야 한다.
            Sprite shared = MakeSprite();
            Sprite ladderArt = MakeSprite();
            _catalog.floor = shared;
            _catalog.ladder = ladderArt;
            var height = new DungeonHeightModel(4);

            Assert.AreSame(
                shared,
                _catalog.TileFor(
                    TileKind.Ladder,
                    DungeonVisualContext.From(height, elevation: 0, progressIndex: 0)));
        }

        [Test]
        public void MonsterFor_EachArchetype_UsesOwnSlot()
        {
            Sprite goblin = MakeSprite();
            Sprite skeleton = MakeSprite();
            Sprite slime = MakeSprite();
            Sprite slinger = MakeSprite();
            Sprite arcDrone = MakeSprite();
            Sprite graveWarden = MakeSprite();
            _catalog.goblin = goblin;
            _catalog.skeleton = skeleton;
            _catalog.slime = slime;
            _catalog.slinger = slinger;
            _catalog.arcDrone = arcDrone;
            _catalog.graveWarden = graveWarden;

            Assert.AreSame(goblin, _catalog.MonsterFor("Goblin"));
            Assert.AreSame(skeleton, _catalog.MonsterFor("Skeleton"));
            Assert.AreSame(slime, _catalog.MonsterFor("Slime"));
            Assert.AreSame(slinger, _catalog.MonsterFor("Slinger"));
            Assert.AreSame(arcDrone, _catalog.MonsterFor("ArcDrone"));
            Assert.AreSame(graveWarden, _catalog.MonsterFor("GraveWarden"));
        }

        [Test]
        public void MonsterFor_EmptySlotOrUnknownId_ReturnsNull_NeverGoblin()
        {
            // goblin 폴백이 살아나면 Slinger/GraveWarden이 같은 그림으로 뭉개지는 회귀가 돌아온다 —
            // 빈 슬롯은 null이어야 호출부의 아키타입 전용 절차 폴백이 동작한다.
            _catalog.goblin = MakeSprite();

            Assert.IsNull(_catalog.MonsterFor("Slinger"));
            Assert.IsNull(_catalog.MonsterFor("GraveWarden"));
            Assert.IsNull(_catalog.MonsterFor("unknown-archetype"));
        }

        private ActorAnimationSet MakeAnimationSet(string actorKey, params string[] tags)
        {
            var set = new ActorAnimationSet { actorKey = actorKey };
            foreach (string tag in tags)
            {
                set.clips.Add(new SpriteClip
                {
                    tag = tag,
                    loop = tag == "idle" || tag == "walk",
                    frames = new[] { MakeSprite(), MakeSprite() },
                    frameStartTimes = new[] { 0f, 0.1f },
                    length = 0.2f
                });
            }

            return set;
        }

        [Test]
        public void AnimationsFor_LooksUpByActorKey_IgnoresEmptySets()
        {
            ActorAnimationSet goblinSet = MakeAnimationSet("goblin", "idle", "walk");
            _catalog.actorAnimations.Add(new ActorAnimationSet { actorKey = "skeleton" }); // 빈 세트
            _catalog.actorAnimations.Add(goblinSet);

            Assert.AreSame(goblinSet, _catalog.AnimationsFor("goblin"));
            Assert.IsNull(_catalog.AnimationsFor("skeleton"), "클립 없는 세트는 없는 것으로 친다");
            Assert.IsNull(_catalog.AnimationsFor("unknown"));
        }

        [Test]
        public void SurvivorAnimations_PrefersKnight_FallsBackToPlayer()
        {
            ActorAnimationSet playerSet = MakeAnimationSet("player", "idle");
            _catalog.actorAnimations.Add(playerSet);
            Assert.AreSame(playerSet, _catalog.SurvivorAnimations);

            ActorAnimationSet knightSet = MakeAnimationSet("knight", "idle");
            _catalog.actorAnimations.Add(knightSet);
            Assert.AreSame(knightSet, _catalog.SurvivorAnimations);
        }

        [Test]
        public void MonsterAnimationsFor_UnknownArchetype_ReturnsNull_NeverGoblin()
        {
            _catalog.actorAnimations.Add(MakeAnimationSet("goblin", "idle"));

            Assert.IsNotNull(_catalog.MonsterAnimationsFor("Goblin"));
            Assert.IsNull(_catalog.MonsterAnimationsFor("unknown-archetype"));
            Assert.IsNull(_catalog.MonsterAnimationsFor("Slinger"), "미등록 슬롯은 null — 뭉개짐 방지선");
        }

        [Test]
        public void MonsterAnimationsFor_ArcDrone_UsesDedicatedAnimationSlot()
        {
            ActorAnimationSet arcDroneSet = MakeAnimationSet("arcDrone", "idle", "attack");
            _catalog.actorAnimations.Add(arcDroneSet);

            Assert.AreSame(arcDroneSet, _catalog.MonsterAnimationsFor("ArcDrone"));
        }

        [Test]
        public void SpriteClip_Find_IsCaseInsensitive()
        {
            ActorAnimationSet set = MakeAnimationSet("goblin", "idle", "attack");

            Assert.IsNotNull(set.Find("IDLE"));
            Assert.IsNotNull(set.Find("Attack"));
            Assert.IsNull(set.Find("fall"));
            Assert.IsNull(set.Find(null));
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
