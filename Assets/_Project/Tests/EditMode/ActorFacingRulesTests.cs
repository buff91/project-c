using System;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>방향 판정과 방향별 스프라이트 태그 계약 — Unity 없이 shim에서도 검증한다.</summary>
    public class ActorFacingRulesTests
    {
        private static readonly GridPos Origin = new GridPos(10, 10, 2);

        [TestCase(0, 1, 0, ActorFacing4.North)]
        [TestCase(1, 0, 0, ActorFacing4.East)]
        [TestCase(0, -1, 0, ActorFacing4.South)]
        [TestCase(-1, 0, 0, ActorFacing4.West)]
        [TestCase(0, 1, 1, ActorFacing4.East)]
        [TestCase(1, 0, 1, ActorFacing4.South)]
        [TestCase(0, -1, 1, ActorFacing4.West)]
        [TestCase(-1, 0, 1, ActorFacing4.North)]
        [TestCase(0, 1, -1, ActorFacing4.West)]
        [TestCase(0, 1, 5, ActorFacing4.East)]
        public void TryResolveView_AppliesSameQuarterTurnSignAsRotateToView(
            int dx,
            int dy,
            int viewQuarterTurns,
            ActorFacing4 expected)
        {
            Assert.IsTrue(ActorFacingRules.TryResolveView(
                Origin,
                Origin.Offset(dx, dy),
                viewQuarterTurns,
                out ActorFacing4 actual));
            Assert.AreEqual(expected, actual);
        }

        [TestCase(3, 1, ActorFacing4.East)]
        [TestCase(-3, 1, ActorFacing4.West)]
        [TestCase(1, 3, ActorFacing4.North)]
        [TestCase(1, -3, ActorFacing4.South)]
        [TestCase(1, 1, ActorFacing4.North)]
        [TestCase(1, -1, ActorFacing4.East)]
        [TestCase(-1, -1, ActorFacing4.South)]
        [TestCase(-1, 1, ActorFacing4.West)]
        public void TryResolveWorld_LongOrDiagonalTargets_UsesStableFourWaySector(
            int dx,
            int dy,
            ActorFacing4 expected)
        {
            Assert.IsTrue(ActorFacingRules.TryResolveWorld(
                Origin,
                Origin.Offset(dx, dy),
                out ActorFacing4 actual));
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TryResolve_DiagonalBoundary_RotatesEquivariantly()
        {
            GridPos target = Origin.Offset(4, 4);
            for (int turn = 0; turn < 4; turn++)
            {
                Assert.IsTrue(ActorFacingRules.TryResolveView(Origin, target, turn, out ActorFacing4 actual));
                Assert.AreEqual(ActorFacingRules.RotateToView(ActorFacing4.North, turn), actual);
            }
        }

        [Test]
        public void SamePlanePosition_ReturnsFalse_OrPreservesFallback()
        {
            GridPos otherElevation = Origin.WithElevation(99);
            Assert.IsFalse(ActorFacingRules.TryResolveWorld(Origin, otherElevation, out _));
            Assert.IsFalse(ActorFacingRules.TryResolveView(Origin, otherElevation, 2, out _));
            Assert.AreEqual(
                ActorFacing4.West,
                ActorFacingRules.ResolveViewOr(Origin, otherElevation, 2, ActorFacing4.West));
        }

        [TestCase(SpriteClipTags.Idle)]
        [TestCase(SpriteClipTags.Walk)]
        [TestCase(SpriteClipTags.Attack)]
        [TestCase(SpriteClipTags.Hit)]
        [TestCase(SpriteClipTags.Fall)]
        [TestCase(SpriteClipTags.Death)]
        public void BaseTags_RemainSupported(string baseTag)
        {
            Assert.IsTrue(DirectionalSpriteClipTags.IsSupportedBaseTag(baseTag));
            Assert.IsTrue(DirectionalSpriteClipTags.IsSupportedTag(baseTag));
        }

        [TestCase(SpriteClipTags.Idle, ActorFacing4.North, "idle-north")]
        [TestCase(SpriteClipTags.Walk, ActorFacing4.East, "walk-east")]
        [TestCase(SpriteClipTags.Attack, ActorFacing4.South, "attack-south")]
        [TestCase(SpriteClipTags.Hit, ActorFacing4.West, "hit-west")]
        public void ComposeAndParse_RoundTripsOfficialDirectionalTags(
            string baseTag,
            ActorFacing4 facing,
            string expected)
        {
            string composed = DirectionalSpriteClipTags.Compose(baseTag, facing);
            Assert.AreEqual(expected, composed);
            Assert.IsTrue(DirectionalSpriteClipTags.IsSupportedTag(composed));
            Assert.IsTrue(DirectionalSpriteClipTags.TryParse(
                composed,
                out string parsedBase,
                out ActorFacing4 parsedFacing));
            Assert.AreEqual(baseTag, parsedBase);
            Assert.AreEqual(facing, parsedFacing);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("run")]
        [TestCase("idle-up")]
        [TestCase("run-north")]
        [TestCase("idle-North")]
        public void UnsupportedTags_AreRejected(string tag)
        {
            Assert.IsFalse(DirectionalSpriteClipTags.IsSupportedTag(tag));
            Assert.IsFalse(DirectionalSpriteClipTags.TryParse(tag, out _, out _));
        }

        [Test]
        public void Compose_InvalidBaseOrFacing_FailsFast()
        {
            Assert.Throws<ArgumentException>(() =>
                DirectionalSpriteClipTags.Compose("run", ActorFacing4.North));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DirectionalSpriteClipTags.Compose(SpriteClipTags.Idle, (ActorFacing4)99));
            Assert.IsFalse(DirectionalSpriteClipTags.TryCompose(
                "run",
                ActorFacing4.North,
                out _));
            Assert.IsFalse(DirectionalSpriteClipTags.TryCompose(
                SpriteClipTags.Idle,
                (ActorFacing4)99,
                out _));
        }
    }
}
