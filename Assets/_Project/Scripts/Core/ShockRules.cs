using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 감전 반응 (GDD §5.5 젖음+감전 → 광역 데미지, 포스트아포 신규 축: 노출 전선 + 침수 바닥).
    /// 물은 도체다 — 감전이 젖은 타일에 닿으면 4방향으로 이어진 웅덩이 전체가 통전해 그 위
    /// 살아있는 대상 전원을 지진다. 마른 칸엔 전파되지 않으므로 "적을 먼저 웅덩이로 몰아넣는"
    /// 셋업 전술의 후반 도구. WaterRules.ChainFreeze 와 같은 계약 — 통전된 칸 목록만 반환하고
    /// 연출은 호출부가 처리한다. (트리거=감전 수류탄/노출 전선 배선은 Gameplay)
    /// </summary>
    public static class ShockRules
    {
        // 4방향 통전 BFS 이웃 오프셋. 매 셀마다 배열을 새로 만들지 않도록 하나만 재사용한다.
        // 순서(+x, -x, +y, -y)는 energized 반환 순서에 영향을 주므로 바꾸지 않는다.
        private static readonly (int dx, int dy)[] Cardinals =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        /// <summary>
        /// center 3×3 블라스트로 직접 대상을 지지고, 블라스트가 닿은 젖은 웅덩이를 4방향으로
        /// 통전시켜 그 위 대상도 지진다. 각 대상은 최대 한 번만 피해. 통전된(젖은) 칸 목록 반환.
        /// </summary>
        public static List<GridPos> Discharge(
            GridMap map, GridPos center, IReadOnlyList<CombatantState> combatants, int damage)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));

            var shocked = new HashSet<CombatantState>();

            // 1) 직접 블라스트(3×3, 마른 바닥 포함).
            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
                DamageAt(combatants, center.Offset(dx, dy), damage, shocked);

            // 2) 젖은 웅덩이 통전 — 블라스트 안 젖은 칸에서 4방향 BFS.
            var energized = new List<GridPos>();
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
                energized.Add(pos);
                DamageAt(combatants, pos, damage, shocked);
                foreach (var (dx2, dy2) in Cardinals)
                {
                    GridPos next = pos.Offset(dx2, dy2);
                    if (map.Get(next)?.wet == true && visited.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return energized;
        }

        private static void DamageAt(
            IReadOnlyList<CombatantState> combatants, GridPos pos, int damage, HashSet<CombatantState> shocked)
        {
            if (combatants == null) return;
            foreach (CombatantState c in combatants)
            {
                if (c == null || !c.IsAlive) continue;
                if (c.Position != pos) continue;
                if (!shocked.Add(c)) continue; // 한 대상은 한 번만
                c.TakeDamage(damage);
            }
        }
    }
}
