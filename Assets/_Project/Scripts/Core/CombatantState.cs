using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>원거리 공격이 성립하지 않는 이유 (피드백/접근 판단용).</summary>
    public enum RangedBlockReason
    {
        None = 0,
        OutOfRange,
        Blocked,
        ElevationMismatch
    }

    /// <summary>
    /// 전투 참가자의 순수 로직 상태. 위치·HP·공격력만 소유하며 연출은 Gameplay에서 담당한다.
    /// </summary>
    public sealed class CombatantState
    {
        public string Id { get; }
        public GridPos Position { get; private set; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int AttackPower { get; }
        public bool IsAlive => Hp > 0;

        /// <summary>상태이상 집합 (화상/빙결). 턴 틱은 행동 파이프라인이 돌린다. (GDD §5.5)</summary>
        public StatusEffects Statuses { get; } = new StatusEffects();

        public CombatantState(string id, GridPos position, int maxHp, int attackPower)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("전투 참가자 ID가 필요합니다.", nameof(id));
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (attackPower <= 0) throw new ArgumentOutOfRangeException(nameof(attackPower));

            Id = id;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;
            AttackPower = attackPower;
        }

        public void MoveTo(GridPos position) => Position = position;

        public int TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int previous = Hp;
            Hp = Math.Max(0, Hp - amount);
            return previous - Hp;
        }

        /// <summary>디버그 전용 HP 강제 설정. 사망 상태(0)에서도 되살릴 수 있다 — 게임 규칙에서 쓰지 말 것.</summary>
        public void OverrideHpForDebug(int hp) => Hp = Math.Clamp(hp, 0, MaxHp);

        /// <summary>MaxHp 를 넘지 않게 회복하고 실제 회복량을 반환한다. 죽은 대상은 회복 불가.</summary>
        public int Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsAlive) return 0;

            int previous = Hp;
            Hp = Math.Min(MaxHp, Hp + amount);
            return Hp - previous;
        }
    }

    public static class CombatRules
    {
        public static bool AreAdjacent(CombatantState first, CombatantState second)
        {
            if (first == null || second == null) return false;
            return first.Position.elevation == second.Position.elevation &&
                   first.Position.ManhattanTo(second.Position) == 1;
        }

        public static bool TryMelee(CombatantState attacker, CombatantState target, out int damage)
        {
            damage = 0;
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
                return false;
            if (!AreAdjacent(attacker, target))
                return false;

            damage = target.TakeDamage(attacker.AttackPower);
            return true;
        }

        /// <param name="attackPower">
        /// 원거리 전용 공격력. 생략하면 근접과 같은 AttackPower.
        /// (밸런스: 무비용 원거리가 근접과 같은 피해면 카이팅으로 접근전이 성립하지 않는다)
        /// </param>
        public static bool TryRanged(
            CombatantState attacker,
            CombatantState target,
            GridMap map,
            int maxRange,
            out int damage,
            int? attackPower = null)
        {
            damage = 0;
            if (attacker == null || target == null || map == null || maxRange < 1 ||
                !attacker.IsAlive || !target.IsAlive)
                return false;
            if (RangedReachCost(attacker.Position, target.Position) > maxRange ||
                !HasLineOfSight(map, attacker.Position, target.Position))
                return false;

            damage = target.TakeDamage(attackPower ?? attacker.AttackPower);
            return true;
        }

        /// <summary>
        /// 원거리 도달 비용: 수평 맨해튼 + 높이차 1칸당 1. 고지대는 새 사선을 얻지만 사거리
        /// 예산을 깎아 카이팅을 억제한다(밸런스: 높이 이점엔 비용). Δe=0이면 평면 맨해튼과 같다.
        /// </summary>
        public static int RangedReachCost(GridPos from, GridPos target) =>
            from.ManhattanTo(target) + Math.Abs(from.elevation - target.elevation);

        /// <summary>이 칸에서 target 을 쏠 수 있는가 — 도달 비용·시야선. 높이차는 비용으로 흡수한다.</summary>
        public static bool CanFireFrom(GridMap map, GridPos from, GridPos target, int maxRange)
        {
            return map != null &&
                   from != target &&
                   RangedReachCost(from, target) <= maxRange &&
                   HasLineOfSight(map, from, target);
        }

        /// <summary>원거리가 안 되는 첫 번째 이유. 도달 비용(사거리+높이) > 시야선 순으로 진단한다.</summary>
        public static RangedBlockReason DiagnoseRanged(GridMap map, GridPos from, GridPos target, int maxRange)
        {
            if (RangedReachCost(from, target) > maxRange) return RangedBlockReason.OutOfRange;
            if (!HasLineOfSight(map, from, target)) return RangedBlockReason.Blocked;
            return RangedBlockReason.None;
        }

        /// <summary>
        /// 사격 가능 위치까지의 최단 경로를 찾는다. 이미 쏠 수 있으면 true + 빈 경로.
        /// 후보는 target 주변 사거리 다이아몬드(같은 elevation·시야선·걷기 가능)로 한정하고,
        /// 동률이면 target 근접 → x → y 순으로 결정적으로 고른다.
        /// </summary>
        public static bool FindFiringPosition(
            GridMap map,
            GridPos shooter,
            GridPos target,
            int maxRange,
            out List<GridPos> firingPath,
            Func<GridPos, bool> isBlocked = null)
        {
            firingPath = new List<GridPos>();
            if (map == null || maxRange < 1) return false;
            if (CanFireFrom(map, shooter, target, maxRange)) return true;

            List<GridPos> best = null;
            GridPos bestPos = default;
            for (int dx = -maxRange; dx <= maxRange; dx++)
            for (int dy = -(maxRange - Math.Abs(dx)); dy <= maxRange - Math.Abs(dx); dy++)
            {
                var candidate = new GridPos(target.x + dx, target.y + dy, target.elevation);
                if (candidate == target || candidate == shooter) continue;
                if (!map.IsWalkable(candidate)) continue;
                if (isBlocked != null && isBlocked(candidate)) continue;
                if (!CanFireFrom(map, candidate, target, maxRange)) continue;

                List<GridPos> path = GridPathfinder.FindPath(map, shooter, candidate, isBlocked);
                if (path.Count < 2) continue;

                bool better = best == null ||
                              path.Count < best.Count ||
                              (path.Count == best.Count &&
                               (candidate.ManhattanTo(target) < bestPos.ManhattanTo(target) ||
                                (candidate.ManhattanTo(target) == bestPos.ManhattanTo(target) &&
                                 (candidate.x < bestPos.x ||
                                  (candidate.x == bestPos.x && candidate.y < bestPos.y)))));
                if (better)
                {
                    best = path;
                    bestPos = candidate;
                }
            }

            if (best == null) return false;
            firingPath = best;
            return true;
        }

        /// <summary>
        /// 높이 인식 시야선(3D 1단계). from→to를 2D 브레젠험으로 걷되, 각 중간 칸에서
        /// 시선의 elevation을 진행 비율로 보간해 그 복셀의 차폐를 본다. void(빈 칸)=불투명 루트를
        /// 지키고, from.elevation == to.elevation이면 상수 보간이라 기존 평면 판정과 완전히 같다.
        /// 같은 컬럼(x==to.x && y==to.y) 수직 투시는 아직 열지 않는다(2단계, VerticalOpeningRules).
        /// </summary>
        public static bool HasLineOfSight(GridMap map, GridPos from, GridPos to)
        {
            if (map == null) return false;
            // 같은 칸: 수직 시야선은 2단계로 미룬다 — 같은 elevation일 때만 자기 자신이 보인다.
            if (from.x == to.x && from.y == to.y) return from.elevation == to.elevation;

            int x = from.x;
            int y = from.y;
            int dx = Math.Abs(to.x - from.x);
            int dy = Math.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int error = dx - dy;

            int steps = Math.Max(dx, dy); // 체비셰프 단계 수 = elevation 보간 분모(>=1)
            int k = 0;

            while (x != to.x || y != to.y)
            {
                int twiceError = error * 2;
                if (twiceError > -dy) { error -= dy; x += sx; }
                if (twiceError < dx) { error += dx; y += sy; }
                k++;
                if (x == to.x && y == to.y) break;

                int e = (int)Math.Round(
                    from.elevation + (to.elevation - from.elevation) * (double)k / steps,
                    MidpointRounding.AwayFromZero);

                TileData tile = map.Get(new GridPos(x, y, e));
                if (tile == null || tile.BlocksSight) return false;
            }

            return true;
        }
    }
}
