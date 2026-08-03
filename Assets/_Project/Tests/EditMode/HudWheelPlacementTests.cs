using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public sealed class HudWheelPlacementTests
    {
        private static readonly Vector2 Panel = new Vector2(640f, 360f);
        private static readonly Vector2 Button = new Vector2(48f, 42f);
        private const float Radius = 56f;
        private const float Margin = 4f;

        [Test]
        public void LeavesAlreadySafeAnchorUnchanged()
        {
            Vector2 desired = new Vector2(320f, 180f);
            Vector2 result = HudWheelPlacement.FindSafeCenter(
                desired, Panel, Button, Radius, Margin,
                new List<Rect> { new Rect(8f, 328f, 624f, 24f) });

            Assert.AreEqual(desired, result);
        }

        [Test]
        public void PushesWholeWheelAboveBottomRail()
        {
            var bottomRail = new Rect(8f, 328f, 624f, 24f);
            Vector2 result = HudWheelPlacement.FindSafeCenter(
                new Vector2(320f, 330f), Panel, Button, Radius, Margin,
                new List<Rect> { bottomRail });

            Rect bounds = HudWheelPlacement.Bounds(result, Button, Radius);
            Assert.LessOrEqual(bounds.yMax, bottomRail.yMin - Margin);
        }

        [Test]
        public void AvoidsCornerInstrumentAndPanelEdges()
        {
            var instrument = new Rect(456f, 8f, 176f, 104f);
            Vector2 result = HudWheelPlacement.FindSafeCenter(
                new Vector2(610f, 40f), Panel, Button, Radius, Margin,
                new List<Rect> { instrument });

            Rect bounds = HudWheelPlacement.Bounds(result, Button, Radius);
            Assert.IsFalse(bounds.Overlaps(new Rect(452f, 4f, 184f, 112f)));
            Assert.GreaterOrEqual(bounds.xMin, Margin);
            Assert.GreaterOrEqual(bounds.yMin, Margin);
            Assert.LessOrEqual(bounds.xMax, Panel.x - Margin);
            Assert.LessOrEqual(bounds.yMax, Panel.y - Margin);
        }

        [Test]
        public void AvoidsLogHintAndBottomRailTogether()
        {
            var reserved = new List<Rect>
            {
                new Rect(8f, 272f, 208f, 52f),
                new Rect(224f, 282f, 192f, 22f),
                new Rect(8f, 328f, 624f, 24f)
            };
            Vector2 result = HudWheelPlacement.FindSafeCenter(
                new Vector2(250f, 318f), Panel, Button, Radius, Margin, reserved);

            Rect bounds = HudWheelPlacement.Bounds(result, Button, Radius);
            foreach (Rect blocked in reserved)
            {
                Rect expanded = Rect.MinMaxRect(
                    blocked.xMin - Margin,
                    blocked.yMin - Margin,
                    blocked.xMax + Margin,
                    blocked.yMax + Margin);
                Assert.IsFalse(bounds.Overlaps(expanded), $"Overlaps {blocked}");
            }
        }

        [Test]
        public void ImpossibleLayoutReturnsLeastOverlapCandidateInsidePanel()
        {
            var coveringPanel = new Rect(0f, 0f, Panel.x, Panel.y);
            Vector2 result = HudWheelPlacement.FindSafeCenter(
                new Vector2(320f, 180f), Panel, Button, Radius, Margin,
                new List<Rect> { coveringPanel });

            Rect bounds = HudWheelPlacement.Bounds(result, Button, Radius);
            Assert.GreaterOrEqual(bounds.xMin, Margin);
            Assert.GreaterOrEqual(bounds.yMin, Margin);
            Assert.LessOrEqual(bounds.xMax, Panel.x - Margin);
            Assert.LessOrEqual(bounds.yMax, Panel.y - Margin);
        }
    }
}
