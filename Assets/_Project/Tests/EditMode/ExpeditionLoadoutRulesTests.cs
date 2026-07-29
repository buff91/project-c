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
            // 칸으로 꽉 채운다(회분 단위로 Capacity 는 칸당 충전이 1보다 크면 절반만 찬다).
            meta.AddLoadoutCount(
                ItemKind.Potion,
                BackpackRules.Capacity * ItemCatalog.ChargesPerItem(ItemKind.Potion));
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
            int fullCharges =
                BackpackRules.Capacity * ItemCatalog.ChargesPerItem(ItemKind.Potion);
            meta.AddLoadoutCount(ItemKind.Potion, fullCharges);

            int returned = ExpeditionLoadoutRules.Reconcile(meta);

            // 기본 지급품은 회분 단위다 — 만충 한 병이 곧 한 칸이라 부분 칸이 안 생긴다.
            int starter = ExpeditionLoadoutRules.StarterCount(ItemKind.Potion);
            Assert.AreEqual(starter, returned, "원정자 기본 지급품만큼 자리를 비운다");
            Assert.AreEqual(fullCharges - starter, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(starter, meta.GetCount(ItemKind.Potion));
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
        /// <summary>
        /// 기본 지급품은 <b>만충 단위</b>여야 한다. 부분 칸으로 지급하면 그 칸에 플레이어가
        /// 넣은 회분이 섞이고, 출정 준비의 "기본 지급품" 잠금 배지가 칸 단위라 그 칸 전체가
        /// 잠겨 플레이어 소유분을 창고로 되돌릴 수 없게 된다.
        /// </summary>
        [Test]
        public void StarterCount_IsAlwaysAWholeNumberOfUnits()
        {
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int starter = ExpeditionLoadoutRules.StarterCount(kind);
                if (starter <= 0) continue;
                Assert.AreEqual(
                    0, starter % ItemCatalog.ChargesPerItem(kind),
                    $"{kind} 기본 지급품이 부분 칸으로 떨어진다");
            }
        }

        /// <summary>창고 ↔ 로드아웃 이동은 1회분 단위다 — "몇 회분 챙길까"가 이 기능의 요점이다.</summary>
        [Test]
        public void MoveToLoadout_MovesASingleCharge()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 4);

            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.Potion));

            Assert.AreEqual(3, meta.GetCount(ItemKind.Potion), "창고에서 1회분만 빠진다");
            Assert.AreEqual(1, meta.GetLoadoutCount(ItemKind.Potion));
        }

        /// <summary>
        /// 이 변경의 <b>핵심 신규 동작</b>: 칸이 꽉 찼어도 부분 칸이 있으면 그 칸에
        /// 회분을 더 넣을 수 있다. 예전 모델에선 무조건 거부됐다.
        /// </summary>
        [Test]
        public void MoveToLoadout_FillsAPartialUnitEvenWhenEveryCellIsTaken()
        {
            int per = ItemCatalog.ChargesPerItem(ItemKind.Potion);
            if (per <= 1) Assert.Ignore("충전 아이템이 없으면 검증할 것이 없다.");

            var meta = new MetaSaveData();
            // 마지막 칸만 1회분 비워 둔 채 백팩을 칸으로 가득 채운다.
            // CreateInventory 가 기본 지급품을 **먼저** 넣으므로 그만큼 빼야 한다 —
            // 안 빼면 지급품이 그 1회분 여유를 먹어서 진짜로 꽉 찬다.
            int starter = ExpeditionLoadoutRules.StarterCount(ItemKind.Potion);
            meta.AddLoadoutCount(
                ItemKind.Potion, BackpackRules.Capacity * per - 1 - starter);
            meta.AddCount(ItemKind.Potion, 1);

            Assert.AreEqual(
                BackpackRules.Capacity,
                ExpeditionLoadoutRules.CreateLayout(meta).UsedCells,
                "칸은 이미 전부 찼다");
            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.Potion),
                "부분 칸이 남아 있으면 칸이 꽉 차도 받아들여야 한다");
        }

    }
}
