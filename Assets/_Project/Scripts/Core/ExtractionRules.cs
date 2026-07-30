namespace ProjectC.Core
{
    /// <summary>
    /// 중간 생환(익스트랙션) 규칙. 출구가 최심층에만 있으면 "더 내려갈까"가 사실상
    /// 한 번의 결정이 되고, 반대로 아무 데서나 나갈 수 있으면 위험이 사라진다.
    /// **정해진 층에만 탈출구**를 두어 구간마다 판돈을 걸고 물러설 기회를 만든다.
    ///
    /// 배고픔과 짝을 이룬다 — 다음 탈출구까지 버틸 식량이 남았는지가 곧 결정이다.
    /// </summary>
    public static class ExtractionRules
    {
        /// <summary>
        /// 중간 탈출구가 있는 <b>진행 지수</b>(0 = 첫 층). 4번째·8번째 층 두 곳뿐이다 —
        /// 잦으면 판돈이 사라지고, 없으면 최심층까지 한 번의 결정이 된다.
        /// 사이 구간(1~3 / 5~7 / 9~10번째)이 곧 물러설 수 없는 구간이다.
        /// <para>
        /// <b>층 라벨로 쓰지 않는다</b> — 아케이드 타워은 상승이라 "B4"가 아니라 2F 다.
        /// 표시는 <see cref="DungeonDirectionRules.FloorLabelFor"/>가 만든다.
        /// </para>
        /// </summary>
        public static readonly int[] ExtractionDepths = { 3, 7 };

        /// <summary>
        /// 이 깊이에 중간 탈출구가 있는가. 최심층은 보스를 잡고 나가는 것이 유일한 길이므로
        /// 제외하고, 첫 층(입구)에도 두지 않는다 — 들어가자마자 나가는 문은 의미가 없다.
        /// </summary>
        public static bool HasExtractionPoint(int depthIndex, int floorCount)
        {
            if (floorCount <= 1) return false;
            if (depthIndex <= 0 || depthIndex >= floorCount - 1) return false;

            foreach (int depth in ExtractionDepths)
                if (depth == depthIndex) return true;
            return false;
        }

        /// <summary>
        /// 숨은 방 보상이 비상 송출기일 확률(%). **아주 가끔**이어야 한다 —
        /// 흔하면 "살아 나갈 권리"가 아니라 기본 소지품이 되고 탈출구가 무의미해진다.
        /// </summary>
        public const int BeaconRewardChancePercent = 20;

        /// <summary>이 깊이에서 다음 탈출구까지 남은 층 수(없으면 -1). 안내 문구용.</summary>
        public static int FloorsToNextExtraction(int depthIndex, int floorCount)
        {
            for (int depth = depthIndex + 1; depth < floorCount; depth++)
                if (HasExtractionPoint(depth, floorCount))
                    return depth - depthIndex;
            return -1;
        }
    }
}
