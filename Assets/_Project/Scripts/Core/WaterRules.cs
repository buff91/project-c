using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
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
        public static List<GridPos> ChainFreeze(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var frozen = new List<GridPos>();
            var visited = new HashSet<GridPos>();
            var frontier = new Queue<GridPos>();

            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                if (map.Get(pos)?.wet == true && visited.Add(pos))
                    frontier.Enqueue(pos);
            }

            while (frontier.Count > 0)
            {
                GridPos pos = frontier.Dequeue();
                frozen.Add(pos);
                foreach (GridPos next in new[]
                         { pos.Offset(1, 0), pos.Offset(-1, 0), pos.Offset(0, 1), pos.Offset(0, -1) })
                {
                    if (map.Get(next)?.wet == true && visited.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return frozen;
        }

        /// <summary>불 폭발 반경의 젖은 타일을 증발시키고 말라버린 칸 목록을 반환한다.</summary>
        public static List<GridPos> Evaporate(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var dried = new List<GridPos>();
            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                TileData tile = map.Get(pos);
                if (tile == null || !tile.wet) continue;
                tile.wet = false;
                dried.Add(pos);
            }
            return dried;
        }
    }
}
