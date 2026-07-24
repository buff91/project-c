using System;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>깊이 구간(밴드)별 콘텐츠 변주 튜닝 테이블과 그 소비처(적 혼합)를 고정한다.</summary>
    public class DungeonBandProfileTests
    {
        [TestCase(0, DungeonDepthBand.Shallow)]
        [TestCase(1, DungeonDepthBand.Shallow)]
        [TestCase(2, DungeonDepthBand.Shallow)]
        [TestCase(3, DungeonDepthBand.Mid)]
        [TestCase(5, DungeonDepthBand.Mid)]
        [TestCase(6, DungeonDepthBand.Deep)]
        [TestCase(8, DungeonDepthBand.Deep)]
        [TestCase(9, DungeonDepthBand.Boss)]
        [TestCase(12, DungeonDepthBand.Boss)]
        public void ForDepth_ResolvesBand_AndSharesInstanceWithRules(int depth, DungeonDepthBand expected)
        {
            Assert.AreEqual(expected, DungeonDepthBandRules.ForDepth(depth));
            Assert.AreSame(DungeonBandProfiles.ForBand(expected), DungeonBandProfiles.ForDepth(depth));
        }

        [Test]
        public void EveryBand_HasPositiveTotalWeight()
        {
            foreach (DungeonDepthBand band in Enum.GetValues(typeof(DungeonDepthBand)))
                Assert.Greater(DungeonBandProfiles.ForBand(band).TotalWeight, 0, band.ToString());
        }

        [Test]
        public void Shallow_HasNoSkeleton_DeeperBandsDo()
        {
            Assert.AreEqual(0, DungeonBandProfiles.ForBand(DungeonDepthBand.Shallow).SkeletonWeight);
            Assert.Greater(DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).SkeletonWeight, 0);
            Assert.Greater(DungeonBandProfiles.ForBand(DungeonDepthBand.Deep).SkeletonWeight, 0);
        }

        [Test]
        public void ExtraEnemies_NonDecreasingWithDepth()
        {
            int shallow = DungeonBandProfiles.ForBand(DungeonDepthBand.Shallow).ExtraEnemies;
            int mid = DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).ExtraEnemies;
            int deep = DungeonBandProfiles.ForBand(DungeonDepthBand.Deep).ExtraEnemies;
            Assert.AreEqual(0, shallow, "얕은 밴드는 기본 밀도");
            Assert.GreaterOrEqual(mid, shallow);
            Assert.GreaterOrEqual(deep, mid);
        }

        [Test]
        public void MonsterMix_SkeletonShareRisesWithDepth()
        {
            int shallow = CountSkeletons(depth: 0);
            int mid = CountSkeletons(depth: 3);
            int deep = CountSkeletons(depth: 6);

            Assert.AreEqual(0, shallow, "얕은 밴드엔 경비 드론(Skeleton)이 없다");
            Assert.Less(shallow, mid);
            Assert.Less(mid, deep, "깊을수록 경비 드론 비중이 커진다");
        }

        private static int CountSkeletons(int depth)
        {
            var random = new Random(12345);
            int count = 0;
            for (int i = 0; i < 2000; i++)
                if (MonsterRoster.PickForDepth(depth, random) == MonsterRoster.Skeleton)
                    count++;
            return count;
        }
    }
}
