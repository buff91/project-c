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
            HeroArchetype hero = HeroRoster.ById("knight");

            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, hero, ItemKind.OilFlask));
            Assert.AreEqual(1, meta.GetCount(ItemKind.OilFlask));
            Assert.AreEqual(1, meta.GetLoadoutCount(ItemKind.OilFlask));

            BackpackLayout layout = ExpeditionLoadoutRules.CreateLayout(meta, hero);
            Assert.AreEqual(3, layout.UsedCells, "기사 기본 물약 1칸 + 기름 병 2칸");
        }

        [Test]
        public void MoveToLoadout_FullBackpackRejectsWithoutChangingStorage()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, BackpackRules.Capacity);
            meta.AddCount(ItemKind.RecallScroll, 1);
            HeroArchetype hero = HeroRoster.ById("ranger");

            Assert.AreEqual(
                LoadoutTransferResult.NoBackpackSpace,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, hero, ItemKind.RecallScroll));
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
        public void Reconcile_HeroStarterKitReturnsOverflowToStash()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, BackpackRules.Capacity);
            HeroArchetype alchemist = HeroRoster.ById("alchemist");

            int returned = ExpeditionLoadoutRules.Reconcile(meta, alchemist);

            Assert.AreEqual(3, returned, "연금술사 기본 폭탄 3칸을 확보한다");
            Assert.AreEqual(21, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(3, meta.GetCount(ItemKind.Potion));
            Assert.AreEqual(
                BackpackRules.Capacity,
                ExpeditionLoadoutRules.CreateLayout(meta, alchemist).UsedCells);
        }

        [Test]
        public void ConsumeLoadout_ClearsSelectionAndMovesItemsToRunInventory()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, 2);
            meta.AddLoadoutCount(ItemKind.RecallScroll, 1);
            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            inventory.Add(ItemKind.Potion); // 기사 기본 지급품

            Assert.AreEqual(3, ExpeditionLoadoutRules.ConsumeLoadout(meta, inventory));
            Assert.AreEqual(3, inventory.Count(ItemKind.Potion));
            Assert.AreEqual(1, inventory.Count(ItemKind.RecallScroll));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.RecallScroll));
        }
    }
}
