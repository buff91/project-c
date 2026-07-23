using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 화면에 투영된 타일 하나. LayerPriority가 높을수록 현재 조작 층에 가깝고,
    /// 같은 레이어에서는 실제 렌더 정렬 순서가 높은 타일을 먼저 고른다.
    /// </summary>
    public readonly struct WorldInputCandidate
    {
        public GridPos Position { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public int LayerPriority { get; }
        public int SortingOrder { get; }

        public WorldInputCandidate(
            GridPos position,
            float centerX,
            float centerY,
            int layerPriority,
            int sortingOrder)
        {
            Position = position;
            CenterX = centerX;
            CenterY = centerY;
            LayerPriority = layerPriority;
            SortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// 화면 입력이 실제 월드 대상으로 전달돼도 되는지 판정한다.
    /// 카메라에 보이는 검은 여백(void)은 격자 좌표로 환산되더라도 맵 입력이 아니다.
    /// </summary>
    public static class WorldInputRules
    {
        /// <summary>
        /// 실제 타일이 정의된 좌표만 월드 탭으로 받는다.
        /// 벽처럼 이동 불가능한 타일도 맵 안이므로 true이며, 이후 상호작용 단계가 처리한다.
        /// </summary>
        public static bool IsMapTile(GridMap map, GridPos target) =>
            map != null && map.Has(target);

        /// <summary>
        /// 화면에 실제 그려진 아이소 다이아몬드만 대상으로 입력 좌표를 고른다.
        /// 현재 층과 아래층 미리보기가 겹치면 LayerPriority가 높은 현재 층이 우선이다.
        /// </summary>
        public static bool TryPickProjectedTile(
            IEnumerable<WorldInputCandidate> candidates,
            float worldX,
            float worldY,
            float tileWidth,
            float tileHeight,
            out GridPos picked)
        {
            picked = default;
            if (candidates == null || tileWidth <= 0f || tileHeight <= 0f)
                return false;

            bool found = false;
            int bestLayer = int.MinValue;
            int bestSorting = int.MinValue;
            float bestDistance = float.MaxValue;
            float halfWidth = tileWidth * 0.5f;
            float halfHeight = tileHeight * 0.5f;

            foreach (WorldInputCandidate candidate in candidates)
            {
                float normalizedX = Math.Abs(worldX - candidate.CenterX) / halfWidth;
                float normalizedY = Math.Abs(worldY - candidate.CenterY) / halfHeight;
                float distance = normalizedX + normalizedY;
                if (distance > 1f) continue;

                bool better = !found ||
                              candidate.LayerPriority > bestLayer ||
                              candidate.LayerPriority == bestLayer &&
                              candidate.SortingOrder > bestSorting ||
                              candidate.LayerPriority == bestLayer &&
                              candidate.SortingOrder == bestSorting &&
                              distance < bestDistance;
                if (!better) continue;

                found = true;
                picked = candidate.Position;
                bestLayer = candidate.LayerPriority;
                bestSorting = candidate.SortingOrder;
                bestDistance = distance;
            }

            return found;
        }
    }
}
