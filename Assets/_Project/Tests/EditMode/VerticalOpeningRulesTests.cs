using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class VerticalOpeningRulesTests
    {
        private readonly DungeonHeightModel _height = new DungeonHeightModel(4);
        private readonly GridPos _opening = new GridPos(2, 3, 0);
        private readonly GridPos _landing = new GridPos(2, 3, -4);

        [Test]
        public void ViewFromFloor_VisibleHoleOnUpperFloor_LooksDown()
        {
            GridMap map = CreateOpeningMap();
            var visible = new HashSet<GridPos> { _opening };

            VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                map, _height, 0, _opening, -8, visible.Contains, out GridPos landing);

            Assert.AreEqual(VerticalOpeningView.Downward, view);
            Assert.AreEqual(_landing, landing);
        }

        [Test]
        public void ViewFromFloor_VisibleLandingOnLowerFloor_LooksUp()
        {
            GridMap map = CreateOpeningMap();
            var visible = new HashSet<GridPos> { _landing };

            VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                map, _height, -1, _opening, -8, visible.Contains, out GridPos landing);

            Assert.AreEqual(VerticalOpeningView.Upward, view);
            Assert.AreEqual(_landing, landing);
        }

        [Test]
        public void ViewFromFloor_HiddenOpeningAndLanding_RevealsNothing()
        {
            GridMap map = CreateOpeningMap();

            VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                map, _height, -1, _opening, -8, _ => false, out _);

            Assert.AreEqual(VerticalOpeningView.None, view);
        }

        [TestCase(TileKind.Stairs)]
        [TestCase(TileKind.Ladder)]
        [TestCase(TileKind.StairsUp)]
        [TestCase(TileKind.StairsDown)]
        public void ViewFromFloor_TraversalConnectors_AreNotSightPortals(TileKind stairKind)
        {
            GridMap map = CreateOpeningMap();
            map.Set(_opening, stairKind);

            VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                map, _height, 0, _opening, -8, _ => true, out _);

            Assert.AreEqual(VerticalOpeningView.None, view);
        }

        private GridMap CreateOpeningMap()
        {
            var map = new GridMap();
            map.Set(_opening, TileKind.Hole);
            map.Set(_landing, TileKind.Floor);
            return map;
        }
    }
}
