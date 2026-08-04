using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class PrototypeActorSpritesTests
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
        public void MonsterFallbacks_AreDedicatedCyberpunkSilhouettes_AndUnknownIsNeutral()
        {
            var sprites = new PrototypeActorSprites(new PrototypeSpriteCache());
            string[] ids =
            {
                "Goblin", "Skeleton", "Slime", "Slinger", "ArcDrone", "GraveWarden"
            };
            var seen = new HashSet<Sprite>();
            Sprite occupationAssault = null;

            foreach (string id in ids)
            {
                Sprite sprite = Track(sprites.GetMonsterSprite(id));
                if (id == "Goblin") occupationAssault = sprite;
                Assert.IsNotNull(sprite, id);
                Assert.IsTrue(seen.Add(sprite), $"{id}가 다른 적 폴백을 재사용한다");
                Assert.IsFalse(ContainsRetiredFantasyColor(sprite),
                    $"{id} 폴백에 구 고블린/해골/슬라임 색이 남아 있다");
            }

            Sprite unknown = Track(sprites.GetMonsterSprite("unregistered-archetype"));
            Assert.IsNotNull(unknown);
            Assert.IsFalse(seen.Contains(unknown), "미등록 ID가 기존 몬스터 얼굴로 뭉개졌다");
            Assert.AreSame(
                occupationAssault,
                sprites.GetCharacterSprite(true),
                "적 인간형 폴백은 구 녹색 캐릭터가 아니라 점거군 실루엣이어야 한다");
        }

        [Test]
        public void ReadOnlyPreviewMarker_HasDiamondBoundaryAndVisibleCross()
        {
            var sprites = new PrototypeActorSprites(new PrototypeSpriteCache());
            Sprite sprite = Track(sprites.GetReadOnlyPreviewSprite());
            Color32[] pixels = ReadBack(sprite.texture);

            Assert.AreEqual(64, sprite.texture.width);
            Assert.AreEqual(32, sprite.texture.height);
            Assert.Greater(pixels[32 + 16 * 64].a, 0, "중앙 X가 보여야 한다");
            Assert.Greater(pixels[32 + 1 * 64].a, 0, "다이아 위 경계가 보여야 한다");
            Assert.AreEqual(0, pixels[0].a, "타일 바깥 모서리는 투명해야 한다");
        }

        private Sprite Track(Sprite sprite)
        {
            _sprites.Add(sprite);
            return sprite;
        }

        private static bool ContainsRetiredFantasyColor(Sprite sprite)
        {
            Color32[] pixels = ReadBack(sprite.texture);
            foreach (Color32 pixel in pixels)
            {
                // GPU readback의 색공간 변환을 허용하면서 구 녹색 피부/점액과 밝은 뼈 팔레트의
                // 상대 채널 특징만 잡는다. 현행 시안의 시안 발광은 B가 높아 이 조건에 안 걸린다.
                bool retiredGreen =
                    pixel.a > 0 && pixel.r >= 75 && pixel.r <= 145 &&
                    pixel.g - pixel.r >= 25 && pixel.g - pixel.b >= 45;
                bool retiredBone =
                    pixel.a > 0 && pixel.r >= 205 && pixel.g >= 195 && pixel.b >= 175;
                if (retiredGreen || retiredBone) return true;
            }
            return false;
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
