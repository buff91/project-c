using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class B2HeroRoomLayoutRulesTests
    {
        [Test]
        public void AppliesOnlyToArcadeTowerStartFloor()
        {
            Assert.IsTrue(B2HeroRoomLayoutRules.AppliesTo(DungeonCatalog.DefaultId, 0));
            Assert.IsFalse(B2HeroRoomLayoutRules.AppliesTo(DungeonCatalog.DefaultId, 1));
            Assert.IsFalse(B2HeroRoomLayoutRules.AppliesTo("flooded-vault", 0));
        }

        [Test]
        public void DefaultSeed1977_KeepsEntryFanAndOnwardSpineClear()
        {
            BuildDefault(1977, out GridMap map, out DungeonLayout dungeon);
            DungeonFloorInfo floor = dungeon.Floors[0];
            GridPos onward = dungeon.OnwardStairOf(floor).Value;
            Dictionary<GridPos, string> before = Snapshot(map);

            Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                DungeonCatalog.DefaultId,
                0,
                1977,
                map,
                floor,
                onward,
                OccupiedByFloor(floor),
                out B2HeroRoomLayout layout));

            Assert.AreEqual(30, layout.RoomCells.Count, "현행 B2 시작방은 6×5다");
            Assert.AreEqual(floor.Entry, layout.ClearSpine[0]);
            Assert.AreEqual(onward, layout.ClearSpine[layout.ClearSpine.Count - 1]);
            Assert.AreEqual(new GridPos(0, 3, 0), layout.Barrel);
            Assert.AreEqual(new GridPos(5, 2, 0), layout.ParkingStop);
            Assert.AreEqual(0, layout.ParkingStopWorldFacingQuarterTurns);
            Assert.AreEqual(new GridPos(5, 1, 0), layout.FallenSign);
            Assert.AreEqual(0, layout.FallenSignWorldFacingQuarterTurns);
            CollectionAssert.AreEqual(
                new[] { new GridPos(5, 2, 0), new GridPos(5, 1, 0) },
                layout.AccentPositions);

            Assert.IsTrue(layout.TryGetFloorPatch(
                new GridPos(0, 3, 0), out B2HeroFloorPatchKind service));
            Assert.AreEqual(B2HeroFloorPatchKind.Service, service);
            Assert.IsTrue(layout.TryGetFloorPatch(
                new GridPos(0, 4, 0), out B2HeroFloorPatchKind grate));
            Assert.AreEqual(B2HeroFloorPatchKind.Grate, grate);
            Assert.IsTrue(layout.TryGetBarrelBay(
                out GridPos servicePos,
                out GridPos drainPos,
                out int barrelBayFacing));
            Assert.AreEqual(new GridPos(0, 3, 0), servicePos);
            Assert.AreEqual(new GridPos(0, 4, 0), drainPos);
            Assert.AreEqual(0, barrelBayFacing);
            Assert.IsTrue(layout.TryGetFloorPatch(
                new GridPos(5, 3, 0), out B2HeroFloorPatchKind cracked));
            Assert.AreEqual(B2HeroFloorPatchKind.Cracked, cracked);
            Assert.IsFalse(layout.TryGetFloorPatch(new GridPos(5, 0, 0), out _),
                "벽 매립 kiosk 아래는 조용한 기본 바닥이어야 한다");
            var expectedMacro = new Dictionary<GridPos, int>
            {
                { new GridPos(3, 1, 0), 0 },
                { new GridPos(4, 1, 0), 1 },
                { new GridPos(3, 2, 0), 2 },
                { new GridPos(4, 2, 0), 3 },
            };
            foreach (KeyValuePair<GridPos, int> pair in expectedMacro)
            {
                Assert.IsTrue(layout.TryGetMacroFloorRole(pair.Key, out int role));
                Assert.AreEqual(pair.Value, role, $"Macro role 불일치: {pair.Key}");
            }

            foreach (GridPos pos in layout.AccentPositions)
                Assert.IsFalse(layout.IsClearSpine(pos), $"드레싱이 진출선에 겹쳤다: {pos}");
            foreach (GridPos pos in layout.RoomCells)
            {
                if (layout.TryGetFloorPatch(pos, out _))
                    Assert.IsFalse(layout.IsClearSpine(pos), $"바닥 군집이 진출선에 겹쳤다: {pos}");
            }

            CollectionAssert.AreEquivalent(before, Snapshot(map),
                "히어로 룸 계획은 지형·상태·링크를 바꾸면 안 된다");
        }

        [Test]
        public void DefaultSeed1977_GroupsServiceWallOnOnePhysicalSide()
        {
            BuildDefault(1977, out GridMap map, out DungeonLayout dungeon);
            DungeonFloorInfo floor = dungeon.Floors[0];
            Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                DungeonCatalog.DefaultId,
                0,
                1977,
                map,
                floor,
                dungeon.OnwardStairOf(floor),
                OccupiedByFloor(floor),
                out B2HeroRoomLayout layout));

            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(5, 0, 0), 0, -1, out int terminal));
            Assert.AreEqual(2, terminal);
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(0, 0, 0), 0, -1, out int sealedArrivalTerminal));
            Assert.AreEqual(2, sealedArrivalTerminal,
                "뒤쪽 중심에는 이동 기능이 없는 봉인 단말이 있어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(5, 0, 0), 1, 0, out int terminalCorner));
            Assert.AreEqual(-1, terminalCorner,
                "단말은 같은 코너 셀의 -Y 물리 벽에만 붙어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(1, 0, 0), 0, -1, out int quiet));
            Assert.AreEqual(1, quiet,
                "홀수 wall bay는 비점등 보조 재질로 반복 박자를 끊어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(2, 0, 0), 0, -1, out int baseMaterial));
            Assert.AreEqual(-1, baseMaterial,
                "짝수 wall bay는 기본 저주파 재질을 유지해야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(3, 0, 0), 0, -1, out int sconceSlot));
            Assert.AreEqual(1, sconceSlot,
                "중앙 -Y 벽도 월드 고정 보조 재질이어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(5, 2, 0), 1, 0, out int oppositeXUtility));
            Assert.AreEqual(0, oppositeXUtility,
                "회전 시 보이는 +X 벽에는 저채도 설비 패널 하나가 있어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(2, 4, 0), 0, 1, out int oppositeYUtility));
            Assert.AreEqual(0, oppositeYUtility,
                "회전 시 보이는 +Y 벽에는 저채도 설비 패널 하나가 있어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(5, 1, 0), 1, 0, out int oppositeXMaterial));
            Assert.AreEqual(1, oppositeXMaterial,
                "+X 벽 홀수 bay도 회전과 무관한 보조 재질이어야 한다");
            Assert.IsTrue(layout.TryGetWallDecoration(
                new GridPos(3, 4, 0), 0, 1, out int oppositeYMaterial));
            Assert.AreEqual(1, oppositeYMaterial,
                "+Y 벽 홀수 bay도 회전과 무관한 보조 재질이어야 한다");
            Assert.IsFalse(layout.TryGetWallDecoration(
                new GridPos(6, 4, 0), 0, 1, out _),
                "닫힌 문/복도 벽은 시작방 군집이 소유하지 않는다");

            Assert.IsTrue(layout.TryGetServiceWallSegment(
                new GridPos(0, 3, 0), -1, 0, out int segment0));
            Assert.AreEqual(0, segment0);
            Assert.IsTrue(layout.TryGetServiceWallSegment(
                new GridPos(0, 2, 0), -1, 0, out int segment1));
            Assert.AreEqual(1, segment1);
            Assert.IsTrue(layout.TryGetServiceWallSegment(
                new GridPos(0, 1, 0), -1, 0, out int segment2));
            Assert.AreEqual(2, segment2);
            foreach (GridPos servicePos in new[]
                     {
                         new GridPos(0, 3, 0),
                         new GridPos(0, 2, 0),
                         new GridPos(0, 1, 0),
                     })
            {
                Assert.IsTrue(layout.TryGetWallDecoration(
                    servicePos, -1, 0, out int serviceDecoration));
                Assert.AreEqual(-1, serviceDecoration,
                    $"서비스 master 셀에 보조 재질이 섞이면 안 된다: {servicePos}");
            }
            Assert.IsFalse(layout.TryGetServiceWallSegment(
                new GridPos(0, 4, 0), -1, 0, out _));
            Assert.IsFalse(layout.TryGetServiceWallSegment(
                new GridPos(0, 0, 0), -1, 0, out _));
            Assert.IsFalse(layout.TryGetServiceWallSegment(
                new GridPos(0, 3, 0), 1, 0, out _),
                "같은 좌표라도 반대 물리 벽에는 서비스 벽이 새면 안 된다");
            Assert.IsFalse(layout.TryGetServiceWallSegment(
                new GridPos(0, 3, 0), -2, 0, out _),
                "서비스 벽 outward는 단위 cardinal만 허용한다");
            Assert.IsFalse(layout.TryGetWallDecoration(
                new GridPos(5, 0, 0), 0, -2, out _),
                "단말 outward도 단위 cardinal만 허용한다");

            foreach (GridPos pos in layout.RoomCells)
            {
                Assert.IsTrue(layout.TryGetWallSconce(pos, out bool authored));
                Assert.AreEqual(
                    pos == new GridPos(0, 2, 0),
                    authored,
                    $"B2 작업등 host 불일치: {pos}");
            }
            Assert.IsFalse(layout.TryGetWallSconce(
                new GridPos(6, 4, 0), out _));
        }

        [Test]
        public void SeedsOneToThirty_KeepPropsInsideStartRoomAndOffSpine()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                BuildDefault(seed, out GridMap map, out DungeonLayout dungeon);
                DungeonFloorInfo floor = dungeon.Floors[0];
                Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                    DungeonCatalog.DefaultId,
                    0,
                    seed,
                    map,
                    floor,
                    dungeon.OnwardStairOf(floor),
                    OccupiedByFloor(floor),
                    out B2HeroRoomLayout layout),
                    $"seed {seed}: B2 배치 계획을 만들지 못했다");

                Assert.IsTrue(layout.Barrel.HasValue, $"seed {seed}: 폭발통 좌표가 없다");
                Assert.IsTrue(layout.ContainsRoomCell(layout.Barrel.Value));
                Assert.IsFalse(layout.IsClearSpine(layout.Barrel.Value));
                Assert.IsTrue(layout.ParkingStop.HasValue,
                    $"seed {seed}: named parking stop 좌표가 없다");
                Assert.IsTrue(layout.FallenSign.HasValue,
                    $"seed {seed}: named fallen sign 좌표가 없다");
                Assert.AreEqual(0, layout.ParkingStopWorldFacingQuarterTurns);
                Assert.AreEqual(0, layout.FallenSignWorldFacingQuarterTurns);
                Assert.IsTrue(layout.TryGetBarrelBay(
                    out GridPos service,
                    out GridPos drain,
                    out int facing),
                    $"seed {seed}: 배럴 service/drain 쌍이 없다");
                Assert.AreEqual(layout.Barrel.Value, service);
                Assert.AreEqual(1, service.ManhattanTo(drain));
                int expectedFacing = drain.y > service.y
                    ? 0
                    : drain.x > service.x
                        ? 1
                        : drain.y < service.y
                            ? 2
                            : 3;
                Assert.AreEqual(expectedFacing, facing, $"seed {seed}: 베이 방향 불일치");
                foreach (GridPos accent in layout.AccentPositions)
                {
                    Assert.IsTrue(layout.ContainsRoomCell(accent));
                    Assert.IsFalse(layout.IsClearSpine(accent));
                    Assert.AreNotEqual(layout.Barrel.Value, accent);
                    Assert.AreEqual(TileKind.Floor, map.Get(accent).kind);
                    Assert.IsFalse(layout.TryGetFloorPatch(accent, out _),
                        $"seed {seed}: 낮은 프롭과 특수 바닥이 겹쳤다: {accent}");
                }
                CollectionAssert.AreEqual(
                    new[] { layout.ParkingStop.Value, layout.FallenSign.Value },
                    layout.AccentPositions);
                GridPos kioskFloor = new GridPos(layout.MaxX, layout.MinY, floor.Entry.elevation);
                CollectionAssert.DoesNotContain(layout.AccentPositions, kioskFloor);
                Assert.IsFalse(layout.TryGetFloorPatch(kioskFloor, out _),
                    $"seed {seed}: kiosk 아래 바닥이 채워졌다");

                var macro = new Dictionary<GridPos, int>();
                foreach (GridPos pos in layout.RoomCells)
                {
                    if (layout.TryGetMacroFloorRole(pos, out int role))
                        macro.Add(pos, role);
                }
                Assert.That(macro.Count, Is.EqualTo(0).Or.EqualTo(4),
                    $"seed {seed}: Macro는 0 또는 4셀이어야 한다");
                if (macro.Count == 4)
                {
                    CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, macro.Values);
                    int minX = macro.Keys.Min(pos => pos.x);
                    int minY = macro.Keys.Min(pos => pos.y);
                    var expected = new Dictionary<GridPos, int>
                    {
                        { new GridPos(minX, minY, floor.Entry.elevation), 0 },
                        { new GridPos(minX + 1, minY, floor.Entry.elevation), 1 },
                        { new GridPos(minX, minY + 1, floor.Entry.elevation), 2 },
                        { new GridPos(minX + 1, minY + 1, floor.Entry.elevation), 3 },
                    };
                    CollectionAssert.AreEquivalent(expected, macro,
                        $"seed {seed}: Macro가 정확한 2×2 물리 role을 이루지 않는다");
                    foreach (GridPos pos in macro.Keys)
                    {
                        Assert.AreEqual(TileKind.Floor, map.Get(pos).kind);
                        Assert.IsFalse(layout.IsClearSpine(pos));
                        Assert.AreNotEqual(layout.Barrel.Value, pos);
                        CollectionAssert.DoesNotContain(layout.AccentPositions, pos);
                        Assert.IsTrue(layout.TryGetFloorPatch(
                            pos, out B2HeroFloorPatchKind patch));
                        Assert.That(patch, Is.GreaterThanOrEqualTo(B2HeroFloorPatchKind.Macro0));
                    }
                }

                Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                    DungeonCatalog.DefaultId,
                    0,
                    seed,
                    map,
                    floor,
                    dungeon.OnwardStairOf(floor),
                    OccupiedByFloor(floor),
                    out B2HeroRoomLayout repeated));
                foreach (GridPos pos in layout.RoomCells)
                {
                    Assert.AreEqual(
                        layout.TryGetMacroFloorRole(pos, out int firstRole),
                        repeated.TryGetMacroFloorRole(pos, out int secondRole));
                    Assert.AreEqual(firstRole, secondRole);
                }
            }
        }

        [Test]
        public void BlockedPreferredRightCluster_UsesNamedSafeFallbackOnly()
        {
            BuildDefault(1977, out GridMap map, out DungeonLayout dungeon);
            DungeonFloorInfo floor = dungeon.Floors[0];
            var occupied = OccupiedByFloor(floor);
            var preferredParking = new GridPos(5, 2, 0);
            var preferredFallen = new GridPos(5, 1, 0);
            var preferredCracked = new GridPos(5, 3, 0);
            occupied.Add(preferredParking);
            occupied.Add(preferredFallen);
            occupied.Add(preferredCracked);

            Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                DungeonCatalog.DefaultId,
                0,
                1977,
                map,
                floor,
                dungeon.OnwardStairOf(floor),
                occupied,
                out B2HeroRoomLayout layout));

            Assert.IsTrue(layout.ParkingStop.HasValue);
            Assert.IsTrue(layout.FallenSign.HasValue);
            Assert.AreNotEqual(preferredParking, layout.ParkingStop.Value);
            Assert.AreNotEqual(preferredFallen, layout.FallenSign.Value);
            foreach (GridPos accent in layout.AccentPositions)
            {
                Assert.IsFalse(occupied.Contains(accent));
                Assert.IsFalse(layout.IsClearSpine(accent));
                Assert.AreEqual(TileKind.Floor, map.Get(accent).kind);
                Assert.IsFalse(layout.TryGetFloorPatch(accent, out _));
            }
            Assert.GreaterOrEqual(
                layout.ParkingStop.Value.ManhattanTo(layout.FallenSign.Value),
                DungeonDressingPlacementRules.MinimumDressingSpacing,
                "fallback일 때는 기존 드레싱 최소 간격을 유지한다");
            Assert.IsFalse(layout.TryGetFloorPatch(preferredCracked, out _));
            Assert.IsFalse(layout.TryGetFloorPatch(new GridPos(5, 0, 0), out _));
        }

        [Test]
        public void FullyReservedRoom_LeavesNoPartialMacroFloor()
        {
            BuildDefault(1977, out GridMap map, out DungeonLayout dungeon);
            DungeonFloorInfo floor = dungeon.Floors[0];
            Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                DungeonCatalog.DefaultId,
                0,
                1977,
                map,
                floor,
                dungeon.OnwardStairOf(floor),
                OccupiedByFloor(floor),
                out B2HeroRoomLayout baseline));

            var fullyReserved = new HashSet<GridPos>(baseline.RoomCells);
            Assert.IsTrue(B2HeroRoomLayoutRules.TryCreate(
                DungeonCatalog.DefaultId,
                0,
                1977,
                map,
                floor,
                dungeon.OnwardStairOf(floor),
                fullyReserved,
                out B2HeroRoomLayout blocked));

            int macroCount = blocked.RoomCells.Count(pos =>
                blocked.TryGetMacroFloorRole(pos, out _));
            Assert.AreEqual(0, macroCount);
            Assert.IsFalse(blocked.ParkingStop.HasValue);
            Assert.IsFalse(blocked.FallenSign.HasValue);
        }

        private static void BuildDefault(
            int seed,
            out GridMap map,
            out DungeonLayout dungeon)
        {
            DungeonDefinition definition = DungeonCatalog.ById(DungeonCatalog.DefaultId);
            map = new GridMap();
            dungeon = DungeonGenerator.Generate(
                map,
                13,
                13,
                definition.FloorCount,
                elevationsPerFloor: 4,
                seed: seed,
                direction: definition.Direction,
                firstBuildingFloor: definition.FirstBuildingFloor,
                region: definition.Region,
                usesLocalElevation: definition.UsesLocalElevation);
        }

        private static HashSet<GridPos> OccupiedByFloor(DungeonFloorInfo floor)
        {
            var occupied = new HashSet<GridPos> { floor.Entry };
            if (floor.UpStairs.HasValue) occupied.Add(floor.UpStairs.Value);
            if (floor.DownStairs.HasValue) occupied.Add(floor.DownStairs.Value);
            if (floor.RestSite.HasValue) occupied.Add(floor.RestSite.Value);
            if (floor.ExtractionPoint.HasValue) occupied.Add(floor.ExtractionPoint.Value);
            if (floor.RescueNpc.HasValue) occupied.Add(floor.RescueNpc.Value);
            if (floor.Landmark.HasValue) occupied.Add(floor.Landmark.Value);
            foreach (GridPos enemy in floor.EnemySpawns) occupied.Add(enemy);
            foreach (ItemSpawn item in floor.Items) occupied.Add(item.Position);
            return occupied;
        }

        private static Dictionary<GridPos, string> Snapshot(GridMap map)
        {
            return map.All().ToDictionary(
                pair => pair.Key,
                pair => string.Join(
                    ":",
                    pair.Value.kind,
                    pair.Value.wet,
                    pair.Value.oiled,
                    string.Join(",", map.LinksFrom(pair.Key).OrderBy(pos => pos.x)
                        .ThenBy(pos => pos.y)
                        .ThenBy(pos => pos.elevation))));
        }
    }
}
