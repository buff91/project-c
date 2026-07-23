using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 수직 이동 수단의 자동 발동 범위와 사다리 월드 표현 크기를 결정한다.
    /// 층 전환 계단은 밟는 즉시 링크를 타지만, 사다리는 명시적 상호작용으로 남긴다.
    /// </summary>
    public static class VerticalTraversalRules
    {
        public static bool TryGetAutomaticFloorDestination(
            GridMap map,
            GridPos entered,
            out GridPos destination)
        {
            destination = default;
            if (map == null) return false;

            TileKind kind = map.Get(entered)?.kind ?? TileKind.Floor;
            if (kind != TileKind.StairsUp && kind != TileKind.StairsDown)
                return false;

            var links = map.LinksFrom(entered);
            if (links.Count == 0) return false;
            destination = links[0];
            return true;
        }

        public static float LadderWorldHeight(
            int elevationDelta,
            float elevationStep,
            float tileHeight)
        {
            float rise = Math.Abs(elevationDelta) * Math.Max(0f, elevationStep);
            float ledgeOverlap = Math.Max(0f, tileHeight) * 0.35f;
            return Math.Max(0.28f, rise + ledgeOverlap);
        }

        public static float LadderScaleY(
            int elevationDelta,
            float elevationStep,
            float tileHeight,
            float spriteWorldHeight)
        {
            if (spriteWorldHeight <= 0f) return 1f;
            return LadderWorldHeight(elevationDelta, elevationStep, tileHeight) /
                   spriteWorldHeight;
        }
    }
}
