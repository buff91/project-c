using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 시작 공간의 낮은 비충돌 드레싱을 고르는 순수 규칙.
    /// 동선을 막지는 않지만, 입구·예약 좌표·방 중앙을 시각적으로 덮지 않게 외곽만 사용한다.
    /// </summary>
    public static class DungeonDressingPlacementRules
    {
        public const int MinimumEntryDistance = 2;
        public const int MaximumEntryPathLength = 8;
        public const int MinimumDressingSpacing = 3;

        private static readonly (int dx, int dy)[] CardinalDirections =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        /// <summary>
        /// 월드에 고정된 소품 방향을 현재 4분기 시점의 화면 방향으로 바꾼다.
        /// <see cref="IsoGrid.RotateToView(float, float)"/>와 같은 회전 부호를 사용한다.
        /// </summary>
        public static int ResolveViewIndex(
            int worldFacingQuarterTurns,
            int viewQuarterTurns)
        {
            int effective = (worldFacingQuarterTurns + viewQuarterTurns) % 4;
            return effective < 0 ? effective + 4 : effective;
        }

        public static IReadOnlyList<GridPos> SelectSafePositions(
            GridMap map,
            GridPos entry,
            IEnumerable<GridPos> orderedCandidates,
            ISet<GridPos> reserved,
            int maximumCount)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (orderedCandidates == null)
                throw new ArgumentNullException(nameof(orderedCandidates));
            if (maximumCount <= 0) return Array.Empty<GridPos>();

            var selected = new List<GridPos>(maximumCount);
            foreach (GridPos candidate in orderedCandidates)
            {
                if (selected.Count >= maximumCount) break;
                if (candidate.elevation != entry.elevation ||
                    candidate == entry ||
                    candidate.ManhattanTo(entry) < MinimumEntryDistance ||
                    (reserved != null && reserved.Contains(candidate)))
                    continue;

                TileData tile = map.Get(candidate);
                if (tile == null || tile.kind != TileKind.Floor ||
                    !IsNearPerimeterOrWall(map, candidate))
                    continue;

                List<GridPos> path = GridPathfinder.FindPath(map, entry, candidate);
                int pathLength = path.Count - 1;
                if (path.Count == 0 || pathLength > MaximumEntryPathLength)
                    continue;

                bool overlapsExisting = false;
                foreach (GridPos existing in selected)
                {
                    if (candidate.ManhattanTo(existing) < MinimumDressingSpacing)
                    {
                        overlapsExisting = true;
                        break;
                    }
                }
                if (overlapsExisting) continue;

                selected.Add(candidate);
            }

            return selected;
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
    }
}
