using ProjectC.Core;
using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 던전·허브 환경(바닥·벽·문·개구부·광원 타일)의 런타임 임시 아트.
    /// 외부 스프라이트가 없을 때 64×32 픽셀 규격으로 절차적으로 그리고,
    /// `IsoVisualCatalog` 슬롯이 채워져 있으면 그 스프라이트를 공용 톤으로 리맵해서 쓴다.
    ///
    /// **격자·던전·플레이어를 참조하지 않는다.** 필요한 사실은 호스트가
    /// <see cref="TileVisualFacts"/> 로 풀어서 넘긴다 — 그래서 어떤 층·어떤 회전에서든
    /// 같은 입력이면 같은 그림이 나온다. 역할색은 <see cref="PrototypePalette"/> 한 곳만 묻는다.
    /// </summary>
    internal sealed class PrototypeEnvironmentSprites
    {
        private readonly PrototypeSpriteCache _spriteCache;
        private readonly PrototypePalette _palette;

        internal PrototypeEnvironmentSprites(PrototypeSpriteCache spriteCache, PrototypePalette palette)
        {
            _spriteCache = spriteCache;
            _palette = palette;
        }

        internal enum EnvironmentAccentMode
        {
            None,
            Wood,
            Signal,
        }

        internal Sprite GetDungeonFogBackdropSprite()
        {
            if (_palette.Catalog != null && _palette.Catalog.dungeonBackdrop != null)
                return _palette.Catalog.dungeonBackdrop;

            Color32 fog = _palette.Fog;
            Color32 fogEdge = _palette.FogEdge;
            string key = $"fog-backdrop-{fog}-{fogEdge}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            // 캔버스 상수(64×32) 기준 2×1타일 크기다 — 절차 규격은 64-레짐에 고정돼 있으므로
            // 카탈로그 자산이 128-레짐이 되어도 이 상수를 올리지 말 것(올리면 절반 크기가 된다).
            const int width = 128;
            const int height = 64;
            var texture = NewTexture(width, height);
            var transparent = new Color32(0, 0, 0, 0);
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                float diamond = Mathf.Abs((px - 63.5f) / 64f) +
                                Mathf.Abs((py - 31.5f) / 32f);
                if (diamond > 1f)
                {
                    texture.SetPixel(px, py, transparent);
                    continue;
                }

                bool edge = diamond > 0.985f;
                Color32 color = edge
                    ? fogEdge
                    : fog;
                texture.SetPixel(px, py, color);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetTileSprite(TileKind kind, GridPos pos, in TileVisualFacts facts)
        {
            // 허브는 던전용 카탈로그 바닥을 공유하지 않는다. 같은 64×32 투영을 유지하되
            // 따뜻한 자주빛 석재와 낮은 대비의 줄눈으로 휴식 공간의 온도를 분리한다.
            if (facts.HubMode && kind == TileKind.Floor)
                return GetHubFloorSprite(pos, facts.Extruded);

            if (kind == TileKind.SecretDoor)
                return GetSecretDoorSprite(facts.Context, facts.PlaneRisesRight, facts.SecretHinted);

            if (kind == TileKind.SecretPassage)
                return GetDoorSprite(TileKind.DoorOpen, facts.Context, facts.PlaneRisesRight);

            if (kind == TileKind.DoorClosed || kind == TileKind.DoorOpen)
            {
                if (_palette.Catalog != null)
                {
                    Sprite mapped = _palette.Catalog.DoorFor(kind, facts.PlaneRisesRight);
                    if (mapped != null)
                        return GetToneMappedEnvironmentSprite(
                            mapped,
                            _palette.Stone,
                            EnvironmentAccentMode.Wood);
                }

                return GetDoorSprite(kind, facts.Context, facts.PlaneRisesRight);
            }

            if (kind == TileKind.Stairs ||
                kind == TileKind.StairsUp ||
                kind == TileKind.StairsDown)
            {
                if (_palette.Catalog != null)
                {
                    Sprite mapped = _palette.Catalog.StairsFor(kind, facts.PlaneRisesRight);
                    if (mapped != null)
                        return GetToneMappedEnvironmentSprite(
                            mapped,
                            _palette.Stone,
                            EnvironmentAccentMode.Signal);
                }
            }

            DungeonVisualContext context = facts.Context;
            bool extruded = facts.Extruded;
            int variation =
                Mathf.Abs(pos.x * 17 + pos.y * 31 + context.ProgressIndex * 13) % 8;
            int variant = variation % 4;
            Color32 baseColor = _palette.SurfaceFor(context);

            if (_palette.Catalog != null)
            {
                Sprite mapped =
                    facts.HospitalDressing && kind == TileKind.Floor
                        ? _palette.Catalog.HospitalFloorFor(variation)
                        : null;
                if (mapped == null)
                    mapped = _palette.Catalog.TileFor(kind, context);
                if (mapped != null)
                {
                    // 밴드 전용 바닥 아트가 없는 동안만 절차 오버레이가 슬롯을 임시 대행한다
                    // (docs/STATUS.md "깊이 변주의 통로" 참조 — 전용 아트가 오면 자동 비활성).
                    // Hole/WeakFloor는 바닥이 아니라 전용 표식이라 마모를 얹지 않는다.
                    DungeonDepthBand overlayBand =
                        !facts.HubMode &&
                        kind != TileKind.Hole &&
                        kind != TileKind.WeakFloor &&
                        _palette.Catalog.BandFloorFallsBackToShared(context)
                            ? context.DepthBand
                            : DungeonDepthBand.Shallow;
                    return GetMappedTileSprite(
                        mapped, baseColor, extruded, facts.HubMode, overlayBand);
                }
            }

            string key =
                $"tile-{kind}-d{context.ProgressIndex}-h{context.LocalHeight}-v{variant}-x{extruded}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int textureHeight = extruded ? 48 : TilePixelHeight;
            int topOffset = extruded ? 16 : 0;
            var texture = NewTexture(TilePixelWidth, textureHeight);
            Color32 transparent = new Color32(0, 0, 0, 0);

            if (extruded)
                DrawExtrudedSides(texture, baseColor, facts.HubMode);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f)
                {
                    if (!extruded)
                        texture.SetPixel(px, py, transparent);
                    continue;
                }

                bool border = diamond > 0.88f;
                Color32 color = border
                    ? _palette.Seam
                    : baseColor;

                bool stoneJoint = diamond < 0.72f &&
                                  ((px + py * 3 + variant * 11) % 29 == 0 ||
                                   (px * 2 - py + variant * 7) % 37 == 0);
                if (stoneJoint) color = _palette.StoneShadow;

                bool moss = variant == 2 &&
                            py < 15 &&
                            px > 9 &&
                            px < 23;
                if (moss && (px + py) % 5 < 2)
                    color = _palette.Moss;

                // 캐치워크(+2단 이상)는 석재가 아니라 걸린 금속 격자로 읽힌다. 색상군은 공통 톤을
                // 유지하고(같은 석재색의 명도 대비) 패턴만 바꿔 "얹힌 발판"임을 알린다.
                if (context.LocalHeight >= 2 && !border)
                {
                    bool grate = (px / 4 + py / 2) % 2 == 0;
                    bool rail = diamond > 0.72f && diamond <= 0.88f;
                    color = rail
                        ? _palette.StoneLight
                        : grate ? _palette.StoneShadow : color;
                }

                if (kind == TileKind.DoorClosed)
                {
                    bool band = (px + py * 2) % 13 < 3;
                    bool iron = Mathf.Abs(px - 32) < 2 || Mathf.Abs(py - 16) < 2;
                    color = border || iron
                        ? _palette.Outline
                        : band ? _palette.WoodLight : _palette.Wood;
                }
                else if (kind == TileKind.DoorOpen)
                {
                    bool threshold = py > 11 && py < 20 && Mathf.Abs(px - 32) < 22;
                    color = border
                        ? _palette.Outline
                        : threshold ? _palette.WoodLight : color;
                }
                // 개구부는 빛이 없는 허공이다 — 발광 테두리 대신 어두운 윤곽, 중심으로
                // 갈수록 짙어져 "떨어지는 곳"으로 읽힌다 (SYSTEMS 「수직 이동」).
                else if (kind == TileKind.Hole)
                    color = border
                        ? _palette.Outline
                        : WithAlpha(_palette.Void, diamond < 0.5f ? (byte)244 : (byte)214);
                else if (kind == TileKind.WeakFloor && IsCrackPixel(px, py))
                    color = _palette.Outline;
                else if (kind == TileKind.Stairs && ((px + py * 2) % 12 < 3))
                    color = border ? _palette.Outline : _palette.StoneLight;
                // Ladder는 바닥 문양이 아니라 두 발판 사이에 세워진 별도 월드 오브젝트로 그린다.
                else if (kind == TileKind.StairsDown && ((px + py) % 10 < 3))
                    color = border ? _palette.Outline : _palette.Amber;
                else if (kind == TileKind.StairsUp && ((px + py) % 10 < 3))
                    color = border ? _palette.Outline : _palette.Magic;

                texture.SetPixel(px, py + topOffset, color);
            }

            texture.Apply(false, true);
            cached = CreateSprite(
                texture,
                extruded ? new Vector2(0.5f, 32f / 48f) : new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetHubFloorSprite(GridPos pos, bool extruded)
        {
            int variant = Mathf.Abs(pos.x * 19 + pos.y * 37) % 5;
                        string key = $"hub-floor-v{variant}-x{extruded}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int textureHeight = extruded ? 48 : TilePixelHeight;
            int topOffset = extruded ? 16 : 0;
            var texture = NewTexture(TilePixelWidth, textureHeight);
            Color32 stone = new Color32(72, 55, 54, 255);
            Color32 stoneLight = new Color32(87, 65, 57, 255);
            Color32 stoneShadow = new Color32(60, 47, 50, 255);
            Color32 seam = new Color32(29, 25, 31, 255);

            if (extruded)
                DrawExtrudedSides(texture, stoneShadow, true);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) +
                                Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f) continue;

                bool border = diamond > 0.94f;
                int cluster = (px / 8 + py / 4 + variant * 3) % 7;
                Color32 color = border
                    ? seam
                    : cluster == 0 ? stoneLight
                    : cluster == 1 ? stoneShadow
                    : stone;

                // 넓은 색 노이즈 대신 드문 픽셀 군집만 두어 캐릭터 실루엣을 방해하지 않는다.
                bool chip = diamond < 0.72f &&
                            ((px + py * 5 + variant * 13) % 47 == 0 ||
                             (px * 3 - py + variant * 11) % 59 == 0);
                if (chip) color = Shift(stoneShadow, -7);

                // 좌상단 광원을 모든 타일에 동일하게 유지한다.
                if (!border && px < 31 && py > 15 && (px + py + variant) % 17 == 0)
                    color = Shift(stoneLight, 5);

                texture.SetPixel(px, py + topOffset, color);
            }

            texture.Apply(false, true);
            cached = CreateSprite(
                texture,
                extruded ? new Vector2(0.5f, 32f / 48f) : new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetMappedTileSprite(
            Sprite topSprite,
            Color32 baseColor,
            bool extruded,
            bool hubFaces,
            DungeonDepthBand overlayBand = DungeonDepthBand.Shallow)
        {
            Texture2D source = topSprite.texture;
            Rect sourceRect = topSprite.rect;
            int sourceWidth = Mathf.RoundToInt(sourceRect.width);
            int sourceHeight = Mathf.RoundToInt(sourceRect.height);
            // 카탈로그 바닥은 64×32의 정수 배(128×64 …)까지 받는다 — 128-레짐 자산이 와도
            // 톤매핑과 단차 측면을 잃지 않는다. 배율이 어긋난 소스만 원본 그대로 돌려준다.
            int scale = sourceWidth / TilePixelWidth;
            if (source == null || !source.isReadable ||
                scale < 1 ||
                sourceWidth != TilePixelWidth * scale ||
                sourceHeight != TilePixelHeight * scale)
                return topSprite;

            // 캐시 키에 밴드가 반드시 들어간다 — 빠지면 첫 밴드가 그린 결과를 전 층이 재사용한다.
            // 타일별 variant는 키에 넣지 않는다(타일 수만큼 텍스처가 생기는 캐시 폭발 방지).
            string key =
                $"mapped-tile-{topSprite.name}-{sourceWidth}x{sourceHeight}" +
                $"-{baseColor.r}-{baseColor.g}-{baseColor.b}-x{extruded}-b{overlayBand}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int textureHeight = (extruded ? 48 : TilePixelHeight) * scale;
            int topOffset = extruded ? 16 * scale : 0;
            var texture = NewTexture(TilePixelWidth * scale, textureHeight);
            if (extruded)
                DrawExtrudedSides(texture, baseColor, hubFaces, scale);

            Color[] pixels = source.GetPixels(
                Mathf.RoundToInt(sourceRect.x),
                Mathf.RoundToInt(sourceRect.y),
                sourceWidth,
                sourceHeight);
            for (int py = 0; py < sourceHeight; py++)
            for (int px = 0; px < sourceWidth; px++)
            {
                Color pixel = pixels[py * sourceWidth + px];
                if (pixel.a <= 0f) continue;
                Color32? overlay = BandOverlayColor(overlayBand, px / scale, py / scale);
                texture.SetPixel(
                    px,
                    py + topOffset,
                    overlay.HasValue
                        ? ToRuntimeColor(overlay.Value, pixel.a)
                        : ToneMapEnvironmentPixel(pixel, baseColor));
            }

            texture.Apply(false, true);
            cached = CreateSprite(
                texture,
                extruded ? new Vector2(0.5f, 32f / 48f) : new Vector2(0.5f, 0.5f),
                PixelsPerUnit * scale);
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetToneMappedEnvironmentSprite(
            Sprite sourceSprite,
            Color32 target,
            EnvironmentAccentMode accentMode = EnvironmentAccentMode.None)
        {
            Texture2D source = sourceSprite.texture;
            Rect sourceRect = sourceSprite.rect;
            if (source == null || !source.isReadable) return sourceSprite;

            int width = Mathf.RoundToInt(sourceRect.width);
            int height = Mathf.RoundToInt(sourceRect.height);
            string key =
                $"mapped-env-{sourceSprite.name}-{target.r}-{target.g}-{target.b}-{accentMode}-{width}x{height}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(width, height);
            Color[] pixels = source.GetPixels(
                Mathf.RoundToInt(sourceRect.x),
                Mathf.RoundToInt(sourceRect.y),
                width,
                height);
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                Color pixel = pixels[py * width + px];
                if (pixel.a > 0f)
                    texture.SetPixel(px, py, ToneMapEnvironmentPixel(pixel, target, accentMode));
            }

            texture.Apply(false, true);
            Vector2 pivot = new Vector2(
                sourceSprite.pivot.x / sourceRect.width,
                sourceSprite.pivot.y / sourceRect.height);
            // 소스 PPU 상속 — 상수 PPU로 만들면 128-레짐 문/계단/벽의 월드 크기가 2배가 된다.
            cached = CreateSprite(texture, pivot, sourceSprite.pixelsPerUnit);
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 절차 밴드 오버레이 — **임시 조치.** 밴드 전용 바닥 슬롯(mid/deep/boss)이 비어 있는
        /// 동안만 공용 바닥 위에 층대(帶) 구분을 얹는다. 전용 아트가 연결되면 호출부 판정
        /// (BandFloorFallsBackToShared)이 자동으로 끈다 — 그때 이 함수는 지워도 된다.
        ///
        /// 규칙: 석재 "색"은 깊이별로 바꾸지 않는다(DungeonSurfaceFor 테스트 고정) —
        /// 기존 역할색(Seam/StoneShadow/StoneLight/Moss) 안에서 **배치 밀도만** 변주한다.
        /// 좌표는 원본 픽셀 밀도(64-공간)로 받아 128-레짐에서도 패턴 간격이 유지된다.
        /// </summary>
        private Color32? BandOverlayColor(DungeonDepthBand band, int sx, int sy)
        {
            switch (band)
            {
                case DungeonDepthBand.Mid:
                    // 마모·파편 — 드문 깨짐 군집이 "쓰인 지 오래된 층"을 알린다.
                    if ((sx + sy * 5) % 31 == 0) return _palette.StoneShadow;
                    if ((sx * 3 - sy + 7) % 43 == 0) return _palette.Seam;
                    return null;
                case DungeonDepthBand.Deep:
                    // 잠식 — 이끼/오염 군집이 늘고 그림자 얼룩이 붙는다.
                    if ((sx + sy * 3) % 23 == 0 && (sx + sy) % 2 == 0) return _palette.Moss;
                    if ((sx * 5 + sy * 2) % 41 == 0) return _palette.StoneShadow;
                    return null;
                case DungeonDepthBand.Boss:
                    // 아레나 — 줄눈이 도드라지고 파편·하이라이트가 최다.
                    if ((sx + sy * 2) % 17 == 0) return _palette.Seam;
                    if ((sx * 3 + sy) % 29 == 0) return _palette.StoneShadow;
                    if ((sx * 7 - sy + 11) % 47 == 0) return _palette.StoneLight;
                    return null;
                default:
                    return null;
            }
        }

        private Color ToneMapEnvironmentPixel(
            Color source,
            Color32 target,
            EnvironmentAccentMode accentMode = EnvironmentAccentMode.None)
        {
            float luminance =
                source.r * 0.2126f +
                source.g * 0.7152f +
                source.b * 0.0722f;
            float chroma =
                Mathf.Max(source.r, Mathf.Max(source.g, source.b)) -
                Mathf.Min(source.r, Mathf.Min(source.g, source.b));

            if (accentMode != EnvironmentAccentMode.None &&
                chroma >= 0.16f &&
                luminance >= 0.14f)
            {
                bool teal =
                    source.g > source.r * 1.12f &&
                    source.b > source.r * 1.12f;
                if (teal)
                    return ToRuntimeColor(_palette.Magic, source.a);

                bool warm =
                    source.r > source.b * 1.15f &&
                    source.g > source.b * 1.05f;
                if (warm)
                {
                    Color32 accent = accentMode == EnvironmentAccentMode.Wood
                        ? luminance >= 0.46f ? _palette.WoodLight : _palette.Wood
                        : luminance >= 0.58f ? _palette.AmberCore : _palette.Amber;
                    return ToRuntimeColor(accent, source.a);
                }
            }

            Color32 wall = _palette.Wall;
            bool wallRamp =
                target.r == wall.r &&
                target.g == wall.g &&
                target.b == wall.b;
            Color32 stoneLight = _palette.StoneLight;
            bool raisedSurfaceRamp =
                target.r == stoneLight.r &&
                target.g == stoneLight.g &&
                target.b == stoneLight.b;
            Color32 mapped = luminance < 0.16f
                ? _palette.Outline
                : luminance < 0.28f
                    ? wallRamp ? _palette.WallShadow : _palette.StoneShadow
                    : luminance < 0.5f
                        ? wallRamp
                            ? _palette.Wall
                            : raisedSurfaceRamp ? _palette.StoneLight : _palette.Stone
                        : wallRamp ? _palette.WallLight : _palette.StoneLight;
            return ToRuntimeColor(mapped, source.a);
        }

        /// <summary>
        /// 전면 타일의 두께(좌·우 측면). 허브는 바닥색을 어둡게 깎아 쓰고,
        /// 던전은 공용 석재/벽 그림자 역할색을 쓴다 — 그래서 허브 여부를 받아야 한다.
        /// </summary>
        private void DrawExtrudedSides(Texture2D texture, Color32 baseColor, bool hubFaces, int scale = 1)
        {
            Color32 leftFace = hubFaces ? Shift(baseColor, -24) : _palette.StoneShadow;
            Color32 rightFace = hubFaces ? Shift(baseColor, -38) : _palette.WallShadow;
            int half = 32 * scale;
            int width = 64 * scale;
            for (int py = 0; py < 32 * scale; py++)
            {
                int leftMin = py < 16 * scale ? half - py * 2 : 0;
                int leftMax = py < 16 * scale ? half : width - py * 2;
                int rightMin = py < 16 * scale ? half : py * 2;
                int rightMax = py < 16 * scale ? half + py * 2 : width - 1;

                // 모르타르 줄눈은 원본 픽셀 밀도(py/scale)로 계산한다 — 배율이 올라도 간격이 유지된다.
                for (int px = Mathf.Max(0, leftMin); px <= Mathf.Min(half - 1, leftMax); px++)
                {
                    bool mortar = (py / scale) % 7 == 0 ||
                                  (px / scale + ((py / scale) / 7) * 8) % 19 == 0;
                    texture.SetPixel(px, py, mortar ? _palette.Outline : leftFace);
                }
                for (int px = Mathf.Max(half, rightMin); px <= Mathf.Min(width - 1, rightMax); px++)
                {
                    bool mortar = (py / scale) % 7 == 0 ||
                                  (px / scale - ((py / scale) / 7) * 7) % 21 == 0;
                    texture.SetPixel(px, py, mortar ? _palette.Outline : rightFace);
                }
            }
        }

        internal Sprite GetDoorSprite(TileKind kind, DungeonVisualContext context, bool risesRight)
        {
            bool closed = kind == TileKind.DoorClosed;
                        string key =
                $"door-iso-{kind}-d{context.ProgressIndex}-h{context.LocalHeight}-r{risesRight}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 64;
            const int height = 80;
            var texture = NewTexture(width, height);
            Color32 baseColor = _palette.SurfaceFor(context);

            // 문 아래에도 동일한 64×32 바닥 다이아몬드를 유지한다.
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f) continue;
                texture.SetPixel(px, py, diamond > 0.88f ? _palette.Seam : baseColor);
            }

            Color32 stone = _palette.Stone;
            Color32 stoneLight = _palette.StoneLight;
            Color32 wood = _palette.Wood;
            Color32 woodLight = _palette.WoodLight;
            Color32 iron = _palette.Iron;

            // 통로 축에 수직인 아이소 평면을 사용한다. 회전해도 문짝이 벽의 사선과 맞는다.
            int leftBase = risesRight ? 9 : 25;
            int rightBase = risesRight ? 25 : 9;
            const int leftX = 15;
            const int rightX = 49;
            const int frameHeight = 40;

            FillSlantedPanel(
                texture,
                leftX,
                leftBase,
                rightX,
                rightBase,
                frameHeight,
                _palette.Void,
                _palette.Fog,
                _palette.Outline);

            if (closed)
            {
                int innerLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 5f / 34f)) + 3;
                int innerRightY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 29f / 34f)) + 3;
                FillSlantedPanel(texture, 20, innerLeftY, 44, innerRightY, 32, wood, woodLight, iron);
                DrawThickLine(texture, 20, innerLeftY + 11, 44, innerRightY + 11, 2, iron);
                DrawThickLine(texture, 20, innerLeftY + 24, 44, innerRightY + 24, 2, iron);
                FillRect(texture, risesRight ? 37 : 24, risesRight ? 31 : 27, 3, 3,
                    _palette.AmberCore);
            }
            else
            {
                // 열린 문짝은 오른쪽 기둥 쪽으로 접혀 중앙 통과 방향을 그대로 드러낸다.
                int foldedLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 25f / 34f)) + 3;
                int foldedRightY = rightBase + 3;
                FillSlantedPanel(texture, 40, foldedLeftY, 47, foldedRightY, 31,
                    wood, woodLight, iron);
            }

            DrawThickLine(texture, leftX, leftBase, leftX, leftBase + frameHeight, 5, stone);
            DrawThickLine(texture, rightX, rightBase, rightX, rightBase + frameHeight, 5,
                _palette.StoneShadow);
            DrawThickLine(texture, leftX, leftBase + frameHeight, rightX, rightBase + frameHeight, 6, stone);
            DrawThickLine(texture, leftX + 2, leftBase + frameHeight + 1,
                rightX - 2, rightBase + frameHeight + 1, 2, stoneLight);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 16f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        /// 실제 Aseprite 에셋이 들어와도 공개 전에는 문 실루엣을 노출하지 않는 규칙을 유지한다.
        /// </summary>
        internal Sprite GetSecretDoorSprite(DungeonVisualContext context, bool risesRight, bool hinted)
        {
            
            string key =
                $"secret-wall-d{context.ProgressIndex}-lh{context.LocalHeight}-r{risesRight}-h{hinted}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 64;
            const int height = 80;
            var texture = NewTexture(width, height);
            Color32 baseColor = _palette.SurfaceFor(context);

            // 발밑은 일반 바닥과 이어져 보이되, 통로 평면 전체는 회색 석재로 봉한다.
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) +
                                Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f) continue;
                texture.SetPixel(px, py, diamond > 0.88f ? _palette.Seam : baseColor);
            }

            int leftBase = risesRight ? 9 : 25;
            int rightBase = risesRight ? 25 : 9;
            Color32 stone = _palette.Wall;
            Color32 stoneLight = _palette.WallLight;
            Color32 mortar = _palette.Seam;
            FillSlantedPanel(
                texture,
                13,
                leftBase,
                51,
                rightBase,
                42,
                stone,
                stoneLight,
                _palette.Outline);

            // 일반 벽과 같은 큰 석재 줄눈. 네모 문짝/손잡이는 의도적으로 그리지 않는다.
            for (int row = 1; row <= 3; row++)
            {
                int leftY = leftBase + row * 10;
                int rightY = rightBase + row * 10;
                DrawThickLine(texture, 15, leftY, 49, rightY, 1, mortar);
            }
            DrawThickLine(
                texture,
                risesRight ? 35 : 29,
                risesRight ? leftBase + 11 : rightBase + 11,
                risesRight ? 35 : 29,
                risesRight ? leftBase + 22 : rightBase + 22,
                1,
                mortar);
            DrawThickLine(
                texture,
                risesRight ? 27 : 37,
                risesRight ? leftBase + 22 : rightBase + 22,
                risesRight ? 27 : 37,
                risesRight ? leftBase + 32 : rightBase + 32,
                1,
                mortar);

            // 한 칸 옆에서만 읽히는 작은 균열. 금색 발광은 색이 아니라 상호작용 가능성의 보조 신호다.
            Color32 crack = hinted
                ? _palette.AmberCore
                : _palette.WallShadow;
            int centerY = (leftBase + rightBase) / 2 + 21;
            DrawThickLine(texture, 32, centerY + 13, 29, centerY + 7, hinted ? 2 : 1, crack);
            DrawThickLine(texture, 29, centerY + 7, 34, centerY + 2, hinted ? 2 : 1, crack);
            DrawThickLine(texture, 34, centerY + 2, 31, centerY - 4, hinted ? 2 : 1, crack);
            if (hinted)
            {
                FillRect(texture, 26, centerY + 5, 2, 2, _palette.AmberCore);
                FillRect(texture, 35, centerY, 2, 2, _palette.AmberCore);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 16f / height));
            _spriteCache[key] = cached;
            return cached;
        }
        internal Sprite GetWallSprite(bool torch)
        {
            // 던전 전용 — 허브 벽은 호출부(RefreshRearWalls)에서 GetHubWallSprite 로 분기한다.
            string key = torch ? "rear-wall-torch" : "rear-wall";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 32;
            const int height = 56;
            const int wallHeight = 40;
            var texture = NewTexture(width, height);
            Color32 stone = _palette.Wall;
            Color32 stoneLight = _palette.WallLight;
            Color32 stoneDark = _palette.WallShadow;

            // 바닥 모서리와 같은 2:1 경사를 가진 평행사변형 벽 패널.
            // 인접 타일의 패널 끝점이 이어져 회전해도 하나의 석벽처럼 보인다.
            for (int px = 0; px < width; px++)
            {
                int bottom = 16 - px / 2;
                for (int localY = 0; localY < wallHeight; localY++)
                {
                    int py = bottom + localY;
                    bool edge = px == 0 || px == width - 1 || localY <= 1 || localY >= wallHeight - 2;
                    bool mortar = localY == 13 || localY == 26 ||
                                  (localY < 13 && px == 16) ||
                                  (localY >= 13 && localY < 26 && (px == 8 || px == 24)) ||
                                  (localY >= 26 && px == 16);
                    bool topCap = localY >= wallHeight - 5;
                    Color32 color = edge || mortar
                        ? _palette.Outline
                        : topCap ? stoneLight : ((px + localY) % 11 == 0 ? stoneLight : stone);
                    texture.SetPixel(px, py, color);
                }
            }

            if (torch)
            {
                FillRect(texture, 13, 20, 6, 3, stoneDark);
                FillRect(texture, 15, 15, 3, 12, _palette.Wood);
                FillRect(texture, 11, 27, 11, 5, _palette.Amber);
                FillRect(texture, 13, 30, 7, 8, _palette.AmberCore);
                FillRect(texture, 15, 34, 3, 7, _palette.AmberCore);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 8f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetHubWallSprite(bool torch, int decoration)
        {
            string key = $"hub-rear-wall-t{torch}-d{decoration}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 32;
            const int height = 68;
            const int wallHeight = 52;
            var texture = NewTexture(width, height);
            Color32 mortar = new Color32(24, 21, 28, 255);
            Color32 stone = new Color32(58, 42, 48, 255);
            Color32 stoneLight = new Color32(84, 57, 54, 255);
            Color32 stoneDark = new Color32(40, 33, 40, 255);

            for (int px = 0; px < width; px++)
            {
                int bottom = 14 - px / 2;
                for (int localY = 0; localY < wallHeight; localY++)
                {
                    int py = bottom + localY;
                    int course = localY / 9;
                    int courseY = localY % 9;
                    int jointOffset = course % 2 == 0 ? 0 : 8;
                    bool edge = px == 0 || px == width - 1 ||
                                localY <= 1 || localY >= wallHeight - 2;
                    bool joint = courseY <= 1 || (px + jointOffset) % 16 <= 1;
                    bool cap = localY >= wallHeight - 5;
                    bool torchGlow = torch &&
                                      Mathf.Abs(px - 16) + Mathf.Abs(localY - 31) < 15;

                    Color32 color = edge || joint
                        ? mortar
                        : cap ? stoneLight
                        : ((px + localY * 3) % 17 == 0 ? Shift(stone, 8) : stone);
                    if (!edge && !joint && torchGlow)
                        color = localY > 20
                            ? new Color32(111, 65, 43, 255)
                            : new Color32(82, 51, 45, 255);
                    if (!edge && localY < 5)
                        color = stoneDark;

                    texture.SetPixel(px, py, color);
                }
            }

            if (torch)
            {
                FillRect(texture, 13, 25, 7, 3, new Color32(32, 25, 27, 255));
                FillRect(texture, 15, 22, 3, 11, new Color32(102, 61, 34, 255));
                FillRect(texture, 11, 33, 11, 5, new Color32(225, 76, 30, 255));
                FillRect(texture, 13, 36, 7, 8, new Color32(255, 155, 44, 255));
                FillRect(texture, 15, 39, 3, 8, new Color32(255, 226, 118, 255));
                texture.SetPixel(11, 44, new Color32(255, 190, 61, 255));
                texture.SetPixel(22, 41, new Color32(255, 190, 61, 255));
            }
            else if (decoration == 1)
            {
                // 자주색 길드 배너: 장면의 큰 색 덩어리이자 허브/던전 구분 표식.
                FillRect(texture, 8, 24, 17, 3, mortar);
                FillRect(texture, 10, 22, 13, 22, new Color32(37, 25, 39, 255));
                FillRect(texture, 11, 23, 11, 19, new Color32(73, 39, 72, 255));
                FillRect(texture, 12, 24, 3, 16, new Color32(105, 52, 87, 255));
                FillRect(texture, 15, 30, 3, 9, new Color32(218, 145, 45, 255));
                FillRect(texture, 13, 33, 7, 3, new Color32(218, 145, 45, 255));
                texture.SetPixel(11, 21, new Color32(233, 183, 75, 255));
                texture.SetPixel(22, 21, new Color32(233, 183, 75, 255));
            }
            else if (decoration == 2)
            {
                // 작은 연금술 선반. 바닥을 점유하지 않으면서 시안의 생활감만 추가한다.
                FillRect(texture, 5, 26, 23, 4, new Color32(43, 27, 25, 255));
                FillRect(texture, 7, 30, 19, 3, new Color32(129, 75, 38, 255));
                FillRect(texture, 8, 34, 4, 7, new Color32(19, 63, 66, 255));
                FillRect(texture, 9, 35, 2, 5, new Color32(71, 191, 181, 255));
                FillRect(texture, 14, 33, 4, 8, new Color32(81, 28, 31, 255));
                FillRect(texture, 15, 35, 2, 5, new Color32(215, 67, 44, 255));
                FillRect(texture, 21, 35, 4, 6, new Color32(116, 83, 36, 255));
                FillRect(texture, 9, 41, 2, 2, new Color32(214, 187, 137, 255));
                FillRect(texture, 15, 41, 2, 2, new Color32(214, 187, 137, 255));
            }
            else if (decoration == 3)
            {
                // 방패와 교차 검: 작은 화면에서도 무기고 벽으로 읽히는 강한 실루엣.
                DrawThickLine(texture, 8, 24, 24, 43, 2, new Color32(177, 168, 153, 255));
                DrawThickLine(texture, 24, 24, 8, 43, 2, new Color32(92, 91, 91, 255));
                FillRect(texture, 13, 29, 7, 12, mortar);
                FillRect(texture, 14, 30, 5, 9, new Color32(69, 74, 77, 255));
                FillRect(texture, 15, 31, 2, 7, new Color32(170, 124, 48, 255));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 7f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 실제 2D Light 대신 바닥 타일 단위의 반투명 색층을 사용한다.
        /// 픽셀 경계와 SpriteRenderer 정렬을 보존하고 던전 FOV/상태 색과도 섞이지 않는다.
        /// </summary>
        internal Sprite GetHubLightTileSprite(string kind, int strength)
        {
            string key = $"hub-light-{kind}-{strength}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 source = kind == "portal"
                ? new Color32(44, 218, 216, 255)
                : new Color32(255, 145, 48, 255);
            int alphaBase = strength == 3 ? 64 : strength == 2 ? 40 : 20;

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) +
                                Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 0.96f) continue;

                int dither = (px + py * 3) & 3;
                int alpha = diamond > 0.78f && dither > 1
                    ? alphaBase / 2
                    : alphaBase;
                texture.SetPixel(px, py, new Color32(source.r, source.g, source.b, (byte)alpha));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }
    }
}
