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
    }
}
