using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// M1 아이템 최소 셋: 물약(회복), 폭탄(광역 피해 + 약한 바닥 붕괴). (GDD §5.6, §5.8)
    /// </summary>
    public enum ItemKind
    {
        Potion = 0,  // 회복 물약
        Bomb = 1     // 폭탄. M4에서 연쇄 폭발/낙하 트리거와 통합.
    }

    /// <summary>던전 생성기가 배치하는 아이템 스폰 지점. 타일이 아니라 타일 위의 오브젝트다.</summary>
    public readonly struct ItemSpawn
    {
        public readonly GridPos Position;
        public readonly ItemKind Kind;

        public ItemSpawn(GridPos position, ItemKind kind)
        {
            Position = position;
            Kind = kind;
        }

        public override string ToString() => $"{Kind}@{Position}";
    }

    /// <summary>종류별 개수만 세는 최소 인벤토리. 장비/스택 상한은 콘텐츠 확장 때 붙인다.</summary>
    public sealed class Inventory
    {
        private readonly Dictionary<ItemKind, int> _counts = new Dictionary<ItemKind, int>();

        public int Count(ItemKind kind) => _counts.TryGetValue(kind, out int count) ? count : 0;

        public int Add(ItemKind kind, int amount = 1)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int next = Count(kind) + amount;
            _counts[kind] = next;
            return next;
        }

        public bool TryUse(ItemKind kind)
        {
            int count = Count(kind);
            if (count <= 0) return false;
            _counts[kind] = count - 1;
            return true;
        }

        public void Clear() => _counts.Clear();
    }

    public sealed class BombResult
    {
        public readonly List<CombatantState> Damaged = new List<CombatantState>();
        public readonly List<GridPos> CollapsedWeakFloors = new List<GridPos>();
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
                if (map.Get(pos)?.kind != TileKind.WeakFloor) continue;
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
