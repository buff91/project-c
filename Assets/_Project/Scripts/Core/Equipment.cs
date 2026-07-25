using System.Collections.Generic;

namespace ProjectC.Core
{
    public enum EquipmentSlot
    {
        Weapon = 0,
        Gear = 1
    }

    /// <summary>
    /// 장비 한 종류. **숫자를 올리는 대신 행동 규칙을 바꾼다** — GDD §11이 경계한 영구 스탯
    /// 크리프를 피하면서(공격력 보정은 두지 않는다) 기둥 1·2(입체·상호작용)에 붙는 선택지를 준다.
    /// 장비는 <see cref="ItemKind"/>로 창고·백팩·세이브에 실려 다니고, 장착은 슬롯당 하나다.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public string Id { get; }
        public ItemKind Item { get; }
        public EquipmentSlot Slot { get; }
        public string DisplayName { get; }
        public string Description { get; }

        /// <summary>평면 근접 사거리(칸). 기본 1. 2 이상이면 직선 + 사이가 뚫려 있어야 한다.</summary>
        public int MeleeReach { get; }

        /// <summary>근접 명중 시 대상을 1칸 밀어낸다 — 구멍·창문·허공으로 유도할 수 있다.</summary>
        public bool KnockbackOnHit { get; }

        /// <summary>받는 물리 피해 감소(상태이상 틱에는 적용하지 않는다).</summary>
        public int Armor { get; }

        /// <summary>안전 낙하 높이 보정. 낙하 전술을 여는 값이다(<see cref="FallRules"/>).</summary>
        public int SafeFallBonus { get; }

        /// <summary>대장간 제작 비용(골드).</summary>
        public int CraftCost { get; }

