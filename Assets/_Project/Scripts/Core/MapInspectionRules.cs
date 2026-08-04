namespace ProjectC.Core
{
    /// <summary>전술 지도 한 칸에 공개할 수 있는 가장 강한 지식 단계.</summary>
    public enum MapInspectionTileState
    {
        None = 0,
        Mapped = 1,
        Explored = 2,
        Visible = 3
    }

    /// <summary>
    /// 전술 지도는 월드 렌더링과 독립된 읽기 전용 스냅샷이다.
    /// 현재 층만 실시간 시야와 mapped 윤곽을 합성하고, 비활성 층은 탐색 기억만 허용한다.
    /// </summary>
    public static class MapInspectionRules
    {
        public static MapInspectionTileState ResolveTile(
            bool isCurrentFloor,
            bool visible,
            bool explored,
            bool mapped)
        {
            if (!isCurrentFloor)
                return explored ? MapInspectionTileState.Explored : MapInspectionTileState.None;

            if (visible) return MapInspectionTileState.Visible;
            if (explored) return MapInspectionTileState.Explored;
            if (mapped) return MapInspectionTileState.Mapped;
            return MapInspectionTileState.None;
        }

        public static bool CanInspectFloor(bool isCurrentFloor, bool hasExplored) =>
            isCurrentFloor || hasExplored;

        public static bool CanShowLiveEntities(bool isCurrentFloor) => isCurrentFloor;
    }
}
