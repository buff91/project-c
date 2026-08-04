using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectC.Core
{
    /// <summary>자동 이동을 멈추게 하는 사건. 값이 클수록 우선한다.</summary>
    public enum TravelInterrupt
    {
        None = 0,
        ItemSighted = 1,
        EnemySighted = 2,
        PlayerDamaged = 3
    }

    /// <summary>
    /// 입력 하나가 소비할 수 있는 자동 이동 예산. 적이 보이는 동안 이동을 한 칸이라도
    /// 사용하면 같은 입력에서 공격·상호작용까지 이어 갈 수 없다.
    /// </summary>
    public readonly struct TravelActionBudget
    {
        public int AllowedSteps { get; }
        public bool AllowsFollowUpAction { get; }

        public TravelActionBudget(int allowedSteps, bool allowsFollowUpAction)
        {
            AllowedSteps = allowedSteps;
            AllowsFollowUpAction = allowsFollowUpAction;
        }
    }

    /// <summary>
    /// SPD식 자동 이동 규칙. (GDD §5.2)
    /// 적이 시야에 있으면 탭당 1스텝만 허용하고, 이동 중 새 위협이 나타나면 즉시 멈춘다.
    /// 순수 판정만 담당 — 시야/적 상태 스냅샷은 호출부(Gameplay)가 만든다.
    /// </summary>
    public static class TravelRules
    {
        /// <summary>이번 탭으로 걸을 수 있는 스텝 수. pathSteps는 시작 칸을 제외한 걸음 수.</summary>
        public static int AllowedSteps(bool enemyInSight, int pathSteps)
        {
            return GetActionBudget(enemyInSight, pathSteps).AllowedSteps;
        }

        /// <summary>
        /// 이번 입력의 이동과 후속 행동 예산. 안전할 때는 기존 자동 접근처럼 전체 경로 뒤
        /// 행동까지 이어 가고, 위협 중에는 이동 1칸 또는 제자리 후속 행동 하나만 허용한다.
        /// </summary>
        public static TravelActionBudget GetActionBudget(bool enemyInSight, int pathSteps)
        {
            int steps = Math.Max(0, pathSteps);
            if (!enemyInSight)
                return new TravelActionBudget(steps, allowsFollowUpAction: true);
            if (steps == 0)
                return new TravelActionBudget(0, allowsFollowUpAction: true);
            return new TravelActionBudget(1, allowsFollowUpAction: false);
        }

        /// <summary>
        /// 접근 이동 뒤 같은 입력의 공격·상호작용을 이어 갈 수 있는지 판정한다.
        /// 이동이 없으면 인접 행동 하나를 허용하고, 이동했다면 시작 예산뿐 아니라 이동 중
        /// 새 위협·피해·아이템 발견까지 없어야 한다.
        /// </summary>
        public static bool CanPerformFollowUpAction(
            TravelActionBudget initialBudget,
            bool moved,
            bool enemyInSightAfterMovement,
            TravelInterrupt interrupt)
        {
            if (!initialBudget.AllowsFollowUpAction) return false;
            if (!moved) return true;
            return !enemyInSightAfterMovement && interrupt == TravelInterrupt.None;
        }

        /// <summary>
        /// 한 스텝 뒤 인터럽트 판정. 우선순위: 피해 > 새로 보인 적 > 새로 보인 아이템.
        /// previouslyVisibleEnemyIds는 스텝 시작 전에 보이던 살아있는 적 ID 집합.
        /// enemySightedDuringAction은 플레이어 행동 직후 보였지만 적 턴 뒤에는 사라진 발견 사건을 보존한다.
        /// </summary>
        public static TravelInterrupt Evaluate(
            IReadOnlyCollection<string> previouslyVisibleEnemyIds,
            IEnumerable<(string Id, bool Visible, bool Alive)> enemies,
            bool newItemSighted,
            bool tookDamage,
            bool enemySightedDuringAction = false)
        {
            if (previouslyVisibleEnemyIds == null) throw new ArgumentNullException(nameof(previouslyVisibleEnemyIds));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            if (tookDamage) return TravelInterrupt.PlayerDamaged;
            if (enemySightedDuringAction) return TravelInterrupt.EnemySighted;

            foreach ((string id, bool visible, bool alive) in enemies)
            {
                if (alive && visible && !previouslyVisibleEnemyIds.Contains(id))
                    return TravelInterrupt.EnemySighted;
            }

            return newItemSighted ? TravelInterrupt.ItemSighted : TravelInterrupt.None;
        }
    }
}
