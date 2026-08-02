using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
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
        public void FitProjectedBounds_FourViewDirections_KeepEquivalentRoomBoundsInsideViewport()
        {
            var iso = new IsoGrid(1f, 0.5f, 0.25f)
            {
                viewPivotX = 2.5f,
                viewPivotY = 2f,
            };
            var cells = new List<GridPos>();
            for (int x = 0; x < 6; x++)
            for (int y = 0; y < 5; y++)
                cells.Add(new GridPos(x, y));
            cells.Add(new GridPos(6, 4)); // 시작방에서 가장 가까운 일반 Door 중심.

            var viewport = new Rect(0.03f, 0.10f, 0.69f, 0.85f);
            var padding = new Vector2(0.70f, 1.15f);
            var sizes = new float[4];
            for (int view = 0; view < 4; view++)
            {
                iso.SetViewRotation(view);
                var projected = new List<Vector2>(cells.Count);
                foreach (GridPos cell in cells)
                    projected.Add(iso.GridToWorld(cell));

                OrthographicCameraFrame frame = OrthographicCameraFraming.FitProjectedBounds(
                    projected,
                    aspect: 16f / 9f,
                    viewport,
                    padding,
                    minimumSize: 2.3f);
                sizes[view] = frame.Size;

                Assert.That(frame.Size, Is.InRange(2.6f, 2.9f));
                AssertPaddedBoundsInsideViewport(
                    projected,
                    padding,
                    frame,
                    aspect: 16f / 9f,
                    viewport);
            }

            Assert.That(sizes[2], Is.EqualTo(sizes[0]).Within(0.0001f));
            Assert.That(sizes[3], Is.EqualTo(sizes[1]).Within(0.0001f));
        }

        [Test]
        public void FitProjectedBounds_RejectsInvalidAspectAndViewport()
        {
            IReadOnlyList<Vector2> points = new[] { Vector2.zero };
            var viewport = new Rect(0.03f, 0.10f, 0.69f, 0.85f);

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.FitProjectedBounds(
                    points,
                    aspect: 0f,
                    viewport,
                    Vector2.zero,
                    minimumSize: 2.3f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.FitProjectedBounds(
                    points,
                    aspect: float.NaN,
                    viewport,
                    Vector2.zero,
                    minimumSize: 2.3f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.FitProjectedBounds(
                    points,
                    aspect: 16f / 9f,
                    new Rect(0.03f, 0.10f, 0f, 0.85f),
                    Vector2.zero,
                    minimumSize: 2.3f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.FitProjectedBounds(
                    points,
                    aspect: 16f / 9f,
                    new Rect(0.40f, 0.10f, 0.70f, 0.85f),
                    Vector2.zero,
                    minimumSize: 2.3f));
        }

        private static void AssertPaddedBoundsInsideViewport(
            IReadOnlyList<Vector2> projected,
            Vector2 padding,
            OrthographicCameraFrame frame,
            float aspect,
            Rect viewport)
        {
            float minX = projected[0].x;
            float maxX = projected[0].x;
            float minY = projected[0].y;
            float maxY = projected[0].y;
            for (int i = 1; i < projected.Count; i++)
            {
                minX = Mathf.Min(minX, projected[i].x);
                maxX = Mathf.Max(maxX, projected[i].x);
                minY = Mathf.Min(minY, projected[i].y);
                maxY = Mathf.Max(maxY, projected[i].y);
            }

            minX -= padding.x;
            maxX += padding.x;
            minY -= padding.y;
            maxY += padding.y;
            Vector2[] corners =
            {
                new Vector2(minX, minY),
                new Vector2(minX, maxY),
                new Vector2(maxX, minY),
                new Vector2(maxX, maxY),
            };
            foreach (Vector2 corner in corners)
            {
                float normalizedX =
                    0.5f + (corner.x - frame.Center.x) / (2f * frame.Size * aspect);
                float normalizedY =
                    0.5f + (corner.y - frame.Center.y) / (2f * frame.Size);
                Assert.That(normalizedX, Is.InRange(viewport.xMin - 0.0001f, viewport.xMax + 0.0001f));
                Assert.That(normalizedY, Is.InRange(viewport.yMin - 0.0001f, viewport.yMax + 0.0001f));
            }
        }
    }
}
