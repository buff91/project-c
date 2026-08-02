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
            Assert.AreEqual(new Color32(10, 13, 19, 255), _catalog.dungeonFog);
            Assert.AreEqual(new Color32(31, 31, 27, 228), _catalog.dungeonFogEdge);
            Assert.AreEqual(new Color32(10, 13, 19, 255), _catalog.dungeonSeam);
            Assert.AreEqual(new Color32(44, 49, 56, 255), _catalog.dungeonStoneShadow);
            Assert.AreEqual(new Color32(59, 63, 69, 255), _catalog.dungeonStone);
            Assert.AreEqual(new Color32(84, 91, 97, 255), _catalog.dungeonStoneLight);
            Assert.AreEqual(new Color32(21, 23, 29, 255), _catalog.dungeonWallShadow);
            Assert.AreEqual(new Color32(44, 49, 56, 255), _catalog.dungeonWall);
            Assert.AreEqual(new Color32(84, 91, 97, 255), _catalog.dungeonWallLight);
            Assert.AreEqual(new Color32(255, 189, 65, 255), _catalog.dungeonAmber);
            Assert.AreEqual(new Color32(255, 213, 84, 255), _catalog.dungeonAmberCore);
            Assert.AreEqual(new Color32(79, 167, 160, 255), _catalog.dungeonMagic);
            Assert.AreEqual(new Color32(61, 225, 232, 255), _catalog.dungeonNeonCyan);
            Assert.AreEqual(new Color32(230, 68, 184, 255), _catalog.dungeonNeonMagenta);
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
        public void HospitalFloorFor_UsesSparseDeterministicSlots()
        {
            Sprite grate = MakeSprite();
            Sprite cracked = MakeSprite();
            Sprite service = MakeSprite();
            _catalog.hospitalFloorGrate = grate;
            _catalog.hospitalFloorCracked = cracked;
            _catalog.hospitalFloorService = service;

            Assert.AreSame(grate, _catalog.HospitalFloorFor(0));
            Assert.AreSame(cracked, _catalog.HospitalFloorFor(3));
            Assert.AreSame(service, _catalog.HospitalFloorFor(6));
            Assert.IsNull(_catalog.HospitalFloorFor(1));
            Assert.AreSame(grate, _catalog.HospitalFloorFor(8));
        }

        [Test]
        public void B2CrackedFloorFor_PrefersDedicatedSlot_ThenFacilityFallback()
        {
            Sprite facilityCracked = MakeSprite();
            Sprite b2Cracked = MakeSprite();
            _catalog.hospitalFloorCracked = facilityCracked;

            Assert.AreSame(facilityCracked, _catalog.B2CrackedFloorFor());

            _catalog.b2CrackedFloor = b2Cracked;
            Assert.AreSame(b2Cracked, _catalog.B2CrackedFloorFor());
        }

        [Test]
        public void B2FloorDressingFor_SelectsFourViews_ThenLegacyFallback()
        {
            Sprite parkingLegacy = MakeSprite();
            Sprite parking0 = MakeSprite();
            Sprite parking1 = MakeSprite();
            Sprite parking2 = MakeSprite();
            Sprite parking3 = MakeSprite();
            Sprite signLegacy = MakeSprite();
            Sprite sign0 = MakeSprite();
            Sprite sign1 = MakeSprite();
            Sprite sign2 = MakeSprite();
            Sprite sign3 = MakeSprite();
            _catalog.b2ParkingWheelStopFloor = parkingLegacy;
            _catalog.b2ParkingWheelStopFloorView0 = parking0;
            _catalog.b2ParkingWheelStopFloorView1 = parking1;
            _catalog.b2ParkingWheelStopFloorView2 = parking2;
            _catalog.b2ParkingWheelStopFloorView3 = parking3;
            _catalog.b2FallenWayfindingFloor = signLegacy;
            _catalog.b2FallenWayfindingFloorView0 = sign0;
            _catalog.b2FallenWayfindingFloorView1 = sign1;
            _catalog.b2FallenWayfindingFloorView2 = sign2;
            _catalog.b2FallenWayfindingFloorView3 = sign3;

            Assert.IsTrue(_catalog.HasB2ParkingWheelStopFloor);
            Assert.IsTrue(_catalog.HasB2FallenWayfindingFloor);
            Assert.AreSame(parking0, _catalog.B2ParkingWheelStopFloorFor(0));
            Assert.AreSame(parking1, _catalog.B2ParkingWheelStopFloorFor(1));
            Assert.AreSame(parking2, _catalog.B2ParkingWheelStopFloorFor(2));
            Assert.AreSame(parking3, _catalog.B2ParkingWheelStopFloorFor(-1));
            Assert.AreSame(sign0, _catalog.B2FallenWayfindingFloorFor(4));
            Assert.AreSame(sign1, _catalog.B2FallenWayfindingFloorFor(5));
            Assert.AreSame(sign2, _catalog.B2FallenWayfindingFloorFor(6));
            Assert.AreSame(sign3, _catalog.B2FallenWayfindingFloorFor(7));

            _catalog.b2ParkingWheelStopFloorView2 = null;
            _catalog.b2FallenWayfindingFloorView1 = null;
            Assert.AreSame(parkingLegacy, _catalog.B2ParkingWheelStopFloorFor(2));
            Assert.AreSame(signLegacy, _catalog.B2FallenWayfindingFloorFor(1));
        }

        [Test]
        public void B2FloorDressingFor_IncompleteViews_UsesLegacyForEveryDirection()
        {
            Sprite parkingLegacy = MakeSprite();
            Sprite parking0 = MakeSprite();
            Sprite parking1 = MakeSprite();
            _catalog.b2ParkingWheelStopFloor = parkingLegacy;
            _catalog.b2ParkingWheelStopFloorView0 = parking0;
            _catalog.b2ParkingWheelStopFloorView1 = parking1;

            Assert.IsTrue(_catalog.HasB2ParkingWheelStopFloor);
            Assert.AreSame(parkingLegacy, _catalog.B2ParkingWheelStopFloorFor(0));
            Assert.AreSame(parkingLegacy, _catalog.B2ParkingWheelStopFloorFor(1));
            Assert.AreSame(parkingLegacy, _catalog.B2ParkingWheelStopFloorFor(2));
            Assert.AreSame(parkingLegacy, _catalog.B2ParkingWheelStopFloorFor(3));
        }

        [Test]
        public void B2FloorDressingFor_NoLegacy_PreservesAxisParityAndNeverDisappears()
        {
            Sprite parking0 = MakeSprite();
            Sprite parking3 = MakeSprite();
            _catalog.b2ParkingWheelStopFloorView0 = parking0;
            _catalog.b2ParkingWheelStopFloorView3 = parking3;

            Assert.IsTrue(_catalog.HasB2ParkingWheelStopFloor);
            Assert.AreSame(parking0, _catalog.B2ParkingWheelStopFloorFor(0));
            Assert.AreSame(parking3, _catalog.B2ParkingWheelStopFloorFor(1));
            Assert.AreSame(parking0, _catalog.B2ParkingWheelStopFloorFor(2));
            Assert.AreSame(parking3, _catalog.B2ParkingWheelStopFloorFor(3));

            _catalog.b2ParkingWheelStopFloorView0 = null;
            Assert.AreSame(
                parking3,
                _catalog.B2ParkingWheelStopFloorFor(0),
                "요청 parity가 전부 비어도 존재하는 슬롯으로 내려가야 한다");

            _catalog.b2ParkingWheelStopFloorView3 = null;
            Assert.IsFalse(_catalog.HasB2ParkingWheelStopFloor);
            Assert.IsNull(_catalog.B2ParkingWheelStopFloorFor(0));
        }

        [Test]
        public void B2BarrelBayFloorFor_RequiresCompleteTwoCellFourViewSet()
        {
            Sprite[] service = { MakeSprite(), MakeSprite(), MakeSprite(), MakeSprite() };
            Sprite[] drain = { MakeSprite(), MakeSprite(), MakeSprite(), MakeSprite() };
            _catalog.b2BarrelBayServiceFloorView0 = service[0];
            _catalog.b2BarrelBayServiceFloorView1 = service[1];
            _catalog.b2BarrelBayServiceFloorView2 = service[2];
            _catalog.b2BarrelBayServiceFloorView3 = service[3];
            _catalog.b2BarrelBayDrainFloorView0 = drain[0];
            _catalog.b2BarrelBayDrainFloorView1 = drain[1];
            _catalog.b2BarrelBayDrainFloorView2 = drain[2];
            _catalog.b2BarrelBayDrainFloorView3 = drain[3];

            Assert.IsTrue(_catalog.HasCompleteB2BarrelBayFloor);
            for (int view = 0; view < 4; view++)
            {
                Assert.AreSame(service[view], _catalog.B2BarrelBayFloorFor(false, view));
                Assert.AreSame(drain[view], _catalog.B2BarrelBayFloorFor(true, view));
            }
            Assert.AreSame(service[3], _catalog.B2BarrelBayFloorFor(false, -1));
            Assert.AreSame(drain[0], _catalog.B2BarrelBayFloorFor(true, 4));

            _catalog.b2BarrelBayDrainFloorView2 = null;
            Assert.IsFalse(_catalog.HasCompleteB2BarrelBayFloor);
            for (int view = 0; view < 4; view++)
            {
                Assert.IsNull(_catalog.B2BarrelBayFloorFor(false, view));
                Assert.IsNull(_catalog.B2BarrelBayFloorFor(true, view));
            }
        }

        [Test]
        public void B2MacroFloorFor_RequiresCompleteFourRoleFourViewSet()
        {
            var sprites = new Sprite[4, 4];
            for (int role = 0; role < 4; role++)
            for (int view = 0; view < 4; view++)
            {
                Sprite sprite = MakeSprite();
                sprites[role, view] = sprite;
                typeof(IsoVisualCatalog)
                    .GetField($"b2MacroFloorRole{role}View{view}")
                    .SetValue(_catalog, sprite);
            }

            Assert.IsTrue(_catalog.HasCompleteB2MacroFloor);
            for (int role = 0; role < 4; role++)
            for (int view = 0; view < 4; view++)
                Assert.AreSame(sprites[role, view], _catalog.B2MacroFloorFor(role, view));
            Assert.AreSame(sprites[2, 3], _catalog.B2MacroFloorFor(2, -1));
            Assert.AreSame(sprites[3, 0], _catalog.B2MacroFloorFor(3, 4));
            Assert.IsNull(_catalog.B2MacroFloorFor(-1, 0));
            Assert.IsNull(_catalog.B2MacroFloorFor(4, 0));

            _catalog.b2MacroFloorRole1View2 = null;
            Assert.IsFalse(_catalog.HasCompleteB2MacroFloor);
            for (int role = 0; role < 4; role++)
            for (int view = 0; view < 4; view++)
                Assert.IsNull(_catalog.B2MacroFloorFor(role, view));
        }

        [Test]
        public void B2RoomFloorLighting_PreservesMeanAndRetainsTwentyPercentLocalContrast()
        {
            var local = new[]
            {
                new Color(0.4f, 0.5f, 0.6f, 0.35f),
                new Color(0.6f, 0.6f, 0.6f, 0.7f),
                new Color(0.8f, 0.7f, 0.6f, 1f),
            };
            Color reference = B2RoomFloorLighting.Average(local);
            Assert.That(reference.r, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(reference.g, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(reference.b, Is.EqualTo(0.6f).Within(0.0001f));

            Color low = B2RoomFloorLighting.Coherent(reference, local[0]);
            Color middle = B2RoomFloorLighting.Coherent(reference, local[1]);
            Color high = B2RoomFloorLighting.Coherent(reference, local[2]);
            Assert.That(low.r, Is.EqualTo(0.56f).Within(0.0001f));
            Assert.That(middle.r, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(high.r, Is.EqualTo(0.64f).Within(0.0001f));
            Assert.That((low.r + middle.r + high.r) / 3f,
                Is.EqualTo(reference.r).Within(0.0001f));
            Assert.That(high.r - low.r,
                Is.EqualTo((local[2].r - local[0].r) *
                    B2RoomFloorLighting.LocalLightRetention).Within(0.0001f));
            Assert.That(low.a, Is.EqualTo(local[0].a).Within(0.0001f));
            Assert.That(middle.a, Is.EqualTo(local[1].a).Within(0.0001f));
            Assert.That(high.a, Is.EqualTo(local[2].a).Within(0.0001f));
        }

        [Test]
        public void B2RoomFloorLighting_EmptyAverageIsNeutralWhite()
        {
            Assert.AreEqual(Color.white, B2RoomFloorLighting.Average(System.Array.Empty<Color>()));
        }

        [Test]
        public void ActorGroundingPresentation_WorldTintCombinesStateElevationAndLocalLight()
        {
            Color result = ActorGroundingPresentation.WorldTint(
                new Color(0.8f, 0.5f, 0.25f, 0.7f),
                new Color(0.75f, 0.8f, 0.9f, 0.2f),
                new Color(0.6f, 0.7f, 0.8f, 0.1f));

            Assert.That(result.r, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That(result.g, Is.EqualTo(0.28f).Within(0.0001f));
            Assert.That(result.b, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(result.a, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void ActorGroundingPresentation_ShadowKeepsReadableDarkTileFloor()
        {
            float dark = ActorGroundingPresentation.ShadowTintAlpha(0.55f, 0f);
            float middle = ActorGroundingPresentation.ShadowTintAlpha(0.55f, 0.5f);
            float bright = ActorGroundingPresentation.ShadowTintAlpha(0.55f, 1f);

            Assert.That(dark, Is.EqualTo(0.3575f).Within(0.0001f));
            Assert.That(middle, Is.GreaterThan(dark));
            Assert.That(bright, Is.EqualTo(0.55f).Within(0.0001f));
        }

        [Test]
        public void ActorGroundingPresentation_PlayerFootprintStaysBelowHalfOpacity()
        {
            Assert.That(
                ActorGroundingPresentation.PlayerFootprintAlpha,
                Is.InRange(0.35f, 0.49f));
        }

        [Test]
        public void RearWallFor_UsesHospitalDecorationBeforeBaseWall()
        {
            Sprite baseWall = MakeSprite();
            Sprite pipes = MakeSprite();
            Sprite window = MakeSprite();
            Sprite cabinet = MakeSprite();
            _catalog.rearWallRisingRight = baseWall;
            _catalog.hospitalWallPipesRisingRight = pipes;
            _catalog.hospitalWallWindowRisingRight = window;
            _catalog.hospitalWallCabinetRisingRight = cabinet;

            Assert.AreSame(pipes, _catalog.RearWallFor(false, true, 0));
            Assert.AreSame(window, _catalog.RearWallFor(false, true, 1));
            Assert.AreSame(cabinet, _catalog.RearWallFor(false, true, 2));
            Assert.AreSame(baseWall, _catalog.RearWallFor(false, true, 3));
        }

        [Test]
        public void B2ServiceWallSegmentFor_RequiresCompleteDirectionalStrip()
        {
            Sprite segment0Right = MakeSprite();
            Sprite segment0Left = MakeSprite();
            Sprite segment1Right = MakeSprite();
            Sprite segment1Left = MakeSprite();
            Sprite segment2Right = MakeSprite();
            Sprite segment2Left = MakeSprite();
            _catalog.b2ServiceWallSegment0RisingRight = segment0Right;
            _catalog.b2ServiceWallSegment0RisingLeft = segment0Left;
            _catalog.b2ServiceWallSegment1RisingRight = segment1Right;
            _catalog.b2ServiceWallSegment1RisingLeft = segment1Left;
            _catalog.b2ServiceWallSegment2RisingRight = segment2Right;
            _catalog.b2ServiceWallSegment2RisingLeft = segment2Left;

            Assert.AreSame(segment0Right, _catalog.B2ServiceWallSegmentFor(0, true));
            Assert.AreSame(segment0Left, _catalog.B2ServiceWallSegmentFor(0, false));
            Assert.AreSame(segment1Right, _catalog.B2ServiceWallSegmentFor(1, true));
            Assert.AreSame(segment1Left, _catalog.B2ServiceWallSegmentFor(1, false));
            Assert.AreSame(segment2Right, _catalog.B2ServiceWallSegmentFor(2, true));
            Assert.AreSame(segment2Left, _catalog.B2ServiceWallSegmentFor(2, false));
            Assert.IsNull(_catalog.B2ServiceWallSegmentFor(-1, true));
            Assert.IsNull(_catalog.B2ServiceWallSegmentFor(3, false));

            _catalog.b2ServiceWallSegment1RisingLeft = null;
            Assert.IsNull(_catalog.B2ServiceWallSegmentFor(0, true));
            Assert.IsNull(_catalog.B2ServiceWallSegmentFor(1, false));
            Assert.IsNull(_catalog.B2ServiceWallSegmentFor(2, true));
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

        [Test]
        public void EnvironmentAnimationsFor_LooksUpIdleSet_IgnoresEmptySets()
        {
            var campfire = new EnvironmentAnimationSet
            {
                slotKey = "hubCampfire",
                clips = new List<SpriteClip>
                {
                    new SpriteClip
                    {
                        tag = "idle",
                        loop = true,
                        frames = new[] { MakeSprite(), MakeSprite() },
                        frameStartTimes = new[] { 0f, 0.1f },
                        length = 0.2f
                    }
                }
            };
            _catalog.environmentAnimations.Add(
                new EnvironmentAnimationSet { slotKey = "hubPortal" });
            _catalog.environmentAnimations.Add(campfire);

            Assert.AreSame(
                campfire,
                _catalog.EnvironmentAnimationsFor("hubCampfire"));
            Assert.IsNull(_catalog.EnvironmentAnimationsFor("hubPortal"));
            Assert.IsNull(_catalog.EnvironmentAnimationsFor("missing"));
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
