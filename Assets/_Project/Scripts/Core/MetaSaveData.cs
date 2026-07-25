using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 판 사이에 유지되는 메타 창고 (extraction 규칙의 저장소).
    /// 생환 시: 전리품은 골드로 환산해 적립, 남은 소모품은 여기 보관된다.
    /// 허브에서 출정 백팩으로 고른 물품만 새 판에 반입하며, 죽으면 그 판 소지품은 소실된다.
    /// </summary>
    [Serializable]
    public class MetaSaveData
    {
        public int gold;
        public string[] unlockedHeroes = { "knight" };
        /// <summary>
        /// 창고와 출정 로드아웃. 아이템 종류마다 필드를 늘리지 않도록 목록 하나로 둔다
        /// (연산은 <see cref="ItemStorage"/>가 공유). 전리품은 창고에 남기지 않는다 —
        /// 생환 시 항상 골드로 환산되므로 <see cref="AddCount"/>가 걸러낸다.
        /// </summary>
        public List<ItemStack> stash = new List<ItemStack>();
        public List<ItemStack> loadout = new List<ItemStack>();

        // 장착 중인 장비 id (EquipmentCatalog). 빈 문자열이면 맨손이다.
        // 장착 장비는 백팩 공간을 쓰지 않지만 **안전하지는 않다** — 원정에 반입되며(창고에서 빠짐)
        // 죽으면 소모품과 함께 잃는다. 창고에 남긴 예비 장비만 안전하다(익스트랙션 규칙).
        public string equippedWeaponId = "";
        public string equippedGearId = "";

        // 현재 원정에 걸린 의뢰 id 목록. 생환/승리 정산 때 비워지고 허브에서 다시 채운다.
        public string[] activeBountyIds = new string[0];

        /// <summary>
        /// 조건을 달성해 드랍 풀에 들어온 도구들((int)<see cref="ItemKind"/>).
        /// <b>죽어도 남는다</b> — 실패한 판도 전진이어야 한다(<see cref="ItemUnlockRules"/>).
        /// 옛 세이브는 빈 목록으로 들어와 "아직 아무것도 안 열림"이 되므로 마이그레이션이 없다.
        /// </summary>
        public List<int> unlockedItems = new List<int>();

        public int GetCount(ItemKind kind) => ItemStorage.Count(stash, kind);

        /// <summary>
        /// 창고 수량을 더한다. 전리품(<see cref="ItemCategory.Treasure"/>)은 보관하지 않는다 —
        /// 생환 정산에서 항상 골드로 바뀌므로 창고에 남으면 이중 계산이 된다.
        /// </summary>
        public void AddCount(ItemKind kind, int amount)
        {
            if (ItemCatalog.CategoryOf(kind) == ItemCategory.Treasure) return;
            ItemStorage.Add(stash, kind, amount);
        }

        /// <summary>창고에서 요청 수량만 제거한다. 보유량을 넘는 요청은 실제 제거량만 반환한다.</summary>
        public int RemoveCount(ItemKind kind, int amount) =>
            ItemStorage.Remove(stash, kind, amount);

        public int GetLoadoutCount(ItemKind kind) => ItemStorage.Count(loadout, kind);

        public void AddLoadoutCount(ItemKind kind, int amount) =>
            ItemStorage.Add(loadout, kind, amount);

        public int RemoveLoadoutCount(ItemKind kind, int amount) =>
            ItemStorage.Remove(loadout, kind, amount);

        public void ClearLoadout() => ItemStorage.Clear(loadout);

        public void ClearItems()
        {
            ItemStorage.Clear(stash);
            ClearLoadout();
        }

        /// <summary>장비를 하나라도 보유하고 있는가(제작 후 창고에 남아 있는 것).</summary>
        public bool OwnsEquipment(EquipmentDefinition definition) =>
            definition != null && GetCount(definition.Item) > 0;

        /// <summary>슬롯에 장착된 장비 id. 없으면 빈 문자열.</summary>
        public string GetEquipped(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? equippedWeaponId ?? "" : equippedGearId ?? "";

        public void SetEquipped(EquipmentSlot slot, string equipmentId)
        {
            string value = equipmentId ?? "";
            if (slot == EquipmentSlot.Weapon) equippedWeaponId = value;
            else equippedGearId = value;
        }

        /// <summary>현재 장착 조합의 전투 보정. 보유하지 않은 장비는 장착으로 치지 않는다.</summary>
        public CombatLoadout EquippedLoadout()
        {
            EquipmentDefinition weapon = EquipmentCatalog.ById(equippedWeaponId);
            EquipmentDefinition gear = EquipmentCatalog.ById(equippedGearId);
            return EquipmentRules.LoadoutFor(
                OwnsEquipment(weapon) ? weapon.Id : null,
                OwnsEquipment(gear) ? gear.Id : null);
        }

        /// <summary>골드가 충분하면 차감하고 true. 상점 구매/해금 공통 경로.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (gold < cost) return false;
            gold -= cost;
            return true;
        }

        public bool IsHeroUnlocked(string heroId)
        {
            if (unlockedHeroes == null) return false;
            foreach (string id in unlockedHeroes)
                if (id == heroId) return true;
            return false;
        }

        public void UnlockHero(string heroId)
        {
            if (IsHeroUnlocked(heroId)) return;
            var next = new string[(unlockedHeroes?.Length ?? 0) + 1];
            unlockedHeroes?.CopyTo(next, 0);
            next[next.Length - 1] = heroId;
            unlockedHeroes = next;
        }

        public bool IsItemUnlocked(ItemKind kind)
        {
            if (unlockedItems == null) return false;
            foreach (int id in unlockedItems)
                if (id == (int)kind) return true;
            return false;
        }

        /// <summary>새로 열렸으면 true. 이미 열려 있으면 아무것도 하지 않는다(중복 방지).</summary>
        public bool UnlockItem(ItemKind kind)
        {
            if (IsItemUnlocked(kind)) return false;
            unlockedItems ??= new List<int>();
            unlockedItems.Add((int)kind);
            return true;
        }

        /// <summary>해금된 종류 목록 — 생성기에 넘기는 형태.</summary>
        public List<ItemKind> UnlockedItemKinds()
        {
            var kinds = new List<ItemKind>();
            if (unlockedItems == null) return kinds;
            foreach (int id in unlockedItems) kinds.Add((ItemKind)id);
            return kinds;
        }

    }
}
