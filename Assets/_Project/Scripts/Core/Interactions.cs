using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 기름 표면 규칙 (GDD §5.5 요소 반응 — 불+기름).
    /// 살포와 발화만 담당하고, 화상 부여·연출은 Gameplay 가 반환 목록으로 처리한다.
    /// </summary>
    public static class OilRules
    {
        /// <summary>중심 3×3의 걷기 가능한 타일에 기름을 뿌리고 젖은 칸 목록을 반환한다.</summary>
        public static List<GridPos> Splash(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var splashed = new List<GridPos>();
            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                TileData tile = map.Get(pos);
                if (tile == null || !tile.IsWalkable || tile.kind == TileKind.Hole) continue;
                if (tile.wet) continue; // 젖은 바닥에는 기름이 붙지 않는다 (GDD §5.5)
                if (!tile.oiled)
                {
                    tile.oiled = true;
                    splashed.Add(pos);
                }
            }
            return splashed;
        }

        /// <summary>
        /// 불 폭발이 닿은 반경의 기름 타일을 발화시킨다.
        /// 기름을 지우고 발화한 칸 목록을 반환한다 — 그 위의 대상 화상은 호출부가 처리.
        /// </summary>
        public static List<GridPos> Ignite(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var ignited = new List<GridPos>();
            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                TileData tile = map.Get(pos);
                if (tile == null || !tile.oiled) continue;
                tile.oiled = false;
                ignited.Add(pos);
            }
            return ignited;
        }
    }

    public sealed class BombResult
    {
        public readonly List<CombatantState> Damaged = new List<CombatantState>();
        public readonly List<GridPos> CollapsedWeakFloors = new List<GridPos>();
        public readonly List<GridPos> ShatteredWindows = new List<GridPos>();
    }

    /// <summary>
    /// 폭탄 투척/폭발의 순수 로직. 폭발은 같은 elevation의 3×3.
    /// 플레이어도 같은 규칙으로 피해를 입는다(자폭 가능 = 긴장, GDD §5.3 원칙).
    /// </summary>
    public static class BombRules
    {
        /// <summary>폭발 반경(체비셰프 거리). 1 = 3×3.</summary>
        public const int BlastRadius = 1;

        public static bool CanThrow(GridMap map, GridPos from, GridPos target, int maxRange)
        {
            return map != null &&
                   from.elevation == target.elevation &&
                   from.ManhattanTo(target) <= maxRange &&
                   map.IsSolidGround(target) &&
                   CombatRules.HasLineOfSight(map, from, target);
        }

        public static bool InBlast(GridPos center, GridPos pos) =>
            pos.elevation == center.elevation && center.ChebyshevTo(pos) <= BlastRadius;

        /// <summary>
        /// 폭발 처리: 반경 내 살아있는 전투 참가자 전원에게 피해를 준 뒤,
        /// (죽었거나 비어서) 아무도 서 있지 않은 약한 바닥을 구멍으로 붕괴시킨다.
        /// 붕괴로 생긴 구멍 위의 낙하 처리는 M4 TryFall()에서 통합한다.
        /// </summary>
        public static BombResult Detonate(
            GridMap map,
            GridPos center,
            IReadOnlyList<CombatantState> combatants,
            int damage)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));

            var result = new BombResult();
            if (combatants != null)
            {
                foreach (CombatantState combatant in combatants)
                {
                    if (combatant == null || !combatant.IsAlive) continue;
                    if (!InBlast(center, combatant.Position)) continue;
                    combatant.TakeDamage(damage);
                    result.Damaged.Add(combatant);
                }
            }

            for (int dx = -BlastRadius; dx <= BlastRadius; dx++)
            for (int dy = -BlastRadius; dy <= BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                TileData tile = map.Get(pos);
                if (tile == null) continue;

                // 폭발은 유리를 깬다(포스트아포: 폭풍·파편) — 창문은 통로가 된다. (GDD §5.2)
                if (tile.CanBreak)
                {
                    if (WindowRules.TryBreak(map, pos))
                        result.ShatteredWindows.Add(pos);
                    continue;
                }

                // (죽었거나 비어서) 아무도 없는 약한 바닥은 구멍으로 붕괴한다.
                if (tile.kind != TileKind.WeakFloor) continue;
                if (IsOccupiedByLiving(combatants, pos)) continue;
                map.Set(pos, TileKind.Hole);
                result.CollapsedWeakFloors.Add(pos);
            }

            return result;
        }

        private static bool IsOccupiedByLiving(IReadOnlyList<CombatantState> combatants, GridPos pos)
        {
            if (combatants == null) return false;
            foreach (CombatantState combatant in combatants)
            {
                if (combatant != null && combatant.IsAlive && combatant.Position == pos)
                    return true;
            }
            return false;
        }
    }
}
