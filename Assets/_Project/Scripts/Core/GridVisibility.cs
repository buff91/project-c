using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 현재 플레이 프로토타입의 제한 시야 계산.
    /// 닫힌 문/벽은 그 칸까지 보이지만 너머로 시야가 전파되지 않는다.
    /// 추후 Recursive Shadowcasting으로 교체해도 호출부는 HashSet 결과만 소비한다.
    /// </summary>
    public static class GridVisibility
    {
        private static readonly (int dx, int dy)[] Directions =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        public static HashSet<GridPos> Compute(
            GridMap map,
            GridPos origin,
            int minElevation,
            int maxElevation,
            int radius)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            var visible = new HashSet<GridPos>();
            if (!map.Has(origin)) return visible;

            var queue = new Queue<(GridPos pos, int distance)>();
            visible.Add(origin);
            queue.Enqueue((origin, 0));

            while (queue.Count > 0)
            {
                (GridPos current, int distance) = queue.Dequeue();
                if (distance >= radius) continue;

                TileData currentTile = map.Get(current);
                foreach ((int dx, int dy) in Directions)
                {
                    for (int elevationDelta = -1; elevationDelta <= 1; elevationDelta++)
                    {
                        int elevation = current.elevation + elevationDelta;
                        if (elevation < minElevation || elevation > maxElevation) continue;

                        var candidate = new GridPos(
                            current.x + dx,
                            current.y + dy,
                            elevation);
                        TileData tile = map.Get(candidate);
                        if (tile == null) continue;

                        bool changesHeight = elevationDelta != 0;
                        bool usesStairs = currentTile.kind == TileKind.Stairs || tile.kind == TileKind.Stairs;
                        if (changesHeight && !usesStairs) continue;

                        if (!visible.Add(candidate)) continue;

                        // 닫힌 문/벽/구멍은 경계 자체는 보이지만 그 너머를 드러내지 않는다.
                        if (!tile.BlocksSight && !tile.CausesFall)
                            queue.Enqueue((candidate, distance + 1));
                    }
                }
            }

            return visible;
        }
    }
}
