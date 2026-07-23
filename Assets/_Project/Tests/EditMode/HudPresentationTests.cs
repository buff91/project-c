using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class HudPresentationTests
    {
        [TestCase(false, HudPresentationMode.Desktop)]
        [TestCase(true, HudPresentationMode.Mobile)]
        public void Resolve_AutoUsesPlatform(
            bool isMobilePlatform,
            HudPresentationMode expected)
        {
            Assert.AreEqual(
                expected,
                HudPresentation.Resolve(HudPresentationMode.Auto, isMobilePlatform));
        }

        [TestCase(HudPresentationMode.Mobile, false)]
        [TestCase(HudPresentationMode.Mobile, true)]
        [TestCase(HudPresentationMode.Desktop, false)]
        [TestCase(HudPresentationMode.Desktop, true)]
        public void Resolve_ExplicitOverrideWins(
            HudPresentationMode requested,
            bool isMobilePlatform)
        {
            Assert.AreEqual(
                requested,
                HudPresentation.Resolve(requested, isMobilePlatform));
        }
    }
}
