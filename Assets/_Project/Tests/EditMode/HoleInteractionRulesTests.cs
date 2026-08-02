using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class HoleInteractionRulesTests
    {
        [Test]
        public void TryCreatePreview_ActualHole_ReportsLandingDamageAndRetreat()
        {
            var map = new GridMap();
            var height = new DungeonHeightModel(4);
            var hole = new GridPos(2, 3, 4);
            var landing = new GridPos(2, 3, 0);
            map.Set(hole, TileKind.Hole);
            map.Set(landing, TileKind.Floor);

            Assert.IsTrue(HoleInteractionRules.TryCreatePreview(
                map,
                height,
                hole,
                minElevation: 0,
                DungeonProgressDirection.Ascend,
                safeFallHeight: 0,
                out HoleDropPreview preview));

            Assert.AreEqual(landing, preview.Landing);
            Assert.AreEqual(0, preview.DestinationFloorIndex);
            Assert.AreEqual(4, preview.DropCells);
            Assert.AreEqual(2, preview.Damage);
            Assert.AreEqual(FallMeaning.Retreat, preview.Meaning);
        }

        [Test]
        public void TryCreatePreview_SafeFallHeight_ReducesPredictedDamage()
        {
            var map = new GridMap();
            var height = new DungeonHeightModel(4);
            var hole = new GridPos(1, 1, 4);
            map.Set(hole, TileKind.Hole);
            map.Set(new GridPos(1, 1, 0), TileKind.Floor);

            Assert.IsTrue(HoleInteractionRules.TryCreatePreview(
                map,
                height,
                hole,
                minElevation: 0,
                DungeonProgressDirection.Descend,
                safeFallHeight: 4,
                out HoleDropPreview preview));

            Assert.AreEqual(0, preview.Damage);
            Assert.AreEqual(FallMeaning.Shortcut, preview.Meaning);
        }

        [Test]
        public void TryCreatePreview_NoLanding_ReturnsFalse()
        {
            var map = new GridMap();
            var hole = new GridPos(0, 0, 4);
            map.Set(hole, TileKind.Hole);

            Assert.IsFalse(HoleInteractionRules.TryCreatePreview(
                map,
                new DungeonHeightModel(4),
                hole,
                minElevation: 0,
                DungeonProgressDirection.Ascend,
                safeFallHeight: 0,
                out _));
        }

        [Test]
        public void ResolveTap_FirstOrDifferentHoleArms_SameHoleConfirms()
        {
            var first = new GridPos(1, 1, 4);
            var second = new GridPos(2, 1, 4);

            Assert.AreEqual(
                HoleDropTapDecision.Arm,
                HoleInteractionRules.ResolveTap(null, first));
            Assert.AreEqual(
                HoleDropTapDecision.Arm,
                HoleInteractionRules.ResolveTap(first, second));
            Assert.AreEqual(
                HoleDropTapDecision.Confirm,
                HoleInteractionRules.ResolveTap(first, first));
        }
    }
}
