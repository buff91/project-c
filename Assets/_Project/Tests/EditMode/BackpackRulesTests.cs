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

            // 칸수는 충전에서 파생된다. 숫자를 박아 두면 칸당 충전을 조정할 때마다 썩는다.
            int expectedCells =
                ChargeUnits.UnitsFor(ItemKind.Relic, 2) * BackpackRules.Footprint(ItemKind.Relic).Area +
                ChargeUnits.UnitsFor(ItemKind.OilFlask, 3) * BackpackRules.Footprint(ItemKind.OilFlask).Area +
                ChargeUnits.UnitsFor(ItemKind.Potion, 4) * BackpackRules.Footprint(ItemKind.Potion).Area;
            Assert.AreEqual(expectedCells, layout.UsedCells);
        }

        /// <summary>
        /// 투척 볼트는 <b>충전과 다칸 풋프린트가 동시에 걸리는 유일한 종류</b>다.
        /// 칸수는 `ceil(충전 / 칸당)`이고 셀 수는 거기에 풋프린트 면적을 곱한 값이라,
        /// 둘 중 하나만 보면 조용히 틀린다(충전만 보면 3자루가 2셀, 면적만 보면 6셀).
        /// </summary>
        [Test]
        public void Layout_MultipliesChargeUnitsByFootprintArea()
        {
            ItemKind knife = ItemKind.ThrowingKnife;
            int per = ItemCatalog.ChargesPerItem(knife);
            int area = BackpackRules.Footprint(knife).Area;
            Assert.Greater(per, 1, "이 테스트는 충전이 있는 다칸 아이템을 전제한다");
            Assert.Greater(area, 1, "이 테스트는 다칸 풋프린트를 전제한다");

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(knife, per);

            BackpackLayout full = inventory.CreateLayout();
            Assert.AreEqual(area, full.UsedCells, "만충 한 칸은 풋프린트 하나만 먹는다");
            Assert.AreEqual(1, CountPlacements(full, knife));

            inventory.Add(knife, 1); // 만충을 하나 넘긴다
            BackpackLayout spilled = inventory.CreateLayout();
            Assert.AreEqual(area * 2, spilled.UsedCells, "새 칸은 풋프린트 하나를 통째로 먹는다");
            Assert.AreEqual(2, CountPlacements(spilled, knife));
        }

        private static int CountPlacements(BackpackLayout layout, ItemKind kind)
        {
            int count = 0;
            foreach (BackpackPlacement placement in layout.Placements)
                if (placement.Kind == kind) count++;
            return count;
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

            int expectedCells =
                ChargeUnits.UnitsFor(ItemKind.Relic, 5) * BackpackRules.Footprint(ItemKind.Relic).Area +
                ChargeUnits.UnitsFor(ItemKind.Potion, 4) * BackpackRules.Footprint(ItemKind.Potion).Area;
            Assert.AreEqual(expectedCells, inventory.CreateLayout().UsedCells);
        }

        [Test]
        public void FailedLargeCraft_RestoresConsumedIngredients()
        {
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            // 백팩을 **칸으로** 꽉 채운다. 회분 단위로 Capacity 만큼 넣으면 칸당 충전이
            // 1보다 클 때 절반만 차서 이 테스트가 거부 경로를 아예 안 밟는다.
            int fullPotionCharges =
                BackpackRules.Capacity * ItemCatalog.ChargesPerItem(ItemKind.Potion);
            inventory.Add(ItemKind.Potion, fullPotionCharges);
            var oversizedRecipe = new Recipe(ItemKind.Potion, ItemKind.Potion, ItemKind.Relic);

            Assert.IsFalse(CraftingRules.TryCraft(inventory, oversizedRecipe));
            Assert.AreEqual(fullPotionCharges, inventory.Count(ItemKind.Potion));
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

        /// <summary>
        /// 칸수 파생 = ceil(충전 / 칸당). 칸당 1인 종류는 충전이 곧 칸수라
        /// 기존 동작이 그대로 성립한다.
        /// </summary>
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(3, 3)]
        [TestCase(-2, 0)]
        public void UnitsFor_IsChargeCountWhenItemHoldsOneCharge(int charges, int expected)
        {
            // Relic 은 전리품이라 칸당 충전이 영구히 1이다(안전 예산).
            Assert.AreEqual(expected, ChargeUnits.UnitsFor(ItemKind.Relic, charges));
        }

        /// <summary>마지막 칸만 덜 찰 수 있다 — 나머지는 전부 만충이다.</summary>
        [Test]
        public void ChargesInUnit_FillsEveryUnitButTheLast()
        {
            ItemKind kind = ItemKind.Potion;
            int per = ItemCatalog.ChargesPerItem(kind);

            for (int charges = 1; charges <= per * 4; charges++)
            {
                int units = ChargeUnits.UnitsFor(kind, charges);
                int total = 0;
                for (int unit = 0; unit < units; unit++)
                {
                    int inUnit = ChargeUnits.ChargesInUnit(kind, charges, unit);
                    Assert.Greater(inUnit, 0, $"충전 {charges}, 칸 {unit}");
                    Assert.LessOrEqual(inUnit, per, $"충전 {charges}, 칸 {unit}");
                    if (unit < units - 1)
                        Assert.AreEqual(per, inUnit, $"충전 {charges}: 마지막이 아닌 칸은 만충");
                    total += inUnit;
                }

                Assert.AreEqual(charges, total, $"충전 {charges}: 칸별 합이 총 충전과 달라졌다");
            }
        }

        [Test]
        public void ChargesInUnit_IsZeroOutsideRange()
        {
            Assert.AreEqual(0, ChargeUnits.ChargesInUnit(ItemKind.Potion, 3, -1));
            Assert.AreEqual(0, ChargeUnits.ChargesInUnit(ItemKind.Potion, 3, 99));
            Assert.AreEqual(0, ChargeUnits.ChargesInUnit(ItemKind.Potion, 0, 0));
        }

        /// <summary>
        /// 레이아웃이 실은 충전 합이 인벤토리 총 충전과 같아야 하고,
        /// <b>덜 찬 칸은 종류당 최대 하나</b>여야 한다. 정렬 tie-break 가
        /// InstanceIndex 오름차순이라 그 칸이 마지막에 놓이는 것도 함께 확인한다.
        /// </summary>
        [Test]
        public void Layout_CarriesChargesAndLeavesAtMostOnePartialUnitPerKind()
        {
            var inventory = new Inventory(
                InventoryPanelControllerColumns, InventoryPanelControllerRows);
            inventory.Add(ItemKind.Potion, 3);
            inventory.Add(ItemKind.Bomb, 2);

            BackpackLayout layout = inventory.CreateLayout();

            foreach (ItemKind kind in new[] { ItemKind.Potion, ItemKind.Bomb })
            {
                int per = ItemCatalog.ChargesPerItem(kind);
                int sum = 0;
                int partials = 0;
                int maxPartialIndex = -1;
                int maxIndex = -1;
                foreach (BackpackPlacement placement in layout.Placements)
                {
                    if (placement.Kind != kind) continue;
                    sum += placement.Charges;
                    maxIndex = System.Math.Max(maxIndex, placement.InstanceIndex);
                    if (placement.Charges >= per) continue;
                    partials++;
                    maxPartialIndex = placement.InstanceIndex;
                }

                Assert.AreEqual(inventory.Count(kind), sum, $"{kind} 충전 합");
                Assert.LessOrEqual(partials, 1, $"{kind} 부분 칸은 하나뿐이어야 한다");
                if (partials == 1)
                    Assert.AreEqual(
                        maxIndex, maxPartialIndex, $"{kind} 부분 칸이 마지막이 아니다");
            }
        }

        private const int InventoryPanelControllerColumns = BackpackRules.Columns;
        private const int InventoryPanelControllerRows = BackpackRules.Rows;
    }
}
