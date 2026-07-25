using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 생성기가 진행 방향을 <b>매개변수로</b> 읽는다는 계약. 방향은 던전별 데이터라
    /// 하강·상승·진입깊이 던전이 함께 존재하며, 어느 방향에서도 아래 불변식이 성립해야 한다.
    ///
    /// <para>
    /// 여기서 지키려는 핵심은 <b>공간(위/아래)과 진행(진출/귀환)의 분리</b>다.
    /// 진행 판정이 고도로 새면 상승 던전에서 조용히 무너지므로(예전 <c>-FloorIndex</c> 결함),
    /// 그 재발을 막는 것이 이 파일의 목적이다.
    /// </para>
    /// </summary>
    public class DungeonDirectionGenerationTests
    {
        private static readonly DungeonProgressDirection[] AllDirections =
        {
            DungeonProgressDirection.Descend,
            DungeonProgressDirection.Ascend,
            DungeonProgressDirection.Inward
        };

        private static DungeonLayout Build(
            DungeonProgressDirection direction,
            out GridMap map,
            int floorCount = 10,
            int seed = 1977)
        {
            map = new GridMap();
            return DungeonGenerator.Generate(map, 13, 13, floorCount, 4, seed, direction);
        }

        /// <summary>문을 다 연 상태로 경로를 찾는 것이 이 리포의 관례다(닫힌 문은 기본적으로 막힌다).</summary>
        private static void OpenAllDoors(GridMap map, DungeonLayout dungeon)
        {
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);
        }

        [Test]
        public void EveryDirection_ProgressIndexIsSequentialFromZero(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out _);

            for (int i = 0; i < dungeon.Floors.Count; i++)
                Assert.AreEqual(i, dungeon.Floors[i].ProgressIndex,
                    "진행 지수는 방향과 무관하게 0부터 순차여야 한다.");
        }

        [Test]
        public void Ascend_FloorIndexGoesUp_AndFinalIsNotTheSpatialBottom()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out _);

            Assert.AreEqual(0, dungeon.BottomFloorIndex, "상승 던전의 첫 층이 공간 최하단이다.");
            Assert.AreEqual(9, dungeon.TopFloorIndex);
            Assert.AreEqual(9, dungeon.FinalFloorIndex, "진행 최종 층은 공간 최상단이다.");
            Assert.AreNotEqual(dungeon.BottomFloorIndex, dungeon.FinalFloorIndex,
                "상승 던전에서 진행 최종 층과 공간 최하단은 달라야 한다 — 같으면 둘을 혼동한 것이다.");
        }

        [Test]
        public void Descend_FinalCoincidesWithSpatialBottom_ButThatIsAnAccident()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Descend, out _);

            Assert.AreEqual(-9, dungeon.BottomFloorIndex);
            Assert.AreEqual(-9, dungeon.FinalFloorIndex,
                "하강 던전에서만 우연히 같다 — 이 값에 기대어 코드를 쓰면 안 된다.");
        }

        [Test]
        public void EveryDirection_OnwardStairMatchesDirectionAndIsWalkable(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);
            TileKind expected = DungeonDirectionRules.OnwardStair(direction);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                GridPos? onward = dungeon.OnwardStairOf(floor);
                Assert.IsTrue(onward.HasValue,
                    $"진행 {floor.ProgressIndex}층에 진출 계단이 없다.");
                Assert.AreEqual(expected, map.Get(onward.Value).kind,
                    "진출 계단의 타일 종류가 방향과 맞지 않는다.");
            }
        }

        [Test]
        public void EveryDirection_BackStairIsAbsentOnFirstFloorAndPresentAfter(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);
            TileKind expected = DungeonDirectionRules.BackStair(direction);

            Assert.IsFalse(dungeon.BackStairOf(dungeon.Floors[0]).HasValue,
                "첫 층에는 되돌아갈 곳이 없다.");

            foreach (DungeonFloorInfo floor in dungeon.Floors.Skip(1))
            {
                GridPos? back = dungeon.BackStairOf(floor);
                Assert.IsTrue(back.HasValue);
                Assert.AreEqual(expected, map.Get(back.Value).kind);
            }
        }

        [Test]
        public void EveryDirection_OnwardStairsLinkToNextFloorsBackStair(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);

            for (int i = 0; i < dungeon.Floors.Count - 1; i++)
            {
                GridPos onward = dungeon.OnwardStairOf(dungeon.Floors[i]).Value;
                GridPos back = dungeon.BackStairOf(dungeon.Floors[i + 1]).Value;
                Assert.Contains(back, map.LinksFrom(onward).ToList(),
                    $"진행 {i}층의 진출 계단이 다음 층 귀환 계단과 이어지지 않는다.");
            }
        }

        [Test]
        public void EveryDirection_FinalOnwardStairHasNoLink_ItIsTheDungeonExit(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);

            GridPos exit = dungeon.OnwardStairOf(dungeon.Floors[dungeon.Floors.Count - 1]).Value;
            Assert.AreEqual(0, map.LinksFrom(exit).Count,
                "링크 없는 진출 계단이 던전 출구다 — 링크가 있으면 출구가 아니다.");
        }

        [Test]
        public void EveryDirection_OnwardStairIsReachableFromFloorEntry(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);
            OpenAllDoors(map, dungeon);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                GridPos onward = dungeon.OnwardStairOf(floor).Value;
                Assert.Greater(
                    GridPathfinder.FindPath(map, floor.Entry, onward).Count, 0,
                    $"진행 {floor.ProgressIndex}층에서 진출 계단까지 걸어갈 수 없다.");
            }
        }

        // ── 중력은 방향을 타지 않는다 ────────────────────────────────────

        [Test]
        public void EveryDirection_HolesLandExactlyOneFloorBelow(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map);
            int bottomElevation = dungeon.Height.Elevation(dungeon.BottomFloorIndex);

            int checked_ = 0;
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (!floor.Hole.HasValue) continue;
                GridPos? landing = map.FindLandingBelow(floor.Hole.Value, bottomElevation);
                Assert.IsTrue(landing.HasValue, "구멍 아래에 착지 지점이 없다.");
                Assert.AreEqual(
                    floor.FloorIndex - 1,
                    dungeon.Height.FloorIndex(landing.Value.elevation),
                    "구멍은 공간적으로 정확히 한 층 아래에 착지해야 한다(2층 관통 금지).");
                Assert.IsTrue(map.Get(landing.Value).IsWalkable);
                checked_++;
            }

            Assert.Greater(checked_, 0, "검사할 구멍이 하나도 없으면 이 테스트는 무의미하다.");
        }

        [Test]
        public void EveryDirection_SpatialBottomFloorHasNoHole(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out _);

            DungeonFloorInfo bottom = dungeon.Floors
                .OrderBy(floor => floor.FloorIndex)
                .First();
            Assert.IsFalse(bottom.Hole.HasValue,
                "가장 아래 층에 구멍을 뚫으면 떨어질 곳이 없다.");
        }

        [Test]
        public void EveryDirection_BossArenaHasNoHole(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out _);

            DungeonFloorInfo arena = dungeon.Floors[dungeon.Floors.Count - 1];
            Assert.IsFalse(arena.Hole.HasValue,
                "보스 아레나에는 구멍이 없어야 한다 — 있으면 보스전 중 낙하로 아레나를 벗어난다. " +
                "하강 던전에서는 아레나가 공간 최하단이라 자동으로 빠지지만, 상승 던전에서는 " +
                "최상층이므로 명시적으로 막아야 한다.");
        }

        // ── 진행 판정이 고도로 새지 않는다 ──────────────────────────────

        [Test]
        public void ProgressDrivenContent_MatchesAcrossDirections()
        {
            // 휴식처·탈출구·보스 표식·숨은 방은 진행 지수만 봐야 한다. 방향이 달라도
            // 같은 진행 지수에서 같은 판정이 나와야 한다 — 다르면 어딘가 고도로 역산하고 있다.
            var byDirection = new Dictionary<DungeonProgressDirection, List<string>>();
            foreach (DungeonProgressDirection direction in AllDirections)
            {
                DungeonLayout dungeon = Build(direction, out _);
                byDirection[direction] = dungeon.Floors
                    .Select(f => $"p{f.ProgressIndex}:" +
                                 $"rest={f.RestSite.HasValue}," +
                                 $"extract={f.ExtractionPoint.HasValue}," +
                                 $"landmark={f.Landmark.HasValue}")
                    .ToList();
            }

            CollectionAssert.AreEqual(
                byDirection[DungeonProgressDirection.Descend],
                byDirection[DungeonProgressDirection.Ascend],
                "진행 기반 콘텐츠가 방향에 따라 달라졌다 — 어딘가 고도로 역산하고 있다.");
            CollectionAssert.AreEqual(
                byDirection[DungeonProgressDirection.Descend],
                byDirection[DungeonProgressDirection.Inward],
                "진행 기반 콘텐츠가 방향에 따라 달라졌다 — 어딘가 고도로 역산하고 있다.");
        }

        [Test]
        public void Ascend_EnemyCountStillRisesWithProgress()
        {
            // 상승 던전에서 -FloorIndex 로 깊이를 뽑으면 전부 음수가 되어 난이도가 첫 층으로
            // 붕괴한다. 적 수가 진행에 따라 늘어나는지로 그 결함을 잡는다.
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out _);

            int first = dungeon.Floors[0].EnemySpawns.Count;
            int late = dungeon.Floors[8].EnemySpawns.Count;
            Assert.Greater(late, first,
                "상승 던전에서도 진행이 깊어지면 적이 늘어야 한다.");
        }

        [Test]
        public void EveryDirection_SameSeedProducesSameDungeon(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            string Dump()
            {
                DungeonLayout dungeon = Build(direction, out GridMap map, seed: 23);
                return string.Join(";", map.All()
                           .Select(pair => $"{pair.Key}:{pair.Value.kind}")
                           .OrderBy(entry => entry, System.StringComparer.Ordinal)) +
                       "#" + string.Join(";", dungeon.Floors.Select(f =>
                           $"{f.FloorIndex}|{f.ProgressIndex}|{f.Entry}|{f.Hole}|{f.RestSite}"));
            }

            Assert.AreEqual(Dump(), Dump(), "같은 seed·같은 방향이면 같은 던전이어야 한다.");
        }

        [Test]
        public void DirectionChangesTheDungeon_SoItIsActuallyRead()
        {
            DungeonLayout descend = Build(DungeonProgressDirection.Descend, out GridMap descendMap);
            DungeonLayout ascend = Build(DungeonProgressDirection.Ascend, out _);

            Assert.AreNotEqual(
                descend.FinalFloorIndex,
                ascend.FinalFloorIndex,
                "방향을 바꿨는데 던전이 그대로면 생성기가 방향을 읽지 않는 것이다.");

            // 하강 던전에 상행 진출 계단이 있어서는 안 된다(반대도 마찬가지).
            GridPos descendExit = descend
                .OnwardStairOf(descend.Floors[descend.Floors.Count - 1]).Value;
            Assert.AreEqual(TileKind.StairsDown, descendMap.Get(descendExit).kind);
        }

        [Test]
        public void LayoutCarriesItsOwnLabelContext_NotAGlobalSelection()
        {
            // 라벨은 방향 + 시작 건물 층으로 만든다. 둘이 다른 출처에서 오면(예: 방향은 레이아웃,
            // 시작 층은 전역 선택) 허브를 그릴 때 던전 값을 쓰거나 던전 체인의 2번째 던전이
            // 1번째 값을 쓴다. 레이아웃이 둘 다 들고 있어야 한다.
            var map = new GridMap();
            DungeonLayout hospital = DungeonGenerator.Generate(
                map, 13, 13, 10, 4, 1977,
                DungeonProgressDirection.Ascend,
                firstBuildingFloor: -2);

            Assert.AreEqual(DungeonProgressDirection.Ascend, hospital.Direction);
            Assert.AreEqual(-2, hospital.FirstBuildingFloor);
            Assert.AreEqual("B2", DungeonDirectionRules.FloorLabelFor(
                hospital.Direction, hospital.FirstBuildingFloor, 0));
            Assert.AreEqual("1F", DungeonDirectionRules.FloorLabelFor(
                hospital.Direction, hospital.FirstBuildingFloor, 2));
            Assert.AreEqual("8F", DungeonDirectionRules.FloorLabelFor(
                hospital.Direction, hospital.FirstBuildingFloor, 9));

            // 표기 기준을 주지 않은 레이아웃(허브 등)은 지하 1층 기준 하강으로 떨어진다.
            var plain = new DungeonLayout(
                new DungeonHeightModel(4),
                new List<DungeonFloorInfo>
                {
                    new DungeonFloorInfo(
                        0, 0, new GridPos(1, 1, 0), null, null, null, null,
                        new[] { new GridPos(2, 2, 0) }, null, null)
                });
            Assert.AreEqual(DungeonProgressDirection.Descend, plain.Direction);
            Assert.AreEqual(-1, plain.FirstBuildingFloor);
            Assert.AreEqual("B1", DungeonDirectionRules.FloorLabelFor(
                plain.Direction, plain.FirstBuildingFloor, 0));
        }

        [Test]
        public void EveryDirection_EveryWalkableTileIsReachableFromEntry(
            [ValueSource(nameof(AllDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out GridMap map, floorCount: 4);
            OpenAllDoors(map, dungeon);

            // 숨은 방은 공개 전에는 벽처럼 막으므로 도달성 검사에서 제외한다.
            var secret = new HashSet<GridPos>();
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (floor.SecretDoor.HasValue) secret.Add(floor.SecretDoor.Value);
                foreach (GridPos tile in floor.SecretRoomTiles) secret.Add(tile);
            }

            foreach (var pair in map.All())
            {
                if (!pair.Value.IsWalkable || secret.Contains(pair.Key)) continue;
                Assert.Greater(
                    GridPathfinder.FindPath(map, dungeon.Entry, pair.Key).Count, 0,
                    $"{pair.Key} 에 입구에서 도달할 수 없다.");
            }
        }
        // ── 엘리베이터 (보스 처치로 전원이 들어오는 복귀 수단) ─────────

        private static readonly DungeonProgressDirection[] VerticalDirections =
        {
            DungeonProgressDirection.Descend,
            DungeonProgressDirection.Ascend
        };

        [Test]
        public void VerticalDungeons_HaveExactlyOneElevator(
            [ValueSource(nameof(VerticalDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out _);

            Assert.AreEqual(
                ElevatorShaftRules.ShaftsPerDungeon,
                dungeon.Floors.Count(f => f.ElevatorShaft.HasValue),
                "여러 대면 '모든 층에서 복귀'가 되어 진행의 무게가 사라진다.");
            Assert.AreEqual(1, dungeon.Floors.Count(f => f.ElevatorLanding.HasValue));
        }

        [Test]
        public void Elevator_EntranceIsOnTheFloorBeforeTheBossArena()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out _);

            DungeonFloorInfo entrance = dungeon.Floors.Single(f => f.ElevatorShaft.HasValue);
            Assert.AreEqual(
                ElevatorShaftRules.EntranceProgressIndex(dungeon.Floors.Count),
                entrance.ProgressIndex,
                "보스로 가는 길에 멈춘 엘리베이터를 먼저 봐야 전원이 들어온 것이 사건이 된다.");
            Assert.IsTrue(
                DungeonBossArenaRules.IsApproachFloor(entrance.ProgressIndex, dungeon.Floors.Count));
        }

        [Test]
        public void Elevator_LandsNearTheDungeonEntrance_ButNotOnIt()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out _);

            DungeonFloorInfo landing = dungeon.Floors.Single(f => f.ElevatorLanding.HasValue);
            Assert.AreEqual(ElevatorShaftRules.LandingProgressIndex, landing.ProgressIndex);
            Assert.AreNotEqual(
                0,
                landing.ProgressIndex,
                "첫 층 입구 방은 진입 연출·세이브 복원이 얽혀 있어 건드리지 않는다.");
        }

        [Test]
        public void Elevator_IsNotLinkedUntilPowered()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out GridMap map);

            GridPos shaft = dungeon.Floors.Single(f => f.ElevatorShaft.HasValue).ElevatorShaft.Value;

            Assert.AreEqual(TileKind.Ladder, map.Get(shaft).kind, "설비는 처음부터 보인다.");
            Assert.AreEqual(
                0,
                map.LinksFrom(shaft).Count,
                "전원 전에 링크가 있으면 경로 탐색이 곧바로 지름길로 쓴다 " +
                "(GridPathfinder 는 링크를 따라간다).");
        }

        [Test]
        public void Elevator_IsPoweredOnlyByDefeatingTheBoss()
        {
            Assert.IsFalse(
                ElevatorShaftRules.IsPowered(dungeonHasBoss: true, bossDefeated: false),
                "보스가 살아 있으면 건물에 전원이 없다.");
            Assert.IsTrue(ElevatorShaftRules.IsPowered(dungeonHasBoss: true, bossDefeated: true));
            Assert.IsTrue(
                ElevatorShaftRules.IsPowered(dungeonHasBoss: false, bossDefeated: false),
                "보스가 없는 던전은 게이트할 사건이 없다.");
        }

        [Test]
        public void Elevator_ReturnsAgainstProgress_NotAlongIt(
            [ValueSource(nameof(VerticalDirections))] DungeonProgressDirection direction)
        {
            DungeonLayout dungeon = Build(direction, out _);
            DungeonFloorInfo entrance = dungeon.Floors.Single(f => f.ElevatorShaft.HasValue);
            DungeonFloorInfo landing = dungeon.Floors.Single(f => f.ElevatorLanding.HasValue);

            Assert.Less(
                landing.ProgressIndex,
                entrance.ProgressIndex,
                "엘리베이터는 진행을 되감는 방향으로만 간다 — 진행 방향이면 지름길이 된다.");

            // 공간 방향은 던전 방향에 따라 뒤집힌다. 상승 던전에서는 내려가고,
            // 하강 던전에서는 올라간다 — 둘 다 "진행의 반대"다.
            bool goesDownInSpace =
                landing.ElevatorLanding.Value.elevation < entrance.ElevatorShaft.Value.elevation;
            Assert.AreEqual(
                direction == DungeonProgressDirection.Ascend,
                goesDownInSpace,
                "복귀의 공간 방향이 던전 방향과 맞지 않는다.");
        }

        [Test]
        public void Inward_HasNoElevator()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Inward, out _);

            Assert.IsFalse(
                dungeon.Floors.Any(f => f.ElevatorShaft.HasValue),
                "오르내림이 진행이 아니라고 선언한 던전에 승강 연출은 화면과 어긋난다.");
        }

        [Test]
        public void Elevator_DoesNotStrandSpawnsOrSpecials()
        {
            DungeonLayout dungeon = Build(DungeonProgressDirection.Ascend, out GridMap map);
            OpenAllDoors(map, dungeon);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos? candidate in new[] { floor.ElevatorShaft, floor.ElevatorLanding })
            {
                if (!candidate.HasValue) continue;
                GridPos pos = candidate.Value;

                CollectionAssert.DoesNotContain(floor.EnemySpawns.ToList(), pos);
                Assert.IsFalse(floor.Items.Any(item => item.Position == pos));
                Assert.AreNotEqual(floor.Entry, pos);
                Assert.AreNotEqual(floor.RestSite, pos);
                Assert.AreNotEqual(floor.ExtractionPoint, pos);
                Assert.AreNotEqual(floor.Hole, pos);

                Assert.Greater(
                    GridPathfinder.FindPath(map, floor.Entry, pos).Count, 0,
                    "엘리베이터 칸에 걸어갈 수 없다.");
            }
        }

    }
}
