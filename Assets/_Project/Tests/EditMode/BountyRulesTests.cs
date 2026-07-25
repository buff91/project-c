using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class BountyRulesTests
    {
        [Test]
        public void SelectOffers_SameSeed_IsDeterministic()
        {
            List<BountyDefinition> a = BountyRules.SelectOffers(1234);
            List<BountyDefinition> b = BountyRules.SelectOffers(1234);

            CollectionAssert.AreEqual(a.Select(x => x.Id).ToArray(), b.Select(x => x.Id).ToArray());
        }

        [Test]
        public void SelectOffers_ReturnsRequestedCountWithoutDuplicates()
        {
            List<BountyDefinition> offers = BountyRules.SelectOffers(7, BountyRules.OfferCount);

            Assert.AreEqual(BountyRules.OfferCount, offers.Count);
            Assert.AreEqual(offers.Count, offers.Select(x => x.Id).Distinct().Count(), "의뢰가 중복되면 안 된다.");
        }

        [Test]
        public void SelectOffers_CountAbovePool_ClampsToPoolSize()
        {
            List<BountyDefinition> offers = BountyRules.SelectOffers(3, BountyRules.Pool.Count + 5);
            Assert.AreEqual(BountyRules.Pool.Count, offers.Count);
        }

        [Test]
        public void ReadMetric_DeepestDepth_ReadsProgressIndex_NotElevationSign()
        {
            // 진행 지수를 그대로 읽는다. 예전에는 -deepestFloorIndex 로 역산했는데,
            // 상승 던전(폐병원)은 층 인덱스가 양수라 값이 음수가 되어 의뢰가 영원히 미완이었다.
            // "진행 지수 ≠ 고도"는 docs/STATUS.md·GDD §5.1 의 규약이다.
            var descending = new RunTelemetry { deepestFloorIndex = -4, deepestProgressIndex = 4 };
            Assert.AreEqual(4, BountyRules.ReadMetric(BountyMetric.DeepestDepth, descending));

            var ascending = new RunTelemetry { deepestFloorIndex = 4, deepestProgressIndex = 4 };
            Assert.AreEqual(
                4,
                BountyRules.ReadMetric(BountyMetric.DeepestDepth, ascending),
                "방향이 달라도 같은 진행이면 같은 값이어야 한다.");
        }

        [Test]
        public void IsComplete_UsesTargetThreshold()
        {
            BountyDefinition cull = BountyRules.ById("cull"); // Kills 12
            Assert.IsFalse(BountyRules.IsComplete(cull, new RunTelemetry { kills = 11 }));
            Assert.IsTrue(BountyRules.IsComplete(cull, new RunTelemetry { kills = 12 }));
            Assert.IsTrue(BountyRules.IsComplete(cull, new RunTelemetry { kills = 20 }));
        }

        [Test]
        public void AssignOffers_WritesActiveIdsToMeta()
        {
            var meta = new MetaSaveData();
            List<BountyDefinition> offers = BountyRules.AssignOffers(meta, 42);

            Assert.IsTrue(BountyRules.HasActiveBounties(meta));
            CollectionAssert.AreEqual(
                offers.Select(x => x.Id).ToArray(),
                meta.activeBountyIds);
        }

        [Test]
        public void Settle_PaysCompletedBountiesAndClearsActiveList()
        {
            var meta = new MetaSaveData { gold = 0 };
            meta.activeBountyIds = new[] { "cull", "descent" }; // Kills 12 (+40), DeepestDepth 4 (+50)

            // 처치 12 달성, 최심층은 B3(깊이 2) 까지만 → cull 완료, descent 미완료.
            var telemetry = new RunTelemetry { kills = 12, deepestFloorIndex = -2 };
            BountyClaimResult result = BountyRules.Settle(meta, telemetry);

            Assert.AreEqual(1, result.CompletedCount);
            Assert.AreEqual(40, result.TotalReward);
            Assert.AreEqual(40, meta.gold, "완료분 보상만 지급된다.");
            Assert.IsFalse(BountyRules.HasActiveBounties(meta), "정산 후 활성 의뢰는 비워진다.");

            BountyClaim cull = result.Claims.Single(c => c.Bounty.Id == "cull");
            Assert.IsTrue(cull.Completed);
            BountyClaim descent = result.Claims.Single(c => c.Bounty.Id == "descent");
            Assert.IsFalse(descent.Completed);
            Assert.AreEqual(0, descent.RewardGold);
        }

        [Test]
        public void Settle_UnknownActiveId_IsIgnored()
        {
            var meta = new MetaSaveData { gold = 5 };
            meta.activeBountyIds = new[] { "does-not-exist" };

            BountyClaimResult result = BountyRules.Settle(meta, new RunTelemetry());

            Assert.AreEqual(0, result.Claims.Count);
            Assert.AreEqual(5, meta.gold);
            Assert.IsFalse(BountyRules.HasActiveBounties(meta));
        }
    }
}
