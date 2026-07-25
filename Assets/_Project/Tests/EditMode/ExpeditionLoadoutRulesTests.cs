using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class ExpeditionLoadoutRulesTests
    {
        [Test]
        public void MoveToLoadout_TransfersOneItemAndUsesRealFootprint()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.OilFlask, 2);

            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.OilFlask));
            Assert.AreEqual(1, meta.GetCount(ItemKind.OilFlask));
            Assert.AreEqual(1, meta.GetLoadoutCount(ItemKind.OilFlask));

            BackpackLayout layout = ExpeditionLoadoutRules.CreateLayout(meta);
            Assert.AreEqual(3, layout.UsedCells, "기사 기본 물약 1칸 + 기름 병 2칸");
        }

        [Test]
        public void MoveToLoadout_FullBackpackRejectsWithoutChangingStorage()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, BackpackRules.Capacity);
            meta.AddCount(ItemKind.RecallScroll, 1);

            Assert.AreEqual(
                LoadoutTransferResult.NoBackpackSpace,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.RecallScroll));
            Assert.AreEqual(1, meta.GetCount(ItemKind.RecallScroll));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.RecallScroll));
        }

        [Test]
        public void MoveToStash_ReturnsSelectedLoadoutItem()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.ThrowingKnife, 2);

            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToStash(meta, ItemKind.ThrowingKnife));
            Assert.AreEqual(1, meta.GetLoadoutCount(ItemKind.ThrowingKnife));
            Assert.AreEqual(1, meta.GetCount(ItemKind.ThrowingKnife));
        }

        [Test]
        public void Reconcile_StarterKitReturnsOverflowToStash()
        {
            // 백팩을 꽉 채워 두면 기본 지급품이 들어갈 자리가 없다 —
            // 넘치는 만큼은 삭제가 아니라 창고로 돌아가야 한다.
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, BackpackRules.Capacity);

            int returned = ExpeditionLoadoutRules.Reconcile(meta);

            Assert.AreEqual(
                SurvivorProfile.StartPotions, returned, "원정자 기본 지급품만큼 자리를 비운다");
            Assert.AreEqual(
                BackpackRules.Capacity - SurvivorProfile.StartPotions,
                meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(SurvivorProfile.StartPotions, meta.GetCount(ItemKind.Potion));
            Assert.AreEqual(
                BackpackRules.Capacity,
                ExpeditionLoadoutRules.CreateLayout(meta).UsedCells);
        }

        [Test]
        public void ConsumeLoadout_ClearsSelectionAndMovesItemsToRunInventory()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, 2);
            meta.AddLoadoutCount(ItemKind.RecallScroll, 1);
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(ItemKind.Potion); // 원정자 기본 지급품

            Assert.AreEqual(3, ExpeditionLoadoutRules.ConsumeLoadout(meta, inventory));
            Assert.AreEqual(3, inventory.Count(ItemKind.Potion));
            Assert.AreEqual(1, inventory.Count(ItemKind.RecallScroll));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.RecallScroll));
        }
    }
}
