using System;

namespace ProjectC.Core
{
    public enum VerticalOpeningView
    {
        None = 0,
        Downward,
        Upward
    }

    /// <summary>
    /// 실제로 뚫린 Hole을 통한 층간 시야를 판정한다.
    /// StairsUp/Down은 던전 층 전환 링크일 뿐 시야 포털이 아니다.
    /// </summary>
    public static class VerticalOpeningRules
    {
        public static VerticalOpeningView ViewFromFloor(
            GridMap map,
            DungeonHeightModel height,
            int observerFloorIndex,
            GridPos opening,
            int minimumElevation,
            Func<GridPos, bool> isVisible,
            out GridPos landing)
        {
            landing = default;
            if (map == null || height == null || isVisible == null ||
                map.Get(opening)?.kind != TileKind.Hole)
                return VerticalOpeningView.None;

            GridPos? foundLanding = map.FindLandingBelow(opening, minimumElevation);
            if (!foundLanding.HasValue)
                return VerticalOpeningView.None;

            landing = foundLanding.Value;
            int openingFloor = height.FloorIndex(opening.elevation);
            int landingFloor = height.FloorIndex(landing.elevation);

            if (observerFloorIndex == openingFloor && isVisible(opening))
                return VerticalOpeningView.Downward;
            if (observerFloorIndex == landingFloor && isVisible(landing))
                return VerticalOpeningView.Upward;
            return VerticalOpeningView.None;
        }
    }
}
