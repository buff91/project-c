using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class VerticalRouteCueTests
    {
        [TestCase(TileKind.Stairs, VerticalRouteRole.LocalStairs, "WALK")]
        [TestCase(TileKind.Ladder, VerticalRouteRole.Ladder, "CLIMB")]
        [TestCase(TileKind.StairsUp, VerticalRouteRole.FloorUp, "B1 ▲")]
        [TestCase(TileKind.StairsDown, VerticalRouteRole.FloorDown, "B2 ▼")]
        public void TryCreate_TraversalTile_ExplainsItsPhysicalAction(
            TileKind kind,
            VerticalRouteRole expectedRole,
            string expectedWorldLabel)
        {
            Assert.IsTrue(VerticalRouteCue.TryCreate(
                kind, viewedFromBelow: false,
                kind == TileKind.StairsUp ? "B1" : "B2",
                out VerticalRouteCue cue));

            Assert.AreEqual(expectedRole, cue.Role);
            Assert.AreEqual(expectedWorldLabel, cue.WorldLabel);
            Assert.IsFalse(string.IsNullOrEmpty(cue.Title));
            Assert.IsFalse(string.IsNullOrEmpty(cue.Detail));
            if (kind == TileKind.StairsUp || kind == TileKind.StairsDown)
                StringAssert.Contains("밟으면 즉시", cue.Detail);
        }

        [Test]
        public void TryCreate_Hole_ChangesMeaningByViewDirection()
        {
            Assert.IsTrue(VerticalRouteCue.TryCreate(
                TileKind.Hole, viewedFromBelow: false, "B2", out VerticalRouteCue down));
            Assert.IsTrue(VerticalRouteCue.TryCreate(
                TileKind.Hole, viewedFromBelow: true, "B1", out VerticalRouteCue up));

            Assert.AreEqual(VerticalRouteRole.OpeningDown, down.Role);
            Assert.AreEqual("B2 ▼", down.WorldLabel);
            StringAssert.Contains("뛰어내린다", down.Detail);
            Assert.AreEqual(VerticalRouteRole.OpeningUp, up.Role);
            Assert.AreEqual("B1 ▲", up.WorldLabel);
            StringAssert.Contains("올려다볼", up.Detail);
        }

        [Test]
        public void TryCreate_Floor_ReturnsFalse()
        {
            Assert.IsFalse(VerticalRouteCue.TryCreate(
                TileKind.Floor, viewedFromBelow: false, null, out _));
        }
    }
}
