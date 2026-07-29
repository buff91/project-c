namespace ProjectC.Core
{
    /// <summary>
    /// 벽 등잔이 걸릴 자리를 정하는 <b>단일</b> 판정. 순수 C# — UnityEngine 비의존.
    ///
    /// <para>
    /// 왜 따로 뺐나: 등잔 <b>아트</b>와 등잔 <b>빛</b>을 서로 무관한 두 해시가 고르고 있었다.
    /// 아트는 <c>|x*3 + y + viewQuarterTurns| % rarity</c>, 빛은
    /// <c>(x*73856093) ^ (y*19349663) ^ (seed*83492791) % rarity</c>였다. 공유하는 건
    /// <c>WallSconceRarity</c> 값뿐이라 두 집합은 <b>우연 말고는 겹치지 않았다</b> —
    /// 그려진 램프는 빛을 내지 않고, 빛 웅덩이에는 보이는 광원이 없었다.
    /// </para>
    /// <para>
    /// 아트 해시가 <c>viewQuarterTurns</c>를 포함한 것도 잠복 버그였다. 시점을 돌리면 같은 벽의
    /// 램프가 붙었다 사라졌다 했다 — 공간이 시점에 따라 바뀌면 기둥 ①(입체 공간)이 거짓말이 된다.
    /// 이 판정에는 시점 인자가 <b>없다</b>. 그게 요점이다.
    /// </para>
    /// </summary>
    public static class SconcePlacement
    {
        /// <summary>
        /// 이 타일에 등잔이 걸리는가. 호출자는 이미 "벽이 설 수 있는 가장자리 타일"임을
        /// 보장해야 한다 — 여기서는 희소도만 정한다.
        /// </summary>
        /// <param name="rarity">
        /// 깊이 밴드가 주는 희소도(<c>WallSconceRarity</c>). 클수록 드물다.
        /// 0 이하는 "등잔 없음"으로 다룬다 — 0으로 나누지 않는다.
        /// </param>
        public static bool IsSconce(int x, int y, int seed, int rarity)
        {
            if (rarity <= 0) return false;

            // 흩뿌리는 해시가 아니라 **격자**다. 이유는 분산이다: 한 방에 보이는 뒷벽은
            // 10칸 남짓이라, 확률 1/rarity 로 흩뿌리면 평균은 맞아도 "한 개도 안 걸리는 방"이
            // 꾸준히 나온다(rarity 5, 벽 11칸 → 0개일 확률 약 9%). 실제로 시작 방 B2가
            // 그렇게 비었다. 격자는 같은 밀도에서 **간격을 보장**한다 — 긴 빈 구간이 없다.
            //
            // x*3 + y 는 원래 아트가 쓰던 격자다(그 시절 벽면의 등잔 간격이 고르게 보인 이유).
            // 거기 섞여 있던 viewQuarterTurns 만 seed 기반 오프셋으로 갈아 끼웠다 —
            // 시점 의존은 램프를 순간이동시키던 버그였고, seed 는 던전마다 위상을 바꿔 준다.
            int offset = (int)((uint)(seed * 2654435761u) % (uint)rarity);
            int lattice = x * 3 + y + offset;
            return ((lattice % rarity) + rarity) % rarity == 0;
        }
    }
}
