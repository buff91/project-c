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
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        public void ResolvePreviewDepth_Continue_UsesSavedProgress(int progressIndex, int expectedDepth)
        {
            var save = new RunSaveData { currentProgressIndex = progressIndex };

            Assert.AreEqual(expectedDepth, RunStartRules.ResolvePreviewDepth(save));
        }

        [Test]
        public void ResolvePreviewDepth_NegativeProgress_ClampsToFirstFloor()
        {
            var save = new RunSaveData { currentProgressIndex = -2 };

            Assert.AreEqual(0, RunStartRules.ResolvePreviewDepth(save));
        }

        /// <summary>
        /// 회귀 방지: 진행 지수는 저장된 값이지 floorIndex 에서 역산한 값이 아니다.
        /// 상승 던전은 floorIndex 가 양수라, 역산하던 예전 구현에서는 8층에서 이어해도
        /// 첫 층으로 되돌아갔다.
        /// </summary>
        [Test]
        public void ResolvePreviewDepth_AscendingDungeon_KeepsProgress()
        {
            var save = new RunSaveData { currentFloorIndex = 7, currentProgressIndex = 9 };

            Assert.AreEqual(9, RunStartRules.ResolvePreviewDepth(save));
        }
    }
}
