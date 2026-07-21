using System;

namespace ProjectC.Core
{
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
            if (attacker.Position.elevation != target.Position.elevation ||
                attacker.Position.ManhattanTo(target.Position) > maxRange ||
                !HasLineOfSight(map, attacker.Position, target.Position))
                return false;

            damage = target.TakeDamage(attackPower ?? attacker.AttackPower);
            return true;
        }

        public static bool HasLineOfSight(GridMap map, GridPos from, GridPos to)
        {
            if (map == null || from.elevation != to.elevation) return false;

            int x = from.x;
            int y = from.y;
            int dx = Math.Abs(to.x - from.x);
            int dy = Math.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int error = dx - dy;

            while (x != to.x || y != to.y)
            {
                int twiceError = error * 2;
                if (twiceError > -dy) { error -= dy; x += sx; }
                if (twiceError < dx) { error += dx; y += sy; }
                if (x == to.x && y == to.y) break;

                TileData tile = map.Get(new GridPos(x, y, from.elevation));
                if (tile == null || tile.BlocksSight) return false;
            }

            return true;
        }
    }
}
