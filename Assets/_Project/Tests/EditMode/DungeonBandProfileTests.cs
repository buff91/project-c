using System;
using System.Collections.Generic;
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

        [TestCase(DungeonDepthBand.Shallow, "1~3번째")]
        [TestCase(DungeonDepthBand.Mid, "4~6번째")]
        [TestCase(DungeonDepthBand.Deep, "7~9번째")]
        [TestCase(DungeonDepthBand.Boss, "10번째+")]
        public void RangeLabel_MatchesBoundaries(DungeonDepthBand band, string expected)
        {
            Assert.AreEqual(expected, DungeonDepthBandRules.RangeLabel(band));
        }

        [Test]
        public void RangeLabel_AgreesWithForDepth_AcrossTheFirstDungeon()
        {
            // 라벨과 판정이 각자 하드코딩되면 리포트가 조용히 거짓말을 한다 — 층마다 대조한다.
            for (int depth = 0; depth <= 11; depth++)
            {
                DungeonDepthBand band = DungeonDepthBandRules.ForDepth(depth);
                string label = DungeonDepthBandRules.RangeLabel(band);
                string floor = RunTelemetry.FormatFloor(-depth);
                bool openEnded = label.EndsWith("+", StringComparison.Ordinal);
                string[] bounds = label.Split('~');
                int firstFloor = LeadingNumber(bounds[0]);
                int lastFloor = openEnded ? int.MaxValue : LeadingNumber(bounds[1]);
                int floorNumber = LeadingNumber(floor.Substring(1));

                Assert.GreaterOrEqual(floorNumber, firstFloor, $"{floor} ∉ {label}");
                Assert.LessOrEqual(floorNumber, lastFloor, $"{floor} ∉ {label}");
            }
        }

        /// <summary>
        /// 앞머리의 숫자만 읽는다. 라벨 표기가 바뀌어도(옛 `B1~B3` → 방향 중립 `1~3번째`)
        /// 경계 대조가 계속 돌게 하려는 것이다 — 고정 오프셋 파싱은 표기 변경에 조용히 깨진다.
        /// </summary>
        private static int LeadingNumber(string text)
        {
            int length = 0;
            while (length < text.Length && char.IsDigit(text[length])) length++;
            Assert.Greater(length, 0, $"숫자로 시작하지 않는다: '{text}'");
            return int.Parse(text.Substring(0, length));
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
        public void SlingerWeight_AbsentInShallow_ThenGrowsWithDepth()
        {
            Assert.AreEqual(0, DungeonBandProfiles.ForBand(DungeonDepthBand.Shallow).SlingerWeight,
                "도입 구간에는 원거리 압박을 넣지 않는다");
            Assert.Greater(DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).SlingerWeight, 0);
            Assert.GreaterOrEqual(
                DungeonBandProfiles.ForBand(DungeonDepthBand.Deep).SlingerWeight,
                DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).SlingerWeight);
        }

        [Test]
        public void PickForDepth_CoversEveryWeightedArchetype()
        {
            // 롤 분기(4-way)가 가중치와 어긋나면 특정 종이 영영 안 나온다 — 실제로 뽑아 확인한다.
            var random = new Random(4242);
            var seen = new HashSet<string>();
            for (int i = 0; i < 3000; i++)
                seen.Add(MonsterRoster.PickForDepth(6, random).Id);

            CollectionAssert.AreEquivalent(
                new[] { "Slime", "Goblin", "Skeleton", "Slinger" }, seen,
                "Deep 밴드는 네 종을 모두 낸다");
        }

        [Test]
        public void CatwalkLength_GrowsWithDepth_ShallowHasNone()
        {
            Assert.AreEqual(0, DungeonBandProfiles.ForBand(DungeonDepthBand.Shallow).CatwalkLength,
                "도입 구간은 평평하게 둔다");
            Assert.GreaterOrEqual(
                DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).CatwalkLength, 1);
            Assert.GreaterOrEqual(
                DungeonBandProfiles.ForBand(DungeonDepthBand.Deep).CatwalkLength,
                DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).CatwalkLength);
        }

        [Test]
        public void WallSconceRarity_GrowsWithDepth_SoDeeperFloorsAreDarker()
        {
            int shallow = DungeonBandProfiles.ForBand(DungeonDepthBand.Shallow).WallSconceRarity;
            int mid = DungeonBandProfiles.ForBand(DungeonDepthBand.Mid).WallSconceRarity;
            int deep = DungeonBandProfiles.ForBand(DungeonDepthBand.Deep).WallSconceRarity;
            int boss = DungeonBandProfiles.ForBand(DungeonDepthBand.Boss).WallSconceRarity;

            Assert.Greater(shallow, 0, "0이면 모든 칸이 등잔이 된다(나눗셈 보호)");
            Assert.GreaterOrEqual(mid, shallow);
            Assert.GreaterOrEqual(deep, mid);
            Assert.GreaterOrEqual(boss, deep);
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
