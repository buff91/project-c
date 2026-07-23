using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class ResponsiveUiLayoutTests
    {
        [TestCase(540f, 960f, false, false, false, false, false, false)]
        [TestCase(489f, 1059f, true, false, false, false, true, false)]
        [TestCase(623f, 831f, false, false, false, true, false, false)]
        [TestCase(1059f, 489f, false, true, true, false, false, true)]
        [TestCase(960f, 540f, false, true, true, false, false, false)]
        [TestCase(1108f, 467f, false, true, true, false, false, true)]
        [TestCase(745f, 596f, false, true, true, true, false, false)]
        public void Classify_UsesLogicalPanelSize(
            float width,
            float height,
            bool narrow,
            bool shortViewport,
            bool landscape,
            bool expanded,
            bool tall,
            bool ultraWide)
        {
            ResponsiveUiLayout.ViewportProfile profile =
                ResponsiveUiLayout.Classify(width, height);

            Assert.AreEqual(narrow, profile.Narrow);
            Assert.AreEqual(shortViewport, profile.Short);
            Assert.AreEqual(landscape, profile.Landscape);
            Assert.AreEqual(expanded, profile.Expanded);
            Assert.AreEqual(tall, profile.Tall);
            Assert.AreEqual(ultraWide, profile.UltraWide);
        }

        [Test]
        public void Classify_RejectsInvalidSize()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ResponsiveUiLayout.Classify(0f, 960f));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ResponsiveUiLayout.Classify(540f, 0f));
        }
    }
}
