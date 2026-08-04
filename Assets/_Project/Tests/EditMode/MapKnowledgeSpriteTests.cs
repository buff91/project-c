using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests.EditMode
{
    public sealed class MapKnowledgeSpriteTests
    {
        private readonly List<Sprite> _sprites = new List<Sprite>();

        [TearDown]
        public void TearDown()
        {
            foreach (Sprite sprite in _sprites)
            {
                if (sprite == null) continue;
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }
            _sprites.Clear();
        }

        [Test]
        public void MapKnowledgeKinds_UseSparseDashedBlueprintGrammar()
        {
            PrototypeEnvironmentSprites sprites = NewEnvironment();
            var signatures = new HashSet<ulong>();

            foreach (MapSilhouetteKind kind in (MapSilhouetteKind[])System.Enum.GetValues(
                         typeof(MapSilhouetteKind)))
            {
                Sprite sprite = Track(sprites.GetMapKnowledgeSprite(kind));
                Color32[] pixels = ReadBack(sprite.texture);
                int visiblePixels = CountVisible(pixels);

                Assert.AreEqual(64, sprite.texture.width, kind.ToString());
                Assert.AreEqual(32, sprite.texture.height, kind.ToString());
                Assert.AreEqual(64f, sprite.pixelsPerUnit, kind.ToString());
                Assert.AreEqual(new Vector2(32f, 16f), sprite.pivot, kind.ToString());
                Assert.GreaterOrEqual(visiblePixels, 70, $"{kind} 윤곽이 너무 성기다");
                Assert.Less(visiblePixels, 280, $"{kind}가 지도 선이 아니라 실제 면처럼 채워졌다");
                Assert.Greater(pixels[32 + 1 * 64].a, 0, $"{kind}의 끊긴 상단 경계가 사라졌다");
                Assert.AreEqual(0, pixels[0].a, $"{kind}의 타일 바깥은 투명해야 한다");
                Assert.IsTrue(signatures.Add(AlphaSignature(pixels)),
                    $"{kind}가 다른 지도 범주와 같은 픽셀 기호를 재사용한다");
            }
        }

        [Test]
        public void MappedFloor_AndObservedFloor_DoNotShareFilledSurfaceGrammar()
        {
            PrototypeEnvironmentSprites sprites = NewEnvironment();
            Sprite mapped = Track(sprites.GetMapKnowledgeSprite(MapSilhouetteKind.Floor));
            Sprite observed = Track(sprites.GetTileSprite(
                TileKind.Floor,
                new GridPos(0, 0, 0),
                new TileVisualFacts(
                    DungeonVisualContext.Preview(),
                    extruded: false,
                    planeRisesRight: false,
                    secretHinted: false,
                    hubMode: false,
                    hospitalDressing: false)));

            int mappedPixels = CountVisible(ReadBack(mapped.texture));
            int observedPixels = CountVisible(ReadBack(observed.texture));

            Assert.Less(mappedPixels, observedPixels * 0.35f,
                "같은 층 mapped Unknown은 선·점 지도여야 하고 다른 층 실제 표면처럼 채우면 안 된다");
            StringAssert.StartsWith("Map Knowledge ", mapped.name);
            Assert.IsFalse(observed.name.StartsWith("Map Knowledge "));
        }

        private Sprite Track(Sprite sprite)
        {
            _sprites.Add(sprite);
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

        private static int CountVisible(Color32[] pixels)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a > 0) count++;
            }
            return count;
        }

        private static ulong AlphaSignature(Color32[] pixels)
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (Color32 pixel in pixels)
            {
                hash ^= pixel.a;
                hash *= prime;
            }
            return hash;
        }

        private static Color32[] ReadBack(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readable = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                source.filterMode = FilterMode.Point;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readable);
            }
        }
    }
}
