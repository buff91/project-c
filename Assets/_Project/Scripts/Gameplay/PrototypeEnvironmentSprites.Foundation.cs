using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// B2 히어로 룸의 얇은 바닥 기초 표현. 기존 타일 스프라이트와 분리된 face-only 자산이라
    /// 바닥 윗면·충돌·격자 규칙에는 관여하지 않고, 현재 화면에 노출된 전면만 골라 붙일 수 있다.
    /// </summary>
    internal sealed partial class PrototypeEnvironmentSprites
    {
        private const int FoundationFaceHeight = 42;
        private const int FoundationPivotY = 26;
        private const int FoundationDepth = 10;

        /// <summary>
        /// 표준 64×32 바닥과 같은 transform에 놓는 10px fascia. 캔버스의 y=10..41은
        /// 바닥 다이아몬드가 차지할 자리지만 여기서는 윗면을 그리지 않아 기존 바닥을 덮지 않는다.
        /// </summary>
        internal Sprite GetB2FoundationFaceSprite(FoundationFaces faces, int ribPhase)
        {
            if (faces == FoundationFaces.None)
                return null;

            int normalizedPhase = ((ribPhase % 4) + 4) % 4;
            string key = $"b2-foundation-face-v1-f{(int)faces}-r{normalizedPhase}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, FoundationFaceHeight);
            bool hasLeft = (faces & FoundationFaces.ScreenLeft) != 0;
            bool hasRight = (faces & FoundationFaces.ScreenRight) != 0;

            if (hasLeft)
                DrawFoundationFace(texture, screenLeft: true);
            if (hasRight)
                DrawFoundationFace(texture, screenLeft: false);

            // 월드 해시가 phase 0인 셀에만 구조 이음매 하나를 둔다. 매 타일마다 반복되는
            // 벽돌/체커 패턴을 피하면서 긴 외곽면의 스케일만 드물게 알려준다.
            if (normalizedPhase == 0)
                DrawFoundationSeam(texture, hasLeft);

            // 이 작은 캐시는 테스트에서 hard-alpha/역할색 계약을 직접 검증할 수 있게 readable로 둔다.
            texture.Apply(false, false);
            cached = CreateSprite(
                texture,
                new Vector2(0.5f, FoundationPivotY / (float)FoundationFaceHeight),
                PixelsPerUnit);
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 외곽의 드문 고정 코너에 매다는 얇은 지지 브래킷. top-center pivot이라 기초면의
        /// 아래 모서리에 직접 맞출 수 있고, 좌우 버전은 명암 방향만 바뀐다.
        /// </summary>
        internal Sprite GetB2FoundationSupportSprite(bool screenLeft)
        {
            string key = $"b2-foundation-support-v1-l{screenLeft}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 12;
            const int height = 38;
            var texture = NewTexture(width, height);
            Color32 outline = _palette.Outline;
            Color32 light = _palette.StoneShadow;
            Color32 dark = _palette.WallShadow;
            Color32 body = screenLeft ? light : dark;
            Color32 shade = screenLeft ? dark : outline;

            // 상단 클램프와 하단 풋은 한쪽으로 한 픽셀 치우쳐 화면 방향을 보존한다.
            int clampX = screenLeft ? 1 : 2;
            int footX = screenLeft ? 2 : 1;
            FillRect(texture, 3, 3, 6, 31, outline);
            FillRect(texture, 4, 4, 4, 29, body);
            FillRect(texture, screenLeft ? 7 : 4, 5, 1, 27, shade);
            FillRect(texture, clampX, 33, 9, 5, outline);
            FillRect(texture, clampX + 1, 34, 7, 3, body);
            FillRect(texture, footX, 0, 9, 5, outline);
            FillRect(texture, footX + 1, 2, 7, 2, shade);

            texture.Apply(false, false);
            cached = CreateSprite(texture, new Vector2(0.5f, 1f), PixelsPerUnit);
            _spriteCache[key] = cached;
            return cached;
        }

        private void DrawFoundationFace(Texture2D texture, bool screenLeft)
        {
            int minX = screenLeft ? 0 : TilePixelWidth / 2;
            int maxX = screenLeft ? TilePixelWidth / 2 - 1 : TilePixelWidth - 1;
            Color32 face = screenLeft ? _palette.StoneShadow : _palette.WallShadow;
            Color32 lip = screenLeft ? _palette.Stone : _palette.StoneShadow;

            for (int x = minX; x <= maxX; x++)
            {
                int top = screenLeft
                    ? FoundationPivotY - x / 2
                    : FoundationPivotY - 16 + (x - TilePixelWidth / 2) / 2;
                int bottom = top - FoundationDepth;
                for (int y = bottom; y <= top; y++)
                {
                    Color32 color = y == top
                        ? lip
                        : y == bottom ? _palette.Outline : face;
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private void DrawFoundationSeam(Texture2D texture, bool preferLeft)
        {
            int x = preferLeft ? 8 : TilePixelWidth - 9;
            int top = preferLeft
                ? FoundationPivotY - x / 2
                : FoundationPivotY - 16 + (x - TilePixelWidth / 2) / 2;
            for (int y = top - FoundationDepth + 1; y < top; y++)
                texture.SetPixel(x, y, _palette.Outline);
        }
    }
}
