namespace ProjectC.Core
{
    /// <summary>
    /// 원정지(지역)의 콘텐츠 정체성 키. <b>던전 ID가 아니라 프로파일</b>이다 —
    /// 여러 던전이 같은 결을 공유할 수 있어야 표가 던전 수만큼 늘어나지 않는다.
    /// <para>
    /// 아트/서사 쪽 대응은 리스킨 표(`docs/art-direction/project-c-postapoc-reskin-table-v1.md` §1)의
    /// 테마 프로파일이다: <see cref="Facility"/> = A(기계·시설), <see cref="Flooded"/> = C(침수·냉각).
    /// <see cref="Ember"/>는 리스킨 표에 아직 칸이 없다 — 여기서는 <b>밸런스 수치만</b> 정의하고
    /// 적 스킨셋은 던전을 열 때 표에 추가한다.
    /// </para>
    /// <para>
    /// <b>스탯·AI는 지역을 타지 않는다.</b> 지역이 가르는 것은 <i>혼합·밀도·무대 확률</i>이며
    /// 아키타입의 HP/공격력/행동 트리는 <see cref="MonsterRoster"/>에서 전 지역 공용이다.
    /// </para>
    /// </summary>
    public enum DungeonRegionProfile
    {
        /// <summary>기계·시설 (폐병원). 감전·폭발이 강조되는 기준 지역.</summary>
        Facility = 0,

        /// <summary>침수·냉각 (침수된 금고). 물 웅덩이가 도처에 있어 빙결·감전의 무대가 넓다.</summary>
        Flooded = 1,

        /// <summary>불·기름 (잿불 성채). 물이 드물고 밀도가 높은 고난도 지역.</summary>
        Ember = 2
    }

    /// <summary>
    /// 깊이 구간(밴드)별 콘텐츠 변주 튜닝값. 적 조합 가중치와 방 구조 확률을
    /// 한 곳에서 관리한다(밸런스 수치 SSOT). 밴드 경계는 <see cref="DungeonDepthBandRules"/>.
    /// </summary>
    public sealed class DungeonBandProfile
    {
        /// <summary>적 조합 가중치(합이 롤 범위). 0이면 그 종은 이 밴드에 안 나온다.</summary>
        public int SlimeWeight { get; }
        public int GoblinWeight { get; }
        public int SkeletonWeight { get; }

        /// <summary>원거리 사수 비중. 얕은 밴드는 0 — 원거리 압박은 도입 구간을 지난 뒤 등장한다.</summary>
        public int SlingerWeight { get; }

        /// <summary>북쪽 방 기본 적 수에 더하는 보정.</summary>
        public int ExtraEnemies { get; }

        /// <summary>막다른 분기 방이 생길 확률(%). 0~100.</summary>
        public int BranchChancePercent { get; }

        /// <summary>남쪽 방에 물 웅덩이가 생길 확률(%). 0~100.</summary>
        public int PuddleChancePercent { get; }

        /// <summary>
        /// 사다리 위 +2단 캐치워크의 길이(칸). 0이면 이 밴드엔 없다.
        /// 큰 단차는 높이 인식 FOV 차폐·내려치기·고지대 사격이 실제로 발동하는 무대다.
        /// </summary>
        public int CatwalkLength { get; }

        /// <summary>
        /// 벽 등잔의 희소도 — 가장자리 후보 N칸 중 1칸에 걸린다. 클수록 광원이 드물어 어둡다.
        /// 깊이 정체성을 새 아트 없이 공용 광원 시스템만으로 준다(공통 팔레트 불변).
        /// </summary>
        public int WallSconceRarity { get; }

