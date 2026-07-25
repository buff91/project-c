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
        /// <summary>탈출구 간격(깊이). 3이면 B3·B6·B9에 하나씩 생긴다.</summary>
        public const int FloorInterval = 3;

        /// <summary>
        /// 이 깊이에 중간 탈출구가 있는가. 최심층은 기존 던전 출구가 담당하므로 제외하고,
        /// 첫 층(입구)에도 두지 않는다 — 들어가자마자 나가는 문은 의미가 없다.
        /// </summary>
        public static bool HasExtractionPoint(int depthIndex, int floorCount)
        {
            if (floorCount <= 1) return false;
            if (depthIndex <= 0 || depthIndex >= floorCount - 1) return false;
            return (depthIndex + 1) % FloorInterval == 0;
        }

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
