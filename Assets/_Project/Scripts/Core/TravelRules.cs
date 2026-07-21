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
    /// SPD식 자동 이동 규칙. (GDD §5.2)
    /// 적이 시야에 있으면 탭당 1스텝만 허용하고, 이동 중 새 위협이 나타나면 즉시 멈춘다.
    /// 순수 판정만 담당 — 시야/적 상태 스냅샷은 호출부(Gameplay)가 만든다.
    /// </summary>
    public static class TravelRules
    {
        /// <summary>이번 탭으로 걸을 수 있는 스텝 수. pathSteps는 시작 칸을 제외한 걸음 수.</summary>
        public static int AllowedSteps(bool enemyInSight, int pathSteps)
        {
            if (pathSteps <= 0) return 0;
            return enemyInSight ? 1 : pathSteps;
        }

        /// <summary>
        /// 한 스텝 뒤 인터럽트 판정. 우선순위: 피해 > 새로 보인 적 > 새로 보인 아이템.
        /// previouslyVisibleEnemyIds는 스텝 시작 전에 보이던 살아있는 적 ID 집합.
        /// </summary>
        public static TravelInterrupt Evaluate(
            IReadOnlyCollection<string> previouslyVisibleEnemyIds,
            IEnumerable<(string Id, bool Visible, bool Alive)> enemies,
            bool newItemSighted,
            bool tookDamage)
        {
            if (previouslyVisibleEnemyIds == null) throw new ArgumentNullException(nameof(previouslyVisibleEnemyIds));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            if (tookDamage) return TravelInterrupt.PlayerDamaged;

            foreach ((string id, bool visible, bool alive) in enemies)
            {
                if (alive && visible && !previouslyVisibleEnemyIds.Contains(id))
                    return TravelInterrupt.EnemySighted;
            }

            return newItemSighted ? TravelInterrupt.ItemSighted : TravelInterrupt.None;
        }
    }
}
