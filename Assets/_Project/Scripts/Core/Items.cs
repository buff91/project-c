using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// M1 아이템 최소 셋: 물약(회복), 폭탄(광역 피해 + 약한 바닥 붕괴). (GDD §5.6, §5.8)
    /// </summary>
    public enum ItemKind
    {
        Potion = 0,        // 회복 물약
        Bomb = 1,          // 폭탄: 3×3 피해 + 화상 + 넉백, 약한 바닥 붕괴·폭발통 유폭.
        FrostBomb = 2,     // 냉기 폭탄: 낮은 피해 + 빙결. 불이 아니라 폭발통은 유폭하지 않는다.
        OilFlask = 3,      // 기름 병: 3×3 기름 살포. 불 폭발과 겹치면 발화 → 화상.
        ThrowingKnife = 4, // 투척 단검: 소모형 단일 대상 원거리 피해.
        RecallScroll = 5,  // 귀환 두루마리: 현재 층 입구로 순간이동.
        CoinPouch = 6,     // 전리품: 생환 시 골드로 환산. 던전 안에서는 쓸 수 없다.
        Gemstone = 7,      // 전리품(중): 생환 시 골드로 환산.
        Relic = 8          // 전리품(대): 희귀. 생환 시 골드로 환산.
    }

    /// <summary>아이템 표시 정보의 단일 출처 — 인벤토리/HUD 가 여기서 이름·설명을 읽는다.</summary>
    public static class ItemCatalog
    {
        public static readonly ItemKind[] AllKinds =
        {
            ItemKind.Potion, ItemKind.Bomb, ItemKind.FrostBomb,
            ItemKind.OilFlask, ItemKind.ThrowingKnife, ItemKind.RecallScroll,
            ItemKind.CoinPouch, ItemKind.Gemstone, ItemKind.Relic
        };

        /// <summary>생환 시 골드 환산 가치. 0 이면 소모품(창고 보관 대상).</summary>
        public static int GoldValue(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.CoinPouch: return 10;
                case ItemKind.Gemstone: return 25;
                case ItemKind.Relic: return 60;
                default: return 0;
            }
        }

        /// <summary>전리품(환금 전용) 여부. 던전 안에서는 사용 불가.</summary>
        public static bool IsTreasure(ItemKind kind) => GoldValue(kind) > 0;

        public static string DisplayName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return "회복 물약";
                case ItemKind.Bomb: return "폭탄";
                case ItemKind.FrostBomb: return "냉기 폭탄";
                case ItemKind.OilFlask: return "기름 병";
                case ItemKind.ThrowingKnife: return "투척 단검";
                case ItemKind.RecallScroll: return "귀환 두루마리";
                case ItemKind.CoinPouch: return "동전 주머니";
                case ItemKind.Gemstone: return "보석";
                case ItemKind.Relic: return "고대 유물";
                default: return kind.ToString();
            }
        }

        public static string Description(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion:
                    return "HP를 회복한다. 마시는 데 행동 1회를 소비한다.";
                case ItemKind.Bomb:
                    return "3×3 폭발 — 화상·넉백, 약한 바닥 붕괴와 폭발통 유폭. 본인도 피해를 입는다.";
                case ItemKind.FrostBomb:
                    return "낮은 피해의 3×3 냉기 폭발 — 맞은 대상을 빙결시킨다. 폭발통은 터뜨리지 않는다.";
                case ItemKind.OilFlask:
                    return "3×3 범위에 기름을 뿌린다. 불 폭발이 닿으면 발화해 위에 있는 모두가 불탄다.";
                case ItemKind.ThrowingKnife:
                    return "적 하나에게 강한 원거리 피해를 준다. 소모품 — 시야선이 필요하다.";
                case ItemKind.RecallScroll:
                    return "현재 층의 입구로 순간이동한다. 행동 1회를 소비한다.";
                case ItemKind.CoinPouch:
                    return "생환하면 10골드로 환산된다. 죽으면 잃는다.";
                case ItemKind.Gemstone:
                    return "생환하면 25골드로 환산된다. 죽으면 잃는다.";
                case ItemKind.Relic:
                    return "깊은 층의 희귀한 유물. 생환하면 60골드로 환산된다.";
                default: return "";
            }
        }
    }

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
