using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// <b>(지역 × 깊이)</b> 콘텐츠 변주 튜닝 테이블과 그 소비처(적 혼합)를 고정한다.
    /// <para>
    /// 두 종류의 테스트가 있다. ① <b>지역 불변식</b> — 어떤 지역을 추가해도 지켜야 하는 성질
    /// (도입 구간엔 원거리·드론·캐치워크가 없다, 깊어질수록 어둡고 밀도가 는다). 새 지역이
    /// 늘어도 자동으로 검사 대상이 된다. ② <b>기준 지역 골든</b> — 폐병원의 실제 수치.
    /// 지역 축을 도입하며 기존 던전이 바뀌지 않았음을 값으로 못박는다.
    /// </para>
    /// </summary>
    public class DungeonBandProfileTests
    {
        private static readonly DungeonDepthBand[] Bands =
        {
            DungeonDepthBand.Shallow, DungeonDepthBand.Mid,
            DungeonDepthBand.Deep, DungeonDepthBand.Boss
        };

        private static IEnumerable<DungeonRegionProfile> Regions =>
            (DungeonRegionProfile[])Enum.GetValues(typeof(DungeonRegionProfile));

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

            // 밴드 경계는 지역과 무관하다 — 지역은 그 밴드의 *값*만 가른다.
            foreach (DungeonRegionProfile region in Regions)
                Assert.AreSame(
                    DungeonBandProfiles.ForBand(region, expected),
                    DungeonBandProfiles.ForDepth(region, depth),
                    region.ToString());
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

        // ── 지역 불변식 (새 지역이 늘어도 자동으로 검사된다) ──────────────────

        [Test]
        public void EveryRegionAndBand_HasPositiveTotalWeight()
        {
            foreach (DungeonRegionProfile region in Regions)
            foreach (DungeonDepthBand band in Bands)
                Assert.Greater(
                    DungeonBandProfiles.ForBand(region, band).TotalWeight, 0,
                    $"{region}/{band} — 0이면 적 롤이 예외를 던진다");
        }

        [Test]
        public void EveryRegion_IntroducesThreatsOnlyAfterTheOpeningBand()
        {
            // 도입 구간의 계약: 단단한 적도, 원거리 압박도, 높이도 아직 없다.
            // 지역이 정체성을 주더라도 첫 세 층의 학습 곡선은 공통이어야 한다.
            foreach (DungeonRegionProfile region in Regions)
            {
                DungeonBandProfile shallow =
                    DungeonBandProfiles.ForBand(region, DungeonDepthBand.Shallow);
                DungeonBandProfile mid =
                    DungeonBandProfiles.ForBand(region, DungeonDepthBand.Mid);

                Assert.AreEqual(0, shallow.SkeletonWeight, $"{region} — 도입 구간엔 경비 드론이 없다");
                Assert.AreEqual(0, shallow.SlingerWeight, $"{region} — 도입 구간엔 원거리 압박이 없다");
                Assert.AreEqual(0, shallow.CatwalkLength, $"{region} — 도입 구간은 평평하게 둔다");
                Assert.AreEqual(0, shallow.ExtraEnemies, $"{region} — 도입 구간은 기본 밀도");

                Assert.Greater(mid.SkeletonWeight, 0, $"{region} — 중반엔 단단한 적이 등장한다");
                Assert.Greater(mid.SlingerWeight, 0, $"{region} — 중반엔 원거리 압박이 등장한다");
            }
        }

        [Test]
        public void EveryRegion_GetsDarkerDenserAndTallerWithDepth()
        {
            // 깊이의 *방향*은 지역과 무관하다 — 지역은 기준선만 옮긴다.
            // 잿불 성채가 전반적으로 밝아도 그 안에서 깊어질수록 어두워져야 읽힌다.
            foreach (DungeonRegionProfile region in Regions)
            {
                for (int i = 1; i < Bands.Length; i++)
                {
                    DungeonBandProfile prev = DungeonBandProfiles.ForBand(region, Bands[i - 1]);
                    DungeonBandProfile cur = DungeonBandProfiles.ForBand(region, Bands[i]);
                    string where = $"{region} {Bands[i - 1]}→{Bands[i]}";

                    Assert.GreaterOrEqual(cur.WallSconceRarity, prev.WallSconceRarity, $"{where} 광원");
                    Assert.GreaterOrEqual(cur.ExtraEnemies, prev.ExtraEnemies, $"{where} 밀도");
                    Assert.GreaterOrEqual(cur.CatwalkLength, prev.CatwalkLength, $"{where} 높이");
                    Assert.GreaterOrEqual(cur.BranchChancePercent, prev.BranchChancePercent, $"{where} 분기");
                }

                foreach (DungeonDepthBand band in Bands)
                {
                    DungeonBandProfile profile = DungeonBandProfiles.ForBand(region, band);
                    Assert.Greater(profile.WallSconceRarity, 0,
                        $"{region}/{band} — 0이면 모든 칸이 등잔이 된다(나눗셈 보호)");
                    Assert.That(profile.PuddleChancePercent, Is.InRange(0, 100), $"{region}/{band}");
                    Assert.That(profile.BranchChancePercent, Is.InRange(0, 100), $"{region}/{band}");
                }
            }
        }

        [Test]
        public void Regions_ActuallyDiffer_OtherwiseTheAxisIsCeremony()
        {
            // 지역 축의 존재 이유는 "다른 던전이 다르게 느껴진다"이다. 표가 전부 같은 값이면
            // 배관만 늘고 얻는 것이 없다 — 정체성 다이얼이 실제로 갈라져 있는지 본다.
            var puddles = new HashSet<int>();
            var sconces = new HashSet<int>();
            foreach (DungeonRegionProfile region in Regions)
            {
                DungeonBandProfile deep = DungeonBandProfiles.ForBand(region, DungeonDepthBand.Deep);
                puddles.Add(deep.PuddleChancePercent);
                sconces.Add(deep.WallSconceRarity);
            }

            Assert.AreEqual(3, puddles.Count, "세 지역의 웅덩이 확률이 서로 달라야 한다");
            Assert.AreEqual(3, sconces.Count, "세 지역의 광원 밀도가 서로 달라야 한다");
        }

        [Test]
        public void FloodedIsWettestAndEmberIsDriest()
        {
            // 반응 무대가 지역 정체성의 핵심이다: 침수된 금고는 빙결·감전의 무대가 넓어야 하고,
            // 잿불 성채는 물이 드물어야 불 연쇄가 선다.
            foreach (DungeonDepthBand band in Bands)
            {
                int flooded = DungeonBandProfiles
                    .ForBand(DungeonRegionProfile.Flooded, band).PuddleChancePercent;
                int facility = DungeonBandProfiles
                    .ForBand(DungeonRegionProfile.Facility, band).PuddleChancePercent;
                int ember = DungeonBandProfiles
                    .ForBand(DungeonRegionProfile.Ember, band).PuddleChancePercent;

                Assert.Greater(flooded, facility, $"{band} — 침수된 금고가 기준보다 젖어 있어야 한다");
                Assert.Less(ember, facility, $"{band} — 잿불 성채가 기준보다 말라 있어야 한다");
            }
        }

        [Test]
        public void EveryCatalogDungeon_ResolvesToAProfile()
        {
            // 던전을 추가하며 지역을 빠뜨리면 조용히 기준 지역이 된다 — 목록으로 대조해 둔다.
            Assert.AreEqual(
                DungeonRegionProfile.Facility,
                DungeonCatalog.ById(DungeonCatalog.DefaultId).Region, "폐병원 = 기계·시설");
            Assert.AreEqual(
                DungeonRegionProfile.Flooded,
                DungeonCatalog.ById("flooded-vault").Region, "침수된 금고 = 침수·냉각");
            Assert.AreEqual(
                DungeonRegionProfile.Ember,
                DungeonCatalog.ById("ember-keep").Region, "잿불 성채 = 불·기름");

            foreach (DungeonDefinition dungeon in DungeonCatalog.All)
                Assert.Greater(
                    DungeonBandProfiles.ForDepth(dungeon.Region, 0).TotalWeight, 0, dungeon.Id);
        }

        // ── 기준 지역(폐병원) 골든 ─────────────────────────────────────────────

        [TestCase(DungeonDepthBand.Shallow, 50, 50, 0, 0, 0, 50, 50, 0, 5)]
        [TestCase(DungeonDepthBand.Mid, 15, 40, 30, 15, 1, 60, 50, 1, 6)]
        [TestCase(DungeonDepthBand.Deep, 5, 35, 40, 20, 1, 70, 60, 2, 8)]
        [TestCase(DungeonDepthBand.Boss, 5, 35, 40, 20, 1, 70, 60, 2, 9)]
        public void Facility_KeepsTheTunedValues(
            DungeonDepthBand band, int slime, int goblin, int skeleton, int slinger,
            int extraEnemies, int branch, int puddle, int catwalk, int sconce)
        {
            // 지역 축 도입이 폐병원의 밸런스를 건드리지 않았음을 값으로 못박는다.
            // (배치가 안 바뀌었다는 증명은 DungeonGeneratorGoldenTests 의 지문이 맡는다.)
            DungeonBandProfile profile =
                DungeonBandProfiles.ForBand(DungeonRegionProfile.Facility, band);

            Assert.AreEqual(slime, profile.SlimeWeight, "SlimeWeight");
            Assert.AreEqual(goblin, profile.GoblinWeight, "GoblinWeight");
            Assert.AreEqual(skeleton, profile.SkeletonWeight, "SkeletonWeight");
            Assert.AreEqual(slinger, profile.SlingerWeight, "SlingerWeight");
            Assert.AreEqual(extraEnemies, profile.ExtraEnemies, "ExtraEnemies");
            Assert.AreEqual(branch, profile.BranchChancePercent, "BranchChancePercent");
            Assert.AreEqual(puddle, profile.PuddleChancePercent, "PuddleChancePercent");
            Assert.AreEqual(catwalk, profile.CatwalkLength, "CatwalkLength");
            Assert.AreEqual(sconce, profile.WallSconceRarity, "WallSconceRarity");
        }

        // ── 소비처: 적 혼합 롤 ────────────────────────────────────────────────

        [Test]
        public void PickForDepth_CoversEveryWeightedArchetype_InEveryRegion()
        {
            // 롤 분기(4-way)가 가중치와 어긋나면 특정 종이 영영 안 나온다 — 실제로 뽑아 확인한다.
            foreach (DungeonRegionProfile region in Regions)
            {
                var random = new Random(4242);
                var seen = new HashSet<string>();
                for (int i = 0; i < 3000; i++)
                    seen.Add(MonsterRoster.PickForDepth(region, 6, random).Id);

                CollectionAssert.AreEquivalent(
                    new[] { "Slime", "Goblin", "Skeleton", "Slinger" }, seen,
                    $"{region} — 후반 밴드는 네 종을 모두 낸다");
            }
        }

        [Test]
        public void MonsterMix_SkeletonShareRisesWithDepth_InEveryRegion()
        {
            foreach (DungeonRegionProfile region in Regions)
            {
                int shallow = CountSkeletons(region, depth: 0);
                int mid = CountSkeletons(region, depth: 3);
                int deep = CountSkeletons(region, depth: 6);

                Assert.AreEqual(0, shallow, $"{region} — 도입 구간엔 경비 드론이 없다");
                Assert.Less(shallow, mid, region.ToString());
                Assert.Less(mid, deep, $"{region} — 깊을수록 경비 드론 비중이 커진다");
            }
        }

        private static int CountSkeletons(DungeonRegionProfile region, int depth)
        {
            var random = new Random(12345);
            int count = 0;
            for (int i = 0; i < 2000; i++)
                if (MonsterRoster.PickForDepth(region, depth, random) == MonsterRoster.Skeleton)
                    count++;
            return count;
        }
    }
}
