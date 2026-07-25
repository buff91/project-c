using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 작은 턴제 격자를 위한 결정론적 A* 경로 탐색.
    /// 같은 elevation 이동과 <b>계단</b>을 통한 한 단계 높이 변화만 허용한다.
    ///
    /// <para>
    /// <b>사다리는 걸어서 못 지나간다.</b> 예전에는 계단과 사다리가 같은 조건식에 묶여 있어
    /// A* 가 사다리를 "그냥 걸어 올라가는 계단"으로 봤고, 그래서 사다리는 사실상
    /// 스프라이트만 다른 계단이었다(HUD 가 약속하는 탭/Space 는 대체 경로일 뿐이었다).
    /// 이제 사다리는 <b>명시적 링크로만</b> 통과하며 그 링크는 <c>canClimb</c> 가 열어 준다 —
    /// 높은 곳은 사다리로만 닿고, 못 오르는 적은 거기까지 따라오지 못한다.
    /// </para>
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly (int dx, int dy)[] Directions =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        /// <param name="isBlocked">true 를 반환하는 칸은 점유된 것으로 보고 우회한다(시작 칸 제외).</param>
        /// <param name="openClosedDoors">닫힌 문을 "열고 지나갈 수 있는" 칸으로 취급한다(몬스터 추격용).</param>
        /// <param name="canClimb">
        /// 사다리 링크를 탈 수 있는가. <b>기본값이 true 인 이유</b>: 호출부 대부분이
        /// 플레이어 이동이거나 "여기에 닿을 수 있나"를 묻는 도달성 검사라 그쪽이 정상값이다.
        /// <b>몬스터는 반드시 자기 아키타입 값을 명시적으로 넘긴다</b>
        /// (<see cref="MonsterArchetype.CanClimb"/>) — 안 넘기면 전부 오르게 되어 이 축이 죽는다.
        /// </param>
        public static List<GridPos> FindPath(
            GridMap map,
            GridPos start,
            GridPos goal,
            Func<GridPos, bool> isBlocked = null,
            bool openClosedDoors = false,
            bool canClimb = true)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!IsEnterable(map, start, openClosedDoors) || !IsEnterable(map, goal, openClosedDoors))
                return new List<GridPos>();
            if (isBlocked != null && isBlocked(goal))
                return new List<GridPos>();
            if (start == goal)
                return new List<GridPos> { start };

            var open = new List<GridPos> { start };
            var closed = new HashSet<GridPos>();
            var cameFrom = new Dictionary<GridPos, GridPos>();
            var gScore = new Dictionary<GridPos, int> { [start] = 0 };

            while (open.Count > 0)
            {
                int bestIndex = FindBestIndex(open, gScore, goal);
                GridPos current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (current == goal)
                    return ReconstructPath(cameFrom, current);

                closed.Add(current);

                foreach (GridPos next in
                         EnumerateNeighbors(map, current, isBlocked, openClosedDoors, canClimb))
                {
                    if (closed.Contains(next)) continue;

                    int tentative = gScore[current] + 1;
                    if (gScore.TryGetValue(next, out int known) && tentative >= known)
                        continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    if (!open.Contains(next))
                        open.Add(next);
                }
            }

            return new List<GridPos>();
        }

        private static bool IsEnterable(GridMap map, GridPos pos, bool openClosedDoors)
        {
            TileData tile = map.Get(pos);
            if (tile == null) return false;
            return tile.IsWalkable || (openClosedDoors && tile.kind == TileKind.DoorClosed);
        }

        private static IEnumerable<GridPos> EnumerateNeighbors(
            GridMap map,
            GridPos current,
            Func<GridPos, bool> isBlocked,
            bool openClosedDoors,
            bool canClimb)
        {
            TileData currentTile = map.Get(current);

            foreach (var direction in Directions)
            {
                for (int elevationDelta = -1; elevationDelta <= 1; elevationDelta++)
                {
                    var candidate = new GridPos(
                        current.x + direction.dx,
                        current.y + direction.dy,
                        current.elevation + elevationDelta);

                    if (!IsEnterable(map, candidate, openClosedDoors)) continue;
                    if (isBlocked != null && isBlocked(candidate)) continue;

                    TileData candidateTile = map.Get(candidate);
                    bool changesHeight = elevationDelta != 0;
                    // 걸어서 높이가 바뀌는 것은 **계단뿐**이고 ±1 단이다.
                    // 사다리는 여기 없다 — 아래 링크 순회에서 canClimb 가 열어 준다.
                    // (사다리 칸 자체에는 같은 높이로 걸어 올라설 수 있다. 못 하는 것은 "타고 오르기"다.)
                    bool usesStairs =
                        currentTile.kind == TileKind.Stairs ||
                        candidateTile.kind == TileKind.Stairs;
                    if (!changesHeight || usesStairs)
                        yield return candidate;
                }
            }

            foreach (GridPos linked in map.LinksFrom(current))
            {
                if (!map.IsWalkable(linked)) continue;
                if (isBlocked != null && isBlocked(linked)) continue;
                if (!canClimb && IsLadderLink(map, current, linked)) continue;
                yield return linked;
            }
        }

        /// <summary>
        /// 이 링크가 <b>사다리</b>인가 — 한쪽 끝이라도 사다리 타일이면 그렇다.
        /// <para>
        /// 층 전환 계단(<c>StairsUp/Down</c>) 링크는 어느 쪽도 사다리가 아니라 걸리지 않는다.
        /// 엘리베이터는 사다리 타일을 재사용하므로 함께 막히는데, 그래도 맞다 —
        /// 복귀 전용 설비라 애초에 몬스터가 탈 것이 아니다.
        /// </para>
        /// </summary>
        private static bool IsLadderLink(GridMap map, GridPos from, GridPos to) =>
            map.Get(from)?.kind == TileKind.Ladder || map.Get(to)?.kind == TileKind.Ladder;

        private static int FindBestIndex(List<GridPos> open, Dictionary<GridPos, int> gScore, GridPos goal)
        {
            int bestIndex = 0;
            int bestScore = TotalScore(open[0], gScore, goal);

            for (int i = 1; i < open.Count; i++)
            {
                int score = TotalScore(open[i], gScore, goal);
                if (score < bestScore)
                {
                    bestIndex = i;
                    bestScore = score;
                }
            }

            return bestIndex;
        }

        private static int TotalScore(GridPos pos, Dictionary<GridPos, int> gScore, GridPos goal)
        {
            // 명시적 층간 링크는 elevation과 x/y를 크게 건너뛸 수 있으므로
            // 휴리스틱을 0으로 둔 Dijkstra 형태가 최단 경로를 안전하게 보장한다.
            return gScore[pos];
        }

        private static List<GridPos> ReconstructPath(Dictionary<GridPos, GridPos> cameFrom, GridPos current)
        {
            var path = new List<GridPos> { current };
            while (cameFrom.TryGetValue(current, out GridPos previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
