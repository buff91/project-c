using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class SpriteOcclusionTests
    {
        [Test]
        public void ShouldFade_OverlappingSpriteInFront_ReturnsTrue()
        {
            var player = new Bounds(Vector3.zero, Vector3.one);
            var occluder = new Bounds(new Vector3(0.25f, 0f, 0f), Vector3.one);

            Assert.That(SpriteOcclusion.ShouldFade(occluder, player, 12, 10), Is.True);
        }

        [Test]
        public void ShouldFade_OverlappingSpriteBehindPlayer_ReturnsFalse()
        {
            var player = new Bounds(Vector3.zero, Vector3.one);
            var occluder = new Bounds(new Vector3(0.25f, 0f, 0f), Vector3.one);

            Assert.That(SpriteOcclusion.ShouldFade(occluder, player, 9, 10), Is.False);
        }

        [Test]
        public void ShouldFade_NonOverlappingSpriteInFront_ReturnsFalse()
        {
            var player = new Bounds(Vector3.zero, Vector3.one);
            var occluder = new Bounds(new Vector3(2f, 0f, 0f), Vector3.one);

            Assert.That(SpriteOcclusion.ShouldFade(occluder, player, 12, 10), Is.False);
        }
    }
}
