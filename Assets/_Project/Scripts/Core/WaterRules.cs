using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 젖은 웅덩이의 4방향 확산 — 결빙(<see cref="WaterRules.ChainFreeze"/>)과
    /// 감전(<see cref="ShockRules.DischargeDetailed"/>)이 공유하는 단 하나의 순회.
    /// <para>
    /// <b>왜 한 곳인가</b>: 두 반응은 "블라스트에 닿은 젖은 칸에서 시작해 이어진 웅덩이 전체로
    /// 번진다"는 같은 사실을 말한다. 두 벌로 두면 한쪽만 대각선을 허용하거나 씨앗 조건이 갈리는
    /// 순간, 같은 웅덩이가 얼 때와 통전될 때 다른 모양이 된다 — 규칙이 깨진 것으로 읽힌다.
    /// </para>
    /// <para>
    /// <b>순서는 계약이다.</b> 씨앗은 블라스트 순서(dx 외곽 → dy 내곽), 확장은 (+x, −x, +y, −y)이며
    /// 반환 목록의 순서가 곧 연출 순서다. 바꾸면 조용히 다른 그림이 나온다.
    /// </para>
    /// </summary>
    public static class WetPoolFlood
    {
        // 4방향 이웃 오프셋. 매 셀 배열 할당을 피하려고 하나만 재사용한다.
        private static readonly (int dx, int dy)[] Cardinals =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        /// <summary>
        /// <paramref name="center"/> 블라스트에 닿은 젖은 칸에서 시작해 이어진 웅덩이를 모은다.
        /// <paramref name="onVisit"/>은 칸을 목록에 담는 시점에 호출되므로(감전 피해처럼)
        /// 순회 순서에 기대는 부수 효과를 안전하게 붙일 수 있다.
        /// </summary>
        public static List<GridPos> Collect(GridMap map, GridPos center, Action<GridPos> onVisit = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var flooded = new List<GridPos>();
            var visited = new HashSet<GridPos>();
            var frontier = new Queue<GridPos>();

            BombRules.ForEachBlastCell(center, pos =>
            {
                if (map.Get(pos)?.wet == true && visited.Add(pos))
                    frontier.Enqueue(pos);
            });

            while (frontier.Count > 0)
            {
                GridPos pos = frontier.Dequeue();
                flooded.Add(pos);
                onVisit?.Invoke(pos);

                foreach (var (dx, dy) in Cardinals)
                {
                    GridPos next = pos.Offset(dx, dy);
                    if (map.Get(next)?.wet == true && visited.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return flooded;
        }
    }

    /// <summary>
    /// 물/젖음 표면 규칙 (GDD §5.5 요소 반응 — 물+빙결 → 광역 결빙).
    /// OilRules 와 같은 계약: 타일 상태 변화와 대상 칸 목록만 반환하고,
    /// 상태이상 부여·연출은 Gameplay 가 반환 목록으로 처리한다.
    /// </summary>
    public static class WaterRules
    {
        /// <summary>
        /// 냉기 폭발 반경 안에 젖은 타일이 있으면, 그 타일들과 4방향으로 이어진
        /// 웅덩이 전체로 결빙을 전파한다. 결빙된(젖음이 유지되는) 칸 목록을 반환 —
        /// 그 위에 서 있는 대상의 빙결은 호출부가 처리.
        /// </summary>
        public static List<GridPos> ChainFreeze(GridMap map, GridPos center) =>
            WetPoolFlood.Collect(map, center);

        /// <summary>불 폭발 반경의 젖은 타일을 증발시키고 말라버린 칸 목록을 반환한다.</summary>
        public static List<GridPos> Evaporate(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var dried = new List<GridPos>();
            BombRules.ForEachBlastCell(center, pos =>
            {
                TileData tile = map.Get(pos);
                if (tile == null || !tile.wet) return;
                tile.wet = false;
                dried.Add(pos);
            });
            return dried;
        }
    }
}
