using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 몬스터 명단과 깊이별 혼합 규칙. (M5 콘텐츠 — 밸런스 수치는 여기 한 곳에서)
    /// 새 몬스터 = archetype 추가 + 깊이 혼합표 갱신 + 스프라이트 슬롯이면 끝난다.
    /// </summary>
    public static class MonsterRoster
    {
        /// <summary>고블린: 기준 몬스터. 겁이 많아 빈사가 되면 도망친다.</summary>
        public static readonly MonsterArchetype Goblin =
            new MonsterArchetype("Goblin", maxHp: 5, attackPower: 1,
                aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.3f);

        /// <summary>해골: 느리게 눈치채지만 단단하고 아프다. 도주하지 않는다.</summary>
        public static readonly MonsterArchetype Skeleton =
            new MonsterArchetype("Skeleton", maxHp: 8, attackPower: 2,
                aggroRange: 5, patrolRadius: 1, fleeThreshold: 0f);

        /// <summary>슬라임: 약하고 흔하다. 넓게 배회하며 겁 없이 달려든다.</summary>
        public static readonly MonsterArchetype Slime =
            new MonsterArchetype("Slime", maxHp: 3, attackPower: 1,
                aggroRange: 4, patrolRadius: 3, fleeThreshold: 0f);

        /// <summary>
        /// 깊이 비례 혼합 (depth 0 = 최상층 B1): 얕은 층은 슬라임/고블린,
        /// 깊어질수록 해골 비중이 커진다. (GDD §5.7 난이도·깊이 연동)
        /// </summary>
        public static MonsterArchetype PickForDepth(int depth, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            int roll = random.Next(0, 100);
            if (depth <= 0) return roll < 50 ? Slime : Goblin;
            if (depth == 1) return roll < 30 ? Slime : roll < 75 ? Goblin : Skeleton;
            return roll < 15 ? Slime : roll < 55 ? Goblin : Skeleton;
        }
    }
}
