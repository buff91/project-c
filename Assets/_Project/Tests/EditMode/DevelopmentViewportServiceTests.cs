using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class DevelopmentViewportServiceTests
    {
        [Test]
        public void Presets_HaveUniqueIdsAndPositiveFixedDimensions()
        {
            var ids = new HashSet<string>();
            foreach (DevelopmentViewportPreset preset in DevelopmentViewportService.Presets)
            {
                Assert.IsTrue(ids.Add(preset.Id), $"Duplicate viewport preset: {preset.Id}");
                if (preset.IsFreeAspect) continue;
                Assert.Greater(preset.Width, 0);
                Assert.Greater(preset.Height, 0);
            }
        }

        [Test]
        public void FindPreset_UnknownIdFallsBackToFreeAspect()
        {
            DevelopmentViewportPreset preset = DevelopmentViewportService.FindPreset("missing");
            Assert.IsTrue(preset.IsFreeAspect);
            Assert.AreEqual("free", preset.Id);
        }

        [Test]
        public void DefaultPreset_IsQhdAndKeepsSixteenByNine()
        {
            DevelopmentViewportPreset preset = DevelopmentViewportService.FindPreset(
                DevelopmentViewportService.DefaultPresetId);

            Assert.AreEqual(2560, preset.Width);
            Assert.AreEqual(1440, preset.Height);
            Assert.AreEqual(16f / 9f, preset.Width / (float)preset.Height, 0.0001f);
        }

        [TestCase(HudPresentationMode.Auto, "AUTO UI")]
        [TestCase(HudPresentationMode.Mobile, "MOBILE UI")]
        [TestCase(HudPresentationMode.Desktop, "PC UI")]
        public void ModeLabel_UsesDeveloperFacingNames(HudPresentationMode mode, string expected)
        {
            Assert.AreEqual(expected, DevelopmentViewportService.ModeLabel(mode));
        }
    }
}
