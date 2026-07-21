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
    }
}
