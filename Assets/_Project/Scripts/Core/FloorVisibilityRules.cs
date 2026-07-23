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
    }
}
