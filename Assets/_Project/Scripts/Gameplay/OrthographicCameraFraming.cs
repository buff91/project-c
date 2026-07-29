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

    /// <summary>
    /// 허브와 던전의 직교 카메라 배율 계약. 플레이 화면은 씬과 화면비에 관계없이
    /// <c>playSize</c>를 그대로 쓰고, 던전 전체 보기만 별도 디버그 배율을 쓴다.
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
    }
}
