using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 실제 생성된 던전에서 FOV가 <b>벽/void를 뚫는지</b> 본다.
    /// <para>
    /// 이 던전은 방 경계를 벽 타일이 아니라 <b>타일 부재(void)</b>로 표현하고
    /// (<see cref="GridVisibility"/> 주석), void 컬럼은 결과에 절대 들어가지 않는다.
    /// 따라서 정상 FOV의 가시 집합은 <b>원점에서 8방향으로 이어져 있어야 한다</b> —
    /// 셰도우캐스팅이 한 칸씩 바깥으로 나아가며 넣기 때문이다.
    /// 끊긴 덩어리가 나오면 시야가 방 경계를 건너뛴 것이다.
    /// </para>
    /// <para>
    /// 합성 격자가 아니라 <b>운영 형상</b>(폐병원 10층)을 쓰고, 원점도 실제로 서게 되는
    /// 칸(입구·적 스폰·계단)만 고른다 — 합성 케이스는 이미 `ShadowcastFovTests`가 덮는다.
    /// </para>
    /// </summary>
    public class FovRoomBoundaryTests
    {
        private const int FieldOfViewRadius = 8;

        [Test]
        public void VisibleTiles_StayConnectedToTheViewer_AcrossTheFirstDungeon()
        {
            var failures = new StringBuilder();
            int checkedOrigins = 0;

            foreach (int seed in new[] { 1, 7, 23, 1977 })
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(
                    map, 13, 13, 10, seed: seed,
                    direction: DungeonProgressDirection.Ascend, firstBuildingFloor: -2);

                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    int minElevation = layout.Height.Elevation(floor.FloorIndex);
                    int maxElevation = minElevation + layout.Height.ElevationsPerFloor - 1;

                    foreach (GridPos origin in OriginsOn(floor))
                    {
                        if (!map.Has(origin)) continue;
                        checkedOrigins++;

                        HashSet<GridPos> visible = GridVisibility.Compute(
                            map, origin, minElevation, maxElevation, FieldOfViewRadius);

                        List<GridPos> stranded = Disconnected(visible, origin);
                        if (stranded.Count == 0) continue;

                        failures.Append($"seed {seed} floor {floor.FloorIndex} 원점 {origin}: ")
                            .Append($"끊긴 가시 칸 {stranded.Count}개 — ");
                        for (int i = 0; i < stranded.Count && i < 6; i++)
                            failures.Append(stranded[i]).Append(' ');
                        failures.Append('\n');
                    }
                }
            }

            Assert.Greater(checkedOrigins, 0, "원점을 하나도 못 골랐다 — 테스트가 헛돌고 있다");
            Assert.IsEmpty(
                failures.ToString(),
                $"FOV가 방 경계를 건너뛰었다 (원점 {checkedOrigins}개 검사):\n{failures}");
        }

        /// <summary>플레이어가 실제로 서게 되는 칸들. 임의 좌표를 넣으면 void 위 시야를 재게 된다.</summary>
        private static IEnumerable<GridPos> OriginsOn(DungeonFloorInfo floor)
        {
            yield return floor.Entry;
            if (floor.UpStairs.HasValue) yield return floor.UpStairs.Value;
            if (floor.DownStairs.HasValue) yield return floor.DownStairs.Value;
            if (floor.RestSite.HasValue) yield return floor.RestSite.Value;
            foreach (GridPos spawn in floor.EnemySpawns) yield return spawn;
        }

        /// <summary>
        /// 가시 칸을 (x, y) 컬럼으로 접어 원점에서 8방향 BFS 한다. 높이를 접는 이유는
        /// 같은 컬럼의 지면과 머리 위 구조물이 함께 잡히기 때문이다(span 해석).
        /// </summary>
        private static List<GridPos> Disconnected(HashSet<GridPos> visible, GridPos origin)
        {
            var columns = new HashSet<(int x, int y)>();
            foreach (GridPos pos in visible) columns.Add((pos.x, pos.y));

            var reached = new HashSet<(int x, int y)>();
            var queue = new Queue<(int x, int y)>();
            var start = (origin.x, origin.y);
            if (columns.Contains(start))
            {
                reached.Add(start);
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                (int x, int y) cell = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = (cell.x + dx, cell.y + dy);
                    if (!columns.Contains(next) || !reached.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }

            var stranded = new List<GridPos>();
            foreach (GridPos pos in visible)
                if (!reached.Contains((pos.x, pos.y)))
                    stranded.Add(pos);
            return stranded;
        }
    }
}