        public EquipmentDefinition(
            string id,
            ItemKind item,
            EquipmentSlot slot,
            string displayName,
            string description,
            int craftCost,
            int meleeReach = 1,
            bool knockbackOnHit = false,
            int armor = 0,
            int safeFallBonus = 0)
        {
            Id = id;
            Item = item;
            Slot = slot;
            DisplayName = displayName;
            Description = description;
            CraftCost = craftCost;
            MeleeReach = meleeReach < 1 ? 1 : meleeReach;
            KnockbackOnHit = knockbackOnHit;
            Armor = armor;
            SafeFallBonus = safeFallBonus;
        }
    }

    /// <summary>
    /// 장착 조합이 만드는 전투 보정. 규칙 함수들이 이 값만 받으면 되도록 평평하게 둔다
    /// (플레이어 전용이 아니라 대칭적인 값 — 나중에 몬스터가 장비를 들어도 같은 경로).
    /// </summary>
    public readonly struct CombatLoadout
    {
        public int MeleeReach { get; }
        public bool KnockbackOnHit { get; }
        public int Armor { get; }
        public int SafeFallHeight { get; }

        public CombatLoadout(int meleeReach, bool knockbackOnHit, int armor, int safeFallHeight)
        {
            MeleeReach = meleeReach < 1 ? 1 : meleeReach;
            KnockbackOnHit = knockbackOnHit;
            Armor = armor < 0 ? 0 : armor;
            SafeFallHeight = safeFallHeight < 0 ? 0 : safeFallHeight;
        }

        /// <summary>맨손 기본값 — 장비가 없을 때의 규칙은 지금까지와 완전히 같다.</summary>
        public static readonly CombatLoadout Unarmed =
            new CombatLoadout(1, false, 0, FallRules.DefaultSafeFallHeight);
    }

    /// <summary>
    /// 장비 목록의 단일 출처. 무기는 "사거리 대 넉백", 보조는 "안전 대 낙하 전술"로 갈린다 —
    /// 어느 쪽도 공격력을 올리지 않으므로 영구 인플레가 생기지 않는다.
    /// </summary>
    public static class EquipmentCatalog
    {
        public static readonly IReadOnlyList<EquipmentDefinition> All = new[]
        {
            new EquipmentDefinition(
                "pipe-spear", ItemKind.PipeSpear, EquipmentSlot.Weapon,
                "긴 파이프",
                "한 칸 떨어져서 직선으로 찌른다. 사수와 슬러지를 붙기 전에 다룬다.",
                craftCost: 55,
                meleeReach: 2),
            new EquipmentDefinition(
                "heavy-wrench", ItemKind.HeavyWrench, EquipmentSlot.Weapon,
                "대형 렌치",
                "때린 대상을 한 칸 밀어낸다. 구멍·창문 앞에서는 그 자체가 처형이다.",
                craftCost: 65,
                knockbackOnHit: true),
            new EquipmentDefinition(
                "sign-shield", ItemKind.SignShield, EquipmentSlot.Gear,
                "표지판 방패",
                "받는 물리 피해 -1. 대신 백팩을 2×2나 차지한다.",
                craftCost: 50,
                armor: 1),
            new EquipmentDefinition(
                "padded-boots", ItemKind.PaddedBoots, EquipmentSlot.Gear,
                "완충 부츠",
                "안전 낙하 높이 +2. 높은 곳에서 뛰어내려도 버틴다 — 지름길로도, 후퇴로도 쓴다.",
                craftCost: 45,
                safeFallBonus: 2)
        };

        public static EquipmentDefinition ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (EquipmentDefinition definition in All)
                if (definition.Id == id) return definition;
            return null;
        }

        public static EquipmentDefinition ForItem(ItemKind item)
        {
            foreach (EquipmentDefinition definition in All)
                if (definition.Item == item) return definition;
            return null;
        }

        /// <summary>이 아이템이 장착 가능한 장비인가.</summary>
        public static bool IsEquipment(ItemKind item) => ForItem(item) != null;
    }

    /// <summary>
    /// 던전에 장비가 굴러다니는 규칙. 파밍으로 얻은 장비가 익스트랙션의 판돈이 되므로
    /// (주워서 살아 나와야 내 것) 등장 깊이와 빈도를 한 곳에서 정한다.
    /// </summary>
    public static class EquipmentDropRules
    {
        /// <summary>장비가 나오기 시작하는 깊이(0 = B1). Shallow 밴드에는 나오지 않는다.</summary>
        public const int FirstDropDepth = 3;

        /// <summary>층당 장비가 놓일 확률(%). 층당 최대 하나다.</summary>
        public const int DropChancePercent = 30;

        public static bool AllowsDrop(int depthIndex) => depthIndex >= FirstDropDepth;

        /// <summary>
        /// 이 층에 놓을 장비를 고른다(없으면 null). 롤은 항상 두 번 — 확률과 종류를
        /// 같은 순서로 소비해 seed 재현성을 유지한다.
        /// </summary>
        public static EquipmentDefinition Roll(int depthIndex, System.Random random)
        {
            if (random == null) throw new System.ArgumentNullException(nameof(random));

            bool allowed = AllowsDrop(depthIndex);
            bool hit = random.Next(0, 100) < DropChancePercent;
            int index = random.Next(0, EquipmentCatalog.All.Count);
            return allowed && hit ? EquipmentCatalog.All[index] : null;
        }
    }

    /// <summary>장착 조합을 전투 보정으로 바꾸는 순수 규칙.</summary>
    public static class EquipmentRules
    {
        /// <summary>
        /// 던전에서 주운 장비를 바로 낄 것인가 — 슬롯이 비어 있을 때만이다.
        /// 이미 낀 장비를 말없이 갈아치우면 "더 좋은 걸 주웠나?" 판단을 뺏는다.
        /// 슬롯이 차 있으면 백팩에 남아 생환해야 창고로 들어간다.
        /// </summary>
        public static bool ShouldAutoEquip(string currentSlotEquipmentId) =>
            string.IsNullOrEmpty(currentSlotEquipmentId);

        /// <summary>
        /// 무기/보조 id 조합의 전투 보정. 없는 id·빈 문자열은 맨손으로 취급하고,
        /// 슬롯이 맞지 않는 id는 무시한다(세이브가 손상돼도 규칙이 깨지지 않게).
        /// </summary>
        public static CombatLoadout LoadoutFor(string weaponId, string gearId)
        {
            EquipmentDefinition weapon = SlotOrNull(weaponId, EquipmentSlot.Weapon);
            EquipmentDefinition gear = SlotOrNull(gearId, EquipmentSlot.Gear);

            return new CombatLoadout(
                weapon?.MeleeReach ?? 1,
                weapon?.KnockbackOnHit ?? false,
                gear?.Armor ?? 0,
                FallRules.DefaultSafeFallHeight + (gear?.SafeFallBonus ?? 0));
        }

        private static EquipmentDefinition SlotOrNull(string id, EquipmentSlot slot)
        {
            EquipmentDefinition definition = EquipmentCatalog.ById(id);
            return definition != null && definition.Slot == slot ? definition : null;
        }
    }
}
