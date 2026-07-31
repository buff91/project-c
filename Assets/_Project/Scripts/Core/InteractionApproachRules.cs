using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 문·적·프롭처럼 직접 밟지 않고 옆에서 사용하는 대상까지의 접근 경로 규칙.
    /// 대상의 북→동→남→서 인접 칸 중 도달 가능한 최단 경로를 결정론적으로 고른다.
    /// </summary>
    public static class InteractionApproachRules
    {
        /// <summary>
        /// 접근 경로의 성공 조건. 대상 칸을 밟는 것이 아니라 같은 높이의 상하좌우 한 칸에
        /// 도착해야 한다. 경로 생산자와 상호작용 소비자가 이 계약을 함께 쓴다.
        /// </summary>
        public static bool IsAdjacent(GridPos actor, GridPos target) =>
            actor.elevation == target.elevation && actor.ManhattanTo(target) == 1;

        public static List<GridPos> FindPathToAdjacent(
            GridMap map,
            GridPos start,
            GridPos target,
            Func<GridPos, bool> isOccupied = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var candidates = new[]
            {
                target.North,
                target.East,
                target.South,
                target.West
            };
            List<GridPos> best = null;

            foreach (GridPos candidate in candidates)
            {
                if (!map.IsWalkable(candidate))
                    continue;

                // 점유자를 고려한 채 경로를 찾아야 한다. 점유자를 무시한 최단 경로를
                // 먼저 만든 뒤 폐기하면, 조금 더 긴 안전한 우회로가 있어도 실패한다.
                List<GridPos> path = GridPathfinder.FindPath(
                    map,
                    start,
                    candidate,
                    isOccupied);
                if (path.Count > 0 && (best == null || path.Count < best.Count))
                    best = path;
            }

            return best ?? new List<GridPos>();
        }
    }
}
