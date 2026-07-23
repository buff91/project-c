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
    }
}
