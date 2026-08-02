using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests.EditMode
{
    public class B2FoundationSpriteTests
    {
        private static PrototypeEnvironmentSprites NewEnvironment(out PrototypePalette palette)
        {
            var fallback = new PrototypePalette.Fallbacks(
                new Color32(84, 74, 66, 255),
                new Color32(110, 100, 90, 255),
                new Color32(30, 28, 34, 255),
                new Color32(10, 10, 14, 255),
                new Color32(70, 190, 180, 255),
                new Color32(24, 28, 38, 255),
                new Color32(38, 44, 58, 255));
            palette = new PrototypePalette(null, fallback);
            return new PrototypeEnvironmentSprites(new PrototypeSpriteCache(), palette);
        }

        [Test]
        public void FaceSprite_Uses64RegimeAndAlignedPivot()
        {
            var environment = NewEnvironment(out _);

            Sprite sprite = environment.GetB2FoundationFaceSprite(FoundationFaces.Both, 1);

            Assert.AreEqual(64f, sprite.rect.width);
            Assert.AreEqual(42f, sprite.rect.height);
            Assert.AreEqual(64f, sprite.pixelsPerUnit);
            Assert.AreEqual(32f, sprite.pivot.x, 0.0001f);
            Assert.AreEqual(26f, sprite.pivot.y, 0.0001f);
            Assert.AreEqual(1f, sprite.bounds.size.x, 0.0001f);
            Assert.AreEqual(42f / 64f, sprite.bounds.size.y, 0.0001f);
        }

        [Test]
        public void FaceSprite_OneSidedMasksNeverBleedAcrossCenter()
        {
            var environment = NewEnvironment(out _);

            Sprite left = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenLeft, 1);
            Sprite right = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenRight, 1);

            CountOpaqueHalves(left.texture, out int leftOnLeft, out int leftOnRight);
            CountOpaqueHalves(right.texture, out int rightOnLeft, out int rightOnRight);
            Assert.Greater(leftOnLeft, 0);
            Assert.AreEqual(0, leftOnRight);
            Assert.AreEqual(0, rightOnLeft);
            Assert.Greater(rightOnRight, 0);
        }

        [Test]
        public void FaceSprite_CacheSeparatesFaceMaskAndRibPhase()
        {
            var environment = NewEnvironment(out _);

            Sprite leftPhase0 = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenLeft, 0);
            Sprite leftPhase0Again = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenLeft, 0);
            Sprite leftPhase1 = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenLeft, 1);
            Sprite rightPhase0 = environment.GetB2FoundationFaceSprite(FoundationFaces.ScreenRight, 0);

            Assert.AreSame(leftPhase0, leftPhase0Again);
            Assert.AreNotSame(leftPhase0, leftPhase1);
            Assert.AreNotSame(leftPhase0, rightPhase0);
        }

        [Test]
        public void FaceAndSupportSprites_UseHardAlphaAndFoundationPaletteRolesOnly()
        {
            var environment = NewEnvironment(out PrototypePalette palette);
            var allowed = new HashSet<Color32>
            {
                palette.Stone,
                palette.StoneShadow,
                palette.WallShadow,
                palette.Outline,
                palette.Void,
            };

            Sprite face = environment.GetB2FoundationFaceSprite(FoundationFaces.Both, 0);
            Sprite leftSupport = environment.GetB2FoundationSupportSprite(screenLeft: true);
            Sprite rightSupport = environment.GetB2FoundationSupportSprite(screenLeft: false);

            AssertHardAlphaAndPalette(face.texture, allowed);
            AssertHardAlphaAndPalette(leftSupport.texture, allowed);
            AssertHardAlphaAndPalette(rightSupport.texture, allowed);
            Assert.AreEqual(new Vector2(12f, 38f), leftSupport.rect.size);
            Assert.AreEqual(64f, leftSupport.pixelsPerUnit);
            Assert.AreEqual(6f, leftSupport.pivot.x, 0.0001f);
            Assert.AreEqual(38f, leftSupport.pivot.y, 0.0001f);
            Assert.AreNotSame(leftSupport, rightSupport);
        }

        private static void CountOpaqueHalves(
            Texture2D texture,
            out int leftCount,
            out int rightCount)
        {
            leftCount = 0;
            rightCount = 0;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                if (texture.GetPixel(x, y).a <= 0f) continue;
                if (x < texture.width / 2) leftCount++;
                else rightCount++;
            }
        }

        private static void AssertHardAlphaAndPalette(
            Texture2D texture,
            HashSet<Color32> allowed)
        {
            int opaqueCount = 0;
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                Assert.That(pixel.a, Is.EqualTo(0).Or.EqualTo(255));
                if (pixel.a == 0) continue;
                opaqueCount++;
                Assert.IsTrue(
                    allowed.Contains(pixel),
                    $"foundation sprite used non-role color {pixel}");
            }

            Assert.Greater(opaqueCount, 0);
        }
    }
}
