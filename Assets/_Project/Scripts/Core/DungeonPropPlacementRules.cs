using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 상호작용 가능한 던전 프롭의 안전한 초기 배치 규칙.
    /// 플레이어 입구와 필수 동선/점유 좌표를 덮는 위험 프롭을 허용하지 않는다.
    /// </summary>
    public static class DungeonPropPlacementRules
    {
        public const int MinimumEntryDistance = 2;

        private static readonly (int dx, int dy)[] CardinalDirections =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        public static bool TrySelectSafePosition(
            GridMap map,
            GridPos entry,
            IEnumerable<GridPos> orderedCandidates,
            ISet<GridPos> reserved,
            out GridPos selected)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (orderedCandidates == null)
                throw new ArgumentNullException(nameof(orderedCandidates));

            GridPos fallback = default;
            bool hasFallback = false;
            foreach (GridPos candidate in orderedCandidates)
            {
                if (candidate == entry ||
                    candidate.ManhattanTo(entry) < MinimumEntryDistance ||
                    ProjectsToEntryColumn(candidate, entry) ||
                    (reserved != null && reserved.Contains(candidate)))
                    continue;

                TileData tile = map.Get(candidate);
                if (tile == null || tile.kind != TileKind.Floor)
                    continue;

                if (GridPathfinder.FindPath(map, entry, candidate).Count == 0)
                    continue;

                if (!hasFallback)
                {
                    fallback = candidate;
                    hasFallback = true;
                }

                // 위험 프롭은 시작 화면 중앙보다 벽/외곽의 드레싱으로 읽혀야 한다.
                // 다만 구석에 박아 상호작용을 잃지 않도록, 플레이어가 실제로 접근해
                // 일반 바닥으로 한 칸 밀 수 있는 후보만 가장자리 선호 대상으로 삼는다.
                if (IsNearPerimeterOrWall(map, candidate) &&
                    HasReachablePushLane(map, entry, candidate, reserved))
                {
                    selected = candidate;
                    return true;
                }
            }

            if (hasFallback)
            {
                selected = fallback;
                return true;
            }

            selected = default;
            return false;
        }

        private static bool IsNearPerimeterOrWall(GridMap map, GridPos candidate)
        {
            foreach ((int dx, int dy) in CardinalDirections)
            {
                TileData neighbor = map.Get(candidate.Offset(dx, dy));
                if (neighbor == null || neighbor.kind == TileKind.Wall)
                    return true;
            }

            return false;
        }

        private static bool HasReachablePushLane(
            GridMap map,
            GridPos entry,
            GridPos candidate,
            ISet<GridPos> reserved)
        {
            foreach ((int dx, int dy) in CardinalDirections)
            {
                GridPos approach = candidate.Offset(-dx, -dy);
                if (!map.IsWalkable(approach) ||
                    (reserved != null && reserved.Contains(approach)))
                    continue;

                List<GridPos> path = GridPathfinder.FindPath(
                    map,
                    entry,
                    approach,
                    pos => pos == candidate || (reserved != null && reserved.Contains(pos)));
                if (path.Count == 0)
                    continue;

                KnockbackOutcome outcome = KnockbackRules.Resolve(
                    map,
                    approach,
                    candidate,
                    pos => reserved != null && reserved.Contains(pos),
                    out GridPos destination);
                if (outcome == KnockbackOutcome.Pushed &&
                    map.Get(destination)?.kind == TileKind.Floor)
                    return true;
            }

            return false;
        }

        private static bool ProjectsToEntryColumn(GridPos candidate, GridPos entry)
        {
            int deltaX = Math.Abs(candidate.x - entry.x);
            int deltaY = Math.Abs(candidate.y - entry.y);
            return deltaX == deltaY;
        }
    }
}
