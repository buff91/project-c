using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public sealed class UiPanelScaleTests
    {
        // PC 검증 대상 3종은 전부 정확히 640×360으로 떨어져야 한다 — 그래야 배치가 하나다.
        [TestCase(1280, 720, 2)]
        [TestCase(1920, 1080, 3)]
        [TestCase(2560, 1440, 4)]
        // 16:9가 아닌 화면은 짧은 축만 360으로 맞추고 긴 축은 남는 만큼 넓어진다.
        [TestCase(1366, 768, 2)]
        [TestCase(2560, 1080, 3)]
        [TestCase(1280, 1024, 2)]
        // 세로 화면도 같은 규칙으로 성립한다 (짧은 축 기준이라서).
        [TestCase(1080, 1920, 3)]
        [TestCase(1170, 2532, 3)]
        [TestCase(844, 390, 1)]
        public void Scale_MatchesDocumentedTable(int width, int height, int expected)
        {
            Assert.That(UiPanelScale.Scale(width, height), Is.EqualTo(expected));
        }

        [TestCase(1280, 720, 640f, 360f)]
        [TestCase(1920, 1080, 640f, 360f)]
        [TestCase(2560, 1440, 640f, 360f)]
        public void LogicalSize_IsExactly640x360_OnEvery16By9PcTarget(
            int width, int height, float expectedW, float expectedH)
        {
            UiPanelScale.LogicalSize(width, height, out float w, out float h);
            Assert.That(w, Is.EqualTo(expectedW).Within(0.001f));
            Assert.That(h, Is.EqualTo(expectedH).Within(0.001f));
        }

        [Test]
        public void Scale_NeverDropsBelowOne_OnTinyOrDegenerateSurfaces()
        {
            Assert.That(UiPanelScale.Scale(320, 200), Is.EqualTo(1));
            Assert.That(UiPanelScale.Scale(1, 1), Is.EqualTo(1));
            Assert.That(UiPanelScale.Scale(0, 0), Is.EqualTo(1));
            Assert.That(UiPanelScale.Scale(-100, -100), Is.EqualTo(1));
        }

        [Test]
        public void LogicalMinorAxis_StaysWithinOneDesignStep()
        {
            // 배율이 내림이므로 논리 짧은 축은 [360, 720)에 든다. 이 범위를 벗어나면
            // 어떤 화면에서 HUD가 두 배로 커지거나 절반이 된다는 뜻이다.
            for (int h = 360; h <= 4320; h += 4)
            {
                int w = h * 16 / 9;
                UiPanelScale.LogicalSize(w, h, out float lw, out float lh);
                float minor = lw < lh ? lw : lh;
                Assert.That(minor, Is.GreaterThanOrEqualTo(360f),
                    "surface " + w + "x" + h);
                Assert.That(minor, Is.LessThan(720f), "surface " + w + "x" + h);
            }
        }
    }
}
