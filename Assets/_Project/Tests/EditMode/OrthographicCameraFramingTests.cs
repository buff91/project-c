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
