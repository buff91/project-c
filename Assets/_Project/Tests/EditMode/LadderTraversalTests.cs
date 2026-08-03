using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 계단과 사다리가 <b>규칙으로</b> 다르다는 것을 고정한다.
    ///
    /// <para>
    /// 예전에는 A* 가 둘을 같은 조건식으로 묶어서, 사다리가 사실상 "스프라이트만 다른 계단"이었다.
    /// HUD 와 문서는 "사다리는 탭/Space 로 오른다"고 약속했지만 강제하는 코드도 <b>테스트도 없었다</b>.
    /// 이 파일이 그 빈틈을 메운다 — 여기가 깨지면 사다리는 다시 계단이 된다.
    /// </para>
    /// <para>
    /// 전체 도달성은 <c>ProceduralDungeonTests.AnySeed_EveryWalkableTileIsReachableFromEntry</c> 가
    /// 이미 지킨다(기본값 <c>canClimb: true</c> = 플레이어 기준). 여기서는 <b>대조</b>만 본다.
    /// </para>
    /// </summary>
    public class LadderTraversalTests
    {
        // 수직 연결은 항상 "가로로 한 칸 옮기면서 높이가 바뀐다" — 경로 탐색이 4방향
        // 이웃만 보고 같은 컬럼 위아래는 이웃으로 치지 않기 때문이다(생성기도 같은 모양이다).
        private static readonly GridPos Start = new GridPos(0, 0, 0);
        private static readonly GridPos Connector = new GridPos(1, 0, 0);
        private static readonly GridPos High = new GridPos(2, 0, 1);

        /// <summary>바닥 두 칸 + 한 단 위 발판. 연결 수단만 갈아끼워 대조한다.</summary>
        private static GridMap TwoLevels(TileKind connector, bool link)
        {
            var map = new GridMap();
            map.Set(Start, TileKind.Floor);
            map.Set(Connector, connector);
            map.Set(High, TileKind.Floor);
            if (link) map.Connect(Connector, High);
            return map;
        }

        [Test]
        public void Stairs_AreWalkable_RegardlessOfClimbing()
        {
            // 계단은 지형이다 — 등반 능력과 무관하게 누구나 걸어서 오른다.
            GridMap map = TwoLevels(TileKind.Stairs, link: false);

            Assert.Greater(
                GridPathfinder.FindPath(map, Start, High, canClimb: true).Count, 0);
            Assert.Greater(
                GridPathfinder.FindPath(map, Start, High, canClimb: false).Count, 0,
                "계단이 사다리 규칙에 휩쓸리면 안 된다 — 못 오르는 적도 계단은 쓴다");
        }

        [Test]
        public void Ladder_IsNotWalkable_WithoutALink()
        {
            // 링크 없이 사다리 타일만 놓으면 높이가 바뀌는 경로가 없어야 한다.
            // 예전에는 여기가 통과했다 — 사다리가 계단과 같은 인접 규칙에 들어 있었다.
            GridMap map = TwoLevels(TileKind.Ladder, link: false);

            Assert.AreEqual(
                0, GridPathfinder.FindPath(map, Start, High, canClimb: true).Count,
                "사다리는 인접 규칙으로 오를 수 없다 — 링크가 있어야 한다");
        }

        [Test]
        public void LadderLink_OpensOnlyForClimbers()
        {
            GridMap map = TwoLevels(TileKind.Ladder, link: true);

            Assert.Greater(
                GridPathfinder.FindPath(map, Start, High, canClimb: true).Count, 0,
                "오를 수 있으면 링크를 탄다");
            Assert.AreEqual(
                0, GridPathfinder.FindPath(map, Start, High, canClimb: false).Count,
                "못 오르면 사다리 너머로 가지 못한다 — 이것이 추격을 끊는 규칙이다");
        }

        [Test]
        public void LadderTile_IsStillReachableOnFoot()
        {
            // 못 오르는 적도 사다리 발판 자체에는 걸어 올라설 수 있어야 한다.
            // 막는 것은 "타고 오르기"지 "그 칸에 서기"가 아니다.
            GridMap map = TwoLevels(TileKind.Ladder, link: true);

            Assert.Greater(
                GridPathfinder.FindPath(map, Start, Connector, canClimb: false).Count, 0);
        }

        [Test]
        public void FloorTransitionLinks_AreNotGatedByClimbing()
        {
            // 층 전환 계단 링크는 사다리가 아니다 — 등반 여부와 무관해야 한다.
            // 여기가 막히면 못 오르는 적이 자기 층에 갇힌다.
            var map = new GridMap();
            map.Set(Start, TileKind.Floor);
            map.Set(Connector, TileKind.StairsDown);
            var below = new GridPos(1, 0, -4);
            map.Set(below, TileKind.StairsUp);
            map.Connect(Connector, below);

            Assert.Greater(
                GridPathfinder.FindPath(map, Start, below, canClimb: false).Count, 0);
        }

        // ── 생성된 던전에서의 계약 ────────────────────────────────────────

        [Test]
        public void GeneratedCatwalk_IsReachableOnlyByClimbing()
        {
            // "높은 곳은 사다리로만"의 실제 계약. 캐치워크가 걸어서 닿으면
            // 사다리는 다시 장식이 된다.
            int checkedTiles = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var map = new GridMap();
                DungeonLayout dungeon = DungeonGenerator.Generate(map, 13, 13, 10, seed: seed);
                OpenEveryDoor(map);

                foreach (GridPos catwalk in CatwalkTiles(map, dungeon))
                {
                    DungeonFloorInfo floor = FloorOf(dungeon, catwalk);
                    if (floor == null) continue;
                    checkedTiles++;

                    Assert.Greater(
                        GridPathfinder.FindPath(map, floor.Entry, catwalk, canClimb: true).Count, 0,
                        $"seed {seed}: 오를 수 있는데도 캐치워크 {catwalk} 에 못 닿는다");
                    Assert.AreEqual(
                        0, GridPathfinder.FindPath(map, floor.Entry, catwalk, canClimb: false).Count,
                        $"seed {seed}: 캐치워크 {catwalk} 가 걸어서 닿는다 — 사다리가 무의미해진다");
                }
            }

            Assert.Greater(checkedTiles, 0, "캐치워크를 하나도 못 찾았다 — 테스트가 헛돌고 있다");
        }

        [Test]
        public void GeneratedLadder_SpansMoreThanOneStep_WhereCatwalkExists()
        {
            // 계단은 ±1 만, 사다리는 여러 단 — 이 대비가 둘을 가른다.
            bool sawMultiStep = false;
            for (int seed = 1; seed <= 12 && !sawMultiStep; seed++)
            {
                var map = new GridMap();
                DungeonGenerator.Generate(map, 13, 13, 10, seed: seed);

                foreach (KeyValuePair<GridPos, TileData> pair in map.All())
                {
                    if (pair.Value.kind != TileKind.Ladder) continue;
                    foreach (GridPos linked in map.LinksFrom(pair.Key))
                        if (System.Math.Abs(linked.elevation - pair.Key.elevation) > 1)
                            sawMultiStep = true;
                }
            }

            Assert.IsTrue(
                sawMultiStep,
                "캐치워크가 있는 층에서 사다리가 한 번에 여러 단을 이어야 한다");
        }

        [Test]
        public void Roster_HasBothClimbersAndNonClimbers()
        {
            // 한쪽으로 쏠리면 이 축이 무의미하다.
            Assert.IsTrue(
                MonsterRoster.Regular.Any(a => a.CanClimb), "오를 수 있는 종이 있어야 한다");
            Assert.IsTrue(
                MonsterRoster.Regular.Any(a => !a.CanClimb), "못 오르는 종이 있어야 한다");
            Assert.IsTrue(
                MonsterRoster.GraveWarden.CanClimb,
                "인간형 사이버사이코 감시자는 기계 폴백의 등반 규칙을 물려받으면 안 된다");
        }

        [Test]
        public void NewArchetypes_DoNotClimbByDefault()
        {
            // 기본값이 true 면 새 몬스터를 늘릴 때마다 조용히 전부 오르게 된다.
            var plain = new MonsterArchetype("Test", maxHp: 1, attackPower: 1,
                aggroRange: 1, patrolRadius: 0);
            Assert.IsFalse(plain.CanClimb);
        }

        private static IEnumerable<GridPos> CatwalkTiles(GridMap map, DungeonLayout dungeon)
        {
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                int catwalkElevation = dungeon.Height.Elevation(floor.FloorIndex) + 2;
                foreach (KeyValuePair<GridPos, TileData> pair in map.All())
                {
                    if (pair.Key.elevation != catwalkElevation) continue;
                    if (pair.Value.kind != TileKind.Floor) continue;
                    yield return pair.Key;
                }
            }
        }

        private static DungeonFloorInfo FloorOf(DungeonLayout dungeon, GridPos pos)
        {
            int floorIndex = dungeon.Height.FloorIndex(pos.elevation);
            return dungeon.TryGetFloor(floorIndex, out DungeonFloorInfo floor) ? floor : null;
        }

        private static void OpenEveryDoor(GridMap map)
        {
            var closed = new List<GridPos>();
            foreach (KeyValuePair<GridPos, TileData> pair in map.All())
                if (pair.Value.kind == TileKind.DoorClosed) closed.Add(pair.Key);
            foreach (GridPos door in closed) map.Set(door, TileKind.DoorOpen);
        }
    }
}
