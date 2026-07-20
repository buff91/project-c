using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 몬스터 활성화/컬링 판정. (GDD §5.7, 모바일 성능 핵심)
    /// 비활성 몬스터는 브레인 Decide 호출 자체를 건너뛴다(휴면).
    ///
    /// 규칙 목록 형태로 유지한다 — M4에서 "보이는 구멍 인접의 아래층 몬스터"처럼
    /// 층을 넘는 활성 규칙이 여기에 추가된다. (현재 같은 층 한정은 의도적 축소)
    /// </summary>
    public static class MonsterActivation
    {
        public static bool IsActive(
            DungeonHeightModel height,
            GridPos player,
            GridPos monster,
            int activeRadius)
        {
            if (height == null) throw new ArgumentNullException(nameof(height));
            if (activeRadius < 0) throw new ArgumentOutOfRangeException(nameof(activeRadius));

            // 규칙 1: 플레이어와 같은 던전 층. (시뮬레이션은 활성 층만, GDD §5.1)
            if (!height.SameFloor(player, monster)) return false;

            // 규칙 2: 활성 반경(체비셰프) 이내.
            return player.ChebyshevTo(monster) <= activeRadius;
        }
    }
}
