using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class DungeonFogBackdropLayoutTests
    {
        [Test]
        public void Calculate_SquareDungeon_CoversProjectedGridWithoutRevealingTiles()
        {
            var iso = new IsoGrid(1f, 0.5f, 0.25f)
            {
                viewPivotX = 6f,
                viewPivotY = 6f
            };

            DungeonFogBackdropFrame frame = DungeonFogBackdropLayout.Calculate(iso, 13, 13, 0);

            Assert.AreEqual(0f, frame.Center.x, 0.0001f);
            Assert.AreEqual(-3f, frame.Center.y, 0.0001f);
            Assert.AreEqual(13f, frame.Width, 0.0001f);
            Assert.AreEqual(6.5f, frame.Height, 0.0001f);
        }

        [Test]
        public void Calculate_ViewRotation_KeepsSquareFootprintStable()
        {
            var iso = new IsoGrid(1f, 0.5f, 0.25f)
            {
                viewPivotX = 6f,
                viewPivotY = 6f
            };
            DungeonFogBackdropFrame before = DungeonFogBackdropLayout.Calculate(iso, 13, 13, -4);

            iso.SetViewRotation(1);
            DungeonFogBackdropFrame after = DungeonFogBackdropLayout.Calculate(iso, 13, 13, -4);

            Assert.AreEqual(before.Center.x, after.Center.x, 0.0001f);
            Assert.AreEqual(before.Center.y, after.Center.y, 0.0001f);
            Assert.AreEqual(before.Width, after.Width, 0.0001f);
            Assert.AreEqual(before.Height, after.Height, 0.0001f);
        }

        [Test]
        public void Calculate_LowerFloor_ShiftsOnlyByElevationProjection()
        {
            var iso = new IsoGrid(1f, 0.5f, 0.25f);

            DungeonFogBackdropFrame upper = DungeonFogBackdropLayout.Calculate(iso, 9, 9, 0);
            DungeonFogBackdropFrame lower = DungeonFogBackdropLayout.Calculate(iso, 9, 9, -4);

            Assert.AreEqual(upper.Center.x, lower.Center.x, 0.0001f);
            Assert.AreEqual(upper.Center.y - 1f, lower.Center.y, 0.0001f);
            Assert.AreEqual(upper.Width, lower.Width, 0.0001f);
            Assert.AreEqual(upper.Height, lower.Height, 0.0001f);
        }

        [TestCase(0, 5)]
        [TestCase(5, 0)]
        public void Calculate_InvalidDimensions_Throws(int width, int height)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                DungeonFogBackdropLayout.Calculate(new IsoGrid(), width, height, 0));
        }
    }
}
