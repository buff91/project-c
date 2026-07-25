namespace ProjectC.Core
{
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
    /// 깊이 밴드별 프로파일 테이블. 얕을수록 약한 적·낮은 밀도, 깊을수록 단단한 적이
    /// 늘어난다. 값은 순수 데이터라 생성기 RNG 스트림을 흔들지 않는다(조회 전용).
    /// </summary>
    public static class DungeonBandProfiles
    {
        // Shallow(B1~B3): 경비 드론(Skeleton) 없음 — 도입 구간을 확실히 구분한다.
        // 캐치워크 없음(평평한 도입) + 가장 촘촘한 등잔(밝고 읽기 쉬움).
        private static readonly DungeonBandProfile Shallow =
            new DungeonBandProfile(50, 50, 0, extraEnemies: 0, branchChancePercent: 50, puddleChancePercent: 50,
                catwalkLength: 0, wallSconceRarity: 5);

        // Mid(B4~B6): 드론과 사수가 함께 등장, 밀도 소폭 상승. 캐치워크 한 칸으로 높이 전술을 소개한다.
        private static readonly DungeonBandProfile Mid =
            new DungeonBandProfile(15, 40, 30, extraEnemies: 1, branchChancePercent: 60, puddleChancePercent: 50,
                catwalkLength: 1, wallSconceRarity: 6, slingerWeight: 15);

        // Deep(B7~B9): 드론 비중 최다, 파밍/물 반응 무대 증가.
        // 캐치워크가 통로가 되고 등잔은 드물어진다 — 깊이는 어둠과 높이로 읽힌다.
        private static readonly DungeonBandProfile Deep =
            new DungeonBandProfile(5, 35, 40, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 60,
                catwalkLength: 2, wallSconceRarity: 8, slingerWeight: 20);

        // Boss(B10~): Deep와 동일 — 전역 누적 깊이가 9를 넘어도 유효한 혼합을 유지한다.
        // 최심층 아레나 자체는 결투 공간을 비우려 캐치워크를 놓지 않는다(생성기가 아레나 축으로 판정).
        private static readonly DungeonBandProfile Boss =
            new DungeonBandProfile(5, 35, 40, extraEnemies: 1, branchChancePercent: 70, puddleChancePercent: 60,
                catwalkLength: 2, wallSconceRarity: 9, slingerWeight: 20);

        public static DungeonBandProfile ForBand(DungeonDepthBand band)
        {
            switch (band)
            {
                case DungeonDepthBand.Shallow: return Shallow;
                case DungeonDepthBand.Mid: return Mid;
                case DungeonDepthBand.Deep: return Deep;
                case DungeonDepthBand.Boss: return Boss;
                default: return Shallow;
            }
        }

        /// <summary>깊이 인덱스(0=B1)로 바로 프로파일을 얻는 편의 함수.</summary>
        public static DungeonBandProfile ForDepth(int depthIndex) =>
            ForBand(DungeonDepthBandRules.ForDepth(depthIndex));
    }
}
