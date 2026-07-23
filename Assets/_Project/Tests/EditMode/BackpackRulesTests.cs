using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class BackpackRulesTests
    {
        [Test]
        public void Footprints_DistinguishSmallTallAndLargeItems()
        {
            Assert.AreEqual(new ItemFootprint(1, 1), BackpackRules.Footprint(ItemKind.Potion));
            Assert.AreEqual(new ItemFootprint(1, 2), BackpackRules.Footprint(ItemKind.OilFlask));
            Assert.AreEqual(new ItemFootprint(1, 2), BackpackRules.Footprint(ItemKind.ThrowingKnife));
            Assert.AreEqual(new ItemFootprint(1, 2), BackpackRules.Footprint(ItemKind.RecallScroll));
            Assert.AreEqual(new ItemFootprint(2, 2), BackpackRules.Footprint(ItemKind.Relic));
        }

        [Test]
        public void Layout_PlacesEveryFootprintInsideGridWithoutOverlap()
        {
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(ItemKind.Relic, 2);
            inventory.Add(ItemKind.OilFlask, 3);
            inventory.Add(ItemKind.Potion, 4);

            BackpackLayout layout = inventory.CreateLayout();
            var occupied = new bool[layout.Columns, layout.Rows];
            int measuredCells = 0;

            foreach (BackpackPlacement placement in layout.Placements)
            {
                Assert.GreaterOrEqual(placement.X, 0);
                Assert.GreaterOrEqual(placement.Y, 0);
                Assert.LessOrEqual(placement.X + placement.Footprint.Width, layout.Columns);
                Assert.LessOrEqual(placement.Y + placement.Footprint.Height, layout.Rows);

                for (int dy = 0; dy < placement.Footprint.Height; dy++)
                for (int dx = 0; dx < placement.Footprint.Width; dx++)
                {
                    int x = placement.X + dx;
                    int y = placement.Y + dy;
                    Assert.IsFalse(occupied[x, y], $"겹친 셀: ({x},{y})");
                    occupied[x, y] = true;
                    measuredCells++;
                }
            }

            Assert.AreEqual(measuredCells, layout.UsedCells);
            Assert.AreEqual(18, layout.UsedCells);
        }

        [Test]
        public void BoundedInventory_RejectsItemWhenNoFootprintFits()
        {
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            Assert.AreEqual(6, inventory.AddUpTo(ItemKind.Relic, 7));
            Assert.AreEqual(BackpackRules.Capacity, inventory.CreateLayout().UsedCells);

            Assert.IsFalse(inventory.TryAdd(ItemKind.Potion, out int potionCount));
            Assert.AreEqual(0, potionCount);
            Assert.AreEqual(6, inventory.Count(ItemKind.Relic));
        }

        [Test]
        public void RemovingItem_ReleasesItsOccupiedCells()
        {
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(ItemKind.Relic, 6);

            Assert.IsTrue(inventory.TryUse(ItemKind.Relic));
            Assert.IsTrue(inventory.TryAdd(ItemKind.Potion, 4, out int potionCount));
            Assert.AreEqual(4, potionCount);
            Assert.AreEqual(BackpackRules.Capacity, inventory.CreateLayout().UsedCells);
        }

        [Test]
        public void FailedLargeCraft_RestoresConsumedIngredients()
        {
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(ItemKind.Potion, BackpackRules.Capacity);
            var oversizedRecipe = new Recipe(ItemKind.Potion, ItemKind.Potion, ItemKind.Relic);

            Assert.IsFalse(CraftingRules.TryCraft(inventory, oversizedRecipe));
            Assert.AreEqual(BackpackRules.Capacity, inventory.Count(ItemKind.Potion));
            Assert.AreEqual(0, inventory.Count(ItemKind.Relic));
        }

        [Test]
        public void MetaStorage_RemovesOnlyTransferredAmount()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.RecallScroll, 5);

            Assert.AreEqual(3, meta.RemoveCount(ItemKind.RecallScroll, 3));
            Assert.AreEqual(2, meta.GetCount(ItemKind.RecallScroll));
            Assert.AreEqual(2, meta.RemoveCount(ItemKind.RecallScroll, 99));
            Assert.AreEqual(0, meta.GetCount(ItemKind.RecallScroll));
        }
    }
}
