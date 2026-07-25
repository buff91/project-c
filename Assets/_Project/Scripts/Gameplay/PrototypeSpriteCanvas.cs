using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 절차적 임시 아트의 저수준 캔버스 — 텍스처 생성과 픽셀 드로잉 프리미티브만 소유한다.
    /// **게임 상태를 알지 못한다**(격자·던전·플레이어 참조 없음). 그리기에 필요한 값은 전부 인자로 받는다.
    /// 스프라이트를 만드는 쪽은 `using static ProjectC.Gameplay.PrototypeSpriteCanvas;`로 끌어다 쓴다.
    ///
    /// 64×32 타일 픽셀 규격의 SSOT가 여기다 — `IsoPrototypeDemo`의 동명 상수가 이 값을 참조한다.
    /// </summary>
    internal static class PrototypeSpriteCanvas
    {
        internal const int TilePixelWidth = 64;
        internal const int TilePixelHeight = 32;
        internal const int PixelsPerUnit = 64;

        internal static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Prototype {width}x{height}"
            };

            var clear = new Color32[width * height];
            texture.SetPixels32(clear);
            return texture;
        }

        internal static Sprite CreateSprite(Texture2D texture, Vector2 pivot) =>
            CreateSprite(texture, pivot, PixelsPerUnit);

        /// <summary>
        /// 자체 PPU를 가진 소스(카탈로그 자산)를 복제·가공할 때는 이 오버로드로 소스 PPU를
        /// 물려줘야 한다 — 상수 PPU로 만들면 128-레짐 자산의 월드 크기가 2배로 어긋난다.
        /// </summary>
        internal static Sprite CreateSprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit)
        {
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        internal static void FillSlantedPanel(
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            int panelHeight,
            Color32 baseColor,
            Color32 lightColor,
            Color32 borderColor)
        {
            int minX = Mathf.Min(x0, x1);
            int maxX = Mathf.Max(x0, x1);
            int span = Mathf.Max(1, maxX - minX);
            for (int x = minX; x <= maxX; x++)
            {
                float t = (x - minX) / (float)span;
                int bottom = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int localY = 0; localY <= panelHeight; localY++)
                {
                    int y = bottom + localY;
                    bool border = x <= minX + 1 || x >= maxX - 1 || localY <= 1 || localY >= panelHeight - 1;
                    bool plankLight = !border && (x - minX) % 8 < 2;
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        texture.SetPixel(x, y, border ? borderColor : plankLight ? lightColor : baseColor);
                }
            }
        }

        internal static void DrawThickLine(
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            int thickness,
            Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int radius = Mathf.Max(0, thickness / 2);

            while (true)
            {
                FillRect(texture, x0 - radius, y0 - radius, radius * 2 + 1, radius * 2 + 1, color);
                if (x0 == x1 && y0 == y1) break;
                int twiceError = error * 2;
                if (twiceError >= dy) { error += dy; x0 += sx; }
                if (twiceError <= dx) { error += dx; y0 += sy; }
            }
        }

        internal static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
            {
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(px, py, color);
            }
        }

        internal static bool IsCrackPixel(int x, int y)
        {
            return (x >= 28 && x <= 34 && y == 14 + (x % 3)) ||
                   (y >= 9 && y <= 15 && x == 29 - (y % 2) * 3) ||
                   (y >= 15 && y <= 20 && x == 35 + (y % 3));
        }

        internal static Color32 Shift(Color32 color, int amount)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r + amount, 0, 255),
                (byte)Mathf.Clamp(color.g + amount, 0, 255),
                (byte)Mathf.Clamp(color.b + amount, 0, 255),
                color.a);
        }

        internal static Color32 WithAlpha(Color32 color, byte alpha) =>
            new Color32(color.r, color.g, color.b, alpha);

        internal static Color32 Blend(Color32 from, Color32 to, float amount)
        {
            float t = Mathf.Clamp01(amount);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.a, to.a, t)));
        }

        internal static Color ToRuntimeColor(Color32 color, float alpha) =>
            new Color(color.r / 255f, color.g / 255f, color.b / 255f, alpha);
    }
}
