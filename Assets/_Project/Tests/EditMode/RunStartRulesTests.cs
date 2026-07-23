using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class RunStartRulesTests
    {
        [Test]
        public void ResolvePreviewDepth_NewRun_AlwaysStartsAtB1()
        {
            Assert.AreEqual(0, RunStartRules.ResolvePreviewDepth(null));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 1)]
        [TestCase(-2, 2)]
        public void ResolvePreviewDepth_Continue_UsesSavedFloor(int floorIndex, int expectedDepth)
        {
            var save = new RunSaveData { currentFloorIndex = floorIndex };

            Assert.AreEqual(expectedDepth, RunStartRules.ResolvePreviewDepth(save));
        }

        [Test]
        public void ResolvePreviewDepth_InvalidPositiveFloor_ClampsToB1()
        {
            var save = new RunSaveData { currentFloorIndex = 2 };

            Assert.AreEqual(0, RunStartRules.ResolvePreviewDepth(save));
        }
    }
}
