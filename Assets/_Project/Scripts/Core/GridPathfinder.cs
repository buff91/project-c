using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 작은 턴제 격자를 위한 결정론적 A* 경로 탐색.
    /// 같은 elevation 이동과 계단을 통한 한 단계 높이 변화만 허용한다.
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

        public static List<GridPos> FindPath(GridMap map, GridPos start, GridPos goal)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!map.IsWalkable(start) || !map.IsWalkable(goal))
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

                foreach (GridPos next in EnumerateNeighbors(map, current))
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

        private static IEnumerable<GridPos> EnumerateNeighbors(GridMap map, GridPos current)
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

                    TileData candidateTile = map.Get(candidate);
                    if (candidateTile == null || !candidateTile.IsWalkable)
                        continue;

                    bool changesHeight = elevationDelta != 0;
                    bool usesStairs = currentTile.kind == TileKind.Stairs || candidateTile.kind == TileKind.Stairs;
                    if (!changesHeight || usesStairs)
                        yield return candidate;
                }
            }

            foreach (GridPos linked in map.LinksFrom(current))
            {
                if (map.IsWalkable(linked))
                    yield return linked;
            }
        }

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
