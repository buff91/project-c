using System;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>던전 구조를 노출하지 않는 층 전체 안개 배경의 월드 영역.</summary>
    public readonly struct DungeonFogBackdropFrame
    {
        public Vector2 Center { get; }
        public float Width { get; }
        public float Height { get; }

        public DungeonFogBackdropFrame(Vector2 center, float width, float height)
        {
            Center = center;
            Width = width;
            Height = height;
        }
    }

    public static class DungeonFogBackdropLayout
    {
        /// <summary>
        /// x/y 격자의 전체 가능 영역을 아이소 투영한 다이아몬드 경계를 계산한다.
        /// 방·복도처럼 실제로 타일이 존재하는 위치는 사용하지 않으므로 미탐색 구조가 드러나지 않는다.
        /// </summary>
        public static DungeonFogBackdropFrame Calculate(
            IsoGrid iso,
            int width,
            int height,
            int elevation)
        {
            if (iso == null) throw new ArgumentNullException(nameof(iso));
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

            var corners = new[]
            {
                new GridPos(0, 0, elevation),
                new GridPos(width - 1, 0, elevation),
                new GridPos(0, height - 1, elevation),
                new GridPos(width - 1, height - 1, elevation)
            };

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (GridPos corner in corners)
            {
                Vector2 world = iso.GridToWorld(corner);
                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxY = Mathf.Max(maxY, world.y);
            }

            return new DungeonFogBackdropFrame(
                new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f),
                maxX - minX + iso.tileWidth,
                maxY - minY + iso.tileHeight);
        }
    }
}