        public DungeonBandProfile(
            int slimeWeight,
            int goblinWeight,
            int skeletonWeight,
            int extraEnemies,
            int branchChancePercent,
            int puddleChancePercent,
            int catwalkLength,
            int wallSconceRarity,
            int slingerWeight = 0)
        {
            SlimeWeight = slimeWeight;
            GoblinWeight = goblinWeight;
            SkeletonWeight = skeletonWeight;
            SlingerWeight = slingerWeight;
            ExtraEnemies = extraEnemies;
            BranchChancePercent = branchChancePercent;
            PuddleChancePercent = puddleChancePercent;
            CatwalkLength = catwalkLength;
            WallSconceRarity = wallSconceRarity < 1 ? 1 : wallSconceRarity;
        }

        /// <summary>적 조합 롤 범위. 항상 &gt; 0 이도록 프로파일을 정의한다.</summary>
        public int TotalWeight => SlimeWeight + GoblinWeight + SkeletonWeight + SlingerWeight;
    }

    /// <summary>
    /// <b>(지역 × 깊이)</b> 프로파일 테이블. 얕을수록 약한 적·낮은 밀도, 깊을수록 단단한 적이
    /// 늘어나고, 그 기울기의 <i>기준선</i>을 지역이 정한다.
    /// 값은 순수 데이터라 생성기 RNG 스트림을 흔들지 않는다(조회 전용).
    /// <para>
    /// <b>지역을 인자로 강제한다</b>(기본값 없음). 예전 <c>DungeonDepthBandRules.ForFloor</c>가
    /// 고도에서 깊이를 역산하다 상승 던전에서 조용히 붕괴한 전례가 있다 — 축이 하나 더
    /// 생겼는데 호출부가 생략할 수 있으면 같은 종류의 조용한 오답이 다시 생긴다.
    /// </para>
    /// </summary>
    public static class DungeonBandProfiles
    {
        // ── Facility (폐병원 · 기계·시설) ─────────────────────────────────────
        // 기준 지역. 다른 지역은 이 값을 기준선으로 두고 정체성 다이얼만 돌린다.

        // Shallow(1~3번째): 경비 드론(Skeleton) 없음 — 도입 구간을 확실히 구분한다.
        // 캐치워크 없음(평평한 도입) + 가장 촘촘한 등잔(밝고 읽기 쉬움).
        private static readonly DungeonBandProfile FacilityShallow =
            new DungeonBandProfile(50, 50, 0, extraEnemies: 0, branchChancePercent: 50, puddleChancePercent: 50,
                catwalkLength: 0, wallSconceRarity: 5);

        // Mid(4~6번째): 드론과 사수가 함께 등장, 밀도 소폭 상승. 캐치워크 한 칸으로 높이 전술을 소개한다.
        private static readonly DungeonBandProfile FacilityMid =
            new DungeonBandProfile(15, 40, 30, extraEnemies: 1, branchChancePercent: 60, puddleChancePercent: 50,
                catwalkLength: 1, wallSconceRarity: 6, slingerWeight: 15);

        // Deep(7~9번째): 드론 비중 최다, 파밍/물 반응 무대 증가.
        // 캐치워크가 통로가 되고 등잔은 드물어진다 — 깊이는 어둠과 높이로 읽힌다.
        private static readonly DungeonBandProfile FacilityDeep =
            new DungeonBandProfile(5, 35, 40, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 60,
                catwalkLength: 2, wallSconceRarity: 8, slingerWeight: 20);

        // Boss(10번째+): Deep와 동일 — 전역 누적 깊이가 9를 넘어도 유효한 혼합을 유지한다.
        // 최심층 아레나 자체는 결투 공간을 비우려 캐치워크를 놓지 않는다(생성기가 아레나 축으로 판정).
        private static readonly DungeonBandProfile FacilityBoss =
            new DungeonBandProfile(5, 35, 40, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 60,
                catwalkLength: 2, wallSconceRarity: 9, slingerWeight: 20);

        // ── Flooded (침수된 금고 · 침수·냉각) ────────────────────────────────
        // 정체성 다이얼 셋: 웅덩이 확률(대폭 ↑ — 빙결·감전 무대가 이 지역의 이유다),
        // 등잔 희소도(↑ — 물에 죽은 비상등), 사수 비중(↓ — 물에서 투척이 어렵다는 결).
        // 나머지는 Facility 기준선을 따른다.
        private static readonly DungeonBandProfile FloodedShallow =
            new DungeonBandProfile(60, 40, 0, extraEnemies: 0, branchChancePercent: 50, puddleChancePercent: 80,
                catwalkLength: 0, wallSconceRarity: 6);

