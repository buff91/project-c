using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class InventoryTests
    {
        [Test]
        public void Add_And_TryUse_TrackCounts()
        {
            var inventory = new Inventory();

            Assert.AreEqual(0, inventory.Count(ItemKind.Potion));
            Assert.AreEqual(1, inventory.Add(ItemKind.Potion));
            Assert.AreEqual(3, inventory.Add(ItemKind.Potion, 2));
            Assert.IsTrue(inventory.TryUse(ItemKind.Potion));
            Assert.AreEqual(2, inventory.Count(ItemKind.Potion));
        }

        [Test]
        public void TryUse_EmptyKind_FailsWithoutGoingNegative()
        {
            var inventory = new Inventory();

            Assert.IsFalse(inventory.TryUse(ItemKind.Bomb));
            Assert.AreEqual(0, inventory.Count(ItemKind.Bomb));
        }
    }

    public class HealTests
    {
        [Test]
        public void Heal_ClampsAtMaxHp_AndReturnsActualAmount()
        {
            var state = new CombatantState("hero", new GridPos(0, 0, 0), maxHp: 8, attackPower: 1);
            state.TakeDamage(5);

            Assert.AreEqual(3, state.Heal(3));
            Assert.AreEqual(6, state.Hp);
            Assert.AreEqual(2, state.Heal(99));
            Assert.AreEqual(state.MaxHp, state.Hp);
        }

        [Test]
        public void Heal_DeadCombatant_DoesNothing()
        {
            var state = new CombatantState("hero", new GridPos(0, 0, 0), maxHp: 3, attackPower: 1);
            state.TakeDamage(3);

            Assert.AreEqual(0, state.Heal(5));
            Assert.IsFalse(state.IsAlive);
        }
    }

    public class BombRulesTests
    {
        private static GridMap FlatMap(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void CanThrow_RequiresRangeElevationSightAndSolidTarget()
        {
            GridMap map = FlatMap(9);
            var from = new GridPos(1, 1, 0);

            Assert.IsTrue(BombRules.CanThrow(map, from, new GridPos(4, 1, 0), maxRange: 4));
            Assert.IsFalse(BombRules.CanThrow(map, from, new GridPos(7, 1, 0), maxRange: 4), "사거리 초과");
            Assert.IsFalse(BombRules.CanThrow(map, from, new GridPos(2, 1, 1), maxRange: 4), "다른 elevation");

            map.Set(new GridPos(2, 1, 0), TileKind.Wall);
            Assert.IsFalse(BombRules.CanThrow(map, from, new GridPos(4, 1, 0), maxRange: 4), "벽이 시야선 차단");

            map.Set(new GridPos(1, 3, 0), TileKind.Hole);
            Assert.IsFalse(BombRules.CanThrow(map, from, new GridPos(1, 3, 0), maxRange: 4), "구멍에는 못 던진다");
        }

        [Test]
        public void Detonate_DamagesEveryoneInThreeByThree_IncludingThrower()
        {
            GridMap map = FlatMap(9);
            var center = new GridPos(4, 4, 0);
            var thrower = new CombatantState("thrower", new GridPos(3, 3, 0), 10, 1);
            var inside = new CombatantState("inside", new GridPos(5, 5, 0), 10, 1);
            var outside = new CombatantState("outside", new GridPos(6, 4, 0), 10, 1);
            var above = new CombatantState("above", new GridPos(4, 4, 1), 10, 1);

            BombResult result = BombRules.Detonate(
                map, center, new[] { thrower, inside, outside, above }, damage: 3);

            Assert.AreEqual(7, thrower.Hp, "인접 대각(체비셰프 1)도 피해");
            Assert.AreEqual(7, inside.Hp);
            Assert.AreEqual(10, outside.Hp, "반경 밖은 무피해");
            Assert.AreEqual(10, above.Hp, "다른 elevation 은 무피해");
            Assert.AreEqual(2, result.Damaged.Count);
        }

        [Test]
        public void Detonate_CollapsesUnoccupiedWeakFloor_IntoHole()
        {
            GridMap map = FlatMap(9);
            var weak = new GridPos(4, 5, 0);
            map.Set(weak, TileKind.WeakFloor);

            BombResult result = BombRules.Detonate(map, new GridPos(4, 4, 0), null, damage: 3);

            Assert.AreEqual(TileKind.Hole, map.Get(weak).kind);
            CollectionAssert.Contains(result.CollapsedWeakFloors, weak);
        }

        [Test]
        public void Detonate_KeepsWeakFloor_UnderLivingCombatant()
        {
            GridMap map = FlatMap(9);
            var weak = new GridPos(4, 5, 0);
            map.Set(weak, TileKind.WeakFloor);
            var survivor = new CombatantState("survivor", weak, 10, 1);

            BombRules.Detonate(map, new GridPos(4, 4, 0), new[] { survivor }, damage: 3);

            Assert.IsTrue(survivor.IsAlive);
            Assert.AreEqual(TileKind.WeakFloor, map.Get(weak).kind, "산 자가 서 있으면 붕괴 보류 (M4 TryFall 통합 전)");
        }

        [Test]
        public void Detonate_CollapsesWeakFloor_WhenBlastKillsItsOccupant()
        {
            GridMap map = FlatMap(9);
            var weak = new GridPos(4, 5, 0);
            map.Set(weak, TileKind.WeakFloor);
            var victim = new CombatantState("victim", weak, maxHp: 2, attackPower: 1);

            BombResult result = BombRules.Detonate(map, new GridPos(4, 4, 0), new[] { victim }, damage: 3);

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(TileKind.Hole, map.Get(weak).kind, "폭사한 점유자는 붕괴를 막지 못한다");
            CollectionAssert.Contains(result.CollapsedWeakFloors, weak);
        }
    }

    public class OilRulesTests
    {
        private static GridMap FlatMap(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void Splash_CoversWalkable3x3_AndSkipsHolesAndWalls()
        {
            GridMap map = FlatMap(7);
            map.Set(new GridPos(3, 4, 0), TileKind.Wall);
            map.Set(new GridPos(4, 4, 0), TileKind.Hole);

            var splashed = OilRules.Splash(map, new GridPos(3, 3, 0));

            Assert.AreEqual(7, splashed.Count, "3×3 중 벽/구멍 2칸 제외");
            Assert.IsTrue(map.Get(new GridPos(3, 3, 0)).oiled);
            Assert.IsFalse(map.Get(new GridPos(4, 4, 0)).oiled, "구멍엔 기름이 고이지 않는다");
        }

        [Test]
        public void Splash_AlreadyOiledTile_IsNotReturnedTwice()
        {
            GridMap map = FlatMap(7);
            OilRules.Splash(map, new GridPos(3, 3, 0));

            var second = OilRules.Splash(map, new GridPos(3, 3, 0));

            Assert.AreEqual(0, second.Count);
        }

        [Test]
        public void Ignite_ClearsOilInBlast_AndReturnsIgnitedTiles()
        {
            GridMap map = FlatMap(7);
            OilRules.Splash(map, new GridPos(3, 3, 0));

            var ignited = OilRules.Ignite(map, new GridPos(2, 3, 0));

            Assert.Greater(ignited.Count, 0);
            foreach (GridPos pos in ignited)
                Assert.IsFalse(map.Get(pos).oiled, "발화한 기름은 소진된다");
            // 폭발 반경(2,3 기준 3×3) 밖의 기름은 남는다.
            Assert.IsTrue(map.Get(new GridPos(4, 3, 0)).oiled);
        }
    }

    public class ItemCatalogTests
    {
        [Test]
        public void GoldValue_PositiveOnlyForTreasures()
        {
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                if (ItemCatalog.IsTreasure(kind))
                    Assert.Greater(ItemCatalog.GoldValue(kind), 0, kind.ToString());
                else
                    Assert.AreEqual(0, ItemCatalog.GoldValue(kind), kind.ToString());
            }
            Assert.IsTrue(ItemCatalog.IsTreasure(ItemKind.CoinPouch));
            Assert.IsTrue(ItemCatalog.IsTreasure(ItemKind.Relic));
            Assert.IsFalse(ItemCatalog.IsTreasure(ItemKind.Potion));
        }

        [Test]
        public void FormatGold_UsesCompactCurrencySymbol_InsteadOfAmbiguousG()
        {
            Assert.AreEqual("$0", ItemCatalog.FormatGold(0));
            Assert.AreEqual("$15", ItemCatalog.FormatGold(15));
            StringAssert.DoesNotContain("G", ItemCatalog.FormatGold(15));
            StringAssert.DoesNotContain("골드", ItemCatalog.FormatGold(15));
        }

        [Test]
        public void MetaSaveData_StoresConsumables_ButNeverTreasures()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 2);
            meta.AddCount(ItemKind.CoinPouch, 5); // 전리품은 무시된다 — 항상 골드로 환산

            Assert.AreEqual(2, meta.GetCount(ItemKind.Potion));
            Assert.AreEqual(0, meta.GetCount(ItemKind.CoinPouch));

            meta.ClearItems();
            Assert.AreEqual(0, meta.GetCount(ItemKind.Potion));
        }

        [Test]
        public void ShopPrice_SellsConsumablesOnly_NeverTreasures()
        {
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                if (ItemCatalog.IsTreasure(kind))
                    Assert.AreEqual(0, ItemCatalog.ShopPrice(kind), $"{kind}: 전리품은 파밍 전용");
                else
                    Assert.Greater(ItemCatalog.ShopPrice(kind), 0, kind.ToString());
            }
        }

        [Test]
        public void MetaSaveData_TrySpend_And_HeroUnlock()
        {
            var meta = new MetaSaveData { gold = 100 };

            Assert.IsFalse(meta.TrySpend(150), "잔액 부족이면 차감하지 않는다");
            Assert.AreEqual(100, meta.gold);
            Assert.IsTrue(meta.TrySpend(80));
            Assert.AreEqual(20, meta.gold);

            Assert.IsTrue(meta.IsHeroUnlocked("knight"), "기사는 기본 해금");
            Assert.IsFalse(meta.IsHeroUnlocked("ranger"));
            meta.UnlockHero("ranger");
            meta.UnlockHero("ranger"); // 중복 해금은 무해
            Assert.IsTrue(meta.IsHeroUnlocked("ranger"));
            Assert.AreEqual(2, meta.unlockedHeroes.Length);
        }

        [Test]
        public void AllKinds_CoverEveryEnumValue_WithNameAndDescription()
        {
            var enumValues = System.Enum.GetValues(typeof(ItemKind));
            Assert.AreEqual(enumValues.Length, ItemCatalog.AllKinds.Length,
                "새 ItemKind 를 추가하면 ItemCatalog.AllKinds 에도 등록해야 한다");

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(ItemCatalog.DisplayName(kind)), kind.ToString());
                Assert.IsFalse(string.IsNullOrWhiteSpace(ItemCatalog.Description(kind)), kind.ToString());
            }
        }
    }
}
