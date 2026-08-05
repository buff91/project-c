using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public sealed class MapInspectionRulesTests
    {
        [TestCase(true, true, true, MapInspectionTileState.Visible)]
        [TestCase(false, true, true, MapInspectionTileState.Explored)]
        [TestCase(false, false, true, MapInspectionTileState.Mapped)]
        [TestCase(false, false, false, MapInspectionTileState.None)]
        public void ResolveTile_CurrentFloor_UsesStrongestAvailableKnowledge(
            bool visible,
            bool explored,
            bool mapped,
            MapInspectionTileState expected)
        {
            Assert.AreEqual(
                expected,
                MapInspectionRules.ResolveTile(
                    isCurrentFloor: true,
                    visible,
                    explored,
                    mapped));
        }

        [TestCase(true, true, true, MapInspectionTileState.Explored)]
        [TestCase(true, false, true, MapInspectionTileState.None)]
        [TestCase(false, false, true, MapInspectionTileState.None)]
        [TestCase(false, true, false, MapInspectionTileState.Explored)]
        public void ResolveTile_InactiveFloor_AllowsExploredMemoryOnly(
            bool visible,
            bool explored,
            bool mapped,
            MapInspectionTileState expected)
        {
            Assert.AreEqual(
                expected,
                MapInspectionRules.ResolveTile(
                    isCurrentFloor: false,
                    visible,
                    explored,
                    mapped));
        }

        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void CanInspectFloor_CurrentOrExploredOnly(
            bool isCurrentFloor,
            bool hasExplored,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                MapInspectionRules.CanInspectFloor(isCurrentFloor, hasExplored));
        }

        [Test]
        public void CanShowLiveEntities_CurrentFloorOnly()
        {
            Assert.IsTrue(MapInspectionRules.CanShowLiveEntities(isCurrentFloor: true));
            Assert.IsFalse(MapInspectionRules.CanShowLiveEntities(isCurrentFloor: false));
        }
    }
}