        private static readonly DungeonBandProfile FloodedMid =
            new DungeonBandProfile(25, 40, 25, extraEnemies: 1, branchChancePercent: 60, puddleChancePercent: 85,
                catwalkLength: 1, wallSconceRarity: 8, slingerWeight: 10);

        private static readonly DungeonBandProfile FloodedDeep =
            new DungeonBandProfile(15, 35, 35, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 90,
                catwalkLength: 2, wallSconceRarity: 10, slingerWeight: 15);

        private static readonly DungeonBandProfile FloodedBoss =
            new DungeonBandProfile(15, 35, 35, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 90,
                catwalkLength: 2, wallSconceRarity: 11, slingerWeight: 15);

        // ── Ember (잿불 성채 · 불·기름) ──────────────────────────────────────
        // 정체성 다이얼 셋: 웅덩이 확률(↓ — 물이 흔하면 불 연쇄가 서지 않는다),
        // 밀도(↑ — 고난도 지역), 등잔 희소도(↓ — 잿불이 도처에 있어 밝다).
        // 등잔이 깊이에 따라 드물어지는 <b>방향</b>은 지역과 무관하게 유지한다.
        private static readonly DungeonBandProfile EmberShallow =
            new DungeonBandProfile(40, 60, 0, extraEnemies: 0, branchChancePercent: 50, puddleChancePercent: 20,
                catwalkLength: 0, wallSconceRarity: 4);

        private static readonly DungeonBandProfile EmberMid =
            new DungeonBandProfile(10, 40, 30, extraEnemies: 1, branchChancePercent: 60, puddleChancePercent: 20,
                catwalkLength: 1, wallSconceRarity: 5, slingerWeight: 20);

        private static readonly DungeonBandProfile EmberDeep =
            new DungeonBandProfile(5, 30, 40, extraEnemies: 2, branchChancePercent: 70, puddleChancePercent: 25,
                catwalkLength: 2, wallSconceRarity: 6, slingerWeight: 25);

        private static readonly DungeonBandProfile EmberBoss =
            new DungeonBandProfile(5, 30, 40, extraEnemies: 2, branchChancePercent: 70, puddleChancePercent: 25,
                catwalkLength: 2, wallSconceRarity: 7, slingerWeight: 25);

        public static DungeonBandProfile ForBand(DungeonRegionProfile region, DungeonDepthBand band)
        {
            switch (region)
            {
                case DungeonRegionProfile.Flooded:
                    switch (band)
                    {
                        case DungeonDepthBand.Mid: return FloodedMid;
                        case DungeonDepthBand.Deep: return FloodedDeep;
                        case DungeonDepthBand.Boss: return FloodedBoss;
                        default: return FloodedShallow;
                    }
                case DungeonRegionProfile.Ember:
                    switch (band)
                    {
                        case DungeonDepthBand.Mid: return EmberMid;
                        case DungeonDepthBand.Deep: return EmberDeep;
                        case DungeonDepthBand.Boss: return EmberBoss;
                        default: return EmberShallow;
                    }
                default:
                    switch (band)
                    {
                        case DungeonDepthBand.Mid: return FacilityMid;
                        case DungeonDepthBand.Deep: return FacilityDeep;
                        case DungeonDepthBand.Boss: return FacilityBoss;
                        default: return FacilityShallow;
                    }
            }
        }

        /// <summary>진행 지수(0 = 첫 층)로 바로 프로파일을 얻는 편의 함수.</summary>
        public static DungeonBandProfile ForDepth(DungeonRegionProfile region, int depthIndex) =>
            ForBand(region, DungeonDepthBandRules.ForDepth(depthIndex));
    }
}
