namespace ProjectC.Core
{
    /// <summary>
    /// 최심층 보스 공간(아레나) 판정. 생성기가 보스를 모르는 것과 같은 방식으로
    /// "가장 깊은 층"만 상대(relative) 깊이로 식별한다 — 짧은 테스트 던전에서도
    /// 바닥 층이 아레나가 되도록. 시각 리스킨용 <see cref="DungeonDepthBand.Boss"/>(절대 깊이 9+)와
    /// 는 다른 축이며 혼동하지 않는다.
    /// </summary>
    public static class DungeonBossArenaRules
    {
        /// <summary>가장 깊은 층(다음 던전 출구가 있는 층)인가.</summary>
        public static bool IsArenaFloor(int depthIndex, int floorCount) =>
            floorCount > 0 && depthIndex == floorCount - 1;

        /// <summary>아레나 바로 위층 — 접근 전조를 알릴 층.</summary>
        public static bool IsApproachFloor(int depthIndex, int floorCount) =>
            floorCount > 1 && depthIndex == floorCount - 2;

        /// <summary>
        /// 아레나 바로 위층에 들어섰을 때 한 번 띄울 전조 문장. 보스가 없는 던전이거나
        /// 이미 처치했으면 알리지 않는다 — 이미 끝난 위협을 다시 예고하지 않기 위해서다.
        /// 문구는 층 이름이 아니라 "아래에서 무엇이 기다리는가"를 말한다.
        /// </summary>
        public static bool TryApproachCue(
            string bossName,
            int depthIndex,
            int floorCount,
            bool bossDefeated,
            out string message)
        {
            message = null;
            if (bossDefeated || string.IsNullOrWhiteSpace(bossName)) return false;
            if (!IsApproachFloor(depthIndex, floorCount)) return false;

            message = $"바닥이 낮게 울린다 — 한 층 아래에서 {bossName}이(가) 기다린다";
            return true;
        }
    }
}
