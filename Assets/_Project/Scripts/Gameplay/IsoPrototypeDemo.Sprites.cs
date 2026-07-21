using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 런타임 임시 아트 생성부.
    /// 외부 스프라이트가 없을 때 64×32 픽셀 규격의 타일/액터/이펙트를 절차적으로 그린다.
    /// IsoVisualCatalog 슬롯이 채워지면 이 코드는 해당 항목에 한해 우회된다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private Sprite GetTileSprite(TileKind kind, GridPos pos)
        {
            if (kind == TileKind.DoorClosed || kind == TileKind.DoorOpen)
            {
                if (visualCatalog != null)
                {
                    Sprite mapped = visualCatalog.DoorFor(kind, DoorPlaneRisesRight(pos));
                    if (mapped != null) return mapped;
                }

                return GetDoorSprite(kind, pos);
            }

            if (kind == TileKind.Stairs ||
                kind == TileKind.StairsUp ||
                kind == TileKind.StairsDown)
            {
                if (visualCatalog != null)
                {
                    Sprite mapped = visualCatalog.StairsFor(kind, StairPlaneRisesRight(pos));
                    if (mapped != null) return mapped;
                }
            }

            int floorIndex = _dungeon != null ? _dungeon.Height.FloorIndex(pos.elevation) : 0;
            int localHeight = _dungeon != null ? _dungeon.Height.LocalHeight(pos.elevation) : pos.elevation;
            bool extruded = localHeight > 0 || IsFrontEdge(pos);
            int variant = Mathf.Abs(pos.x * 17 + pos.y * 31 + floorIndex * 13) % 4;
            Color32 baseColor = localHeight > 0
                ? raisedTop
                : floorIndex < 0 ? Shift(lowerTop, floorIndex * 5) : floorTop;

            if (visualCatalog != null)
            {
                Sprite mapped = visualCatalog.TileFor(kind, pos.elevation);
                if (mapped != null)
                    return extruded ? GetExtrudedMappedTileSprite(mapped, baseColor) : mapped;
            }

            string key = $"tile-{kind}-f{floorIndex}-h{localHeight}-v{variant}-x{extruded}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int textureHeight = extruded ? 48 : TilePixelHeight;
            int topOffset = extruded ? 16 : 0;
            var texture = NewTexture(TilePixelWidth, textureHeight);
            Color32 transparent = new Color32(0, 0, 0, 0);

            if (extruded)
                DrawExtrudedSides(texture, baseColor);

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
                int noise = ((px / 7) + (py / 4) * 3 + variant) % 4;
                Color32 color = border ? tileSeam : Shift(baseColor, noise == 0 ? 5 : noise == 1 ? 0 : -4);

                bool stoneJoint = diamond < 0.72f &&
                                  ((px + py * 3 + variant * 11) % 29 == 0 ||
                                   (px * 2 - py + variant * 7) % 37 == 0);
                if (stoneJoint) color = Shift(baseColor, -14);

                bool moss = floorIndex < 0 && variant == 2 && py < 15 && px > 9 && px < 23;
                if (moss && (px + py) % 5 < 2)
                    color = new Color32(54, 78, 55, 255);

                if (kind == TileKind.DoorClosed)
                {
                    bool band = (px + py * 2) % 13 < 3;
                    bool iron = Mathf.Abs(px - 32) < 2 || Mathf.Abs(py - 16) < 2;
                    color = border || iron
                        ? outline
                        : band ? new Color32(164, 91, 43, 255) : new Color32(103, 57, 35, 255);
                }
                else if (kind == TileKind.DoorOpen)
                {
                    bool threshold = py > 11 && py < 20 && Mathf.Abs(px - 32) < 22;
                    color = border
                        ? outline
                        : threshold ? new Color32(177, 111, 52, 255) : color;
                }
                else if (kind == TileKind.Hole)
                    color = border ? accent : new Color32(4, 8, 11, 190);
                else if (kind == TileKind.WeakFloor && IsCrackPixel(px, py))
                    color = new Color32(24, 20, 19, 255);
                else if (kind == TileKind.Stairs && ((px + py * 2) % 12 < 3))
                    color = border ? outline : Shift(baseColor, 25);
                else if (kind == TileKind.StairsDown && ((px + py) % 10 < 3))
                    color = border ? outline : new Color32(220, 119, 47, 255);
                else if (kind == TileKind.StairsUp && ((px + py) % 10 < 3))
                    color = border ? outline : new Color32(74, 181, 219, 255);

                texture.SetPixel(px, py + topOffset, color);
            }

            texture.Apply(false, true);
            cached = CreateSprite(
                texture,
                extruded ? new Vector2(0.5f, 32f / 48f) : new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetExtrudedMappedTileSprite(Sprite topSprite, Color32 baseColor)
        {
            Texture2D source = topSprite.texture;
            Rect sourceRect = topSprite.rect;
            if (source == null || !source.isReadable ||
                Mathf.RoundToInt(sourceRect.width) != TilePixelWidth ||
                Mathf.RoundToInt(sourceRect.height) != TilePixelHeight)
                return topSprite;

            string key = $"mapped-extruded-{topSprite.name}-{baseColor.r}-{baseColor.g}-{baseColor.b}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, 48);
            DrawExtrudedSides(texture, baseColor);

            Color[] pixels = source.GetPixels(
                Mathf.RoundToInt(sourceRect.x),
                Mathf.RoundToInt(sourceRect.y),
                TilePixelWidth,
                TilePixelHeight);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                Color pixel = pixels[py * TilePixelWidth + px];
                if (pixel.a > 0f)
                    texture.SetPixel(px, py + 16, pixel);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 32f / 48f));
            _spriteCache[key] = cached;
            return cached;
        }

        private void DrawExtrudedSides(Texture2D texture, Color32 baseColor)
        {
            Color32 leftFace = Shift(baseColor, -24);
            Color32 rightFace = Shift(baseColor, -38);
            for (int py = 0; py < 32; py++)
            {
                int leftMin = py < 16 ? 32 - py * 2 : 0;
                int leftMax = py < 16 ? 32 : 64 - py * 2;
                int rightMin = py < 16 ? 32 : py * 2;
                int rightMax = py < 16 ? 32 + py * 2 : 63;

                for (int px = Mathf.Max(0, leftMin); px <= Mathf.Min(31, leftMax); px++)
                {
                    bool mortar = py % 7 == 0 || (px + (py / 7) * 8) % 19 == 0;
                    texture.SetPixel(px, py, mortar ? outline : leftFace);
                }
                for (int px = Mathf.Max(32, rightMin); px <= Mathf.Min(63, rightMax); px++)
                {
                    bool mortar = py % 7 == 0 || (px - (py / 7) * 7) % 21 == 0;
                    texture.SetPixel(px, py, mortar ? outline : rightFace);
                }
            }
        }

        private Sprite GetDoorSprite(TileKind kind, GridPos pos)
        {
            int floorIndex = _dungeon != null ? _dungeon.Height.FloorIndex(pos.elevation) : 0;
            bool closed = kind == TileKind.DoorClosed;
            bool risesRight = DoorPlaneRisesRight(pos);
            string key = $"door-iso-{kind}-f{floorIndex}-r{risesRight}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 64;
            const int height = 80;
            var texture = NewTexture(width, height);
            Color32 baseColor = floorIndex < 0 ? Shift(lowerTop, floorIndex * 5) : floorTop;

            // 문 아래에도 동일한 64×32 바닥 다이아몬드를 유지한다.
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f) continue;
                texture.SetPixel(px, py, diamond > 0.88f ? tileSeam : baseColor);
            }

            Color32 stone = new Color32(70, 76, 77, 255);
            Color32 stoneLight = new Color32(102, 105, 99, 255);
            Color32 wood = new Color32(118, 66, 37, 255);
            Color32 woodLight = new Color32(170, 97, 48, 255);
            Color32 iron = new Color32(39, 43, 44, 255);

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
                new Color32(6, 9, 11, 255),
                new Color32(10, 14, 16, 255),
                outline);

            if (closed)
            {
                int innerLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 5f / 34f)) + 3;
                int innerRightY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 29f / 34f)) + 3;
                FillSlantedPanel(texture, 20, innerLeftY, 44, innerRightY, 32, wood, woodLight, iron);
                DrawThickLine(texture, 20, innerLeftY + 11, 44, innerRightY + 11, 2, iron);
                DrawThickLine(texture, 20, innerLeftY + 24, 44, innerRightY + 24, 2, iron);
                FillRect(texture, risesRight ? 37 : 24, risesRight ? 31 : 27, 3, 3,
                    new Color32(227, 173, 70, 255));
            }
            else
            {
                // 열린 문짝은 오른쪽 기둥 쪽으로 접혀 중앙 통과 방향을 그대로 드러낸다.
                int foldedLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 25f / 34f)) + 3;
                int foldedRightY = rightBase + 3;
                FillSlantedPanel(texture, 40, foldedLeftY, 47, foldedRightY, 31,
                    Shift(wood, -12), woodLight, iron);
            }

            DrawThickLine(texture, leftX, leftBase, leftX, leftBase + frameHeight, 5, stone);
            DrawThickLine(texture, rightX, rightBase, rightX, rightBase + frameHeight, 5, Shift(stone, -9));
            DrawThickLine(texture, leftX, leftBase + frameHeight, rightX, rightBase + frameHeight, 6, stone);
            DrawThickLine(texture, leftX + 2, leftBase + frameHeight + 1,
                rightX - 2, rightBase + frameHeight + 1, 2, stoneLight);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 16f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        private bool DoorPlaneRisesRight(GridPos pos)
        {
            bool passageNorthSouth = HasDoorSide(pos.North) && HasDoorSide(pos.South);
            Vector2Int planeAxis = passageNorthSouth ? Vector2Int.right : Vector2Int.up;
            return AxisRisesRight(pos, planeAxis);
        }

        private bool StairPlaneRisesRight(GridPos pos)
        {
            if (StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing))
                return _grid.iso.ProjectsToScreenRight(pos, landing);

            return AxisRisesRight(pos, Vector2Int.up);
        }

        private bool AxisRisesRight(GridPos pos, Vector2Int worldAxis)
        {
            Vector3 center = _grid.GridToWorld(pos);
            Vector3 alongPlane = _grid.GridToWorld(pos.Offset(worldAxis.x, worldAxis.y)) - center;
            return alongPlane.x * alongPlane.y >= 0f;
        }

        private bool HasDoorSide(GridPos pos)
        {
            TileData tile = _grid.Map.Get(pos);
            return tile != null && tile.IsSolidGround;
        }

        private Sprite GetWallSprite(bool torch)
        {
            string key = torch ? "rear-wall-torch" : "rear-wall";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 32;
            const int height = 56;
            const int wallHeight = 40;
            var texture = NewTexture(width, height);
            Color32 stone = new Color32(46, 52, 56, 255);
            Color32 stoneLight = new Color32(63, 68, 70, 255);
            Color32 stoneDark = new Color32(30, 36, 41, 255);

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
                        ? outline
                        : topCap ? stoneLight : ((px + localY) % 11 == 0 ? Shift(stone, 7) : stone);
                    texture.SetPixel(px, py, color);
                }
            }

            if (torch)
            {
                FillRect(texture, 13, 20, 6, 3, stoneDark);
                FillRect(texture, 15, 15, 3, 12, new Color32(79, 53, 34, 255));
                FillRect(texture, 11, 27, 11, 5, new Color32(235, 116, 35, 255));
                FillRect(texture, 13, 30, 7, 8, new Color32(255, 202, 72, 255));
                FillRect(texture, 15, 34, 3, 7, new Color32(255, 238, 143, 255));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 8f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetSelectionSprite()
        {
            const string key = "selection";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool ring = diamond > 0.77f && diamond <= 0.94f;
                texture.SetPixel(px, py, ring ? new Color32(255, 177, 72, 230) : new Color32(0, 0, 0, 0));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetPlayerFootprintSprite()
        {
            const string key = "player-footprint";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 glow = new Color32(77, 232, 219, 235);
            Color32 core = new Color32(220, 255, 246, 255);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool outer = diamond > 0.82f && diamond <= 0.96f;
                bool tick = (px < 10 || px > 53) && diamond > 0.65f && diamond <= 0.98f;
                if (outer || tick)
                    texture.SetPixel(px, py, outer && (px + py) % 5 == 0 ? core : glow);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetPlayerLocatorSprite()
        {
            const string key = "player-locator";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(24, 24);
            Color32 glow = new Color32(94, 242, 219, 255);
            Color32 core = new Color32(224, 255, 239, 255);
            for (int y = 5; y < 18; y++)
            {
                int half = (17 - y) / 2;
                for (int x = 12 - half; x <= 12 + half; x++)
                    texture.SetPixel(x, y, y > 13 ? glow : core);
            }
            FillRect(texture, 10, 2, 5, 4, glow);
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetProjectileSprite()
        {
            const string key = "ranged-projectile";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(16, 16);
            FillRect(texture, 2, 6, 12, 4, new Color32(45, 94, 91, 220));
            FillRect(texture, 5, 7, 8, 3, new Color32(104, 244, 220, 255));
            FillRect(texture, 10, 6, 4, 5, new Color32(238, 255, 226, 255));
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetDoorInteractionSprite(bool opening)
        {
            string key = opening ? "door-open-burst" : "door-close-burst";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(48, 48);
            Color32 edge = opening
                ? new Color32(111, 245, 205, 255)
                : new Color32(255, 160, 72, 255);
            Color32 core = new Color32(255, 239, 166, 255);
            for (int i = 5; i < 43; i++)
            {
                if (i % 3 == 0)
                {
                    texture.SetPixel(i, 8, edge);
                    texture.SetPixel(i, 39, edge);
                    texture.SetPixel(8, i, edge);
                    texture.SetPixel(39, i, edge);
                }
            }
            FillRect(texture, 22, 3, 4, 9, core);
            FillRect(texture, 22, 36, 4, 9, core);
            FillRect(texture, 3, 22, 9, 4, core);
            FillRect(texture, 36, 22, 9, 4, core);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetShaftSprite(bool hole)
        {
            string key = hole ? "shaft-hole" : "shaft-stairs";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(20, 64);
            Color32 edge = hole
                ? new Color32(67, 224, 211, 220)
                : new Color32(239, 139, 55, 220);
            Color32 core = hole
                ? new Color32(173, 255, 242, 255)
                : new Color32(255, 220, 126, 255);
            for (int y = 0; y < 64; y++)
            {
                if (y % 8 < 5)
                {
                    texture.SetPixel(3, y, edge);
                    texture.SetPixel(16, y, edge);
                }
                if (y % 16 >= 10 && y % 16 <= 12)
                {
                    for (int x = 7; x <= 12; x++) texture.SetPixel(x, y, core);
                    texture.SetPixel(6, y + 1, edge);
                    texture.SetPixel(13, y + 1, edge);
                }
            }
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetShaftEndpointSprite(bool hole, bool arrival)
        {
            string key = $"shaft-end-{hole}-{arrival}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 edge = hole
                ? new Color32(62, 226, 214, 245)
                : new Color32(246, 144, 57, 245);
            Color32 core = hole
                ? new Color32(203, 255, 244, 255)
                : new Color32(255, 230, 155, 255);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool ring = diamond > 0.72f && diamond <= 0.94f;
                if (ring && (!arrival || (px + py) % 5 < 3))
                    texture.SetPixel(px, py, edge);
            }

            int arrowY = arrival ? 7 : 18;
            FillRect(texture, 29, arrowY, 6, 7, core);
            if (arrival)
            {
                FillRect(texture, 26, 7, 12, 3, core);
                FillRect(texture, 28, 10, 8, 3, core);
            }
            else
            {
                FillRect(texture, 26, 23, 12, 3, core);
                FillRect(texture, 28, 20, 8, 3, core);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetHealthBarSprite(bool filled)
        {
            string key = filled ? "health-filled" : "health-background";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 4);
            Color32 color = filled
                ? new Color32(87, 205, 96, 255)
                : new Color32(25, 29, 31, 230);

            FillRect(texture, 0, 0, texture.width, texture.height, color);
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetCharacterSprite(bool goblin)
        {
            string key = goblin ? "goblin" : "player";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 48);
            Color32 skin = goblin ? new Color32(113, 151, 62, 255) : new Color32(205, 177, 139, 255);
            Color32 body = goblin ? new Color32(94, 62, 39, 255) : new Color32(48, 90, 133, 255);
            Color32 metal = new Color32(172, 183, 183, 255);
            Color32 dark = new Color32(20, 25, 28, 255);

            // 짙은 외곽선을 먼저 그리고 내부 색을 덮어 픽셀 실루엣을 선명하게 만든다.
            FillRect(texture, 10, 2, 12, 7, dark);
            FillRect(texture, 7, 7, 18, 21, dark);
            FillRect(texture, 5, 12, 5, 15, dark);
            FillRect(texture, 22, 12, 5, 15, dark);
            FillRect(texture, 8, 25, 16, 15, dark);

            FillRect(texture, 12, 3, 4, 5, new Color32(37, 43, 47, 255));
            FillRect(texture, 17, 3, 4, 5, new Color32(31, 36, 40, 255));
            FillRect(texture, 9, 9, 14, 17, body);
            FillRect(texture, 10, 11, 3, 12, Shift(body, 22));
            FillRect(texture, 6, 14, 3, 11, skin);
            FillRect(texture, 23, 14, 3, 11, skin);
            FillRect(texture, 10, 27, 12, 11, skin);
            FillRect(texture, 12, 29, 3, 2, dark);
            FillRect(texture, 18, 29, 3, 2, dark);
            FillRect(texture, 14, 26, 5, 2, Shift(skin, 20));

            if (goblin)
            {
                FillRect(texture, 3, 31, 8, 3, dark);
                FillRect(texture, 21, 31, 8, 3, dark);
                FillRect(texture, 5, 32, 6, 2, skin);
                FillRect(texture, 21, 32, 6, 2, skin);
                FillRect(texture, 11, 35, 10, 3, Shift(skin, -12));
                FillRect(texture, 12, 17, 8, 3, new Color32(137, 78, 39, 255));
            }
            else
            {
                FillRect(texture, 8, 31, 16, 5, metal);
                FillRect(texture, 11, 35, 10, 5, new Color32(116, 129, 134, 255));
                FillRect(texture, 14, 35, 2, 4, Shift(metal, 30));
                FillRect(texture, 22, 10, 7, 15, dark);
                FillRect(texture, 23, 11, 5, 13, new Color32(47, 88, 126, 255));
                FillRect(texture, 24, 13, 2, 9, new Color32(74, 132, 177, 255));
                FillRect(texture, 4, 8, 2, 24, metal);
                FillRect(texture, 2, 28, 6, 2, new Color32(210, 160, 60, 255));
                FillRect(texture, 12, 16, 8, 3, new Color32(181, 142, 58, 255));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetBlastSprite(bool fiery = true)
        {
            string key = fiery ? "bomb-blast" : "frost-blast";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(48, 48);
            Color32 outer = fiery ? new Color32(232, 99, 42, 235) : new Color32(74, 156, 214, 235);
            Color32 mid = fiery ? new Color32(255, 170, 64, 255) : new Color32(126, 214, 236, 255);
            Color32 core = fiery ? new Color32(255, 240, 178, 255) : new Color32(226, 250, 255, 255);
            for (int py = 0; py < 48; py++)
            for (int px = 0; px < 48; px++)
            {
                float dx = (px - 23.5f) / 24f;
                float dy = (py - 23.5f) / 24f;
                float dist = dx * dx + dy * dy;
                bool spike = ((px + py * 2) % 9 < 2 || (px * 2 - py + 48) % 11 < 2);
                if (dist < 0.16f) texture.SetPixel(px, py, core);
                else if (dist < 0.5f) texture.SetPixel(px, py, spike ? core : mid);
                else if (dist < 0.95f && spike) texture.SetPixel(px, py, outer);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetItemSprite(ItemKind kind)
        {
            string key = $"item-{kind}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(20, 24);
            if (kind == ItemKind.Potion)
            {
                Color32 glass = new Color32(158, 200, 214, 255);
                Color32 liquid = new Color32(214, 62, 74, 255);
                Color32 liquidLight = new Color32(240, 116, 112, 255);
                Color32 cork = new Color32(150, 106, 58, 255);
                FillRect(texture, 6, 2, 8, 11, glass);      // 몸통
                FillRect(texture, 7, 3, 6, 7, liquid);
                FillRect(texture, 8, 7, 2, 3, liquidLight); // 하이라이트
                FillRect(texture, 8, 13, 4, 4, glass);      // 목
                FillRect(texture, 8, 17, 4, 3, cork);
            }
            else if (kind == ItemKind.FrostBomb)
            {
                Color32 shell = new Color32(46, 84, 110, 255);
                Color32 ice = new Color32(126, 214, 236, 255);
                Color32 shine = new Color32(226, 250, 255, 255);
                FillRect(texture, 5, 2, 10, 10, shell);     // 몸통
                FillRect(texture, 7, 4, 6, 6, ice);         // 얼음 결정
                FillRect(texture, 9, 6, 2, 4, shine);
                FillRect(texture, 9, 12, 2, 4, ice);        // 심지 대신 서리 기둥
                FillRect(texture, 8, 16, 4, 2, shine);
            }
            else if (kind == ItemKind.OilFlask)
            {
                Color32 glass = new Color32(120, 112, 74, 255);
                Color32 oil = new Color32(96, 82, 34, 255);
                Color32 sheen = new Color32(190, 164, 84, 255);
                Color32 cork = new Color32(150, 106, 58, 255);
                FillRect(texture, 6, 2, 8, 10, glass);      // 몸통
                FillRect(texture, 7, 3, 6, 6, oil);
                FillRect(texture, 8, 6, 2, 2, sheen);
                FillRect(texture, 8, 12, 4, 4, glass);      // 목
                FillRect(texture, 8, 16, 4, 3, cork);
            }
            else if (kind == ItemKind.ThrowingKnife)
            {
                Color32 blade = new Color32(176, 184, 194, 255);
                Color32 edge = new Color32(228, 234, 240, 255);
                Color32 grip = new Color32(96, 68, 40, 255);
                FillRect(texture, 9, 8, 3, 12, blade);      // 날
                FillRect(texture, 10, 10, 1, 9, edge);
                FillRect(texture, 8, 4, 5, 4, grip);        // 손잡이
            }
            else if (kind == ItemKind.RecallScroll)
            {
                Color32 paper = new Color32(212, 196, 158, 255);
                Color32 shadow = new Color32(168, 150, 112, 255);
                Color32 band = new Color32(122, 92, 49, 255);
                Color32 rune = new Color32(84, 211, 197, 255);
                FillRect(texture, 5, 4, 10, 14, paper);     // 말린 종이
                FillRect(texture, 5, 4, 2, 14, shadow);
                FillRect(texture, 5, 10, 10, 2, band);      // 묶음 띠
                FillRect(texture, 9, 6, 2, 2, rune);        // 귀환 문양
                FillRect(texture, 9, 14, 2, 2, rune);
            }
            else if (kind == ItemKind.CoinPouch)
            {
                Color32 pouch = new Color32(120, 92, 44, 255);
                Color32 tie = new Color32(84, 58, 20, 255);
                Color32 coin = new Color32(255, 213, 84, 255);
                FillRect(texture, 5, 2, 10, 10, pouch);     // 주머니
                FillRect(texture, 8, 12, 4, 3, tie);        // 묶은 목
                FillRect(texture, 7, 5, 2, 2, coin);        // 비치는 동전
                FillRect(texture, 11, 7, 2, 2, coin);
            }
            else if (kind == ItemKind.Gemstone)
            {
                Color32 gem = new Color32(64, 170, 190, 255);
                Color32 lightFacet = new Color32(180, 240, 250, 255);
                Color32 darkFacet = new Color32(32, 108, 126, 255);
                FillRect(texture, 6, 4, 8, 8, gem);         // 몸체
                FillRect(texture, 8, 12, 4, 3, gem);        // 상단 꼭짓점
                FillRect(texture, 7, 8, 3, 3, lightFacet);  // 반짝임
                FillRect(texture, 11, 5, 2, 3, darkFacet);
            }
            else if (kind == ItemKind.Relic)
            {
                Color32 gold = new Color32(200, 156, 60, 255);
                Color32 goldLit = new Color32(255, 213, 84, 255);
                Color32 baseStone = new Color32(84, 58, 20, 255);
                Color32 eye = new Color32(84, 211, 197, 255);
                FillRect(texture, 6, 2, 8, 3, baseStone);   // 받침
                FillRect(texture, 7, 5, 6, 10, gold);       // 우상 몸체
                FillRect(texture, 8, 15, 4, 3, goldLit);    // 머리
                FillRect(texture, 9, 10, 2, 2, eye);        // 눈
            }
            else if (kind == ItemKind.Herb)
            {
                Color32 stem = new Color32(74, 110, 52, 255);
                Color32 leaf = new Color32(104, 143, 77, 255);
                Color32 leafLit = new Color32(150, 196, 110, 255);
                FillRect(texture, 9, 2, 2, 12, stem);       // 줄기
                FillRect(texture, 5, 8, 4, 5, leaf);        // 왼 잎
                FillRect(texture, 11, 10, 4, 5, leaf);      // 오른 잎
                FillRect(texture, 8, 14, 4, 4, leafLit);    // 새순
            }
            else if (kind == ItemKind.BlastPowder)
            {
                Color32 sack = new Color32(120, 100, 74, 255);
                Color32 powder = new Color32(60, 56, 52, 255);
                Color32 spark2 = new Color32(255, 202, 72, 255);
                FillRect(texture, 6, 2, 8, 8, sack);        // 자루
                FillRect(texture, 7, 10, 6, 3, powder);     // 넘치는 화약
                FillRect(texture, 9, 14, 2, 2, spark2);
            }
            else if (kind == ItemKind.FrostShard)
            {
                Color32 shard = new Color32(126, 214, 236, 255);
                Color32 core = new Color32(226, 250, 255, 255);
                Color32 deep = new Color32(70, 140, 170, 255);
                FillRect(texture, 8, 2, 4, 14, shard);      // 기둥 결정
                FillRect(texture, 9, 6, 2, 6, core);
                FillRect(texture, 6, 4, 2, 6, deep);        // 곁가지
                FillRect(texture, 12, 8, 2, 5, deep);
            }
            else
            {
                Color32 shell = new Color32(43, 47, 52, 255);
                Color32 shine = new Color32(92, 100, 108, 255);
                Color32 fuse = new Color32(150, 106, 58, 255);
                Color32 spark = new Color32(255, 202, 72, 255);
                FillRect(texture, 5, 2, 10, 10, shell);     // 몸통
                FillRect(texture, 7, 8, 3, 3, shine);       // 하이라이트
                FillRect(texture, 9, 12, 2, 4, fuse);       // 심지
                FillRect(texture, 10, 16, 3, 3, spark);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.02f));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>허브 캠프 프롭 임시 아트: campfire / stash / portal.</summary>
        private Sprite GetHubPropSprite(string kind)
        {
            string key = $"hub-{kind}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(28, 32);
            if (kind == "campfire")
            {
                Color32 wood = new Color32(96, 66, 36, 255);
                Color32 flame = new Color32(255, 150, 48, 255);
                Color32 flameCore = new Color32(255, 220, 96, 255);
                FillRect(texture, 6, 2, 16, 4, wood);       // 장작
                FillRect(texture, 9, 6, 10, 10, flame);     // 불꽃
                FillRect(texture, 11, 8, 6, 10, flameCore);
                FillRect(texture, 13, 18, 2, 4, flame);     // 불티
            }
            else if (kind == "stash")
            {
                Color32 chest = new Color32(110, 76, 40, 255);
                Color32 lid = new Color32(140, 100, 54, 255);
                Color32 band = new Color32(200, 156, 60, 255);
                FillRect(texture, 4, 2, 20, 10, chest);     // 몸통
                FillRect(texture, 4, 12, 20, 6, lid);       // 뚜껑
                FillRect(texture, 12, 2, 4, 16, band);      // 금속 띠
                FillRect(texture, 13, 8, 2, 3, band);       // 자물쇠
            }
            else // portal
            {
                Color32 rim = new Color32(84, 211, 197, 255);
                Color32 core = new Color32(24, 60, 66, 255);
                Color32 swirl = new Color32(150, 240, 230, 255);
                FillRect(texture, 6, 2, 16, 26, rim);       // 게이트 테두리
                FillRect(texture, 9, 5, 10, 20, core);      // 심연
                FillRect(texture, 12, 9, 4, 4, swirl);      // 소용돌이
                FillRect(texture, 14, 17, 3, 3, swirl);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.02f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetMonsterSprite(string archetypeId)
        {
            switch (archetypeId)
            {
                case "Skeleton": return GetSkeletonSprite();
                case "Slime": return GetSlimeSprite();
                default: return GetCharacterSprite(true);
            }
        }

        private Sprite GetSkeletonSprite()
        {
            const string key = "skeleton";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 48);
            Color32 bone = new Color32(222, 216, 196, 255);
            Color32 boneShade = new Color32(168, 160, 138, 255);
            Color32 dark = new Color32(20, 25, 28, 255);

            FillRect(texture, 10, 27, 12, 12, dark);      // 두개골 외곽
            FillRect(texture, 11, 28, 10, 10, bone);
            FillRect(texture, 12, 31, 3, 3, dark);        // 눈
            FillRect(texture, 18, 31, 3, 3, dark);
            FillRect(texture, 13, 28, 6, 2, boneShade);   // 턱
            FillRect(texture, 14, 24, 4, 3, boneShade);   // 목
            FillRect(texture, 9, 14, 14, 10, dark);       // 흉곽 외곽
            FillRect(texture, 10, 15, 12, 8, bone);
            FillRect(texture, 10, 17, 12, 1, boneShade);  // 갈비 골
            FillRect(texture, 10, 20, 12, 1, boneShade);
            FillRect(texture, 6, 13, 3, 10, bone);        // 팔
            FillRect(texture, 23, 13, 3, 10, bone);
            FillRect(texture, 11, 4, 4, 10, bone);        // 다리
            FillRect(texture, 17, 4, 4, 10, bone);
            FillRect(texture, 24, 6, 2, 18, boneShade);   // 낡은 검
            FillRect(texture, 22, 22, 6, 2, dark);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetSlimeSprite()
        {
            const string key = "slime";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(26, 20);
            Color32 body = new Color32(96, 176, 88, 255);
            Color32 shade = new Color32(64, 128, 62, 255);
            Color32 shine = new Color32(178, 232, 164, 255);
            Color32 dark = new Color32(24, 44, 26, 255);

            FillRect(texture, 4, 2, 18, 10, shade);       // 몸통 아래
            FillRect(texture, 5, 6, 16, 8, body);         // 몸통 위
            FillRect(texture, 7, 12, 12, 3, body);        // 둥근 머리
            FillRect(texture, 8, 10, 3, 3, shine);        // 하이라이트
            FillRect(texture, 9, 6, 2, 3, dark);          // 눈
            FillRect(texture, 15, 6, 2, 3, dark);
            FillRect(texture, 4, 2, 18, 1, dark);         // 바닥선

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.05f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetBarrelSprite()
        {
            const string key = "barrel";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(24, 32);
            Color32 wood = new Color32(140, 65, 41, 255);
            Color32 bright = new Color32(194, 92, 48, 255);
            Color32 band = new Color32(50, 43, 39, 255);
            FillRect(texture, 5, 3, 14, 24, wood);
            FillRect(texture, 7, 5, 4, 20, bright);
            FillRect(texture, 4, 6, 16, 3, band);
            FillRect(texture, 4, 21, 16, 3, band);
            FillRect(texture, 9, 13, 6, 6, new Color32(229, 177, 60, 255));
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        private static Texture2D NewTexture(int width, int height)
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

        private static Sprite CreateSprite(Texture2D texture, Vector2 pivot)
        {
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                pivot,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private static void FillSlantedPanel(
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

        private static void DrawThickLine(
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

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
            {
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(px, py, color);
            }
        }

        private static bool IsCrackPixel(int x, int y)
        {
            return (x >= 28 && x <= 34 && y == 14 + (x % 3)) ||
                   (y >= 9 && y <= 15 && x == 29 - (y % 2) * 3) ||
                   (y >= 15 && y <= 20 && x == 35 + (y % 3));
        }

        private static Color32 Shift(Color32 color, int amount)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r + amount, 0, 255),
                (byte)Mathf.Clamp(color.g + amount, 0, 255),
                (byte)Mathf.Clamp(color.b + amount, 0, 255),
                color.a);
        }
    }
}
