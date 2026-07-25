using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Recursive Shadowcasting FOV. (GDD §5.2, M2)
    /// (x, y) 2D로 8옥탄트를 캐스팅하되, 각 컬럼의 해석은 <see cref="SightRules.ViewColumn"/>에
    /// 위임한다 — 시야 판정의 단일 출처를 전투 LoS와 공유하기 위해서다(3D 시야선 3단계).
    ///
    /// 컬럼은 높이맵이 아니라 <b>span</b>으로 본다: 한 컬럼에 솔리드 구간이 여럿일 수 있으므로
    /// (올라온 단 위에 얹힌 캐치워크 등) 눈높이 이하의 <b>지면</b>과 눈높이 위의
    /// <b>머리 위 구조물</b>을 각각 결과에 넣는다. 그래서 캐치워크 아래 바닥도 실제로 보인다.
    ///
    /// 차단 규칙:
    /// - 타일이 없는 컬럼(void) = 불투명. 이 던전은 방 경계를 벽 타일이 아니라
    ///   타일 부재로 표현하므로(벽은 비주얼 전용), void가 투명하면 닫힌 문 뒤 방이
    ///   빈 공간 너머로 통째로 드러나 문 불변식이 깨진다.
    /// - Wall/DoorClosed = 그 칸 자체는 보이지만 너머로 전파되지 않는다.
    /// - Hole/WeakFloor/Stairs/DoorOpen = 투과. (SightRules.HasLineOfSight와 동일 기준)
    /// - 눈높이보다 <see cref="SightRules.HeightBlockThreshold"/>를 초과해 높은 타일은
    ///   벽처럼 너머를 막는다. 1단(raised) 단차는 막지 않는다.
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

        /// <summary>컬럼에서 실제로 보이는 타일(지면·머리 위 구조물)을 결과에 넣는다.</summary>
        private static void AddVisibleTiles(HashSet<GridPos> visible, ColumnView column)
        {
            if (column.HasGround) visible.Add(column.Ground);
            if (column.HasOverhead) visible.Add(column.Overhead);
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
                    ColumnView column = SightRules.ViewColumn(
                        map, mapX, mapY, origin, minElevation, maxElevation);
                    AddVisibleTiles(visible, column);

                    if (blocked)
                    {
                        if (column.BlocksBeyond)
                        {
                            newStart = rightSlope;
                        }
                        else
                        {
                            blocked = false;
                            start = newStart;
                        }
                    }
                    else if (column.BlocksBeyond && j < radius)
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
