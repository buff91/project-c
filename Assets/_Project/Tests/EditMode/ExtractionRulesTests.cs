using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 중간 탈출구: 구간마다 물러설 기회를 주되 아무 데서나 나가지는 못하게 한다.
    /// 배고픔과 짝을 이루는 결정("다음 탈출구까지 버틸 식량이 있나")의 골격이다.
    /// </summary>
    public class ExtractionRulesTests
    {
        [TestCase(0, 10, false)] // 입구 층 — 들어가자마자 나가는 문은 두지 않는다
        [TestCase(2, 10, false)]
        [TestCase(3, 10, true)]  // B4
        [TestCase(4, 10, false)]
        [TestCase(6, 10, false)]
        [TestCase(7, 10, true)]  // B8
        [TestCase(8, 10, false)]
        [TestCase(9, 10, false)] // 최심층은 보스를 잡고 나가는 것이 유일한 길이다
        public void HasExtractionPoint_OnlyOnB4AndB8(int depth, int floorCount, bool expected)
        {
            Assert.AreEqual(expected, ExtractionRules.HasExtractionPoint(depth, floorCount));
        }

        [Test]
        public void ExtractionPoints_AreExactlyTwo_InTheFirstDungeon()
        {
            int count = 0;
            for (int depth = 0; depth < 10; depth++)
                if (ExtractionRules.HasExtractionPoint(depth, 10)) count++;

            Assert.AreEqual(2, count, "B4·B8 두 곳뿐이어야 구간이 판돈이 된다");
        }

        [Test]
        public void ShortDungeons_HaveNoMidExtraction()
        {
            for (int depth = 0; depth < 3; depth++)
            {
                Assert.IsFalse(ExtractionRules.HasExtractionPoint(depth, 1));
                Assert.IsFalse(ExtractionRules.HasExtractionPoint(depth, 3));
            }
        }

        [Test]
        public void FloorsToNextExtraction_CountsForward_AndReportsNone()
        {
            Assert.AreEqual(3, ExtractionRules.FloorsToNextExtraction(0, 10), "B1 → B4");
            Assert.AreEqual(4, ExtractionRules.FloorsToNextExtraction(3, 10), "B4 → B8");
            Assert.AreEqual(-1, ExtractionRules.FloorsToNextExtraction(7, 10),
                "B8 아래로는 중간 탈출구가 없다 — 보스를 잡아야 나간다");
        }

        [Test]
        public void Generator_PlacesExtractionPoint_OnEveryExtractionFloor()
        {
            for (int seed = 0; seed < 16; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);

                // 도달성은 문을 다 연 상태로 본다 — `ProceduralDungeonTests`의 도달성 검사와 같은 전제다.
                // (플레이어는 문을 열고 지나가지만 `FindPath`는 기본적으로 닫힌 문을 막는다.)
                foreach (DungeonFloorInfo floor in layout.Floors)
                foreach (GridPos door in floor.Doors)
                    map.Set(door, TileKind.DoorOpen);

                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    int depth = -floor.FloorIndex;
                    bool expected = ExtractionRules.HasExtractionPoint(depth, layout.Floors.Count);
                    Assert.AreEqual(expected, floor.ExtractionPoint.HasValue,
                        $"seed {seed} depth {depth}: 탈출구 유무가 규칙과 다르다");

                    if (!floor.ExtractionPoint.HasValue) continue;

                    GridPos point = floor.ExtractionPoint.Value;
                    Assert.AreEqual(TileKind.Floor, map.Get(point)?.kind, "탈출구는 걷는 바닥 위에 선다");
                    Assert.AreNotEqual(floor.Entry, point, "입구 칸을 덮지 않는다");
                    Assert.IsFalse(floor.EnemySpawns.Contains(point), "적 스폰과 겹치지 않는다");
                    foreach (ItemSpawn spawn in floor.Items)
                        Assert.AreNotEqual(spawn.Position, point, "아이템과 겹치지 않는다");
                    Assert.Greater(
                        GridPathfinder.FindPath(map, floor.Entry, point).Count, 0,
                        $"seed {seed}: 탈출구는 도달 가능해야 한다");
                }
            }
        }

        [Test]
        public void SecretRewards_YieldBeaconsRarely_NotAsStandardLoot()
        {
            int secrets = 0;
            int beacons = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);
                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    if (!floor.SecretReward.HasValue) continue;
                    secrets++;
                    foreach (ItemSpawn spawn in floor.Items)
                        if (spawn.Position == floor.SecretReward.Value &&
                            spawn.Kind == ItemKind.ExtractionBeacon)
                            beacons++;
                }
            }

            Assert.Greater(secrets, 0, "표본이 있어야 확률을 말할 수 있다");
            Assert.Less(beacons * 2, secrets, "송출기는 숨은 방 보상의 예외여야 한다(절반 미만)");
        }

        [Test]
        public void Beacon_IsAConsumableEscape_SoldExpensively()
        {
            Assert.IsFalse(ItemCatalog.IsTreasure(ItemKind.ExtractionBeacon));
            Assert.IsFalse(ItemCatalog.IsMaterial(ItemKind.ExtractionBeacon));
            Assert.Greater(
                ItemCatalog.ShopPrice(ItemKind.ExtractionBeacon),
                ItemCatalog.ShopPrice(ItemKind.CannedFood),
                "살아 나갈 권리는 식량보다 비싸다");
        }
    }
}
