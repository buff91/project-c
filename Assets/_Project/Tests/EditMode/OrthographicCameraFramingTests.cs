using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class OrthographicCameraFramingTests
    {
        [Test]
        public void Follow_HubAndDungeonPlay_UseExactlySameScale()
        {
            var center = new Vector2(1.5f, -2f);
            OrthographicCameraFrame hub = OrthographicCameraFraming.Follow(
                center,
                hubMode: true,
                DungeonViewMode.Play,
                playSize: 2.3f,
                debugSize: 8.8f);
            OrthographicCameraFrame dungeon = OrthographicCameraFraming.Follow(
                center,
                hubMode: false,
                DungeonViewMode.Play,
                playSize: 2.3f,
                debugSize: 8.8f);

            Assert.That(hub.Center, Is.EqualTo(center));
            Assert.That(dungeon.Center, Is.EqualTo(center));
            Assert.That(hub.Size, Is.EqualTo(2.3f));
            Assert.That(dungeon.Size, Is.EqualTo(hub.Size));
        }

        [Test]
        public void Follow_HubNeverUsesDungeonDebugScale()
        {
            OrthographicCameraFrame frame = OrthographicCameraFraming.Follow(
                Vector2.zero,
                hubMode: true,
                DungeonViewMode.DebugAll,
                playSize: 2.3f,
                debugSize: 8.8f);

            Assert.That(frame.Size, Is.EqualTo(2.3f));
        }

        [Test]
        public void Follow_DungeonDebugAll_UsesDebugScale()
        {
            OrthographicCameraFrame frame = OrthographicCameraFraming.Follow(
                Vector2.zero,
                hubMode: false,
                DungeonViewMode.DebugAll,
                playSize: 2.3f,
                debugSize: 8.8f);

            Assert.That(frame.Size, Is.EqualTo(8.8f));
        }

        [Test]
        public void Follow_RejectsInvalidSizes()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.Follow(
                    Vector2.zero,
                    hubMode: false,
                    DungeonViewMode.Play,
                    playSize: 0f,
                    debugSize: 8.8f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.Follow(
                    Vector2.zero,
                    hubMode: false,
                    DungeonViewMode.Play,
                    playSize: 2.3f,
                    debugSize: 0f));
        }

        [Test]
        public void ScreenDragToWorldDelta_SameViewportFraction_IsResolutionIndependent()
        {
            Vector2 lowResolution = OrthographicCameraFraming.ScreenDragToWorldDelta(
                new Vector2(100f, -50f),
                orthographicSize: 2.3f,
                pixelHeight: 1000f);
            Vector2 highResolution = OrthographicCameraFraming.ScreenDragToWorldDelta(
                new Vector2(200f, -100f),
                orthographicSize: 2.3f,
                pixelHeight: 2000f);

            Assert.That(highResolution.x, Is.EqualTo(lowResolution.x).Within(0.0001f));
            Assert.That(highResolution.y, Is.EqualTo(lowResolution.y).Within(0.0001f));
            Assert.Less(lowResolution.x, 0f, "화면을 오른쪽으로 끌면 카메라는 왼쪽으로 가야 한다");
            Assert.Greater(lowResolution.y, 0f, "화면을 아래로 끌면 카메라는 위로 가야 한다");
        }

        [Test]
        public void ScreenDragToWorldDelta_RejectsInvalidCameraValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.ScreenDragToWorldDelta(
                    Vector2.one,
                    orthographicSize: 0f,
                    pixelHeight: 720f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.ScreenDragToWorldDelta(
                    Vector2.one,
                    orthographicSize: 2.3f,
                    pixelHeight: float.NaN));
        }

        [Test]
        public void ClampCenterToProjectedBounds_UsesKnownBoundsAndPadding()
        {
            IReadOnlyList<Vector2> known = new[]
            {
                new Vector2(-2f, -1f),
                new Vector2(3f, 4f)
            };

            Vector2 clamped = OrthographicCameraFraming.ClampCenterToProjectedBounds(
                new Vector2(12f, -8f),
                known,
                new Vector2(0.5f, 0.25f));

            Assert.That(clamped.x, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(-1.25f).Within(0.0001f));
        }

        [Test]
        public void ClampCenterToProjectedBounds_RejectsMissingOrInvalidBounds()
        {
            Assert.Throws<System.ArgumentException>(() =>
                OrthographicCameraFraming.ClampCenterToProjectedBounds(
                    Vector2.zero,
                    new Vector2[0],
                    Vector2.zero));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.ClampCenterToProjectedBounds(
                    Vector2.zero,
                    new[] { Vector2.zero },
                    new Vector2(-1f, 0f)));
        }
    }
}
