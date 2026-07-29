using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 등잔 판정이 하나뿐이라는 것을 지키는 테스트. 아트와 빛이 각자 해시를 갖던 시절엔
    /// 두 집합이 우연 말고는 겹치지 않았고, 아무도 그걸 알아채지 못했다.
    /// </summary>
    public sealed class SconcePlacementTests
    {
        [Test]
        public void IsSconce_IsDeterministicForSameInputs()
        {
            for (int x = -8; x <= 8; x++)
            for (int y = -8; y <= 8; y++)
                Assert.AreEqual(
                    SconcePlacement.IsSconce(x, y, 1337, 7),
                    SconcePlacement.IsSconce(x, y, 1337, 7),
                    $"({x},{y})");
        }

        [Test]
        public void IsSconce_DependsOnSeed()
        {
            int a = 0, b = 0;
            for (int x = 0; x < 40; x++)
            for (int y = 0; y < 40; y++)
            {
                if (SconcePlacement.IsSconce(x, y, 1, 7)) a++;
                if (SconcePlacement.IsSconce(x, y, 999, 7)) b++;
            }

            // 같은 희소도면 개수는 비슷하되 배치는 달라야 한다 — 시드가 살아 있다는 뜻이다.
            Assert.Greater(a, 0);
            Assert.Greater(b, 0);
            bool anyDifferent = false;
            for (int x = 0; x < 40 && !anyDifferent; x++)
            for (int y = 0; y < 40 && !anyDifferent; y++)
                if (SconcePlacement.IsSconce(x, y, 1, 7) !=
                    SconcePlacement.IsSconce(x, y, 999, 7))
                    anyDifferent = true;
            Assert.IsTrue(anyDifferent, "시드가 배치를 바꾸지 못한다.");
        }

        /// <summary>
        /// 판정에 시점 인자가 아예 없다는 것을 계약으로 못박는다. 예전 아트 해시는
        /// <c>viewQuarterTurns</c>를 포함해서 시점을 돌리면 램프가 순간이동했다 —
        /// 공간이 시점에 따라 바뀌면 기둥 ①이 거짓말이 된다.
        /// </summary>
        [Test]
        public void IsSconce_HasNoViewDependentParameter()
        {
            var parameters = typeof(SconcePlacement)
                .GetMethod(nameof(SconcePlacement.IsSconce))
                .GetParameters();

            Assert.AreEqual(4, parameters.Length);
            foreach (var parameter in parameters)
                StringAssert.DoesNotContain(
                    "view", parameter.Name.ToLowerInvariant(),
                    "등잔 자리는 시점에 의존하면 안 된다.");
        }

        [Test]
        public void IsSconce_RarityOneLightsEveryEdgeTile()
        {
            for (int x = 0; x < 20; x++)
            for (int y = 0; y < 20; y++)
                Assert.IsTrue(SconcePlacement.IsSconce(x, y, 42, 1), $"({x},{y})");
        }

        [Test]
        public void IsSconce_GetsRarerAsRarityGrows()
        {
            int dense = 0, sparse = 0;
            for (int x = 0; x < 60; x++)
            for (int y = 0; y < 60; y++)
            {
                if (SconcePlacement.IsSconce(x, y, 7, 3)) dense++;
                if (SconcePlacement.IsSconce(x, y, 7, 12)) sparse++;
            }

            Assert.Greater(dense, sparse, "희소도가 커져도 등잔이 드물어지지 않는다.");
        }

        /// <summary>0으로 나누지 않는다 — 밴드 프로필이 0을 줄 수 있다.</summary>
        [Test]
        public void IsSconce_TreatsNonPositiveRarityAsNoSconce()
        {
            Assert.IsFalse(SconcePlacement.IsSconce(3, 4, 11, 0));
            Assert.IsFalse(SconcePlacement.IsSconce(3, 4, 11, -5));
        }

        /// <summary>
        /// 음수 좌표에서도 안전해야 한다. 해시가 음수가 되면 <c>%</c> 결과도 음수라
        /// 절대 참이 되지 않는 사각지대가 생긴다 — 마스크로 막고 있는지 확인한다.
        /// </summary>
        [Test]
        public void IsSconce_WorksOnNegativeCoordinates()
        {
            int hits = 0;
            for (int x = -60; x < 0; x++)
            for (int y = -60; y < 0; y++)
                if (SconcePlacement.IsSconce(x, y, 5, 6)) hits++;

            Assert.Greater(hits, 0, "음수 좌표에서 등잔이 하나도 안 걸린다.");
        }
    }
}
