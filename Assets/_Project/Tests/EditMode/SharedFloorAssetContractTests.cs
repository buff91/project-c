using System.Linq;
using NUnit.Framework;
using ProjectC.EditorTools;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests.EditMode
{
    /// <summary>
    /// 공용 바닥은 Aseprite를 정식 원본으로 쓰되 PNG 폴백과 픽셀·캔버스가 같아야 한다.
    /// 또한 원본의 은은한 저주파 마모가 런타임에서는 단일 Stone 역할색으로 수렴하게
    /// 고정해, 같은 명암 얼룩이 셀마다 스탬프처럼 반복되는 회귀를 막는다.
    /// </summary>
    public class SharedFloorAssetContractTests
    {
        private const string AsepritePath =
            "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite";
        private const string PngPath =
            "Assets/_Project/Art/Environment/env-floor.png";
        private const string CatalogPath =
            "Assets/_Project/Art/Environment/ProjectCEnvironmentCatalog.asset";

        [Test]
        public void SharedFloorSources_KeepGeometryPixelsAndCatalogBindings()
        {
            Sprite source = LoadFirstAsepriteSprite();
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(PngPath);
            IsoVisualCatalog catalog =
                AssetDatabase.LoadAssetAtPath<IsoVisualCatalog>(CatalogPath);

            Assert.IsNotNull(source, $"Aseprite 첫 프레임이 없다: {AsepritePath}");
            Assert.IsNotNull(fallback, $"PNG 폴백 스프라이트가 없다: {PngPath}");
            Assert.IsNotNull(catalog, $"환경 카탈로그가 없다: {CatalogPath}");
            AssertSpriteGeometry(source, "Aseprite");
            AssertSpriteGeometry(fallback, "PNG fallback");

            Assert.AreSame(source, catalog.floor, "floor는 정식 Aseprite 첫 프레임을 가리켜야 한다");
            Assert.AreSame(
                fallback,
                catalog.raisedFloor,
                "raisedFloor는 별도 발주 전까지 공용 PNG 폴백을 유지한다");
            Assert.AreSame(
                fallback,
                catalog.lowerFloor,
                "lowerFloor는 호환용 공용 PNG 폴백을 유지한다");

            AssertReadablePixelsMatch(source, fallback);
        }

        [Test]
        public void MappedSharedFloor_ConvergesToSingleStoneRoleWithoutStampedWear()
        {
            Sprite source = LoadFirstAsepriteSprite();
            Assert.IsNotNull(source, $"Aseprite 첫 프레임이 없다: {AsepritePath}");

            var fallbacks = new PrototypePalette.Fallbacks(
                new Color32(59, 63, 69, 255),
                new Color32(84, 91, 97, 255),
                new Color32(18, 21, 28, 255),
                new Color32(5, 7, 12, 255),
                new Color32(79, 167, 160, 255),
                new Color32(24, 28, 38, 255),
                new Color32(38, 44, 58, 255));
            var palette = new PrototypePalette(null, fallbacks);
            var environment = new PrototypeEnvironmentSprites(
                new PrototypeSpriteCache(),
                palette);

            Sprite mapped = null;
            Texture2D mappedTexture = null;
            try
            {
                mapped = environment.GetMappedTileSprite(
                    source,
                    palette.Stone,
                    extruded: false,
                    hubFaces: false);
                mappedTexture = mapped.texture;

                Assert.AreNotSame(source, mapped, "128-regime 정식 바닥은 톤매핑 경로를 타야 한다");
                AssertSpriteGeometry(mapped, "mapped floor");
                Assert.AreEqual(128, mappedTexture.width);
                Assert.AreEqual(64, mappedTexture.height);

                Color32[] pixels = ReadBackPixels(mappedTexture);
                int visible = 0;
                int stone = 0;
                int shadow = 0;
                int light = 0;
                int outline = 0;
                int unexpected = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.a == 0) continue;
                    visible++;
                    if (Near(pixel, palette.Stone)) stone++;
                    else if (Near(pixel, palette.StoneShadow)) shadow++;
                    else if (Near(pixel, palette.StoneLight)) light++;
                    else if (Near(pixel, palette.Outline)) outline++;
                    else unexpected++;
                }

                Assert.Greater(visible, 0, "공용 바닥에 보이는 픽셀이 있어야 한다");
                Assert.AreEqual(0, unexpected, "공용 바닥은 승인된 stone 역할색만 사용해야 한다");
                Assert.AreEqual(visible, stone, "공용 base는 런타임에서 단일 Stone 톤이어야 한다");
                Assert.AreEqual(0, shadow, "반복되는 StoneShadow 얼룩을 공용 base에 넣지 않는다");
                Assert.AreEqual(0, light, "반복되는 StoneLight 얼룩을 공용 base에 넣지 않는다");
                Assert.AreEqual(0, outline, "바닥 내부에 Outline 역할색 테두리를 넣지 않는다");
            }
            finally
            {
                if (mapped != null && mapped != source)
                    Object.DestroyImmediate(mapped);
                if (mappedTexture != null && mappedTexture != source.texture)
                    Object.DestroyImmediate(mappedTexture);
            }
        }

        private static Sprite LoadFirstAsepriteSprite()
        {
            return ProjectCAsepritePipeline.SelectFirstFrame(
                AssetDatabase.LoadAllAssetsAtPath(AsepritePath).OfType<Sprite>());
        }

        private static void AssertSpriteGeometry(Sprite sprite, string label)
        {
            Assert.AreEqual(128f, sprite.rect.width, $"{label} width");
            Assert.AreEqual(64f, sprite.rect.height, $"{label} height");
            Assert.AreEqual(128f, sprite.pixelsPerUnit, $"{label} PPU");
            Assert.AreEqual(
                0.5f,
                sprite.pivot.x / sprite.rect.width,
                0.0001f,
                $"{label} pivot x");
            Assert.AreEqual(
                0.5f,
                sprite.pivot.y / sprite.rect.height,
                0.0001f,
                $"{label} pivot y");
        }

        private static void AssertReadablePixelsMatch(Sprite source, Sprite fallback)
        {
            Assert.IsTrue(source.texture.isReadable, "Aseprite 바닥은 런타임 톤매핑을 위해 readable이어야 한다");
            Assert.IsTrue(fallback.texture.isReadable, "PNG 폴백도 픽셀 동등성 검증을 위해 readable이어야 한다");
            Color32[] sourcePixels = ReadSpritePixels(source);
            Color32[] fallbackPixels = ReadSpritePixels(fallback);
            Assert.AreEqual(sourcePixels.Length, fallbackPixels.Length);

            int width = Mathf.RoundToInt(source.rect.width);
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color32 expected = fallbackPixels[i];
                Color32 actual = sourcePixels[i];
                int x = i % width;
                int y = i / width;
                Assert.AreEqual(expected.a, actual.a, $"alpha mismatch at ({x}, {y})");
                if (expected.a > 0)
                    Assert.AreEqual(expected, actual, $"visible pixel mismatch at ({x}, {y})");
            }
        }

        private static Color32[] ReadSpritePixels(Sprite sprite)
        {
            Rect rect = sprite.rect;
            Color[] colors = sprite.texture.GetPixels(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height));
            var pixels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                pixels[i] = colors[i];
            return pixels;
        }

        private static Color32[] ReadBackPixels(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var readable = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false);
            try
            {
                target.filterMode = FilterMode.Point;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readable.ReadPixels(
                    new Rect(0, 0, source.width, source.height),
                    0,
                    0,
                    false);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readable);
            }
        }

        private static bool Near(Color32 actual, Color32 expected)
        {
            const int tolerance = 2;
            return Mathf.Abs(actual.r - expected.r) <= tolerance &&
                   Mathf.Abs(actual.g - expected.g) <= tolerance &&
                   Mathf.Abs(actual.b - expected.b) <= tolerance &&
                   Mathf.Abs(actual.a - expected.a) <= tolerance;
        }
    }
}
