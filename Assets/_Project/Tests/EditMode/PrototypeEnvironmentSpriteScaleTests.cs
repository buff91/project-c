using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests.EditMode
{
    /// <summary>
    /// 128-레짐(128×64 타일 / PPU 128) 전환 대비 — 절차 톤매핑 경로가 소스의 PPU와 배율을
    /// 보존해서, 혼합 PPU 상태에서도 카탈로그 경유 스프라이트의 월드 크기가 유지되는지 고정한다.
    /// 이 계약이 깨지면 문/계단/바닥이 화면에서 2배(또는 절반) 크기로 렌더된다.
    /// </summary>
    public class PrototypeEnvironmentSpriteScaleTests
    {
        private static Sprite MakeReadableSprite(int width, int height, float ppu, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(120, 110, 100, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        private static PrototypeEnvironmentSprites NewEnvironment()
        {
            var fallback = new PrototypePalette.Fallbacks(
                new Color32(84, 74, 66, 255),
                new Color32(110, 100, 90, 255),
                new Color32(30, 28, 34, 255),
                new Color32(10, 10, 14, 255),
                new Color32(70, 190, 180, 255),
                new Color32(24, 28, 38, 255),
                new Color32(38, 44, 58, 255));
            return new PrototypeEnvironmentSprites(
                new PrototypeSpriteCache(),
                new PrototypePalette(null, fallback));
        }

        private static PrototypeEnvironmentSprites NewEnvironment(IsoVisualCatalog catalog) =>
            new PrototypeEnvironmentSprites(
                new PrototypeSpriteCache(),
                new PrototypePalette(
                    catalog,
                    new PrototypePalette.Fallbacks(
                        Color.black,
                        Color.gray,
                        Color.black,
                        Color.black,
                        Color.cyan,
                        Color.black,
                        Color.black)));

        [Test]
        public void ToneMapped_64Regime_KeepsSourcePpuAndWorldSize()
        {
            var environment = NewEnvironment();
            Sprite source = MakeReadableSprite(64, 80, 64f, "door-64");

            Sprite mapped = environment.GetToneMappedEnvironmentSprite(
                source, new Color32(84, 74, 66, 255));

            Assert.AreEqual(64f, mapped.pixelsPerUnit);
            Assert.AreEqual(1.0f, mapped.bounds.size.x, 1e-4f);
            Assert.AreEqual(80f / 64f, mapped.bounds.size.y, 1e-4f);
        }

        [Test]
        public void ToneMapped_128Regime_InheritsSourcePpu_SameWorldSize()
        {
            var environment = NewEnvironment();
            Sprite source = MakeReadableSprite(128, 160, 128f, "door-128");

            Sprite mapped = environment.GetToneMappedEnvironmentSprite(
                source, new Color32(84, 74, 66, 255));

            Assert.AreEqual(128f, mapped.pixelsPerUnit);
            Assert.AreEqual(1.0f, mapped.bounds.size.x, 1e-4f);
            Assert.AreEqual(160f / 128f, mapped.bounds.size.y, 1e-4f);
        }

        [Test]
        public void MappedTile_64Regime_KeepsCanvasContract()
        {
            var environment = NewEnvironment();
            Sprite floor = MakeReadableSprite(64, 32, 64f, "floor-64");

            Sprite mapped = environment.GetMappedTileSprite(
                floor, new Color32(84, 74, 66, 255), extruded: false, hubFaces: false);

            Assert.AreNotSame(floor, mapped, "정배율 소스는 톤매핑을 타야 한다.");
            Assert.AreEqual(64f, mapped.pixelsPerUnit);
            Assert.AreEqual(64f, mapped.rect.width);
            Assert.AreEqual(32f, mapped.rect.height);
            Assert.AreEqual(1.0f, mapped.bounds.size.x, 1e-4f);
            Assert.AreEqual(0.5f, mapped.bounds.size.y, 1e-4f);
        }

        [Test]
        public void MappedTile_128Regime_Extruded_KeepsWorldSizeAndPivot()
        {
            var environment = NewEnvironment();
            Sprite floor = MakeReadableSprite(128, 64, 128f, "floor-128");

            Sprite mapped = environment.GetMappedTileSprite(
                floor, new Color32(84, 74, 66, 255), extruded: true, hubFaces: false);

            Assert.AreNotSame(floor, mapped, "정배율(×2) 소스도 톤매핑·단차 측면을 타야 한다.");
            Assert.AreEqual(128f, mapped.pixelsPerUnit);
            Assert.AreEqual(128f, mapped.rect.width);
            Assert.AreEqual(96f, mapped.rect.height, "단차 캔버스는 48px의 ×2여야 한다.");
            Assert.AreEqual(1.0f, mapped.bounds.size.x, 1e-4f);
            Assert.AreEqual(0.75f, mapped.bounds.size.y, 1e-4f);
            Assert.AreEqual(0.5f, mapped.pivot.x / mapped.rect.width, 1e-4f);
            Assert.AreEqual(32f / 48f, mapped.pivot.y / mapped.rect.height, 1e-4f);
        }

        [Test]
        public void MappedTile_BandOverlay_CachesPerBand_NeverSharesAcrossBands()
        {
            // 캐시 키에 밴드가 빠지면 첫 밴드가 그린 결과를 전 층이 재사용한다 — 이 계약을 고정한다.
            var environment = NewEnvironment();
            Sprite floor = MakeReadableSprite(128, 64, 128f, "floor-band");
            var baseColor = new Color32(84, 74, 66, 255);

            Sprite mid = environment.GetMappedTileSprite(
                floor, baseColor, extruded: false, hubFaces: false, ProjectC.Core.DungeonDepthBand.Mid);
            Sprite deep = environment.GetMappedTileSprite(
                floor, baseColor, extruded: false, hubFaces: false, ProjectC.Core.DungeonDepthBand.Deep);
            Sprite midAgain = environment.GetMappedTileSprite(
                floor, baseColor, extruded: false, hubFaces: false, ProjectC.Core.DungeonDepthBand.Mid);
            Sprite shallowDefault = environment.GetMappedTileSprite(
                floor, baseColor, extruded: false, hubFaces: false);
            Sprite shallowExplicit = environment.GetMappedTileSprite(
                floor, baseColor, extruded: false, hubFaces: false, ProjectC.Core.DungeonDepthBand.Shallow);

            Assert.AreNotSame(mid, deep, "밴드가 다르면 다른 스프라이트여야 한다");
            Assert.AreNotSame(mid, shallowDefault, "오버레이 밴드는 공용 바닥과 달라야 한다");
            Assert.AreSame(mid, midAgain, "같은 밴드는 캐시를 재사용해야 한다");
            Assert.AreSame(shallowDefault, shallowExplicit, "기본 인자 = Shallow(오버레이 없음)");
            Assert.AreEqual(128f, mid.pixelsPerUnit, "오버레이가 레짐/PPU를 바꾸면 안 된다");
        }

        [Test]
        public void MappedTile_NonIntegerScale_ReturnsSourceUntouched()
        {
            var environment = NewEnvironment();
            Sprite odd = MakeReadableSprite(96, 48, 96f, "floor-odd");

            Sprite mapped = environment.GetMappedTileSprite(
                odd, new Color32(84, 74, 66, 255), extruded: false, hubFaces: false);

            Assert.AreSame(odd, mapped, "64×32 정수 배가 아닌 소스는 가공 없이 원본을 돌려준다.");
        }

        [Test]
        public void ToneMapped_NeonModes_PreserveCyanAndRemapCoolScreenToMagenta()
        {
            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            var environment = NewEnvironment(catalog);
            Color32 cyan = environment.ToneMapEnvironmentPixel(
                new Color32(34, 220, 228, 255),
                catalog.dungeonWall,
                PrototypeEnvironmentSprites.EnvironmentAccentMode.NeonCyan);
            Color32 magenta = environment.ToneMapEnvironmentPixel(
                new Color32(42, 214, 70, 255),
                catalog.dungeonWall,
                PrototypeEnvironmentSprites.EnvironmentAccentMode.NeonMagenta);

            Assert.AreEqual(catalog.dungeonNeonCyan, cyan);
            Assert.AreEqual(catalog.dungeonNeonMagenta, magenta);
        }

        [Test]
        public void ToneMapped_SignalMode_KeepsGameplayTealSeparateFromDecorativeCyan()
        {
            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            var environment = NewEnvironment(catalog);
            Color32 source =
                new Color32(34, 210, 218, 255);

            Color32 mapped = environment.ToneMapEnvironmentPixel(
                source,
                catalog.dungeonWall,
                PrototypeEnvironmentSprites.EnvironmentAccentMode.Signal);

            Assert.AreEqual(catalog.dungeonMagic, mapped);
            Assert.AreNotEqual(catalog.dungeonNeonCyan, mapped);
        }

        [Test]
        public void DungeonAtmosphereBackdrop_IsCameraAspectSprite_IndependentOfDungeonGeometry()
        {
            var environment = NewEnvironment();

            Sprite backdrop = environment.GetDungeonAtmosphereBackdropSprite();
            Sprite cached = environment.GetDungeonAtmosphereBackdropSprite();

            Assert.AreEqual(320f, backdrop.rect.width);
            Assert.AreEqual(180f, backdrop.rect.height);
            Assert.AreEqual(16f / 9f, backdrop.bounds.size.x / backdrop.bounds.size.y, 0.0001f);
            Assert.AreSame(backdrop, cached, "분위기층은 방 좌표 없이 한 장을 재사용해야 한다.");
        }

        [Test]
        public void FacilityNeonWallOverlay_KeepsSourceCanvasPpuAndPivot()
        {
            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            var environment = NewEnvironment(catalog);
            Sprite source = MakeReadableSprite(64, 112, 128f, "facility-window");

            Sprite overlay = environment.GetFacilityNeonWallOverlaySprite(
                source,
                PrototypeEnvironmentSprites.EnvironmentAccentMode.NeonMagenta,
                risesRight: true);

            Assert.NotNull(overlay);
            Assert.AreEqual(source.rect.size, overlay.rect.size);
            Assert.AreEqual(source.pixelsPerUnit, overlay.pixelsPerUnit);
            Assert.AreEqual(
                source.pivot.x / source.rect.width,
                overlay.pivot.x / overlay.rect.width,
                0.0001f);
            Assert.AreEqual(
                source.pivot.y / source.rect.height,
                overlay.pivot.y / overlay.rect.height,
                0.0001f);
            Assert.IsNull(environment.GetFacilityNeonWallOverlaySprite(
                source,
                PrototypeEnvironmentSprites.EnvironmentAccentMode.Signal,
                risesRight: true));
        }
    }
}
