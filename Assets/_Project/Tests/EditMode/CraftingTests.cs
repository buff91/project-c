using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class CraftingTests
    {
        [Test]
        public void TryFindRecipe_OrderIndependent()
        {
            Assert.IsTrue(CraftingRules.TryFindRecipe(ItemKind.Bomb, ItemKind.FrostShard, out Recipe forward));
            Assert.IsTrue(CraftingRules.TryFindRecipe(ItemKind.FrostShard, ItemKind.Bomb, out Recipe reversed));
            Assert.AreEqual(ItemKind.FrostBomb, forward.Output);
            Assert.AreEqual(ItemKind.FrostBomb, reversed.Output);
        }

        [Test]
        public void TryFindRecipe_UnknownPair_ReturnsFalse()
        {
            Assert.IsFalse(CraftingRules.TryFindRecipe(ItemKind.Potion, ItemKind.RecallScroll, out _));
        }

        [Test]
        public void PairRecipe_NeedsTwoOfSameKind()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Herb);
            Assert.IsTrue(CraftingRules.TryFindRecipe(ItemKind.Herb, ItemKind.Herb, out Recipe recipe));

            Assert.IsFalse(CraftingRules.CanCraft(inventory, recipe));

            inventory.Add(ItemKind.Herb);
            Assert.IsTrue(CraftingRules.CanCraft(inventory, recipe));
        }

        [Test]
        public void TryCraft_ConsumesIngredients_AddsOutput()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Herb, 3);
            CraftingRules.TryFindRecipe(ItemKind.Herb, ItemKind.Herb, out Recipe recipe);

            Assert.IsTrue(CraftingRules.TryCraft(inventory, recipe));
            Assert.AreEqual(1, inventory.Count(ItemKind.Herb));
            Assert.AreEqual(1, inventory.Count(ItemKind.Potion));
        }

        [Test]
        public void TryCraft_InsufficientMaterials_LeavesInventoryUntouched()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Bomb);
            CraftingRules.TryFindRecipe(ItemKind.Bomb, ItemKind.FrostShard, out Recipe recipe);

            Assert.IsFalse(CraftingRules.TryCraft(inventory, recipe));
            Assert.AreEqual(1, inventory.Count(ItemKind.Bomb));
            Assert.AreEqual(0, inventory.Count(ItemKind.FrostBomb));
        }

        [Test]
        public void MixedRecipe_ConsumesOneOfEach()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Bomb);
            inventory.Add(ItemKind.FrostShard);
            CraftingRules.TryFindRecipe(ItemKind.Bomb, ItemKind.FrostShard, out Recipe recipe);

            Assert.IsTrue(CraftingRules.TryCraft(inventory, recipe));
            Assert.AreEqual(0, inventory.Count(ItemKind.Bomb));
            Assert.AreEqual(0, inventory.Count(ItemKind.FrostShard));
            Assert.AreEqual(1, inventory.Count(ItemKind.FrostBomb));
        }

        [Test]
        public void CraftableRecipes_ListsOnlyAffordable()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.BlastPowder, 2);

            var craftable = CraftingRules.CraftableRecipes(inventory);

            Assert.AreEqual(1, craftable.Count);
            Assert.AreEqual(ItemKind.Bomb, craftable[0].Output);
        }

        [Test]
        public void Materials_AreMaterials_NotTreasure()
        {
            foreach (ItemKind kind in new[] { ItemKind.Herb, ItemKind.BlastPowder, ItemKind.FrostShard })
            {
                Assert.IsTrue(ItemCatalog.IsMaterial(kind), kind.ToString());
                Assert.IsFalse(ItemCatalog.IsTreasure(kind), kind.ToString());
            }
            Assert.IsFalse(ItemCatalog.IsMaterial(ItemKind.Potion));
        }

        [Test]
        public void MetaSaveData_StoresMaterials()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Herb, 2);
            meta.AddCount(ItemKind.FrostShard, 1);

            Assert.AreEqual(2, meta.GetCount(ItemKind.Herb));
            Assert.AreEqual(1, meta.GetCount(ItemKind.FrostShard));

            meta.ClearItems();
            Assert.AreEqual(0, meta.GetCount(ItemKind.Herb));
            Assert.AreEqual(0, meta.GetCount(ItemKind.FrostShard));
        }

        [Test]
        public void RecipeOutputs_AreExistingUsableItems()
        {
            foreach (Recipe recipe in CraftingRules.Recipes)
            {
                Assert.IsFalse(ItemCatalog.IsMaterial(recipe.Output), recipe.ToString());
                Assert.IsFalse(ItemCatalog.IsTreasure(recipe.Output), recipe.ToString());
            }
        }

        /// <summary>
        /// 에너지 셀은 전리품(코어 파편)을 태워야 나온다. 사격 자체는 기다리면 공짜로 차므로
        /// 셀이 사는 것은 <b>사격 횟수가 아니라 시간</b>이다 — 급할 때 즉시 만충시키는 값으로
        /// 생환 시 $25가 될 물건을 태우는 게 맞는지가 판단거리가 된다.
        /// </summary>
        [Test]
        public void EnergyCell_IsCraftedFromTreasureAndPowder()
        {
            Assert.IsTrue(
                CraftingRules.TryFindRecipe(
                    ItemKind.Gemstone, ItemKind.BlastPowder, out Recipe recipe));
            Assert.AreEqual(ItemKind.EnergyCell, recipe.Output);

            var inventory = new Inventory();
            inventory.Add(ItemKind.Gemstone);
            inventory.Add(ItemKind.BlastPowder);

            Assert.IsTrue(CraftingRules.TryCraft(inventory, recipe));
            Assert.AreEqual(0, inventory.Count(ItemKind.Gemstone));
            Assert.AreEqual(0, inventory.Count(ItemKind.BlastPowder));
            Assert.AreEqual(1, inventory.Count(ItemKind.EnergyCell));
            // 급속 충전재라 상시 휴대품이 아니다 — 칸당 회분을 적게 둔다.
            Assert.AreEqual(2, ItemCatalog.ChargesPerItem(ItemKind.EnergyCell));
        }
    }
}
