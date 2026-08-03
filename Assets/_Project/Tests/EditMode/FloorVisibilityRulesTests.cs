using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class FloorVisibilityRulesTests
    {
        [Test]
        public void ShouldRenderWorldGeometry_ExploredTileOnActiveFloor_IsVisible()
        {
            Assert.IsTrue(FloorVisibilityRules.ShouldRenderWorldGeometry(
                debugAll: false,
                tileFloorIndex: 0,
                activeFloorIndex: 0,
                visible: false,
                explored: true,
                verticalPreview: false));
        }

        [Test]
        public void ShouldRenderWorldGeometry_ExploredTileOnInactiveFloor_IsHidden()
        {
            Assert.IsFalse(FloorVisibilityRules.ShouldRenderWorldGeometry(
                debugAll: false,
                tileFloorIndex: -1,
                activeFloorIndex: 0,
                visible: false,
                explored: true,
                verticalPreview: false),
                "다른 층의 탐색 기억이 월드에 남으면 투시처럼 보인다.");
        }

        [Test]
        public void ShouldRenderWorldGeometry_VerticalOpeningOnInactiveFloor_IsVisible()
        {
            Assert.IsTrue(FloorVisibilityRules.ShouldRenderWorldGeometry(
                debugAll: false,
                tileFloorIndex: -1,
                activeFloorIndex: 0,
                visible: false,
                explored: false,
                verticalPreview: true));
        }

        [Test]
        public void ShouldRenderWorldGeometry_DebugAll_ShowsEveryFloor()
        {
            Assert.IsTrue(FloorVisibilityRules.ShouldRenderWorldGeometry(
                debugAll: true,
                tileFloorIndex: -2,
                activeFloorIndex: 0,
                visible: false,
                explored: false,
                verticalPreview: false));
        }

        [Test]
        public void ShouldRenderMappedSilhouette_UnknownMappedTileOnActiveFloor_IsVisible()
        {
            Assert.IsTrue(FloorVisibilityRules.ShouldRenderMappedSilhouette(
                debugAll: false,
                tileFloorIndex: 0,
                activeFloorIndex: 0,
                visible: false,
                explored: false,
                mapped: true));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void ShouldRenderMappedSilhouette_ActualOrInactivePresentation_IsHidden(
            bool visible,
            bool explored,
            bool inactiveFloor)
        {
            Assert.IsFalse(FloorVisibilityRules.ShouldRenderMappedSilhouette(
                debugAll: false,
                tileFloorIndex: inactiveFloor ? -1 : 0,
                activeFloorIndex: 0,
                visible: visible,
                explored: explored,
                mapped: true));
        }

        [Test]
        public void ShouldRenderMappedSilhouette_DebugAll_UsesActualGeometryOnly()
        {
            Assert.IsFalse(FloorVisibilityRules.ShouldRenderMappedSilhouette(
                debugAll: true,
                tileFloorIndex: 0,
                activeFloorIndex: 0,
                visible: false,
                explored: false,
                mapped: true));
        }
    }
}
