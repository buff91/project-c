using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 배고픔은 탐색을 죽이지 않는 부드러운 시계여야 한다 — 즉사 없이 신호를 주고,
    /// 통조림 한두 개로 관리되며, 층·던전이 바뀌어도 이어진다.
    /// </summary>
    public class HungerRulesTests
    {
        [Test]
        public void Stages_MoveFedToHungryToStarving()
        {
            Assert.AreEqual(HungerStage.Fed, HungerRules.StageFor(HungerRules.MaxSatiation));
            Assert.AreEqual(HungerStage.Fed, HungerRules.StageFor(HungerRules.HungryThreshold));
            Assert.AreEqual(HungerStage.Hungry, HungerRules.StageFor(HungerRules.HungryThreshold - 1));
            Assert.AreEqual(HungerStage.Hungry, HungerRules.StageFor(1));
            Assert.AreEqual(HungerStage.Starving, HungerRules.StageFor(0));
        }

        [Test]
        public void Tick_DrainsWithoutDamage_WhileFoodRemains()
        {
            var hunger = new HungerState();
            int damage = 0;
            for (int i = 0; i < HungerRules.MaxSatiation; i++)
                damage += hunger.Tick();

            Assert.AreEqual(0, damage, "배가 남아 있는 동안에는 피해가 없다");
            Assert.AreEqual(0, hunger.satiation);
            Assert.AreEqual(HungerStage.Starving, hunger.Stage);
        }

        [Test]
        public void Starving_DamagesOnInterval_NotEveryTurn()
        {
            var hunger = new HungerState { satiation = 0 };

            int hits = 0;
            int totalDamage = 0;
            for (int i = 0; i < HungerRules.StarvingDamageInterval * 3; i++)
            {
                int damage = hunger.Tick();
                if (damage <= 0) continue;
                hits++;
                totalDamage += damage;
            }

            Assert.AreEqual(3, hits, "주기마다 한 번씩만 깎는다");
            Assert.AreEqual(3 * HungerRules.StarvingDamage, totalDamage);
        }

        [Test]
        public void Feed_RefillsAndClearsStarvation_WithoutOverflow()
        {
            var hunger = new HungerState { satiation = 0, starvingTurns = 5 };

            int fed = hunger.Feed(HungerRules.RationSatiation);

            Assert.AreEqual(HungerRules.RationSatiation, fed);
            Assert.AreEqual(0, hunger.starvingTurns, "먹으면 굶주림 카운터가 풀린다");
            Assert.AreNotEqual(HungerStage.Starving, hunger.Stage);

            hunger.satiation = HungerRules.MaxSatiation;
            Assert.AreEqual(0, hunger.Feed(HungerRules.RationSatiation), "가득 차면 낭비된다");
            Assert.AreEqual(HungerRules.MaxSatiation, hunger.satiation);
        }

        [Test]
        public void OneRation_CoversALargeSliceOfTheRun_ButNotAllOfIt()
        {
            // 통조림이 판 전체를 덮으면 압박이 사라지고, 너무 적으면 굶주림이 기본값이 된다.
            Assert.Less(HungerRules.RationSatiation, HungerRules.MaxSatiation,
                "한 통이 배를 통째로 채우진 않는다");
            Assert.Greater(HungerRules.RationSatiation, HungerRules.MaxSatiation / 4,
                "한 통이 의미 없을 만큼 적지도 않다");
        }

        [Test]
        public void Clone_CopiesState_ForCheckpoints()
        {
            var hunger = new HungerState { satiation = 42, starvingTurns = 3 };
            HungerState copy = hunger.Clone();
            hunger.Tick();

            Assert.AreEqual(42, copy.satiation, "체크포인트 사본은 이후 변화에 영향받지 않는다");
            Assert.AreEqual(3, copy.starvingTurns);
        }

        [Test]
        public void CannedFood_IsAConsumable_SoldAndSpawned()
        {
            Assert.IsFalse(ItemCatalog.IsTreasure(ItemKind.CannedFood));
            Assert.IsFalse(ItemCatalog.IsMaterial(ItemKind.CannedFood));
            Assert.Greater(ItemCatalog.ShopPrice(ItemKind.CannedFood), 0, "상점에서 살 수 있어야 한다");
        }

        /// <summary>
        /// 통조림 한 칸은 여러 회분을 담는다. 배고픔이 판 전체를 관통하는 상시 압박이라
        /// 1회분 = 1칸이면 식량이 백팩을 <b>상시 점유</b>하고, 그 자리는 전부 파밍(기둥 ④)에서
        /// 빠져나간다. 세금은 판돈(회복)보다 촘촘히 쌓이므로 물약보다 작을 이유가 없다.
        /// </summary>
        [Test]
        public void CannedFood_StacksCharges_SoUpkeepDoesNotEatTheBackpack()
        {
            Assert.IsTrue(ItemCatalog.IsCharged(ItemKind.CannedFood),
                "통조림은 칸당 여러 회분을 담는다");
            Assert.GreaterOrEqual(
                ItemCatalog.ChargesPerItem(ItemKind.CannedFood),
                ItemCatalog.ChargesPerItem(ItemKind.Potion),
                "상시 세금인 식량이 판돈인 회복보다 촘촘할 이유는 없다");

            int per = ItemCatalog.ChargesPerItem(ItemKind.CannedFood);
            Assert.AreEqual(1, ChargeUnits.UnitsFor(ItemKind.CannedFood, per));
            Assert.AreEqual(2, ChargeUnits.UnitsFor(ItemKind.CannedFood, per + 1),
                "만충을 넘기면 새 칸이 열린다");
        }

        /// <summary>
        /// <b>칸 압박 완화이지 배부름 버프가 아니다.</b> 획득도 소비도 여전히 1회분 단위라
        /// 골드당·습득당 배부름이 정확히 보존된다 — 바뀌는 것은 회분이 차지하는 칸 수뿐이다.
        /// 만충 습득으로 바꾸면 층당 식량 공급이 칸당 충전만큼 배가 된다.
        /// </summary>
        [Test]
        public void CannedFood_IsAcquiredAndEatenOneRationAtATime()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.CannedFood, 1); // 상점 1회 구매
            Assert.AreEqual(1, meta.GetCount(ItemKind.CannedFood), "구매 한 번 = 1회분");

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            Assert.IsTrue(inventory.TryAdd(ItemKind.CannedFood, out _)); // 바닥 습득
            Assert.AreEqual(1, inventory.Count(ItemKind.CannedFood), "습득 한 번 = 1회분");

            int per = ItemCatalog.ChargesPerItem(ItemKind.CannedFood);
            inventory.Add(ItemKind.CannedFood, per - 1); // 한 칸을 만충으로
            for (int eaten = 0; eaten < per; eaten++)
                Assert.IsTrue(inventory.TryUse(ItemKind.CannedFood), $"{eaten + 1}번째 식사");

            Assert.AreEqual(0, inventory.Count(ItemKind.CannedFood));
            Assert.IsFalse(inventory.TryUse(ItemKind.CannedFood), "빈 칸에서는 못 먹는다");
        }

        [Test]
        public void Generator_SpawnsFood_AcrossTheFirstDungeon()
        {
            int food = 0;
            for (int seed = 0; seed < 12; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);
                foreach (DungeonFloorInfo floor in layout.Floors)
                foreach (ItemSpawn spawn in floor.Items)
                    if (spawn.Kind == ItemKind.CannedFood) food++;
            }

            Assert.Greater(food, 0, "던전에서 식량이 실제로 나온다 — 굶주림에 답이 있어야 한다");
        }
    }
}
