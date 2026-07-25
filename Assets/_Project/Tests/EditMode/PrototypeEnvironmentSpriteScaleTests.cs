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
        public void MappedTile_NonIntegerScale_ReturnsSourceUntouched()
        {
            var environment = NewEnvironment();
            Sprite odd = MakeReadableSprite(96, 48, 96f, "floor-odd");

            Sprite mapped = environment.GetMappedTileSprite(
                odd, new Color32(84, 74, 66, 255), extruded: false, hubFaces: false);

            Assert.AreSame(odd, mapped, "64×32 정수 배가 아닌 소스는 가공 없이 원본을 돌려준다.");
        }
    }
}
