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
    /// <c>playSize</c>를 그대로 쓴다. 던전 전체 보기는 별도 디버그 배율을 쓰며,
    /// 명시적 수직 관찰의 임시 구도는 호출자가 계산한 크기를 <c>playSize</c> 자리에 전달한다.
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
        /// 화면을 끌어 월드를 옮기는 감각이 되도록 스크린 드래그의 반대 방향을
        /// 직교 카메라 중심 이동량으로 바꾼다. 같은 화면 비율만큼 끌면 해상도와
        /// 관계없이 같은 월드 거리를 이동한다.
        /// </summary>
        public static Vector2 ScreenDragToWorldDelta(
            Vector2 screenDelta,
            float orthographicSize,
            float pixelHeight)
        {
            if (!IsFinite(screenDelta.x) || !IsFinite(screenDelta.y))
                throw new ArgumentOutOfRangeException(nameof(screenDelta));
            if (!IsFinite(orthographicSize) || orthographicSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(orthographicSize));
            if (!IsFinite(pixelHeight) || pixelHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));

            float worldUnitsPerPixel = orthographicSize * 2f / pixelHeight;
            return -screenDelta * worldUnitsPerPixel;
        }

        /// <summary>
        /// 이미 보거나 탐색한 칸의 투영 중심 경계 안으로 자유 카메라 중심을 제한한다.
        /// viewport 자체를 경계 안에 가두지 않는 이유는 가장자리 칸도 화면 중앙까지
        /// 끌어 볼 수 있게 하기 위해서다. 경계 밖은 기존 Unknown/void 표현이 맡는다.
        /// </summary>
        public static Vector2 ClampCenterToProjectedBounds(
            Vector2 center,
            IReadOnlyList<Vector2> projectedCenters,
            Vector2 padding)
        {
            if (!IsFinite(center.x) || !IsFinite(center.y))
                throw new ArgumentOutOfRangeException(nameof(center));
            if (projectedCenters == null)
                throw new ArgumentNullException(nameof(projectedCenters));
            if (projectedCenters.Count == 0)
                throw new ArgumentException("투영 중심이 하나 이상 필요하다.", nameof(projectedCenters));
            if (!IsFinite(padding.x) || !IsFinite(padding.y) || padding.x < 0f || padding.y < 0f)
                throw new ArgumentOutOfRangeException(nameof(padding));

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

            return new Vector2(
                Mathf.Clamp(center.x, minX - padding.x, maxX + padding.x),
                Mathf.Clamp(center.y, minY - padding.y, maxY + padding.y));
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
