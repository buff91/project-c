using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests
{
    public class RuntimeSpriteGroundingTests
    {
        [Test]
        public void StaticWalkArtOffset_LiftsOnlyInsideStepAndReturnsToBaseline()
        {
            Assert.AreEqual(0f, IsoPrototypeDemo.StaticWalkArtOffset(0f), 0.0001f);
            Assert.Greater(IsoPrototypeDemo.StaticWalkArtOffset(0.5f), 0.06f);
            Assert.AreEqual(0f, IsoPrototypeDemo.StaticWalkArtOffset(1f), 0.0001f);
            Assert.AreEqual(0f, IsoPrototypeDemo.StaticWalkArtOffset(-1f), 0.0001f);
            Assert.AreEqual(0f, IsoPrototypeDemo.StaticWalkArtOffset(2f), 0.0001f);
        }

        [Test]
        public void StaticWalkPose_LeansAndStretchesThenRestoresExactBaseline()
        {
            IsoPrototypeDemo.StaticWalkPose start =
                IsoPrototypeDemo.StaticWalkPoseAt(0f, 1f);
            IsoPrototypeDemo.StaticWalkPose middle =
                IsoPrototypeDemo.StaticWalkPoseAt(0.5f, 1f);
            IsoPrototypeDemo.StaticWalkPose end =
                IsoPrototypeDemo.StaticWalkPoseAt(1f, 1f);

            Assert.AreEqual(Vector2.zero, start.Offset);
            Assert.AreEqual(0f, start.RotationDegrees, 0.0001f);
            Assert.AreEqual(Vector2.one, start.Scale);
            Assert.Greater(middle.Offset.y, 0.06f);
            Assert.Less(middle.RotationDegrees, -4.9f);
            Assert.Less(middle.Scale.x, 1f);
            Assert.Greater(middle.Scale.y, 1.05f);
            Assert.AreEqual(0f, end.Offset.x, 0.0001f);
            Assert.AreEqual(0f, end.Offset.y, 0.0001f);
            Assert.AreEqual(0f, end.RotationDegrees, 0.0001f);
            Assert.AreEqual(1f, end.Scale.x, 0.0001f);
            Assert.AreEqual(1f, end.Scale.y, 0.0001f);
        }

        [Test]
        public void VisualAnimationProgress_RequiresMinimumVisibleFramesAtLowFps()
        {
            Assert.AreEqual(
                0.2f,
                IsoPrototypeDemo.VisualAnimationProgress(1f, 0.18f, 1, 5),
                0.0001f);
            Assert.AreEqual(
                0.8f,
                IsoPrototypeDemo.VisualAnimationProgress(1f, 0.18f, 4, 5),
                0.0001f);
            Assert.AreEqual(
                1f,
                IsoPrototypeDemo.VisualAnimationProgress(1f, 0.18f, 5, 5),
                0.0001f);
        }

        [Test]
        public void EnemyFallMotion_VisibleOriginHiddenDestination_RemainsPresented()
        {
            Vector3 endpoint = new Vector3(2.5f, -4f, 0f);

            IsoPrototypeDemo.EnemyFallMotionPlan motion =
                IsoPrototypeDemo.ResolveEnemyFallMotion(
                    debugAll: false,
                    originVisible: true,
                    destinationVisible: false,
                    originVerticalTarget: false,
                    destinationVerticalTarget: false,
                    persistentEndpoint: endpoint);

            Assert.IsTrue(motion.Present);
            Assert.AreEqual(endpoint, motion.Endpoint);
        }

        [Test]
        public void EnemyFallMotion_DebugOrVerticalObserver_KeepsSeparatedVisualEndpoint()
        {
            Vector3 rawGridEndpoint = new Vector3(1f, -0.5f, 0f);
            Vector3 debugVisualEndpoint = rawGridEndpoint + Vector3.up * 3.2f;
            Vector3 verticalVisualEndpoint = rawGridEndpoint + Vector3.down * 0.38f;

            IsoPrototypeDemo.EnemyFallMotionPlan debugMotion =
                IsoPrototypeDemo.ResolveEnemyFallMotion(
                    debugAll: true,
                    originVisible: false,
                    destinationVisible: false,
                    originVerticalTarget: false,
                    destinationVerticalTarget: false,
                    persistentEndpoint: debugVisualEndpoint);
            IsoPrototypeDemo.EnemyFallMotionPlan verticalMotion =
                IsoPrototypeDemo.ResolveEnemyFallMotion(
                    debugAll: false,
                    originVisible: false,
                    destinationVisible: false,
                    originVerticalTarget: false,
                    destinationVerticalTarget: true,
                    persistentEndpoint: verticalVisualEndpoint);

            Assert.IsTrue(debugMotion.Present);
            Assert.AreEqual(debugVisualEndpoint, debugMotion.Endpoint);
            Assert.AreNotEqual(rawGridEndpoint, debugMotion.Endpoint);
            Assert.IsTrue(verticalMotion.Present);
            Assert.AreEqual(verticalVisualEndpoint, verticalMotion.Endpoint);
            Assert.AreNotEqual(rawGridEndpoint, verticalMotion.Endpoint);
        }

        [Test]
        public void EnemyFallCompletion_SurvivorReturnsToIdle_DeathRemainsHeld()
        {
            Assert.AreEqual(
                IsoPrototypeDemo.EnemyFallCompletion.ReturnToIdle,
                IsoPrototypeDemo.ResolveEnemyFallCompletion(isAlive: true));
            Assert.AreEqual(
                IsoPrototypeDemo.EnemyFallCompletion.PreserveDeath,
                IsoPrototypeDemo.ResolveEnemyFallCompletion(isAlive: false));
        }

        [Test]
        public void StaticFacingPose_KeepsScaleStableAcrossViewDirections()
        {
            IsoPrototypeDemo.StaticFacingPose north =
                IsoPrototypeDemo.StaticFacingPoseFor(ActorFacing4.North);
            IsoPrototypeDemo.StaticFacingPose east =
                IsoPrototypeDemo.StaticFacingPoseFor(ActorFacing4.East);
            IsoPrototypeDemo.StaticFacingPose south =
                IsoPrototypeDemo.StaticFacingPoseFor(ActorFacing4.South);
            IsoPrototypeDemo.StaticFacingPose west =
                IsoPrototypeDemo.StaticFacingPoseFor(ActorFacing4.West);

            Assert.IsTrue(north.FlipX);
            Assert.IsFalse(east.FlipX);
            Assert.IsFalse(south.FlipX);
            Assert.IsTrue(west.FlipX);
            Assert.AreEqual(Vector2.one, north.Scale);
            Assert.AreEqual(Vector2.one, east.Scale);
            Assert.AreEqual(Vector2.one, south.Scale);
            Assert.AreEqual(Vector2.one, west.Scale);
            Assert.Greater(south.Offset.y, 0f);
            Assert.Greater(west.Offset.y, 0f);
        }

        [Test]
        public void ImpactRecoilDirection_PushesAwayFromIncomingOrigin()
        {
            Vector2 right = IsoPrototypeDemo.ImpactRecoilDirection(
                new Vector2(2f, 1f),
                new Vector2(1f, 1f));
            Vector2 diagonal = IsoPrototypeDemo.ImpactRecoilDirection(
                new Vector2(-1f, 2f),
                new Vector2(1f, 0f));

            Assert.AreEqual(Vector2.right, right);
            Assert.AreEqual(1f, diagonal.magnitude, 0.0001f);
            Assert.Less(diagonal.x, 0f);
            Assert.Greater(diagonal.y, 0f);
            Assert.AreEqual(
                Vector2.zero,
                IsoPrototypeDemo.ImpactRecoilDirection(Vector2.one, Vector2.one));
        }

        // 128-레짐: 기대값은 픽셀 단위라 캔버스 ×2와 함께 전부 ×2 (정규화 피벗은 불변).
        [TestCase("prop-campfire", 12f)]
        [TestCase("prop-explosive-barrel", 10f)]
        [TestCase("prop-portal", 12f)]
        [TestCase("prop-stash", 22f)]
        [TestCase("item-blast-powder", 10f)]
        [TestCase("item-bomb", 8f)]
        [TestCase("item-coin-pouch", 12f)]
        [TestCase("item-frost-bomb", 8f)]
        [TestCase("item-frost-shard", 8f)]
        [TestCase("item-gemstone", 4f)]
        [TestCase("item-herb", 10f)]
        [TestCase("item-oil-flask", 8f)]
        [TestCase("item-potion", 8f)]
        [TestCase("item-recall-scroll", 6f)]
        [TestCase("item-relic", 6f)]
        [TestCase("item-throwing-knife", 4f)]
        public void WorldSprite_PivotMatchesOpaqueGroundContact(string assetName, float expectedPivotY)
        {
            string path = $"Assets/_Project/Art/Runtime/{assetName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            Assert.IsNotNull(sprite, $"Runtime sprite missing: {path}");
            Assert.AreEqual(expectedPivotY, sprite.pivot.y, 0.01f, $"Ground pivot mismatch: {path}");
        }
    }
}
