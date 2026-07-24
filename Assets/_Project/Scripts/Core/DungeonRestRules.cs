using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 던전 내부 제한 휴식처 규칙. 생성 위치와 회복량을 Unity 비의존 데이터로 고정한다.
    /// </summary>
    public static class DungeonRestRules
    {
        public const int DepthInterval = 3;

        /// <summary>
        /// 첫 층과 보스 층을 제외하고 세 층마다 배치한다.
        /// B1부터 시작하는 10층 던전에서는 B4·B7이다.
        /// </summary>
        public static bool ShouldPlace(int depthIndex, int floorCount) =>
            depthIndex > 0 &&
            depthIndex < floorCount - 1 &&
            depthIndex % DepthInterval == 0;

        /// <summary>잃은 HP의 절반을 올림해 회복한다. 풀 HP·사망 상태는 회복하지 않는다.</summary>
        public static int HealingAmount(int hp, int maxHp)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (hp <= 0 || hp >= maxHp) return 0;
            return (maxHp - hp + 1) / 2;
        }
    }
}
