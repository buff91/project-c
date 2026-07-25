using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Tests
{
    /// <summary>
    /// 아이템 저장 구조의 계약. 예전에는 세이브 클래스마다 int 필드와 switch 가 있어서
    /// 종류를 하나 늘릴 때 여섯 군데를 고쳐야 했고, 한 곳만 빠뜨려도 아이템이 조용히 사라졌다.
    /// **ItemKind 에 값을 더하는 것만으로 저장·복원이 되는지**를 여기서 고정한다.
    /// </summary>
    public class ItemStorageTests
    {
        [Test]
        public void EveryItemKind_SurvivesRunSaveRoundTrip()
        {
            var inventory = new Inventory();
            foreach (ItemKind kind in ItemCatalog.AllKinds)
                inventory.Add(kind, 2);

            var save = new RunSaveData();
            save.WriteItems(inventory);
            RunSaveData restored = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(save));

            var rebuilt = new Inventory();
            restored.AddItemsTo(rebuilt);

            foreach (ItemKind kind in ItemCatalog.AllKinds)
                Assert.AreEqual(2, rebuilt.Count(kind), $"{kind} 이(가) 이월에서 누락됐다");
        }

        [Test]
        public void EveryStorableKind_SurvivesMetaSaveRoundTrip()
        {
            var meta = new MetaSaveData();
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                meta.AddCount(kind, 3);
                meta.AddLoadoutCount(kind, 1);
            }

            MetaSaveData restored = JsonUtility.FromJson<MetaSaveData>(JsonUtility.ToJson(meta));

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int expected = ItemCatalog.IsTreasure(kind) ? 0 : 3;
                Assert.AreEqual(expected, restored.GetCount(kind), $"창고 {kind}");
                Assert.AreEqual(1, restored.GetLoadoutCount(kind), $"로드아웃 {kind}");
            }
        }

        [Test]
        public void Treasure_IsNeverStashed_BecauseItAlwaysBecomesGold()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Relic, 5);

            Assert.AreEqual(0, meta.GetCount(ItemKind.Relic),
                "전리품이 창고에 남으면 생환 환금과 이중 계산이 된다");
        }

        [Test]
        public void Add_RemovesEmptyStacks_AndNeverGoesNegative()
        {
            var stacks = new List<ItemStack>();
            ItemStorage.Add(stacks, ItemKind.Potion, 2);
            Assert.AreEqual(1, stacks.Count);

            ItemStorage.Add(stacks, ItemKind.Potion, -5);
            Assert.AreEqual(0, stacks.Count, "0 이하가 된 칸은 저장에 남기지 않는다");
            Assert.AreEqual(0, ItemStorage.Count(stacks, ItemKind.Potion));
        }

        [Test]
        public void Remove_ReturnsActualAmount_NotRequested()
        {
            var stacks = new List<ItemStack>();
            ItemStorage.Add(stacks, ItemKind.Bomb, 2);

            Assert.AreEqual(2, ItemStorage.Remove(stacks, ItemKind.Bomb, 5));
            Assert.AreEqual(0, ItemStorage.Count(stacks, ItemKind.Bomb));
        }

        [Test]
        public void CategoryOf_CoversEveryKind_AndAgreesWithTheOldPredicates()
        {
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                ItemCategory category = ItemCatalog.CategoryOf(kind);

                Assert.AreEqual(category == ItemCategory.Treasure, ItemCatalog.IsTreasure(kind), $"{kind}");
                Assert.AreEqual(category == ItemCategory.Material, ItemCatalog.IsMaterial(kind), $"{kind}");
                Assert.AreEqual(
                    category == ItemCategory.Equipment,
                    EquipmentCatalog.IsEquipment(kind),
                    $"{kind} — 장비 분류는 EquipmentCatalog 에서만 파생돼야 한다");
                Assert.AreEqual(category == ItemCategory.Consumable, ItemCatalog.IsUsable(kind), $"{kind}");
            }
        }

        [Test]
        public void AllKinds_ListsEveryEnumValue()
        {
            // AllKinds 에 빠진 종류는 창고·백팩·세이브 어디에서도 보이지 않는다.
            foreach (ItemKind kind in System.Enum.GetValues(typeof(ItemKind)))
                CollectionAssert.Contains(ItemCatalog.AllKinds, kind, $"{kind} 이(가) AllKinds 에 없다");
        }
    }
}
