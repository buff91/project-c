using ProjectC.Core;
using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// FOV 밖 현재 층의 지도 지식 표현. 실제 타일 재질을 축소 복제하지 않고,
    /// 선·점으로 된 장비 지도 레이어만 그려 다른 층의 실제 표면 미리보기와 분리한다.
    /// </summary>
    internal sealed partial class PrototypeEnvironmentSprites
    {
        internal Sprite GetMapKnowledgeSprite(MapSilhouetteKind kind)
        {
            string key = $"map-knowledge-{kind}-v1";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            var clear = new Color32(0, 0, 0, 0);
            var dashedEdge = new Color32(255, 255, 255, 210);
            var stipple = new Color32(255, 255, 255, 58);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond =
                    Mathf.Abs((px - 31.5f) / 32f) +
                    Mathf.Abs((py - 15.5f) / 16f);
                bool edge = diamond > 0.84f && diamond <= 0.94f;
                bool dash = (((px / 4) + (py / 2)) & 1) == 0;
                bool dot = diamond <= 0.72f &&
                           py % 4 == 2 &&
                           (px + (py / 4) * 4) % 8 == 0;
                texture.SetPixel(
                    px,
                    py,
                    edge && dash ? dashedEdge : dot ? stipple : clear);
            }

            DrawMapKnowledgeGlyph(texture, kind);
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            cached.name = $"Map Knowledge {kind}";
            _spriteCache[key] = cached;
            return cached;
        }

        private static void DrawMapKnowledgeGlyph(
            Texture2D texture,
            MapSilhouetteKind kind)
        {
            var glyph = new Color32(255, 255, 255, 235);
            switch (kind)
            {
                case MapSilhouetteKind.Barrier:
                    // 평행 차단선: 실제 벽의 높이·재질을 공개하지 않고 통행 불가만 말한다.
                    DrawThickLine(texture, 23, 13, 41, 13, 1, glyph);
                    DrawThickLine(texture, 23, 18, 41, 18, 1, glyph);
                    break;

                case MapSilhouetteKind.Door:
                    // 열린 하단을 가진 문틀. 골드/시안 없이 형태만으로 Door 범주를 읽는다.
                    DrawThickLine(texture, 26, 10, 26, 20, 1, glyph);
                    DrawThickLine(texture, 38, 10, 38, 20, 1, glyph);
                    DrawThickLine(texture, 26, 20, 38, 20, 1, glyph);
                    break;

                case MapSilhouetteKind.Gap:
                    // 속이 빈 작은 다이아: 다른 층 관찰의 X와 겹치지 않는 void 표식이다.
                    DrawThickLine(texture, 32, 10, 43, 16, 1, glyph);
                    DrawThickLine(texture, 43, 16, 32, 22, 1, glyph);
                    DrawThickLine(texture, 32, 22, 21, 16, 1, glyph);
                    DrawThickLine(texture, 21, 16, 32, 10, 1, glyph);
                    break;
            }
        }
    }
}
