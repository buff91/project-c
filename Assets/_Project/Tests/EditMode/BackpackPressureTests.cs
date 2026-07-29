using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 백팩 압박 게이지 규칙. UI가 자체 판정하면 "숫자와 막대가 다른 말을 하는" 상태가
    /// 조용히 생기므로 판정은 Core 한 곳뿐이고, 그 한 곳을 여기서 잠근다.
    /// </summary>
    public sealed class BackpackPressureTests
    {
        [TestCase(0, 24, 0f)]
        [TestCase(6, 24, 0.25f)]
        [TestCase(12, 24, 0.5f)]
        [TestCase(24, 24, 1f)]
        public void Ratio_MatchesOccupancy(int used, int capacity, float expected)
        {
            Assert.That(
                BackpackPressure.Ratio(used, capacity), Is.EqualTo(expected).Within(0.0001f));
        }

        /// <summary>
        /// 큰 아이템이 칸을 넘겨 잡는 경우가 생겨도 막대가 트랙 밖으로 나가면 안 된다.
        /// </summary>
        [Test]
        public void Ratio_ClampsAboveFull()
        {
            Assert.That(BackpackPressure.Ratio(30, 24), Is.EqualTo(1f));
        }

        /// <summary>용량이 없는 상태(허브 밖·초기화 전)에서 0으로 나누지 않는다.</summary>
        [TestCase(0, 0)]
        [TestCase(5, 0)]
        [TestCase(5, -1)]
        [TestCase(-3, 24)]
        public void Ratio_IsZeroWhenThereIsNothingToShow(int used, int capacity)
        {
            Assert.That(BackpackPressure.Ratio(used, capacity), Is.EqualTo(0f));
            Assert.IsFalse(BackpackPressure.IsWarning(used, capacity));
        }

        [TestCase(0, 24, false)]
        [TestCase(12, 24, false)]   // 0.50
        [TestCase(18, 24, false)]   // 0.75 — 아직 경고 아님
        [TestCase(20, 24, true)]    // 0.83
        [TestCase(24, 24, true)]    // 가득
        public void IsWarning_TurnsOnNearFull(int used, int capacity, bool expected)
        {
            Assert.AreEqual(expected, BackpackPressure.IsWarning(used, capacity));
        }

        /// <summary>
        /// 경계는 규칙 상수에서 파생시킨다. 0.8을 손으로 적어 두면 상수를 옮길 때
        /// 테스트가 조용히 거짓말을 한다.
        /// </summary>
        [Test]
        public void IsWarning_BoundaryFollowsTheConstant()
        {
            const int capacity = 100;
            int justUnder = (int)(BackpackPressure.WarningRatio * capacity) - 1;
            int atThreshold = (int)(BackpackPressure.WarningRatio * capacity);

            Assert.IsFalse(BackpackPressure.IsWarning(justUnder, capacity), "임계 직전");
            Assert.IsTrue(BackpackPressure.IsWarning(atThreshold, capacity), "임계값");
        }

        /// <summary>실제 백팩 치수(6×4=24)에서 규칙이 성립하는지 함께 본다.</summary>
        [Test]
        public void IsWarning_UsesRealBackpackCapacity()
        {
            int capacity = BackpackRules.Columns * BackpackRules.Rows;
            Assert.AreEqual(24, capacity);

            Assert.IsFalse(BackpackPressure.IsWarning(19, capacity));  // 0.79
            Assert.IsTrue(BackpackPressure.IsWarning(20, capacity));   // 0.83
        }
    }
}
