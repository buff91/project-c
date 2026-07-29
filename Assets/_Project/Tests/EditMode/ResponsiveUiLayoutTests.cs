using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class ResponsiveUiLayoutTests
    {
        /// <summary>
        /// 케이스는 전부 <see cref="UiPanelScale"/>가 실제 화면에서 만들어내는 논리 캔버스다.
        /// 손으로 고른 숫자가 아니다 — 아래 <c>Classify_CasesAreReachableCanvases</c>가
        /// 표면 해상도에서 같은 값이 나오는지 되짚는다. 옛 케이스들은 540×960 시절
        /// 배율 산술이라 캔버스가 640×360으로 옮겨간 뒤로는 어떤 화면에서도 안 나온다.
        /// </summary>
        //          논리 W  논리 H  narrow  short  landsc  expand  tall  ultraW
        // 1280×720 · 1920×1080 · 2560×1440 — PC 검증 3종이 전부 여기로 모인다.
        [TestCase(640f, 360f, false, true, true, false, false, false)]
        // 1366×768 — 유일하게 16:9가 아닌 노트북 프리셋.
        [TestCase(683f, 384f, false, true, true, false, false, false)]
        // 2560×1080 — 울트라와이드.
        [TestCase(853f, 360f, false, true, true, false, false, true)]
        // 1280×1024 — 5:4. PC에서 is-expanded가 실제로 켜지는 유일한 경로다.
        [TestCase(640f, 512f, false, false, true, true, false, false)]
        // 1080×1920 — 세로 태블릿/폰. 짧은 축 기준이라 캔버스가 무너지지 않는다.
        [TestCase(360f, 640f, true, false, false, false, false, false)]
        // 1170×2532 — 세로 폰(19.5:9). 극단 종횡비.
        [TestCase(390f, 844f, true, false, false, false, true, false)]
        // 844×390 — 가로 폰. 배율 1이라 논리 크기가 표면과 같다.
        [TestCase(844f, 390f, false, true, true, false, false, true)]
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

            Assert.AreEqual(narrow, profile.Narrow, "narrow");
            Assert.AreEqual(shortViewport, profile.Short, "short");
            Assert.AreEqual(landscape, profile.Landscape, "landscape");
            Assert.AreEqual(expanded, profile.Expanded, "expanded");
            Assert.AreEqual(tall, profile.Tall, "tall");
            Assert.AreEqual(ultraWide, profile.UltraWide, "ultrawide");
        }

        /// <summary>
        /// 위 케이스들이 진짜 도달 가능한 캔버스인지 표면 해상도에서 되짚는다.
        /// 이게 없으면 임계값을 옮길 때 아무도 안 쓰는 캔버스에 맞춰 튜닝하게 된다 —
        /// 옛 테스트가 정확히 그 상태였다.
        /// </summary>
        [TestCase(1280, 720, 640f, 360f)]
        [TestCase(1920, 1080, 640f, 360f)]
        [TestCase(2560, 1440, 640f, 360f)]
        [TestCase(1366, 768, 683f, 384f)]
        [TestCase(2560, 1080, 853f, 360f)]
        [TestCase(1280, 1024, 640f, 512f)]
        [TestCase(1080, 1920, 360f, 640f)]
        [TestCase(1170, 2532, 390f, 844f)]
        [TestCase(844, 390, 844f, 390f)]
        public void Classify_CasesAreReachableCanvases(
            int surfaceWidth,
            int surfaceHeight,
            float expectedLogicalWidth,
            float expectedLogicalHeight)
        {
            UiPanelScale.LogicalSize(
                surfaceWidth, surfaceHeight, out float width, out float height);

            Assert.That(width, Is.EqualTo(expectedLogicalWidth).Within(0.5f));
            Assert.That(height, Is.EqualTo(expectedLogicalHeight).Within(0.5f));
        }

        /// <summary>
        /// PC 3종은 정확히 같은 캔버스라 정확히 같은 분기를 받아야 한다 —
        /// 이 계획이 640×360으로 옮긴 이유 전체가 이 한 줄이다(배치 하나, 분기 없음).
        /// </summary>
        [Test]
        public void Classify_IsIdenticalAcrossEveryPcTarget()
        {
            (int w, int h)[] targets = { (1280, 720), (1920, 1080), (2560, 1440) };
            ResponsiveUiLayout.ViewportProfile first = default;

            for (int i = 0; i < targets.Length; i++)
            {
                UiPanelScale.LogicalSize(
                    targets[i].w, targets[i].h, out float lw, out float lh);
                ResponsiveUiLayout.ViewportProfile profile =
                    ResponsiveUiLayout.Classify(lw, lh);

                if (i == 0) { first = profile; continue; }
                Assert.AreEqual(first.Narrow, profile.Narrow);
                Assert.AreEqual(first.Short, profile.Short);
                Assert.AreEqual(first.Landscape, profile.Landscape);
                Assert.AreEqual(first.Expanded, profile.Expanded);
                Assert.AreEqual(first.Tall, profile.Tall);
                Assert.AreEqual(first.UltraWide, profile.UltraWide);
            }
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
