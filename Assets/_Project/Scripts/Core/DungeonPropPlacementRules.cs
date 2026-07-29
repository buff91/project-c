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

                selected = candidate;
                return true;
            }

            selected = default;
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
