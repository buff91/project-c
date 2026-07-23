using System;
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

    /// <summary>월드 경계를 주어진 화면비의 직교 카메라 안에 맞추는 순수 계산.</summary>
    public static class OrthographicCameraFraming
    {
        public static OrthographicCameraFrame Fit(
            float minX,
            float maxX,
            float minY,
            float maxY,
            float aspect,
            float minimumSize,
            float horizontalPadding,
            float verticalPadding)
        {
            if (maxX < minX) throw new ArgumentException("maxX must be greater than or equal to minX.");
            if (maxY < minY) throw new ArgumentException("maxY must be greater than or equal to minY.");
            if (aspect <= 0f) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (minimumSize <= 0f) throw new ArgumentOutOfRangeException(nameof(minimumSize));
            if (horizontalPadding < 0f)
                throw new ArgumentOutOfRangeException(nameof(horizontalPadding));
            if (verticalPadding < 0f)
                throw new ArgumentOutOfRangeException(nameof(verticalPadding));

            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float halfWidth = (maxX - minX) * 0.5f + horizontalPadding;
            float halfHeight = (maxY - minY) * 0.5f + verticalPadding;
            float size = Mathf.Max(minimumSize, halfHeight, halfWidth / aspect);
            return new OrthographicCameraFrame(center, size);
        }
    }
}
