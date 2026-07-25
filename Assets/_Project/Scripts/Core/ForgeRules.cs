using System;

namespace ProjectC.Core
{
    public enum ForgeResult
    {
        Crafted = 0,
        AlreadyOwned = 1,
        InsufficientGold = 2,
        UnknownEquipment = 3
    }

    /// <summary>
    /// 대장간: 골드로 장비를 만들고 슬롯에 장착한다. 옛 영구 스탯 강화(무기 연마 등)를 대체한다 —
    /// GDD §11이 경계한 것은 **영구 스탯 강화**였고, 권한 것은 장비/시작 장비 해금이다.
    ///
    /// 장비는 숫자를 올리지 않고 행동 규칙만 바꾸므로(사거리·넉백·방어·안전 낙하)
    /// 판을 거듭해도 공격력 인플레가 생기지 않는다. 제작한 장비는 창고에 남고,
    /// 장착한 장비는 백팩 공간을 쓰지 않는다.
    /// </summary>
    public static class ForgeRules
    {
        /// <summary>이 장비를 만들 수 있는가(이미 보유했으면 다시 만들지 않는다).</summary>
        public static bool CanCraft(MetaSaveData meta, EquipmentDefinition definition)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            return definition != null &&
                   !meta.OwnsEquipment(definition) &&
                   meta.gold >= definition.CraftCost;
        }

        /// <summary>골드를 지불하고 장비를 창고에 넣는다. 처음 만든 장비는 바로 장착한다.</summary>
        public static ForgeResult TryCraft(MetaSaveData meta, string equipmentId)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            EquipmentDefinition definition = EquipmentCatalog.ById(equipmentId);
            if (definition == null) return ForgeResult.UnknownEquipment;
            if (meta.OwnsEquipment(definition)) return ForgeResult.AlreadyOwned;
            if (!meta.TrySpend(definition.CraftCost)) return ForgeResult.InsufficientGold;

            meta.AddCount(definition.Item, 1);
            // 빈 슬롯이면 바로 채운다 — 만들어 놓고 장착을 잊는 흔한 함정을 없앤다.
            if (string.IsNullOrEmpty(meta.GetEquipped(definition.Slot)))
                meta.SetEquipped(definition.Slot, definition.Id);
            return ForgeResult.Crafted;
        }

        /// <summary>
        /// 슬롯에 장비를 끼운다. 보유하지 않은 장비는 거부하고, 이미 낀 것을 다시 고르면 해제한다
        /// (허브 UI가 버튼 하나로 토글할 수 있게).
        /// </summary>
        public static bool TryToggleEquip(MetaSaveData meta, string equipmentId)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            EquipmentDefinition definition = EquipmentCatalog.ById(equipmentId);
            if (definition == null || !meta.OwnsEquipment(definition)) return false;

            bool alreadyEquipped = meta.GetEquipped(definition.Slot) == definition.Id;
            meta.SetEquipped(definition.Slot, alreadyEquipped ? "" : definition.Id);
            return true;
        }

        /// <summary>
        /// 원정 반입: 장착한 장비를 창고에서 **꺼내** 들고 나간다. 반입한 순간부터 그 장비는
        /// 소모품과 같은 위험에 놓인다 — 죽으면 잃고, 살아 나와야 돌려받는다(익스트랙션 규칙).
        /// 창고에 남긴 예비 장비는 안전하다. 반환값은 이번 판에 적용할 전투 보정이다.
        /// </summary>
        public static CombatLoadout TakeIntoExpedition(
            MetaSaveData meta, out string weaponId, out string gearId)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            weaponId = CarryOut(meta, EquipmentSlot.Weapon);
            gearId = CarryOut(meta, EquipmentSlot.Gear);
            return EquipmentRules.LoadoutFor(weaponId, gearId);
        }

        /// <summary>생환·승리: 들고 나갔던 장비를 창고로 되돌리고 장착 상태를 유지한다.</summary>
        public static void ReturnFromExpedition(MetaSaveData meta, string weaponId, string gearId)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            CarryBack(meta, weaponId, EquipmentSlot.Weapon);
            CarryBack(meta, gearId, EquipmentSlot.Gear);
        }

        /// <summary>
        /// 사망·포기: 반입한 장비는 돌아오지 않는다. 창고에서 이미 꺼냈으므로 되돌리지 않고,
        /// 슬롯만 비워 허브가 "장착 중"이라고 거짓말하지 않게 한다.
        /// </summary>
        public static void LoseExpeditionEquipment(
            MetaSaveData meta, string weaponId, string gearId)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            if (!string.IsNullOrEmpty(weaponId)) meta.SetEquipped(EquipmentSlot.Weapon, "");
            if (!string.IsNullOrEmpty(gearId)) meta.SetEquipped(EquipmentSlot.Gear, "");
        }

        private static string CarryOut(MetaSaveData meta, EquipmentSlot slot)
        {
            EquipmentDefinition definition = EquipmentCatalog.ById(meta.GetEquipped(slot));
            if (definition == null || !meta.OwnsEquipment(definition))
            {
                meta.SetEquipped(slot, "");
                return "";
            }

            meta.RemoveCount(definition.Item, 1);
            return definition.Id;
        }

        private static void CarryBack(MetaSaveData meta, string equipmentId, EquipmentSlot slot)
        {
            EquipmentDefinition definition = EquipmentCatalog.ById(equipmentId);
            if (definition == null || definition.Slot != slot) return;

            meta.AddCount(definition.Item, 1);
            meta.SetEquipped(slot, definition.Id);
        }

        /// <summary>이 장비가 현재 슬롯에 끼워져 있는가.</summary>
        public static bool IsEquipped(MetaSaveData meta, EquipmentDefinition definition)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            return definition != null && meta.GetEquipped(definition.Slot) == definition.Id;
        }
    }
}
