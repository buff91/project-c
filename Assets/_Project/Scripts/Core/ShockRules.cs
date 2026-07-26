using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public sealed class ShockResult
    {
        public List<GridPos> Energized { get; }
        public List<CombatantState> Damaged { get; }

        internal ShockResult(List<GridPos> energized, List<CombatantState> damaged)
        {
            Energized = energized;
            Damaged = damaged;
        }
    }

    /// <summary>
    /// 감전 반응 (GDD §5.5 젖음+감전 → 광역 데미지, 포스트아포 신규 축: 노출 전선 + 침수 바닥).
    /// 물은 도체다 — 감전이 젖은 타일에 닿으면 4방향으로 이어진 웅덩이 전체가 통전해 그 위
    /// 살아있는 대상 전원을 지진다. 마른 칸엔 전파되지 않으므로 "적을 먼저 웅덩이로 몰아넣는"
    /// 셋업 전술의 후반 도구. WaterRules.ChainFreeze 와 같은 계약 — 통전된 칸 목록만 반환하고
    /// 연출은 호출부가 처리한다. (트리거=감전 수류탄/노출 전선 배선은 Gameplay)
    /// </summary>
    public static class ShockRules
    {
        /// <summary>
        /// center 3×3 블라스트로 직접 대상을 지지고, 블라스트가 닿은 젖은 웅덩이를 4방향으로
        /// 통전시켜 그 위 대상도 지진다. 각 대상은 최대 한 번만 피해. 통전된(젖은) 칸 목록 반환.
        /// </summary>
        public static List<GridPos> Discharge(
            GridMap map, GridPos center, IReadOnlyList<CombatantState> combatants, int damage) =>
            DischargeDetailed(map, center, combatants, damage).Energized;

        /// <summary>
        /// <see cref="Discharge"/>와 같은 판정을 수행하되 피격 대상도 함께 반환한다.
        /// Gameplay가 전도 피해의 FOV·사망 연출을 기존 피격 경로로 보낼 때 사용한다.
        /// </summary>
        public static ShockResult DischargeDetailed(
            GridMap map,
            GridPos center,
            IReadOnlyList<CombatantState> combatants,
            int damage)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));

            var shocked = new HashSet<CombatantState>();
            var damaged = new List<CombatantState>();

            // 1) 직접 블라스트(3×3, 마른 바닥 포함).
            BombRules.ForEachBlastCell(
                center, pos => DamageAt(combatants, pos, damage, shocked, damaged));

            // 2) 젖은 웅덩이 통전 — 결빙과 같은 확산을 쓰고, 지지는 것만 얹는다.
            //    각 대상은 shocked 집합이 막아 최대 한 번만 맞는다.
            List<GridPos> energized = WetPoolFlood.Collect(
                map, center, pos => DamageAt(combatants, pos, damage, shocked, damaged));

            return new ShockResult(energized, damaged);
        }

        private static void DamageAt(
            IReadOnlyList<CombatantState> combatants,
            GridPos pos,
            int damage,
            HashSet<CombatantState> shocked,
            List<CombatantState> damaged)
        {
            if (combatants == null) return;
            foreach (CombatantState c in combatants)
            {
                if (c == null || !c.IsAlive) continue;
                if (c.Position != pos) continue;
                if (!shocked.Add(c)) continue; // 한 대상은 한 번만
                c.TakeDamage(damage);
                damaged.Add(c);
            }
        }
    }
}
