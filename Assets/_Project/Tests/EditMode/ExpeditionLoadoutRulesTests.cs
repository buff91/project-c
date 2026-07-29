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

        /// <summary>
        /// Core는 여전히 1회분까지 옮길 수 있다 — 화면이 칸 단위로 부르는 것이지
        /// 규칙이 칸 아래를 못 다루는 것이 아니다(기본값이 1인 이유).
        /// </summary>
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

        /// <summary>
        /// 한 칸 분량 = 만충이거나, 창고에 그보다 적게 남았으면 그 전부.
        /// UI가 이 값을 그대로 옮기므로 여기가 곧 클릭 한 번의 뜻이다.
        /// </summary>
        [Test]
        public void UnitChargesInStash_IsAFullUnitOrWhateverIsLeft()
        {
            int per = ItemCatalog.ChargesPerItem(ItemKind.Potion);
            if (per <= 1) Assert.Ignore("충전 아이템이 없으면 검증할 것이 없다.");

            var meta = new MetaSaveData();
            Assert.AreEqual(
                0, ExpeditionLoadoutRules.UnitChargesInStash(meta, ItemKind.Potion),
                "창고가 비면 옮길 것이 없다");

            meta.AddCount(ItemKind.Potion, per - 1);
            Assert.AreEqual(
                per - 1, ExpeditionLoadoutRules.UnitChargesInStash(meta, ItemKind.Potion),
                "만충보다 적게 남았으면 남은 전부");

            meta.AddCount(ItemKind.Potion, per + 5);
            Assert.AreEqual(
                per, ExpeditionLoadoutRules.UnitChargesInStash(meta, ItemKind.Potion),
                "넉넉하면 딱 한 칸 분량");
        }

        /// <summary>
        /// 한 칸 분량을 옮기면 셀은 <b>정확히 하나</b>만 늘어난다 — 덜 찬 칸도 만충과 같은
        /// 셀을 먹으므로 1회분씩 옮기는 것은 클릭만 늘고 얻는 것이 없다.
        /// </summary>
        [Test]
        public void MoveToLoadout_MovesAWholeUnitForOneExtraFootprint()
        {
            ItemKind kind = ItemKind.Potion;
            int per = ItemCatalog.ChargesPerItem(kind);
            if (per <= 1) Assert.Ignore("충전 아이템이 없으면 검증할 것이 없다.");

            var meta = new MetaSaveData();
            meta.AddCount(kind, per * 3);
            int before = ExpeditionLoadoutRules.CreateLayout(meta).UsedCells;

            int unit = ExpeditionLoadoutRules.UnitChargesInStash(meta, kind);
            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, kind, unit));

            Assert.AreEqual(per, unit, "한 번에 만충 한 칸");
            Assert.AreEqual(per * 2, meta.GetCount(kind), "창고에서 한 칸 분량이 빠진다");
            Assert.AreEqual(per, meta.GetLoadoutCount(kind));
            Assert.AreEqual(
                before + BackpackRules.Footprint(kind).Area,
                ExpeditionLoadoutRules.CreateLayout(meta).UsedCells,
                "셀은 풋프린트 하나만 늘어난다");
        }

        /// <summary>
        /// <b>전부 아니면 전무.</b> 창고 잔량보다 많이 요청하면 아무것도 옮기지 않는다 —
        /// 부분 성공을 허용하면 화면은 "옮겼다"고 말하는데 합이 요청과 다른 상태가 생긴다.
        /// </summary>
        [Test]
        public void MoveToLoadout_RejectsWithoutMovingWhenStashHasTooFew()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 2);

            Assert.AreEqual(
                LoadoutTransferResult.MissingFromStash,
                ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.Potion, 3));
            Assert.AreEqual(2, meta.GetCount(ItemKind.Potion), "창고는 그대로다");
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.IsFalse(
                ExpeditionLoadoutRules.CanMoveToLoadout(meta, ItemKind.Potion, 3));
        }

        /// <summary>
        /// 되돌리기는 칸 하나의 잔여 충전을 옮긴다. UI가 들고 있던 값이 낡았어도
        /// <b>실제로 뺀 만큼만</b> 창고에 들어가야 한다 — 아니면 회분이 불어난다.
        /// </summary>
        [Test]
        public void MoveToStash_MovesTheWholeCellAndNeverInflatesCharges()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, 5);

            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToStash(meta, ItemKind.Potion, 2));
            Assert.AreEqual(3, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(2, meta.GetCount(ItemKind.Potion));

            // 낡은 값으로 과하게 요청해도 있는 만큼만 옮긴다.
            Assert.AreEqual(
                LoadoutTransferResult.Success,
                ExpeditionLoadoutRules.TryMoveToStash(meta, ItemKind.Potion, 99));
            Assert.AreEqual(0, meta.GetLoadoutCount(ItemKind.Potion));
            Assert.AreEqual(5, meta.GetCount(ItemKind.Potion), "총 회분은 보존된다");

            Assert.AreEqual(
                LoadoutTransferResult.MissingFromLoadout,
                ExpeditionLoadoutRules.TryMoveToStash(meta, ItemKind.Potion, 1));
        }

        [Test]
        public void Transfers_RejectNonPositiveCharges()
        {
            var meta = new MetaSaveData();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ExpeditionLoadoutRules.TryMoveToLoadout(meta, ItemKind.Potion, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ExpeditionLoadoutRules.TryMoveToStash(meta, ItemKind.Potion, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ExpeditionLoadoutRules.CanMoveToLoadout(meta, ItemKind.Potion, 0));
        }
    }
}
