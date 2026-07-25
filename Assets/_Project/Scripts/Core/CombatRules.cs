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

    public static class CombatRules
    {
        /// <summary>근접 타격이 닿는 최대 높이차. 옆칸이 한 단(≤1) 위/아래여도 친다(단차 타격). (건물형 수직성 v0.3)</summary>
        public const int MeleeReachHeight = 1;

        /// <summary>위에서 아래로 내려칠 때의 추가 피해 — 높이 이점을 근접에도 부여. (한 줄 콘셉트 "위에서 내려치며")</summary>
        public const int DownStrikeBonus = 1;

        /// <summary>
        /// 근접 사거리 판정: 평면 정사각 인접 + 높이차가 <see cref="MeleeReachHeight"/> 이내.
        /// 기하 판정 자체는 <see cref="SightRules.CanReachAcross"/>가 소유한다 — 마법·특수
        /// 사거리가 생겨도 "높이차를 얼마까지 넘어 닿는가"를 한 곳에서만 정의하기 위해서다.
        /// </summary>
        public static bool AreAdjacent(CombatantState first, CombatantState second) =>
            CanMelee(null, first, second);

        /// <summary>
        /// 근접이 닿는가. 사거리 2 이상(창)은 직선이면서 사이가 뚫려 있어야 한다 —
        /// 벽·닫힌 문 너머로 찌를 수 없다. 사거리 1이면 맵 없이도 판정이 같다(기존 동작).
        /// </summary>
        public static bool CanMelee(
            GridMap map, CombatantState attacker, CombatantState target, int meleeReach = 1)
        {
            if (attacker == null || target == null) return false;
            if (!SightRules.CanReachAcross(
                    attacker.Position, target.Position, MeleeReachHeight, meleeReach))
                return false;
            if (meleeReach <= 1 || map == null) return true;
            return SightRules.HasLineOfSight(map, attacker.Position, target.Position);
        }

        /// <param name="map">사거리 2 이상일 때 사이 차폐를 보기 위해 필요하다. 사거리 1이면 무시된다.</param>
        /// <param name="meleeReach">장비가 주는 평면 근접 사거리(<see cref="CombatLoadout.MeleeReach"/>).</param>
        /// <param name="targetArmor">대상의 방어(장비). 물리 피해만 줄이며 최소 1은 남는다.</param>
        public static bool TryMelee(
            CombatantState attacker,
            CombatantState target,
            out int damage,
            GridMap map = null,
            int meleeReach = 1,
            int targetArmor = 0)
        {
            damage = 0;
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
                return false;
            if (!CanMelee(map, attacker, target, meleeReach))
                return false;

            // 위에서 내려치면 추가 피해 — 높이 이점을 근접에도 부여한다.
            int power = attacker.AttackPower;
            if (attacker.Position.elevation > target.Position.elevation)
                power += DownStrikeBonus;

            damage = target.TakeDamage(Mitigate(power, targetArmor));
            return true;
        }

        /// <summary>
        /// 방어로 물리 피해를 줄이되 최소 1은 남긴다 — 방어 장비가 약한 공격을 완전히
        /// 무효화하면 저티어 적이 무의미해지고 스탯 크리프와 같은 문제가 생긴다.
        /// 상태이상 틱(화상·중독)은 이 경로를 타지 않는다.
        /// </summary>
        public static int Mitigate(int power, int armor) =>
            armor <= 0 ? power : Math.Max(1, power - armor);

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
            int? attackPower = null,
            int targetArmor = 0)
        {
            damage = 0;
            if (attacker == null || target == null || map == null || maxRange < 1 ||
                !attacker.IsAlive || !target.IsAlive)
                return false;
            if (RangedReachCost(attacker.Position, target.Position) > maxRange ||
                !HasLineOfSight(map, attacker.Position, target.Position))
                return false;

            damage = target.TakeDamage(Mitigate(attackPower ?? attacker.AttackPower, targetArmor));
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
        /// 높이 인식 시야선. 판정의 단일 출처는 <see cref="SightRules.HasLineOfSight"/>이며
        /// 여기서는 전투 호출부를 위한 얇은 위임만 한다(3D 시야선 2단계 — 수평·경사·수직 통합).
        /// </summary>
        public static bool HasLineOfSight(GridMap map, GridPos from, GridPos to) =>
            SightRules.HasLineOfSight(map, from, to);
    }
}
