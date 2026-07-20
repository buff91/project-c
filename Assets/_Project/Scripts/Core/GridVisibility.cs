using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Recursive Shadowcasting FOV. (GDD §5.2, M2)
    /// (x, y) 2D로 8옥탄트를 캐스팅하되, 각 컬럼의 타일은 elevation 대역을
    /// 위→아래로 스캔한 첫 타일(=표면)로 해석하고 결과에도 그 표면 GridPos를 넣는다.
    ///
    /// 차단 규칙:
    /// - 타일이 없는 컬럼(void) = 불투명. 이 던전은 방 경계를 벽 타일이 아니라
    ///   타일 부재로 표현하므로(벽은 비주얼 전용), void가 투명하면 닫힌 문 뒤 방이
    ///   빈 공간 너머로 통째로 드러나 문 불변식이 깨진다.
    /// - Wall/DoorClosed = 그 칸 자체는 보이지만 너머로 전파되지 않는다.
    /// - Hole/WeakFloor/Stairs/DoorOpen = 투과. (CombatRules.HasLineOfSight와 동일 기준)
    /// - 1단 높이차(raised)는 시야를 막지 않는다. 필요해지면 여기의 표면 판정에
    ///   "표면 elevation − 눈높이 > 임계 → 불투명" 규칙을 추가한다.
    /// </summary>
    public static class GridVisibility
    {
        // 8옥탄트 좌표 변환 행렬.
        private static readonly int[] MultXx = { 1, 0, 0, -1, -1, 0, 0, 1 };
        private static readonly int[] MultXy = { 0, 1, -1, 0, 0, -1, 1, 0 };
        private static readonly int[] MultYx = { 0, 1, 1, 0, 0, -1, -1, 0 };
        private static readonly int[] MultYy = { 1, 0, 0, 1, -1, 0, 0, -1 };

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
            visible.Add(origin);

            for (int octant = 0; octant < 8; octant++)
            {
                CastOctant(
                    map, visible, origin, minElevation, maxElevation, radius,
                    1, 1.0, 0.0,
                    MultXx[octant], MultXy[octant], MultYx[octant], MultYy[octant]);
            }

            return visible;
        }

        /// <summary>컬럼 (x,y)의 표면 타일. 대역에 타일이 없으면 false(void).</summary>
        private static bool TryGetSurface(
            GridMap map,
            int x,
            int y,
            int minElevation,
            int maxElevation,
            out GridPos surface,
            out TileData tile)
        {
            for (int e = maxElevation; e >= minElevation; e--)
            {
                var pos = new GridPos(x, y, e);
                if (map.TryGet(pos, out tile))
                {
                    surface = pos;
                    return true;
                }
            }

            surface = default;
            tile = null;
            return false;
        }

        private static bool IsOpaque(GridMap map, int x, int y, int minElevation, int maxElevation)
        {
            return !TryGetSurface(map, x, y, minElevation, maxElevation, out _, out TileData tile) ||
                   tile.BlocksSight;
        }

        private static void CastOctant(
            GridMap map,
            HashSet<GridPos> visible,
            GridPos origin,
            int minElevation,
            int maxElevation,
            int radius,
            int row,
            double start,
            double end,
            int xx,
            int xy,
            int yx,
            int yy)
        {
            if (start < end) return;

            for (int j = row; j <= radius; j++)
            {
                int dx = -j - 1;
                int dy = -j;
                bool blocked = false;
                double newStart = start;

                while (dx <= 0)
                {
                    dx++;
                    int mapX = origin.x + dx * xx + dy * xy;
                    int mapY = origin.y + dx * yx + dy * yy;
                    double leftSlope = (dx - 0.5) / (dy + 0.5);
                    double rightSlope = (dx + 0.5) / (dy - 0.5);

                    if (start < rightSlope) continue;
                    if (end > leftSlope) break;

                    // 옥탄트 좌표에서 Chebyshev 거리 == j ≤ radius 이므로 반경은 자동 만족.
                    if (TryGetSurface(map, mapX, mapY, minElevation, maxElevation,
                            out GridPos surface, out _))
                        visible.Add(surface);

                    if (blocked)
                    {
                        if (IsOpaque(map, mapX, mapY, minElevation, maxElevation))
                        {
                            newStart = rightSlope;
                        }
                        else
                        {
                            blocked = false;
                            start = newStart;
                        }
                    }
                    else if (IsOpaque(map, mapX, mapY, minElevation, maxElevation) && j < radius)
                    {
                        blocked = true;
                        CastOctant(
                            map, visible, origin, minElevation, maxElevation, radius,
                            j + 1, start, leftSlope, xx, xy, yx, yy);
                        newStart = rightSlope;
                    }
                }

                if (blocked) break;
            }
        }
    }
}
