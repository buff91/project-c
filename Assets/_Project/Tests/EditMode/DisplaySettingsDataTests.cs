using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class DisplaySettingsDataTests
    {
        [Test]
        public void Defaults_MatchDesignedReadabilityValues()
        {
            DisplaySettingsData value = DisplaySettingsData.Defaults;

            Assert.IsTrue(value.FadePlayerOccluders);
            Assert.IsTrue(value.ShowRearWalls);
            Assert.AreEqual(0.3f, value.PlayerOccluderAlpha);
            Assert.AreEqual(0.54f, value.VerticalPreviewAlpha);
            Assert.AreEqual(0.16f, value.ExploredAlpha);
        }

        [Test]
        public void Clamped_RestrictsEveryAlphaToUiRange()
        {
            var value = new DisplaySettingsData
            {
                PlayerOccluderAlpha = -1f,
                VerticalPreviewAlpha = 2f,
                ExploredAlpha = 3f
            };

            DisplaySettingsData clamped = value.Clamped();

            Assert.AreEqual(0.12f, clamped.PlayerOccluderAlpha);
            Assert.AreEqual(0.8f, clamped.VerticalPreviewAlpha);
            Assert.AreEqual(0.4f, clamped.ExploredAlpha);
        }
    }
}
