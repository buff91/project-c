using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public readonly struct OrthographicCameraFrame
    {
        public readonly Vector2 Center;
        public readonly float Size;

        public OrthographicCameraFrame(Vector2 center, float size)
        {
            Center = center;
            Size = size;
        }
    }

    /// <summary>
    /// 허브와 던전의 직교 카메라 배율 계약. 일반 플레이 화면은 씬과 화면비에 관계없이
    /// <c>playSize</c>를 그대로 쓰고, 명시적으로 발주된 히어로 룸만 HUD 안전 영역에 맞춘다.
    /// 던전 전체 보기는 별도 디버그 배율을 쓴다.
    /// </summary>
    public static class OrthographicCameraFraming
    {
        public static OrthographicCameraFrame Follow(
            Vector2 center,
            bool hubMode,
            DungeonViewMode viewMode,
            float playSize,
            float debugSize)
        {
            if (playSize <= 0f) throw new ArgumentOutOfRangeException(nameof(playSize));
            if (debugSize <= 0f) throw new ArgumentOutOfRangeException(nameof(debugSize));

            float size = !hubMode && viewMode == DungeonViewMode.DebugAll
                ? debugSize
                : playSize;
            return new OrthographicCameraFrame(center, size);
        }

        /// <summary>
        /// 이미 현재 시점으로 투영된 월드 중심들의 경계를 직교 카메라의 정규화 viewport 안에 맞춘다.
        /// viewport 중심이 화면 중심과 다르면 카메라 중심도 반대로 보정하여 HUD 아래로 숨지 않게 한다.
        /// </summary>
        public static OrthographicCameraFrame FitProjectedBounds(
            IReadOnlyList<Vector2> projectedCenters,
            float aspect,
            Rect normalizedViewport,
            Vector2 padding,
            float minimumSize)
        {
            if (projectedCenters == null)
                throw new ArgumentNullException(nameof(projectedCenters));
            if (projectedCenters.Count == 0)
                throw new ArgumentException("투영 중심이 하나 이상 필요하다.", nameof(projectedCenters));
            if (!IsFinite(aspect) || aspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            if (!IsValidViewport(normalizedViewport))
                throw new ArgumentOutOfRangeException(nameof(normalizedViewport));
            if (!IsFinite(padding.x) || !IsFinite(padding.y) || padding.x < 0f || padding.y < 0f)
                throw new ArgumentOutOfRangeException(nameof(padding));
            if (!IsFinite(minimumSize) || minimumSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumSize));

            Vector2 first = projectedCenters[0];
            if (!IsFinite(first.x) || !IsFinite(first.y))
                throw new ArgumentException("투영 중심은 유한한 값이어야 한다.", nameof(projectedCenters));

            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int i = 1; i < projectedCenters.Count; i++)
            {
                Vector2 point = projectedCenters[i];
                if (!IsFinite(point.x) || !IsFinite(point.y))
                    throw new ArgumentException("투영 중심은 유한한 값이어야 한다.", nameof(projectedCenters));

                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            minX -= padding.x;
            maxX += padding.x;
            minY -= padding.y;
            maxY += padding.y;

            float requiredForWidth =
                (maxX - minX) / (2f * aspect * normalizedViewport.width);
            float requiredForHeight =
                (maxY - minY) / (2f * normalizedViewport.height);
            float size = Mathf.Max(minimumSize, requiredForWidth, requiredForHeight);

            Vector2 boundsCenter = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            Vector2 viewportCenter = normalizedViewport.center;
            Vector2 center = new Vector2(
                boundsCenter.x - (viewportCenter.x - 0.5f) * 2f * size * aspect,
                boundsCenter.y - (viewportCenter.y - 0.5f) * 2f * size);
            return new OrthographicCameraFrame(center, size);
        }

        private static bool IsValidViewport(Rect viewport)
        {
            if (!IsFinite(viewport.x) ||
                !IsFinite(viewport.y) ||
                !IsFinite(viewport.width) ||
                !IsFinite(viewport.height))
                return false;
            if (viewport.x < 0f || viewport.y < 0f ||
                viewport.width <= 0f || viewport.height <= 0f)
                return false;

            return viewport.x + viewport.width <= 1f &&
                   viewport.y + viewport.height <= 1f;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
