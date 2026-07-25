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

        public static bool CanMoveToLoadout(
            MetaSaveData meta,
            ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (ItemCatalog.IsTreasure(kind) || meta.GetCount(kind) <= 0) return false;
            Inventory inventory = CreateInventory(meta);
            return inventory.TryAdd(kind, out _);
        }

        public static LoadoutTransferResult TryMoveToLoadout(
            MetaSaveData meta,
            ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (ItemCatalog.IsTreasure(kind)) return LoadoutTransferResult.UnsupportedItem;
            if (meta.GetCount(kind) <= 0) return LoadoutTransferResult.MissingFromStash;

            Inventory inventory = CreateInventory(meta);
            if (!inventory.TryAdd(kind, out _))
                return LoadoutTransferResult.NoBackpackSpace;

            meta.RemoveCount(kind, 1);
            meta.AddLoadoutCount(kind, 1);
            return LoadoutTransferResult.Success;
        }

        public static LoadoutTransferResult TryMoveToStash(MetaSaveData meta, ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (ItemCatalog.IsTreasure(kind)) return LoadoutTransferResult.UnsupportedItem;
            if (meta.RemoveLoadoutCount(kind, 1) <= 0)
                return LoadoutTransferResult.MissingFromLoadout;

            meta.AddCount(kind, 1);
            return LoadoutTransferResult.Success;
        }

        /// <summary>
        /// 영웅 교체나 구버전 저장으로 현재 구성이 넘치면 들어가는 만큼만 유지하고
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
        /// destination에는 호출 전에 영웅 기본 지급품이 들어 있어야 한다.
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
