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

        /// <summary>
        /// 원거리 사거리(<see cref="CombatRules.RangedReachCost"/> 예산). 0이면 이 무기는
        /// 원거리를 주지 않는다 — 그때는 내장 이미터 기본값이 대신 쓰인다
        /// (<see cref="RangedWeaponRules.Baseline"/>).
        /// </summary>
        public int RangedRange { get; }

        /// <summary>원거리 최대 충전(= 연속 사격 가능 횟수). 0이면 무기가 원거리를 주지 않는다.</summary>
        public int RangedCapacity { get; }

        /// <summary>충전 1칸이 자연 회복되는 데 걸리는 턴. 작을수록 빨리 다시 쏜다.</summary>
        public int RangedRechargeTurns { get; }

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
            int safeFallBonus = 0,
            int rangedRange = 0,
            int rangedCapacity = 0,
            int rangedRechargeTurns = 0)
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
            RangedRange = rangedRange < 0 ? 0 : rangedRange;
            RangedCapacity = rangedCapacity < 0 ? 0 : rangedCapacity;
            RangedRechargeTurns = rangedRechargeTurns < 1 ? 1 : rangedRechargeTurns;
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

        /// <summary>원거리 사거리. 내장 이미터가 있으므로 실사용 조합에서는 항상 1 이상이다.</summary>
        public int RangedRange { get; }

        /// <summary>원거리 최대 충전(연속 사격 횟수).</summary>
        public int RangedCapacity { get; }

        /// <summary>충전 1칸 자연 회복에 걸리는 턴.</summary>
        public int RangedRechargeTurns { get; }

        /// <summary>이 조합으로 원거리를 쏠 수 있는가 — 남은 충전은 별도다.</summary>
        public bool HasRanged => RangedRange > 0 && RangedCapacity > 0;

        public CombatLoadout(
            int meleeReach,
            bool knockbackOnHit,
            int armor,
            int safeFallHeight,
            int rangedRange = 0,
            int rangedCapacity = 0,
            int rangedRechargeTurns = 1)
        {
            MeleeReach = meleeReach < 1 ? 1 : meleeReach;
            KnockbackOnHit = knockbackOnHit;
            Armor = armor < 0 ? 0 : armor;
            SafeFallHeight = safeFallHeight < 0 ? 0 : safeFallHeight;
            RangedRange = rangedRange < 0 ? 0 : rangedRange;
            RangedCapacity = rangedCapacity < 0 ? 0 : rangedCapacity;
            RangedRechargeTurns = rangedRechargeTurns < 1 ? 1 : rangedRechargeTurns;
        }

        /// <summary>
        /// 맨손 기본값. 근접·방어·낙하는 지금까지와 같고, 원거리는 **내장 이미터**가 준다 —
        /// 원거리를 아예 못 보면 플레이어가 그 축을 배우지도 저울질하지도 못한다.
        /// 아크 캐스터는 이 기본형의 상위 티어다(<see cref="RangedWeaponRules.Baseline"/>).
        /// </summary>
        public static readonly CombatLoadout Unarmed =
            new CombatLoadout(
                1, false, 0, FallRules.DefaultSafeFallHeight,
                RangedWeaponRules.Baseline.Range,
                RangedWeaponRules.Baseline.Capacity,
                RangedWeaponRules.Baseline.RechargeTurns);
    }

    /// <summary>
    /// 장비 목록의 단일 출처. 무기는 "사거리 대 넉백", 보조는 "안전 대 낙하 전술"로 갈린다 —
    /// 어느 쪽도 공격력을 올리지 않으므로 영구 인플레가 생기지 않는다.
    ///
    /// <para>
    /// <b>제작비는 골드의 주 목적지다.</b> 직업제를 걷어낼 때 사라진 영웅 해금 비용(200G)의
    /// 몫을 여기로 옮겼다 — 4종 합계 215G → 410G. 생환 보상이 갈 곳이
    /// 소모품밖에 없으면 골드가 남아돌고, 그러면 "무엇을 걸고 나갈지"가 판돈이 아니게 된다.
    /// 값은 실플레이 전 임시다(생환 밸런스 재조정 때 함께 본다).
    /// </para>
    /// </summary>
    public static class EquipmentCatalog
    {
        public static readonly IReadOnlyList<EquipmentDefinition> All = new[]
        {
            // 표시명은 리스킨 표 §4-b(사이버펑크)를 따른다. 새 이름도 행동 규칙을 설명해야 한다 —
            // 규칙(사거리·넉백·감산·낙하 보너스)과 코드 ID·craftCost 는 불변.
            new EquipmentDefinition(
                "pipe-spear", ItemKind.PipeSpear, EquipmentSlot.Weapon,
                "빔 랜스",
                "빔 날이 한 칸을 더 뻗는다 — 떨어져서 직선으로 찌른다. 사수와 슬러지를 붙기 전에 다룬다.",
                craftCost: 105,
                meleeReach: 2),
            new EquipmentDefinition(
                "heavy-wrench", ItemKind.HeavyWrench, EquipmentSlot.Weapon,
                "임팩트 렌치",
                "동력 충격이 때린 대상을 한 칸 밀어낸다. 구멍·창문 앞에서는 그 자체가 처형이다.",
                craftCost: 125,
                knockbackOnHit: true),
            new EquipmentDefinition(
                "sign-shield", ItemKind.SignShield, EquipmentSlot.Gear,
                "전광판 방패",
                "뜯어낸 전광판 패널. 받는 물리 피해 -1. 대신 백팩을 2×2나 차지한다.",
                craftCost: 95,
                armor: 1),
            new EquipmentDefinition(
                "padded-boots", ItemKind.PaddedBoots, EquipmentSlot.Gear,
                "서스펜션 부츠",
                "유압 완충으로 안전 낙하 높이 +2. 높은 곳에서 뛰어내려도 버틴다 — 지름길로도, 후퇴로도 쓴다.",
                craftCost: 85,
                safeFallBonus: 2),
            // 원거리 상위 티어. 내장 이미터(사거리 3·충전 2·6턴)를 사거리 5·충전 4·4턴으로
            // 끌어올린다 — 새 축을 여는 게 아니라 이미 쥔 축을 깊게 만든다. 근접 사거리는
            // 늘리지 않는다: 늘리면 "붙을까 떨어질까"가 선택이 아니게 된다.
            new EquipmentDefinition(
                "arc-caster", ItemKind.ArcCaster, EquipmentSlot.Weapon,
                "아크 캐스터",
                "내장 이미터를 대체하는 사격 장비 — 사거리 5, 충전 4, 재충전이 빠르다. 사선이 필요하다.",
                craftCost: 145,
                rangedRange: 5,
                rangedCapacity: 4,
                rangedRechargeTurns: 4)
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
        /// <para>
        /// <paramref name="forgeOpen"/>이 false 면 장비가 나오지 않는다. 대장장이를 구출하기
        /// 전에는 장비를 쓸 수도 고칠 수도 없어서 주워도 의미가 없다
        /// (<see cref="ShelterNpcRoster"/>). <b>롤은 그대로 소비하고 결과만 막는다</b> —
        /// 그래야 대장간 상태가 던전의 나머지를 흔들지 않는다.
        /// </para>
        /// </summary>
        public static EquipmentDefinition Roll(
            int depthIndex,
            System.Random random,
            bool forgeOpen = true)
        {
            if (random == null) throw new System.ArgumentNullException(nameof(random));

            bool allowed = AllowsDrop(depthIndex);
            bool hit = random.Next(0, 100) < DropChancePercent;
            int index = random.Next(0, EquipmentCatalog.All.Count);
            return allowed && hit && forgeOpen ? EquipmentCatalog.All[index] : null;
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

            // 무기가 원거리를 주지 않으면 내장 이미터로 떨어진다 — 근접 무기를 골랐다고
            // 원거리 축이 사라지지는 않는다(사거리·충전만 기본형으로 내려간다).
            bool weaponIsRanged = weapon != null && weapon.RangedRange > 0 && weapon.RangedCapacity > 0;

            return new CombatLoadout(
                weapon?.MeleeReach ?? 1,
                weapon?.KnockbackOnHit ?? false,
                gear?.Armor ?? 0,
                FallRules.DefaultSafeFallHeight + (gear?.SafeFallBonus ?? 0),
                weaponIsRanged ? weapon.RangedRange : RangedWeaponRules.Baseline.Range,
                weaponIsRanged ? weapon.RangedCapacity : RangedWeaponRules.Baseline.Capacity,
                weaponIsRanged ? weapon.RangedRechargeTurns : RangedWeaponRules.Baseline.RechargeTurns);
        }

        private static EquipmentDefinition SlotOrNull(string id, EquipmentSlot slot)
        {
            EquipmentDefinition definition = EquipmentCatalog.ById(id);
            return definition != null && definition.Slot == slot ? definition : null;
        }
    }
}
