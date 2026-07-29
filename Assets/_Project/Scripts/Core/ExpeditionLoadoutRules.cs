using System;

namespace ProjectC.Core
{
    public enum LoadoutTransferResult
    {
        Success = 0,
        MissingFromStash = 1,
        MissingFromLoadout = 2,
        NoBackpackSpace = 3,
        UnsupportedItem = 4
    }

    /// <summary>
    /// 허브 창고와 출정 백팩 사이의 이동 규칙.
    /// 기본 지급품(<see cref="SurvivorProfile"/>)도 같은 6×4 용량에 포함해
    /// 실제 던전 백팩과 결과를 맞춘다.
    ///
    /// <para>
    /// <b>이동량은 충전(회분) 단위로 받고, 화면은 그것을 「칸」으로 부른다.</b>
    /// 규칙은 1회분까지 옮길 수 있지만(기본값 1) 출정 준비 화면은 한 칸 분량
    /// (<see cref="UnitChargesInStash"/> / 선택한 칸의 잔여)을 넘긴다 — 덜 찬 칸도
    /// 백팩 셀은 만충과 똑같이 먹으므로 1회분씩 옮기면 클릭만 늘고 얻는 것이 없다.
    /// </para>
    /// </summary>
    public static class ExpeditionLoadoutRules
    {
        public static Inventory CreateInventory(MetaSaveData meta)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            AddStarterKit(inventory);
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = meta.GetLoadoutCount(kind);
                if (count > 0) inventory.AddUpTo(kind, count);
            }
            return inventory;
        }

        public static BackpackLayout CreateLayout(MetaSaveData meta) =>
            CreateInventory(meta).CreateLayout();

        public static int StarterCount(ItemKind kind) => SurvivorProfile.StarterCount(kind);

        /// <summary>
        /// 창고에서 한 번에 반입하는 <b>한 칸</b> 분량 — 만충이거나, 창고에 그보다 적게
        /// 남았으면 그 전부다. 0이면 옮길 것이 없다.
        ///
        /// <para>
        /// 이동 단위가 칸인 이유: <b>덜 찬 칸도 백팩 셀은 만충과 똑같이 먹는다</b>
        /// (<c>ceil(충전 / 칸당)</c>). 1회분씩 옮기면 같은 셀을 쓰면서 회분만 적게
        /// 들고 가는 셈이라 클릭 수만 늘고 얻는 것이 없다.
        /// </para>
        /// </summary>
        public static int UnitChargesInStash(MetaSaveData meta, ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            int available = meta.GetCount(kind);
            if (available <= 0) return 0;
            int per = ItemCatalog.ChargesPerItem(kind);
            return available < per ? available : per;
        }

        public static bool CanMoveToLoadout(
            MetaSaveData meta,
            ItemKind kind,
            int charges = 1)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (charges <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
            if (ItemCatalog.IsTreasure(kind) || meta.GetCount(kind) < charges) return false;
            Inventory inventory = CreateInventory(meta);
            return inventory.TryAdd(kind, charges, out _);
        }

        /// <summary>
        /// 창고 → 출정 백팩. <b>전부 아니면 전무</b>다 — 부분 성공을 허용하면 화면이
        /// "옮겼다"고 말하는데 창고와 백팩의 합이 요청과 다른 상태가 조용히 생긴다.
        /// 한 칸 분량(<see cref="UnitChargesInStash"/>)은 언제나 셀 하나만 더 먹으므로
        /// 실패는 "칸이 없다" 하나뿐이다.
        /// </summary>
        public static LoadoutTransferResult TryMoveToLoadout(
            MetaSaveData meta,
            ItemKind kind,
            int charges = 1)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (charges <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
            if (ItemCatalog.IsTreasure(kind)) return LoadoutTransferResult.UnsupportedItem;
            if (meta.GetCount(kind) < charges) return LoadoutTransferResult.MissingFromStash;

            Inventory inventory = CreateInventory(meta);
            if (!inventory.TryAdd(kind, charges, out _))
                return LoadoutTransferResult.NoBackpackSpace;

            meta.RemoveCount(kind, charges);
            meta.AddLoadoutCount(kind, charges);
            return LoadoutTransferResult.Success;
        }

        /// <summary>
        /// 출정 백팩 → 창고. 창고에는 <b>실제로 뺀 만큼만</b> 넣는다 — UI가 들고 있던
        /// 칸 잔여가 낡았을 때(그 사이 재배치) 요청량을 그대로 더하면 회분이 불어난다.
        /// </summary>
        public static LoadoutTransferResult TryMoveToStash(
            MetaSaveData meta,
            ItemKind kind,
            int charges = 1)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (charges <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
            if (ItemCatalog.IsTreasure(kind)) return LoadoutTransferResult.UnsupportedItem;

            int removed = meta.RemoveLoadoutCount(kind, charges);
            if (removed <= 0) return LoadoutTransferResult.MissingFromLoadout;

            meta.AddCount(kind, removed);
            return LoadoutTransferResult.Success;
        }

        /// <summary>
        /// 구버전 저장이나 백팩 규격 변경으로 현재 구성이 넘치면 들어가는 만큼만 유지하고
        /// 나머지는 창고로 돌려보낸다. 반환값은 되돌린 아이템 개수다.
        /// </summary>
        public static int Reconcile(MetaSaveData meta)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            AddStarterKit(inventory);
            var accepted = new int[ItemCatalog.AllKinds.Length];
            int returned = 0;

            for (int i = 0; i < ItemCatalog.AllKinds.Length; i++)
            {
                ItemKind kind = ItemCatalog.AllKinds[i];
                int requested = meta.GetLoadoutCount(kind);
                if (requested <= 0) continue;
                accepted[i] = inventory.AddUpTo(kind, requested);
                int overflow = requested - accepted[i];
                if (overflow <= 0) continue;
                meta.AddCount(kind, overflow);
                returned += overflow;
            }

            meta.ClearLoadout();
            for (int i = 0; i < ItemCatalog.AllKinds.Length; i++)
            {
                if (accepted[i] > 0)
                    meta.AddLoadoutCount(ItemCatalog.AllKinds[i], accepted[i]);
            }
            return returned;
        }

        /// <summary>
        /// 출발 시 선택 물품을 런 인벤토리로 옮긴다. 예외적인 초과분은 창고에 보존한다.
        /// destination에는 호출 전에 원정자 기본 지급품(<c>SurvivorProfile</c>)이 들어 있어야 한다.
        /// </summary>
        public static int ConsumeLoadout(MetaSaveData meta, Inventory destination)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            int moved = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int requested = meta.GetLoadoutCount(kind);
                if (requested <= 0) continue;
                int accepted = destination.AddUpTo(kind, requested);
                moved += accepted;
                int overflow = requested - accepted;
                if (overflow > 0) meta.AddCount(kind, overflow);
            }
            meta.ClearLoadout();
            return moved;
        }

        /// <summary>
        /// 모든 원정자가 같은 것을 들고 나간다 — 지급품은 <see cref="SurvivorProfile"/> 한 곳에 있다.
        /// 종류를 늘릴 때 여기와 <see cref="SurvivorProfile.StarterCount"/>가 어긋나면
        /// 출정 준비 화면의 잠금 표시와 실제 반입량이 갈리므로, 목록을 두 벌 만들지 않는다.
        /// </summary>
        private static void AddStarterKit(Inventory inventory)
        {
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = SurvivorProfile.StarterCount(kind);
                if (count > 0) inventory.AddUpTo(kind, count);
            }
        }
    }
}
