namespace ProjectC.Core
{
    /// <summary>
    /// 엘리베이터 통로 — 상승 던전의 <b>후퇴·탈출 동선</b>(GDD §5.3).
    ///
    /// <para>
    /// <b>왜 낙하가 아니라 링크인가.</b> GDD 는 "위험하면 엘리베이터 통로로 뛰어내려 빠르게
    /// 하강한다"고 적었지만, 낙뎀 곡선(<c>floors×(floors+1)</c>: 1층 2 · 2층 6 · 3층 12)과
    /// 영웅 HP(8~10)로는 여러 층 자유낙하가 곧 죽음이다. 완충 부츠(+2칸)를 껴도 3층이 9 피해다.
    /// 그래서 통로는 낙하가 아니라 <b>케이블·버팀대를 타고 내려가는 축</b>으로 만든다 —
    /// 의도(빠른 후퇴)는 살리고 낙뎀 곡선은 건드리지 않는다(M5 시뮬로 맞춘 값이다).
    /// </para>
    /// <para>
    /// <b>한 방향(아래로만)이다.</b> 올라갈 수 있으면 계단을 건너뛰는 진행 지름길이 되어
    /// 상승 던전의 페이싱이 무너진다. 후퇴에는 대가가 있어야 한다 — 내려간 만큼 다시 올라온다.
    /// </para>
    /// <para>
    /// <b>하강 던전에는 두지 않는다.</b> 거기서는 아래가 곧 전진이라 통로가 후퇴 수단이 아니고,
    /// 넣으면 그냥 지름길이 하나 더 생긴다. 기존 던전 생성이 바뀌지 않는 실용적 이점도 있다.
    /// </para>
    /// </summary>
    public static class ElevatorShaftRules
    {
        /// <summary>
        /// 통로가 몇 진행 층 아래로 내려가는가. 1이면 귀환 계단과 다를 게 없고,
        /// 크게 잡으면 판이 한 번에 되감긴다. 실플레이 리포트로 조정할 값이다.
        /// </summary>
        public const int DropProgressFloors = 2;

        /// <summary>
        /// 통로가 처음 등장하는 진행 지수. 초반에는 되돌아갈 거리 자체가 짧아 의미가 없고,
        /// 첫 층들은 구조를 가르치는 구간이라 비워 둔다.
        /// </summary>
        public const int FirstProgressIndex = 3;

        /// <summary>
        /// 이 방향의 던전이 엘리베이터 통로를 갖는가. 낙하가 <b>후퇴</b>를 뜻하는 던전만이다.
        /// </summary>
        public static bool AppliesTo(DungeonProgressDirection direction) =>
            DungeonDirectionRules.FallMeaningFor(direction) == FallMeaning.Retreat;

        /// <summary>
        /// 이 층에 통로 입구를 둘지. 보스 아레나는 제외한다 —
        /// 구멍을 두지 않는 것과 같은 이유로, 보스전 중 아레나를 벗어나게 만들지 않는다.
        /// </summary>
        public static bool ShouldPlace(
            DungeonProgressDirection direction,
            int progressIndex,
            int floorCount)
        {
            if (!AppliesTo(direction)) return false;
            if (progressIndex < FirstProgressIndex) return false;
            if (DungeonBossArenaRules.IsArenaFloor(progressIndex, floorCount)) return false;

            return DestinationProgressIndex(progressIndex) >= 0;
        }

        /// <summary>통로가 도착하는 진행 지수. 음수면 목적지가 없다는 뜻이다.</summary>
        public static int DestinationProgressIndex(int progressIndex) =>
            progressIndex - DropProgressFloors;
    }
}
