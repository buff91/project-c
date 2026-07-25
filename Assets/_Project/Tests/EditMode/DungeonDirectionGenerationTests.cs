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
    }
}
