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
    /// 선택한 영웅의 기본 지급품도 같은 6×4 용량에 포함해 실제 던전 백팩과 결과를 맞춘다.
    /// </summary>
    public static class ExpeditionLoadoutRules
    {
        public static Inventory CreateInventory(MetaSaveData meta, HeroArchetype hero)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            AddStarterKit(inventory, hero);
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = meta.GetLoadoutCount(kind);
                if (count > 0) inventory.AddUpTo(kind, count);
            }
            return inventory;
        }

        public static BackpackLayout CreateLayout(MetaSaveData meta, HeroArchetype hero) =>
            CreateInventory(meta, hero).CreateLayout();

        public static int StarterCount(HeroArchetype hero, ItemKind kind)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            switch (kind)
            {
                case ItemKind.Potion: return hero.StartPotions;
                case ItemKind.Bomb: return hero.StartBombs;
                case ItemKind.FrostBomb: return hero.StartFrostBombs;
                default: return 0;
            }
        }

        public static bool CanMoveToLoadout(
            MetaSaveData meta,
            HeroArchetype hero,
            ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (ItemCatalog.IsTreasure(kind) || meta.GetCount(kind) <= 0) return false;
            Inventory inventory = CreateInventory(meta, hero);
            return inventory.TryAdd(kind, out _);
        }

        public static LoadoutTransferResult TryMoveToLoadout(
            MetaSaveData meta,
            HeroArchetype hero,
            ItemKind kind)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (ItemCatalog.IsTreasure(kind)) return LoadoutTransferResult.UnsupportedItem;
            if (meta.GetCount(kind) <= 0) return LoadoutTransferResult.MissingFromStash;

            Inventory inventory = CreateInventory(meta, hero);
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
        public static int Reconcile(MetaSaveData meta, HeroArchetype hero)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            var inventory = new Inventory(BackpackRules.Columns, BackpackRules.Rows);
            AddStarterKit(inventory, hero);
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

        private static void AddStarterKit(Inventory inventory, HeroArchetype hero)
        {
            if (hero.StartPotions > 0)
                inventory.AddUpTo(ItemKind.Potion, hero.StartPotions);
            if (hero.StartBombs > 0)
                inventory.AddUpTo(ItemKind.Bomb, hero.StartBombs);
            if (hero.StartFrostBombs > 0)
                inventory.AddUpTo(ItemKind.FrostBomb, hero.StartFrostBombs);
        }
    }
}
