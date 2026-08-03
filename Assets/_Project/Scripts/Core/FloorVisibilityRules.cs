namespace ProjectC.Core
{
    /// <summary>
    /// 다층 던전의 월드 지오메트리 표시 범위를 결정한다.
    /// 탐색 기록은 층별로 보존하되, 실제 월드에는 현재 층과 수직 개구부만 그린다.
    /// </summary>
    public static class FloorVisibilityRules
    {
        public static bool ShouldRenderWorldGeometry(
            bool debugAll,
            int tileFloorIndex,
            int activeFloorIndex,
            bool visible,
            bool explored,
            bool verticalPreview)
        {
            if (debugAll) return true;
            if (tileFloorIndex == activeFloorIndex)
                return visible || explored;
            return verticalPreview;
        }

        /// <summary>
        /// 지도 실루엣은 실제 월드 지오메트리와 겹치지 않는다.
        /// 현재 활성 층의 아직 보지 않은 mapped 좌표에서만 대신 표시한다.
        /// </summary>
        public static bool ShouldRenderMappedSilhouette(
            bool debugAll,
            int tileFloorIndex,
            int activeFloorIndex,
            bool visible,
            bool explored,
            bool mapped)
        {
            return !debugAll &&
                   tileFloorIndex == activeFloorIndex &&
                   mapped &&
                   !visible &&
                   !explored;
        }
    }
}
