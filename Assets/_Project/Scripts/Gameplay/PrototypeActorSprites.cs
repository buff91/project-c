using ProjectC.Core;
using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 액터·프롭·아이템·랜드마크의 런타임 임시 아트. 64×32 타일 규격 위에 절차적으로 그린다.
    /// IsoVisualCatalog 슬롯이 채워지면 호출부가 해당 항목에 한해 이 코드를 우회한다.
    ///
    /// **게임 상태를 알지 못한다** — 격자·던전·플레이어를 참조하지 않고, 그림을 결정하는 값은
    /// 전부 인자로 받는다(`bool goblin`, `ItemKind kind` 처럼). 그래서 이 클래스는 캐시만 있으면
    /// 어디서든 같은 그림을 낸다. 이 무지(無知)를 깨지 말 것 — 깨는 순간 신 클래스로 되돌아간다.
    /// 환경(타일·벽·문)은 팔레트가 필요해서 <see cref="PrototypeEnvironmentSprites"/>가 따로 그린다.
    /// </summary>
    internal sealed class PrototypeActorSprites
    {
        private readonly PrototypeSpriteCache _spriteCache;

        internal PrototypeActorSprites(PrototypeSpriteCache spriteCache)
        {
            _spriteCache = spriteCache;
        }

        /// <summary>
        /// 액터 발밑 접촉 그림자: 납작한 다이아몬드로 중심이 진하고 가장자리로 부드럽게 사라진다.
        /// 흰색으로 굽고 알파에 모양을 담아, 런타임에 renderer.color로 void색·세기를 입힌다.
        /// </summary>
        internal Sprite GetContactShadowSprite()
        {
            const string key = "contact-shadow";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                // 타일보다 작은 납작한 타원. py 중심을 살짝 아래(13.5)로 둬 발밑에 고이게 한다.
                float d = Mathf.Abs((px - 31.5f) / 19f) + Mathf.Abs((py - 13.5f) / 9f);
                if (d >= 1f) continue;
                float k = 1f - d;
                byte a = (byte)Mathf.RoundToInt(255f * k * k);
                texture.SetPixel(px, py, new Color32(255, 255, 255, a));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetSelectionSprite()
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

        internal Sprite GetBossExitSealSprite(bool unlocked)
        {
            string key = unlocked ? "boss-exit-unlocked" : "boss-exit-locked";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outer = unlocked
                ? new Color32(103, 241, 218, 245)
                : new Color32(222, 69, 52, 245);
            Color32 inner = unlocked
                ? new Color32(255, 220, 104, 230)
                : new Color32(126, 24, 28, 230);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond =
                    Mathf.Abs((px - 31.5f) / 32f) +
                    Mathf.Abs((py - 15.5f) / 16f);
                bool outerRing = diamond > 0.68f && diamond <= 0.92f;
                bool innerRing = diamond > 0.38f && diamond <= 0.48f;
                texture.SetPixel(px, py, outerRing ? outer : innerRing ? inner : transparent);
            }

            if (unlocked)
            {
                DrawThickLine(texture, 24, 15, 30, 9, 2, inner);
                DrawThickLine(texture, 30, 9, 41, 21, 2, inner);
            }
            else
            {
                DrawThickLine(texture, 24, 10, 40, 22, 3, inner);
                DrawThickLine(texture, 40, 10, 24, 22, 3, inner);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 최심층 아레나의 제단. 공용 톤을 따른다 — 횃불에 데워진 석재 몸통 위에
        /// 마법/출구 신호색인 틸 코어. 보스를 쓰러뜨리면 런타임 틴트로 식힌다.
        /// </summary>
        internal Sprite GetBossAltarSprite()
        {
            const string key = "boss-altar";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(48, 64);
            Color32 stoneDark = new Color32(31, 34, 38, 255);
            Color32 stone = new Color32(67, 65, 61, 255);
            Color32 stoneLight = new Color32(126, 112, 91, 255);
            Color32 teal = new Color32(45, 148, 142, 255);
            Color32 tealCore = new Color32(103, 241, 218, 255);

            // 받침 → 기둥 → 상단 접시 순으로 아래에서 위로 쌓는다.
            FillRect(texture, 6, 4, 36, 9, stoneDark);
            FillRect(texture, 9, 6, 30, 5, stone);
            FillRect(texture, 15, 13, 18, 21, stoneDark);
            FillRect(texture, 18, 13, 12, 19, stone);
            FillRect(texture, 10, 34, 28, 7, stoneDark);
            FillRect(texture, 12, 35, 24, 4, stoneLight);

            // 틸 코어: 접시 위에 뜬 균열의 빛.
            FillRect(texture, 19, 41, 10, 12, teal);
            FillRect(texture, 22, 43, 4, 14, tealCore);
            DrawThickLine(texture, 24, 57, 18, 47, 2, teal);
            DrawThickLine(texture, 24, 57, 30, 47, 2, teal);
            FillRect(texture, 21, 39, 6, 2, tealCore);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 중간 탈출구 — 지상으로 끌어올리는 승강 장치. 출구(틸 신호)와 같은 색군을 써서
        /// "여기로 나간다"가 한눈에 읽히게 하고, 제단·모닥불과는 실루엣으로 구분한다.
        /// </summary>
        internal Sprite GetExtractionPointSprite()
        {
            const string key = "extraction-point";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(40, 56);
            Color32 frameDark = new Color32(28, 32, 36, 255);
            Color32 frame = new Color32(74, 80, 86, 255);
            Color32 teal = new Color32(45, 148, 142, 255);
            Color32 tealCore = new Color32(103, 241, 218, 255);

            FillRect(texture, 4, 2, 32, 6, frameDark);        // 발판
            FillRect(texture, 7, 3, 26, 3, frame);
            DrawThickLine(texture, 7, 6, 7, 50, 3, frameDark); // 좌우 기둥
            DrawThickLine(texture, 32, 6, 32, 50, 3, frameDark);
            FillRect(texture, 4, 48, 32, 6, frameDark);        // 상단 대들보
            FillRect(texture, 7, 49, 26, 3, frame);

            // 위로 뻗는 신호 — 올라간다는 방향을 색과 화살로 함께 말한다.
            FillRect(texture, 18, 8, 4, 38, teal);
            FillRect(texture, 19, 10, 2, 34, tealCore);
            DrawThickLine(texture, 20, 46, 13, 38, 2, tealCore);
            DrawThickLine(texture, 20, 46, 27, 38, 2, tealCore);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.06f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetLocalStairLandmarkSprite()
        {
            const string key = "landmark-local-stairs";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(64, 38);
            Color32 riser = new Color32(34, 50, 52, 255);
            Color32 tread = new Color32(105, 214, 194, 255);
            Color32 edge = new Color32(188, 244, 224, 255);
            for (int step = 0; step < 4; step++)
            {
                int y = 7 + step * 6;
                int inset = 7 + step * 4;
                DrawThickLine(texture, inset, y, 63 - inset, y, 4, riser);
                DrawThickLine(texture, inset, y + 2, 63 - inset, y + 2, 2,
                    step == 3 ? edge : tread);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.42f));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>
        /// 엘리베이터 설비 표지. <paramref name="powered"/>로 <b>멈춘 것과 움직이는 것</b>을 가른다 —
        /// 플레이어는 보스로 가는 길에 멈춘 것을 먼저 보고, 보스를 잡은 뒤 같은 자리가 켜진 것을 본다.
        /// 그 대비가 곧 "건물이 깨어났다"라서, 두 변주의 실루엣은 같고 <b>신호등과 문틈만</b> 달라진다.
        ///
        /// <para>
        /// 신호색은 틸이다 — 이 게임에서 틸은 "이제 열렸다"의 어휘이고(보스 출구 해금·포탈),
        /// 토치 골드는 물리 광원이라 해금 신호로 쓰면 두 언어가 섞인다.
        /// </para>
        /// </summary>
        internal Sprite GetElevatorLandmarkSprite(bool powered)
        {
            string key = powered ? "landmark-elevator-on" : "landmark-elevator-off";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(40, 76);
            Color32 outline = new Color32(18, 16, 21, 255);
            Color32 frame = new Color32(88, 88, 95, 255);
            Color32 frameLight = new Color32(124, 124, 130, 255);
            Color32 door = new Color32(58, 58, 65, 255);
            Color32 doorRib = new Color32(44, 44, 50, 255);
            Color32 rust = new Color32(112, 66, 38, 255);
            Color32 signal = powered
                ? new Color32(71, 191, 181, 255)
                : new Color32(50, 44, 49, 255);
            Color32 signalCore = powered
                ? new Color32(176, 244, 236, 255)
                : new Color32(62, 55, 59, 255);

            // 프레임: "문이 있는 상자". 사다리의 가로대 반복과 실루엣이 달라야 한다.
            FillRect(texture, 4, 4, 32, 62, outline);
            FillRect(texture, 6, 6, 28, 58, frame);
            // 좌상단 광원 규약 — 왼쪽 레일과 인방 위쪽만 밝다.
            FillRect(texture, 6, 6, 3, 58, frameLight);
            FillRect(texture, 6, 61, 28, 3, frameLight);

            // 문짝 두 짝. 프레임보다 어두워야 문으로 읽힌다(같은 값이면 빈 판이 된다).
            const int doorLeft = 11;
            const int doorRight = 29;   // exclusive
            const int doorBottom = 10;
            const int doorTop = 56;     // exclusive
            int half = (doorLeft + doorRight) / 2;
            int parting = powered ? 3 : 0;  // 전원이 들어오면 가운데가 벌어진다

            FillRect(texture, doorLeft - 1, doorBottom - 1,
                doorRight - doorLeft + 2, doorTop - doorBottom + 2, outline);

            // 왼쪽 짝
            FillRect(texture, doorLeft, doorBottom,
                half - parting - doorLeft, doorTop - doorBottom, door);
            // 오른쪽 짝
            FillRect(texture, half + parting, doorBottom,
                doorRight - half - parting, doorTop - doorBottom, door);

            // 문짝 리브 — 금속 문으로 읽히게 하는 최소 디테일.
            for (int ribY = doorBottom + 8; ribY < doorTop - 4; ribY += 14)
            {
                DrawThickLine(texture, doorLeft + 1, ribY, half - parting - 2, ribY, 1, doorRib);
                DrawThickLine(texture, half + parting + 1, ribY, doorRight - 2, ribY, 1, doorRib);
            }

            if (powered)
            {
                // 벌어진 틈으로 보이는 켜진 승강로. 검은 슬릿으로 두면 "빛나는 기둥"처럼
                // 보이므로, 틈 자체를 신호색으로 채우고 위아래만 어둡게 남긴다.
                FillRect(texture, half - parting, doorBottom, parting * 2, doorTop - doorBottom, signal);
                FillRect(texture, half - parting, doorTop - 6, parting * 2, 6, signalCore);
                FillRect(texture, half - parting, doorBottom, parting * 2, 3, outline);
            }
            else
            {
                // 닫힌 문의 맞물림선.
                DrawThickLine(texture, half, doorBottom, half, doorTop - 1, 1, outline);
            }

            // 층 표시등: 인방 위. 전원 상태가 가장 먼저 읽히는 지점이다.
            FillRect(texture, 13, 67, 14, 7, outline);
            FillRect(texture, 14, 68, 12, 5, signal);
            FillRect(texture, 17, 69, 6, 3, signalCore);

            // 문턱.
            FillRect(texture, 7, 5, 26, 3, powered ? signal : frameLight);

            if (!powered)
            {
                // 녹은 프레임 하단 모서리에 붙인다 — 떠 있는 얼룩이 아니라 풍화로 읽히게.
                FillRect(texture, 6, 8, 3, 11, rust);
                FillRect(texture, 31, 6, 3, 8, rust);
                FillRect(texture, 6, 30, 2, 5, rust);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.07f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetLadderLandmarkSprite()
        {
            const string key = "landmark-ladder";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(36, 72);
            Color32 shadow = new Color32(45, 30, 19, 255);
            Color32 wood = new Color32(181, 113, 45, 255);
            Color32 gold = new Color32(246, 190, 68, 255);
            Color32 shine = new Color32(255, 226, 134, 255);

            DrawThickLine(texture, 8, 5, 8, 65, 6, shadow);
            DrawThickLine(texture, 27, 5, 27, 65, 6, shadow);
            DrawThickLine(texture, 8, 5, 8, 65, 3, wood);
            DrawThickLine(texture, 27, 5, 27, 65, 3, wood);
            for (int y = 10; y <= 61; y += 9)
            {
                DrawThickLine(texture, 8, y, 27, y, 5, shadow);
                DrawThickLine(texture, 9, y + 1, 26, y + 1, 2, gold);
            }
            FillRect(texture, 6, 63, 5, 5, shine);
            FillRect(texture, 25, 63, 5, 5, shine);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetFloorTransitionLandmarkSprite(bool down)
        {
            string key = down ? "landmark-floor-down" : "landmark-floor-up";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(58, 72);
            Color32 stoneDark = new Color32(31, 34, 38, 255);
            Color32 stone = new Color32(67, 65, 61, 255);
            Color32 stoneLight = new Color32(126, 112, 91, 255);
            Color32 voidColor = new Color32(3, 5, 8, 255);
            Color32 route = down
                ? new Color32(245, 126, 43, 255)
                : new Color32(255, 178, 69, 255);
            Color32 routeCore = new Color32(255, 226, 140, 255);

            FillRect(texture, 7, 5, 44, 50, stoneDark);
            FillRect(texture, 11, 7, 36, 46, stone);
            FillRect(texture, 16, 7, 26, 40, voidColor);
            FillRect(texture, 7, 51, 44, 8, stoneLight);
            FillRect(texture, 12, 57, 34, 5, stone);

            for (int step = 0; step < 4; step++)
            {
                int y = 8 + step * 7;
                int inset = down ? step * 2 : (3 - step) * 2;
                FillRect(texture, 17 + inset, y, 24 - inset * 2, 3, route);
                FillRect(texture, 19 + inset, y + 2, 20 - inset * 2, 1, routeCore);
            }

            int arrowCenter = 29;
            int arrowY = down ? 34 : 31;
            FillRect(texture, arrowCenter - 2, arrowY, 5, 11, routeCore);
            if (down)
            {
                DrawThickLine(texture, arrowCenter, arrowY - 2, arrowCenter - 7, arrowY + 5, 3, routeCore);
                DrawThickLine(texture, arrowCenter, arrowY - 2, arrowCenter + 7, arrowY + 5, 3, routeCore);
            }
            else
            {
                DrawThickLine(texture, arrowCenter, arrowY + 13, arrowCenter - 7, arrowY + 6, 3, routeCore);
                DrawThickLine(texture, arrowCenter, arrowY + 13, arrowCenter + 7, arrowY + 6, 3, routeCore);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetHoleLandmarkSprite()
        {
            const string key = "landmark-hole";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(64, 44);
            Color32 deep = new Color32(1, 4, 7, 250);
            Color32 stone = new Color32(52, 66, 70, 255);
            Color32 broken = new Color32(103, 129, 128, 255);
            Color32 depth = new Color32(75, 218, 221, 220);

            for (int y = 7; y <= 31; y++)
            {
                float normalized = Mathf.Abs((y - 19f) / 13f);
                int half = Mathf.RoundToInt((1f - normalized) * 25f);
                for (int x = 32 - half; x <= 32 + half; x++)
                    texture.SetPixel(x, y, deep);
            }

            DrawThickLine(texture, 32, 4, 60, 19, 4, stone);
            DrawThickLine(texture, 60, 19, 32, 35, 4, broken);
            DrawThickLine(texture, 32, 35, 4, 19, 4, stone);
            DrawThickLine(texture, 4, 19, 32, 4, 4, broken);
            DrawThickLine(texture, 24, 16, 29, 13, 2, depth);
            DrawThickLine(texture, 32, 20, 32, 11, 2, depth);
            DrawThickLine(texture, 40, 16, 35, 13, 2, depth);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.43f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetPlayerFootprintSprite()
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

        internal Sprite GetPlayerLocatorSprite()
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

        internal Sprite GetProjectileSprite()
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

        internal Sprite GetDoorInteractionSprite(bool opening)
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

        internal Sprite GetShaftSprite(bool hole)
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

        internal Sprite GetShaftEndpointSprite(bool hole, bool arrival)
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

        internal Sprite GetHealthBarSprite(bool filled)
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

        internal Sprite GetCharacterSprite(bool goblin)
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

        internal Sprite GetBlastSprite(bool fiery = true)
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

        internal Sprite GetItemSprite(ItemKind kind)
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
            else if (EquipmentCatalog.IsEquipment(kind))
            {
                // 바닥에 떨어진 장비 — 소모품(둥근 병·폭탄)과 실루엣이 달라야 주울지 판단이 선다.
                Color32 steel = new Color32(120, 128, 136, 255);
                Color32 steelDark = new Color32(58, 64, 70, 255);
                Color32 grip = new Color32(96, 74, 48, 255);
                Color32 signal = new Color32(226, 188, 96, 255);
                bool bulky = BackpackRules.Footprint(kind).Width > 1; // 방패류는 넓적하게
                if (bulky)
                {
                    FillRect(texture, 3, 4, 14, 14, steelDark);
                    FillRect(texture, 5, 6, 10, 10, steel);
                    FillRect(texture, 8, 9, 4, 4, signal);
                }
                else
                {
                    FillRect(texture, 8, 2, 4, 18, steelDark); // 긴 자루
                    FillRect(texture, 9, 3, 2, 16, steel);
                    FillRect(texture, 7, 4, 6, 4, grip);       // 손잡이
                    FillRect(texture, 7, 17, 6, 4, signal);    // 머리 부분
                }
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
            // 임시 아이템 아트는 y=2부터 그리므로 그 접지선을 타일 중심에 맞춘다.
            cached = CreateSprite(texture, new Vector2(0.5f, 2f / 24f));
            _spriteCache[key] = cached;
            return cached;
        }

        /// <summary>허브 캠프 프롭 임시 아트: campfire / stash / portal.</summary>
        internal Sprite GetHubPropSprite(string kind)
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
            else if (kind == "smith")
            {
                Color32 baseWood = new Color32(60, 40, 24, 255);
                Color32 anvil = new Color32(70, 74, 82, 255);
                Color32 anvilTop = new Color32(104, 110, 120, 255);
                Color32 spark = new Color32(255, 202, 72, 255);
                FillRect(texture, 6, 2, 16, 5, baseWood);   // 나무 받침
                FillRect(texture, 9, 7, 10, 6, anvil);      // 모루 몸통
                FillRect(texture, 5, 13, 18, 4, anvilTop);  // 모루 상단 뿔
                FillRect(texture, 18, 17, 3, 3, spark);     // 불티
            }
            else if (kind == "bounty")
            {
                Color32 post = new Color32(84, 58, 32, 255);
                Color32 board = new Color32(120, 84, 48, 255);
                Color32 paper = new Color32(226, 214, 180, 255);
                Color32 wax = new Color32(176, 60, 52, 255);
                FillRect(texture, 12, 2, 4, 12, post);      // 기둥
                FillRect(texture, 4, 12, 20, 16, board);    // 게시판
                FillRect(texture, 7, 15, 7, 9, paper);      // 공고문 1
                FillRect(texture, 15, 16, 6, 8, paper);     // 공고문 2
                FillRect(texture, 10, 24, 2, 2, wax);       // 봉랍
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
            // 임시 허브 프롭도 y=2부터 그린다. 캔버스 바닥이 아닌 실제 접지선 기준.
            cached = CreateSprite(texture, new Vector2(0.5f, 2f / 32f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetMonsterSprite(string archetypeId)
        {
            switch (archetypeId)
            {
                case "Skeleton": return GetSkeletonSprite();
                case "Slime": return GetSlimeSprite();
                case "Slinger": return GetSlingerSprite();
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

        /// <summary>
        /// 투석 약탈자. 근접 약탈자와 한눈에 구분돼야 대응(엄폐·돌진)이 성립하므로
        /// 치켜든 팔과 투척끈으로 실루엣을 다르게 잡는다.
        /// </summary>
        private Sprite GetSlingerSprite()
        {
            const string key = "slinger";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 48);
            Color32 dark = new Color32(20, 25, 28, 255);
            Color32 coat = new Color32(108, 92, 66, 255);
            Color32 coatLight = new Color32(148, 128, 92, 255);
            Color32 skin = new Color32(198, 158, 118, 255);
            Color32 sling = new Color32(226, 188, 96, 255);

            FillRect(texture, 11, 2, 10, 12, dark);       // 다리
            FillRect(texture, 12, 3, 8, 10, coat);
            FillRect(texture, 9, 13, 14, 16, dark);       // 몸통 외곽
            FillRect(texture, 10, 14, 12, 14, coat);
            FillRect(texture, 10, 22, 12, 3, coatLight);  // 어깨끈
            FillRect(texture, 12, 29, 8, 9, dark);        // 머리
            FillRect(texture, 13, 30, 6, 7, skin);
            FillRect(texture, 13, 33, 6, 2, dark);        // 눈가리개

            // 치켜든 팔 + 투척끈 — 원거리 몬스터임을 실루엣으로 알린다.
            FillRect(texture, 22, 24, 4, 12, coat);
            FillRect(texture, 22, 34, 4, 3, skin);
            DrawThickLine(texture, 24, 37, 29, 43, 2, sling);
            FillRect(texture, 27, 42, 4, 4, sling);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.05f));
            _spriteCache[key] = cached;
            return cached;
        }

        internal Sprite GetBarrelSprite()
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
    }
}
