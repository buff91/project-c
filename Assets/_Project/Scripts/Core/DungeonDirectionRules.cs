namespace ProjectC.Core
{
    /// <summary>
    /// 던전의 주 진행 방향. <b>던전별 정체성 축</b>이지 게임 전체의 성질이 아니다 —
    /// 하강이 주 목적인 던전과 상승이 주 목적인 던전이 함께 존재한다(GDD §10.1, §11).
    /// <para>
    /// 이 값은 <b>주된</b> 방향일 뿐 경로가 단조롭다는 뜻이 아니다. 한 던전 안에서
    /// 올라갔다 떨어지거나 내려갔다 올라오는 구간이 있을 수 있으므로, 난이도·구간 판정은
    /// 언제나 <see cref="DungeonFloorInfo.ProgressIndex"/>를 쓰고 고도를 쓰지 않는다.
    /// </para>
    /// </summary>
    public enum DungeonProgressDirection
    {
        /// <summary>아래로 파고든다(지하 시설·지하묘지). 층 인덱스가 음수로 간다.</summary>
        Descend = 0,

        /// <summary>위로 올라간다(폐병원·탑). 층 인덱스가 양수로 간다.</summary>
        Ascend = 1,

        /// <summary>
        /// <b>고도가 축이 아닌 던전.</b> 안으로 파고들며 "얼마나 깊이 들어왔는가"가 진행이고,
        /// 오르내림은 층 안의 국소 지형일 뿐이다(침수 도관·잠식 구역·평면 복합시설).
        /// <para>
        /// 층을 쌓는 것은 렌더/컬링을 위한 <b>엔진 사정</b>이지 플레이어가 보는 사실이 아니다.
        /// 이 던전에서는 층 전환이 계단이 아니라 통로로 읽히고, 라벨도 B1/1F 가 아니라 구역 번호다.
        /// </para>
        /// </summary>
        Inward = 2
    }

    /// <summary>
    /// 진행 방향이 실제로 바꾸는 것만 모은 규칙. <b>중력은 방향을 타지 않는다</b> —
    /// 낙하·수직 시야는 언제나 아래로 향하며(<see cref="FallRules"/>·<see cref="SightRules"/>)
    /// 이 클래스와 무관하다.
    /// <para>
    /// 핵심 구분은 <b>공간(위/아래)</b>과 <b>진행(진출/귀환)</b>이다.
    /// <c>StairsUp</c>/<c>StairsDown</c>은 공간 이름이라 고정이고,
    /// "다음 층으로 가는 계단"이 둘 중 무엇인지가 방향을 탄다.
    /// 하강 던전에서는 진출=<c>StairsDown</c>, 상승 던전에서는 진출=<c>StairsUp</c>이다.
    /// </para>
    /// </summary>
    public static class DungeonDirectionRules
    {
        /// <summary>
        /// 이 던전에서 <b>고도가 진행 축인가</b>. <c>Inward</c>만 false다 —
        /// 층은 여전히 쌓이지만 그건 엔진 사정이고, 플레이어에게 오르내림은 진행이 아니다.
        /// </summary>
        public static bool UsesVerticalProgress(DungeonProgressDirection direction) =>
            direction != DungeonProgressDirection.Inward;

        /// <summary>
        /// 진행 지수 → 층 인덱스. 부호만 방향을 탄다.
        /// 첫 층(진행 0)은 어느 방향이든 층 인덱스 0이다.
        /// <para>
        /// <c>Inward</c>도 층은 쌓는다 — 층마다 다른 elevation 대역을 줘야 렌더·컬링·
        /// "활성 층" 분리가 성립하기 때문이다. 방향은 임의로 아래를 쓰되, 이건
        /// <b>플레이어에게 보이지 않는 내부 규약</b>이며 라벨·연출은 수평으로 읽힌다.
        /// </para>
        /// </summary>
        public static int FloorIndexFor(DungeonProgressDirection direction, int progressIndex) =>
            direction == DungeonProgressDirection.Ascend ? progressIndex : -progressIndex;

        /// <summary>다음 층(진행 +1)으로 나아가는 계단.</summary>
        public static TileKind OnwardStair(DungeonProgressDirection direction) =>
            direction == DungeonProgressDirection.Ascend ? TileKind.StairsUp : TileKind.StairsDown;

        /// <summary>이전 층(진행 −1)으로 되돌아가는 계단.</summary>
        public static TileKind BackStair(DungeonProgressDirection direction) =>
            direction == DungeonProgressDirection.Ascend ? TileKind.StairsDown : TileKind.StairsUp;

        /// <summary>
        /// 낙하는 진행을 <b>전진</b>시키는가. 하강 던전에서 구멍은 지름길이지만,
        /// 상승 던전에서는 되돌아가는 사고이고(대신 후퇴·탈출 수단 — GDD §5.3 주석),
        /// <c>Inward</c>에서는 <b>진행과 아무 관계가 없다</b> — 그냥 지형 위험이다.
        /// 규칙(<see cref="FallRules"/>)이 아니라 레벨 디자인의 의미가 바뀐다.
        /// </summary>
        public static bool FallAdvancesProgress(DungeonProgressDirection direction) =>
            direction == DungeonProgressDirection.Descend;

        // ── 건물 층 번호 ──────────────────────────────────────────────
        // 실제 건물에는 0층이 없다: … B2, B1, 1F, 2F … 라서 부호만 뒤집어서는
        // 라벨이 안 나온다(폐병원은 B2 → B1 → 1F 로 0을 건너뛰며 올라간다).
        // 그래서 표시용 건물 층 번호와, 계산용 연속 지수를 따로 둔다.

        /// <summary>건물 층 번호(0 없음) → 연속 지수. B1=−1, 1F=0, 2F=1 …</summary>
        private static int ToContinuous(int buildingFloor) =>
            buildingFloor > 0 ? buildingFloor - 1 : buildingFloor;

        /// <summary>연속 지수 → 건물 층 번호(0 없음).</summary>
        private static int FromContinuous(int continuous) =>
            continuous >= 0 ? continuous + 1 : continuous;

        /// <summary>
        /// 진행 지수 → 표시용 건물 층 번호. <paramref name="firstBuildingFloor"/>는
        /// 던전이 시작하는 건물 층이다(폐병원 = −2 → B2, 지하 던전 = −1 → B1).
        /// <c>Inward</c> 던전에는 건물 층 개념이 없다 — <see cref="FloorLabelFor"/>를 쓴다.
        /// </summary>
        public static int BuildingFloorFor(
            DungeonProgressDirection direction,
            int firstBuildingFloor,
            int progressIndex)
        {
            int step = direction == DungeonProgressDirection.Ascend ? progressIndex : -progressIndex;
            return FromContinuous(ToContinuous(firstBuildingFloor) + step);
        }

        /// <summary>건물 층 번호를 사람이 읽는 라벨로. 지하는 <c>B2</c>, 지상은 <c>3F</c>.</summary>
        public static string BuildingFloorLabel(int buildingFloor) =>
            buildingFloor < 0 ? $"B{-buildingFloor}" : $"{buildingFloor}F";

        /// <summary>
        /// 진행 지수에서 바로 라벨까지. 고도가 축인 던전은 건물 층 표기(<c>B2</c>/<c>3F</c>),
        /// <c>Inward</c> 던전은 진입 깊이 표기(<c>1구역</c>)를 쓴다 —
        /// 수평으로 파고드는 곳에 B1/1F 를 붙이면 화면이 거짓말을 한다.
        /// </summary>
        public static string FloorLabelFor(
            DungeonProgressDirection direction,
            int firstBuildingFloor,
            int progressIndex)
        {
            if (!UsesVerticalProgress(direction))
                return $"{(progressIndex < 0 ? 0 : progressIndex) + 1}구역";

            return BuildingFloorLabel(
                BuildingFloorFor(direction, firstBuildingFloor, progressIndex));
        }
    }
}
