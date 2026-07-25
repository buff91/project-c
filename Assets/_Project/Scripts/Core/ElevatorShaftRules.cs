namespace ProjectC.Core
{
    /// <summary>
    /// 엘리베이터 — <b>보스를 잡아 건물 전원이 들어온 뒤에야</b> 움직이는 복귀 수단(GDD §5.3).
    ///
    /// <para>
    /// <b>왜 낙하가 아닌가.</b> GDD 는 "통로로 뛰어내려 빠르게 하강"이라 적었지만 수치가 막는다 —
    /// 낙뎀 곡선이 <c>floors×(floors+1)</c>(3층=12)인데 영웅 HP 는 8~10 이고, 완충 부츠(+2칸)를
    /// 껴도 3층이 9 다. 여러 층 자유낙하는 탈출이 아니라 자살이다. 그래서 엘리베이터는
    /// <b>탑승</b>이고 낙뎀 곡선은 건드리지 않는다(M5 시뮬 600판으로 맞춘 값이다).
    /// </para>
    /// <para>
    /// <b>왜 보스 게이트인가.</b> 층마다 자유롭게 되돌아갈 수 있으면 진행의 긴장이 사라진다.
    /// 대신 올라오는 길에 <b>멈춘 엘리베이터</b>를 보게 하고, 보스를 잡는 순간 전원이 들어와
    /// 그것이 움직이게 한다 — 건물이 깨어나는 한 장면이 곧 복귀 동선의 해금이다.
    /// 사용 횟수를 세는 대신 상태 하나(전원)로 다루므로 UI·세이브도 단순하다.
    /// </para>
    /// <para>
    /// <b>복귀 전용이다.</b> 진행의 반대 방향으로만 간다 — 하강 던전에서는 위로, 상승 던전에서는
    /// 아래로. 진행 방향으로 태우면 계단을 건너뛰는 지름길이 되어 페이싱이 무너진다.
    /// </para>
    /// <para>
    /// 보스를 잡은 뒤의 쓸모: 최종 출구는 이미 열려 있으므로 엘리베이터는 <b>선택</b>이다 —
    /// 지나친 층의 파밍(기둥 4)을 회수하러 내려갔다 나올 수 있다. 강제 동선이 아니다.
    /// </para>
    /// </summary>
    public static class ElevatorShaftRules
    {
        /// <summary>
        /// 던전당 엘리베이터는 <b>하나</b>다. 여러 개면 "모든 층에서 복귀"가 되어
        /// 진행의 무게가 사라진다 — 건물의 주 엘리베이터 한 대라는 읽기를 유지한다.
        /// </summary>
        public const int ShaftsPerDungeon = 1;

        /// <summary>
        /// 탑승구가 놓이는 진행 지수 = 보스 아레나 바로 앞 층. 보스로 향하는 길에 반드시
        /// 지나가므로 <b>멈춰 있는 것을 먼저 본다</b> — 그래야 전원이 들어온 것이 사건이 된다.
        /// </summary>
        public static int EntranceProgressIndex(int floorCount) => floorCount - 2;

        /// <summary>
        /// 도착 진행 지수. 첫 층(0)이 아니라 1인 이유는 첫 층 입구 방을 건드리지 않기 위해서다 —
        /// 입구는 진입 연출과 세이브 복원이 얽혀 있어 특수 타일을 얹지 않는다.
        /// </summary>
        public const int LandingProgressIndex = 1;

        /// <summary>
        /// 이 방향의 던전이 엘리베이터를 갖는가. <c>Inward</c>는 제외한다 —
        /// 거기서는 오르내림이 진행이 아니라고 선언했으므로(라벨도 `1구역`),
        /// 층을 관통하는 승강 연출이 화면과 어긋난다.
        /// </summary>
        public static bool AppliesTo(DungeonProgressDirection direction) =>
            DungeonDirectionRules.UsesVerticalProgress(direction);

        /// <summary>
        /// 이 던전에 엘리베이터를 놓을 수 있는가. 탑승구와 도착층이 서로 다르고
        /// 둘 다 존재할 만큼 층이 있어야 한다(최소 4층).
        /// </summary>
        public static bool AppliesToDungeon(DungeonProgressDirection direction, int floorCount)
        {
            if (!AppliesTo(direction)) return false;

            int entrance = EntranceProgressIndex(floorCount);
            return entrance > LandingProgressIndex && entrance < floorCount;
        }

        /// <summary>이 층이 탑승구를 갖는 층인가.</summary>
        public static bool IsEntranceFloor(
            DungeonProgressDirection direction,
            int progressIndex,
            int floorCount) =>
            AppliesToDungeon(direction, floorCount) &&
            progressIndex == EntranceProgressIndex(floorCount);

        /// <summary>
        /// 엘리베이터가 지금 움직이는가. <b>전원은 보스 처치로 들어온다</b> —
        /// 보스가 없는 던전(예고 원정지)은 처음부터 움직인다고 본다.
        /// </summary>
        public static bool IsPowered(bool dungeonHasBoss, bool bossDefeated) =>
            !dungeonHasBoss || bossDefeated;
    }
}
