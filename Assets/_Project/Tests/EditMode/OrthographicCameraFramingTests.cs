using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class OrthographicCameraFramingTests
    {
        private const float MinX = -3f;
        private const float MaxX = 4f;
        private const float MinY = -3.5f;
        private const float MaxY = 0f;

        [Test]
        public void Fit_Portrait_ExpandsToContainHorizontalBounds()
        {
            OrthographicCameraFrame frame = OrthographicCameraFraming.Fit(
                MinX, MaxX, MinY, MaxY,
                aspect: 9f / 16f,
                minimumSize: 5.2f,
                horizontalPadding: 0.75f,
                verticalPadding: 1.5f);

            Assert.That(frame.Center, Is.EqualTo(new Vector2(0.5f, -1.75f)));
            Assert.That(frame.Size, Is.EqualTo(4.25f / (9f / 16f)).Within(0.001f));
        }

        [Test]
        public void Fit_Landscape_PreservesMinimumSize()
        {
            OrthographicCameraFrame frame = OrthographicCameraFraming.Fit(
                MinX, MaxX, MinY, MaxY,
                aspect: 16f / 9f,
                minimumSize: 5.2f,
                horizontalPadding: 0.75f,
                verticalPadding: 1.5f);

            Assert.That(frame.Size, Is.EqualTo(5.2f));
        }

        /// <summary>
        /// 허브(13×9)를 PC 가로에서 담을 때 <b>던전 카메라 크기와 정확히 같아야 한다</b>.
        /// <para>
        /// 모든 플로우의 배율이 같아야 "같은 세계"로 읽힌다. 예전엔 허브 전용 최소 크기
        /// 필드(2.55)가 따로 있었고 그게 흘러내려 허브가 1.4배 확대돼 보였다 —
        /// 값이 두 벌이면 한쪽이 어긋나도 아무도 모른다. 이 테스트가 그 재발을 막는다.
        /// </para>
        /// </summary>
        [TestCase(16f / 9f)]
        [TestCase(1388f / 744f)]
        [TestCase(4f / 3f)]
        public void Fit_HubBounds_MatchesDungeonCameraSize_OnDesktop(float aspect)
        {
            const float dungeonCameraSize = 5.2f;

            // 13×9 허브의 아이소 투영 경계 (HalfW 0.5 / HalfH 0.25).
            //   x = (gx - gy) * 0.5  → [-4, 6]
            //   y = -(gx + gy) * 0.25 → [-5, 0]
            OrthographicCameraFrame frame = OrthographicCameraFraming.Fit(
                minX: -4f, maxX: 6f, minY: -5f, maxY: 0f,
                aspect: aspect,
                minimumSize: dungeonCameraSize,
                horizontalPadding: 0.6f,
                verticalPadding: 1.2f);

            Assert.That(
                frame.Size, Is.EqualTo(dungeonCameraSize).Within(0.001f),
                $"aspect {aspect}: 허브 배율이 던전과 달라졌다");
        }

        [Test]
        public void Fit_NarrowWindow_BacksOffOnlyAsMuchAsNeeded()
        {
            // 던전과 같게 두되, 캠프가 화면 밖으로 나갈 때만 물러난다.
            // 이 예외가 없으면 세로로 긴 창에서 캠프가 잘린다.
            const float dungeonCameraSize = 5.2f;
            OrthographicCameraFrame frame = OrthographicCameraFraming.Fit(
                minX: -4f, maxX: 6f, minY: -5f, maxY: 0f,
                aspect: 1f,
                minimumSize: dungeonCameraSize,
                horizontalPadding: 0.6f,
                verticalPadding: 1.2f);

            Assert.Greater(frame.Size, dungeonCameraSize, "좁은 창에서는 더 물러나야 한다");
            Assert.That(frame.Size, Is.EqualTo(5.6f).Within(0.001f), "필요한 만큼만 물러난다");
        }

        [Test]
        public void Fit_RejectsInvalidAspect()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthographicCameraFraming.Fit(
                    MinX, MaxX, MinY, MaxY,
                    aspect: 0f,
                    minimumSize: 5.2f,
                    horizontalPadding: 0.75f,
                    verticalPadding: 1.5f));
        }
    }
}
