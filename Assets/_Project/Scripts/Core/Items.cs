using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// M1 아이템 최소 셋: 응급 키트(회복), 급조 폭발물(광역 피해 + 약한 바닥 붕괴). (GDD §5.6, §5.8)
    /// 표시명은 리스킨 표 §4(사이버펑크)를 따른다 — 규칙·enum 이름·세이브 키는 불변.
    /// </summary>
    public enum ItemKind
    {
        Potion = 0,        // 응급 키트(회복)
        Bomb = 1,          // 급조 폭발물: 3×3 피해 + 화상 + 넉백, 약한 바닥 붕괴·폭발통 유폭.
        FrostBomb = 2,     // 냉각재 수류탄: 낮은 피해 + 빙결. 불이 아니라 폭발통은 유폭하지 않는다.
        OilFlask = 3,      // 연료통: 3×3 연료 살포. 불 폭발과 겹치면 발화 → 화상.
        ThrowingKnife = 4, // 투척 볼트: 소모형 단일 대상 원거리 피해.
        RecallScroll = 5,  // 귀환 비컨: 현재 층 입구로 순간이동.
        CoinPouch = 6,     // 전리품: 생환 시 골드로 환산. 던전 안에서는 쓸 수 없다.
        Gemstone = 7,      // 전리품(중): 생환 시 골드로 환산.
        Relic = 8,         // 전리품(대): 희귀. 생환 시 골드로 환산.
        Herb = 9,          // 조합 재료: 정화 균사. 2개로 응급 키트를 만든다.
        BlastPowder = 10,  // 조합 재료: 뇌관 화약. 2개로 급조 폭발물을 만든다.
        FrostShard = 11,   // 조합 재료: 냉매 결정. 폭발물에 섞어 냉각재 수류탄을 만든다.
        // 장비 (대장간 제작 — 스탯이 아니라 행동 규칙을 바꾼다. EquipmentCatalog 참조)
        PipeSpear = 12,    // 빔 랜스: 근접 사거리 2(직선).
        HeavyWrench = 13,  // 임팩트 렌치: 근접 명중 시 1칸 넉백.
        SignShield = 14,   // 전광판 방패: 받는 물리 피해 -1. 2×2 점유.
        PaddedBoots = 15,  // 서스펜션 부츠: 안전 낙하 높이 +2.
        CannedFood = 16,   // 통조림: 배고픔을 채운다. (HungerRules)
        ExtractionBeacon = 17, // 비상 송출기: 어디서든 즉시 생환. (ExtractionRules)
        ArcCaster = 18,    // 아크 캐스터: 원거리 사격을 여는 무기. 셀이 있어야 쏜다.
        EnergyCell = 19    // 에너지 셀: 아크 캐스터 탄약. 1발당 1충전. (RangedWeaponRules)
    }

    /// <summary>
    /// 아이템이 어떤 종류의 물건인가. 분류가 여러 곳에 흩어져 있으면(전리품인가? 재료인가?
    /// 장비인가?) 새 아이템을 넣을 때마다 조건문을 빠뜨리게 된다 — 한 함수로 모은다.
    /// </summary>
    public enum ItemCategory
    {
        /// <summary>쓰면 사라지는 물건(물약·폭탄·통조림·송출기…).</summary>
        Consumable = 0,
        /// <summary>생환해야 값이 되는 환금 전용 전리품.</summary>
        Treasure = 1,
        /// <summary>조합에서만 소비되는 재료.</summary>
        Material = 2,
        /// <summary>슬롯에 장착하는 장비.</summary>
        Equipment = 3
    }

    /// <summary>한 아이템의 분류·경제·표시·백팩 크기를 묶은 불변 정의.</summary>
    public sealed class ItemDefinition
    {
        public ItemKind Kind { get; }
        public ItemCategory Category { get; }
        public string DisplayName { get; }
        public string ShortLabel { get; }
        public string Description { get; }
        public int GoldValue { get; }
        public int ShopPrice { get; }
        public ItemFootprint Footprint { get; }

        /// <summary>
        /// 백팩 한 칸이 담는 사용 횟수. 기본 1(= 한 칸에 한 번 쓸 것 하나).
        ///
        /// <para>
        /// 이 값이 1보다 크면 <c>ItemKind → int</c>가 세는 것이 "개수"가 아니라
        /// <b>총 충전 횟수</b>가 되고, 점유 칸수는 <c>ceil(충전 / 이 값)</c>으로 파생한다
        /// (<see cref="BackpackRules.UnitsFor"/>). 소모품 하나가 한 칸을 통째로 먹던 시절엔
        /// 회복을 챙길수록 전리품 자리가 사라져 "몇 회분 챙길까"가 아니라 "포기할까"만 남았다.
        /// </para>
        /// <para>
        /// <b>1보다 큰 값은 소모품에만 허용한다.</b> 이 불변식이 전리품 정산
        /// (<c>GoldValue * count</c>)·조합 재료 판정(<c>>= 2</c>)·장비 보유 판정
        /// (<c>GetCount > 0</c>)을 전부 무변경으로 살린다 — count가 곧 개수라는 가정이
        /// 그쪽에 그대로 남아 있기 때문이다. 생성자가 정적으로 거부한다.
        /// </para>
        /// </summary>
        public int ChargesPerItem { get; }

        public ItemDefinition(
            ItemKind kind,
            ItemCategory category,
            string displayName,
            string shortLabel,
            string description,
            int goldValue,
            int shopPrice,
            ItemFootprint footprint,
            int chargesPerItem = 1)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("아이템 표시 이름이 비어 있다.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(shortLabel))
                throw new ArgumentException("아이템 짧은 라벨이 비어 있다.", nameof(shortLabel));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("아이템 설명이 비어 있다.", nameof(description));
            if (goldValue < 0) throw new ArgumentOutOfRangeException(nameof(goldValue));
            if (shopPrice < 0) throw new ArgumentOutOfRangeException(nameof(shopPrice));
            if (category == ItemCategory.Treasure && goldValue <= 0)
                throw new ArgumentException("전리품은 생환 가치가 있어야 한다.", nameof(goldValue));
            if (category != ItemCategory.Treasure && goldValue != 0)
                throw new ArgumentException("전리품이 아닌 아이템은 생환 가치를 가질 수 없다.", nameof(goldValue));
            if (footprint.Width <= 0 || footprint.Height <= 0)
                throw new ArgumentException("아이템 백팩 크기는 양수여야 한다.", nameof(footprint));
            if (chargesPerItem <= 0)
                throw new ArgumentOutOfRangeException(nameof(chargesPerItem));
            // 이 가드가 이 설계의 안전 예산 전체다 — 소모품이 아닌 종류에서 count 가 개수를
            // 뜻한다는 가정이 정산·조합·장비 판정에 그대로 남아 있다. static 초기화 시점에
            // 터지므로 테스트보다 먼저 잡힌다.
            if (chargesPerItem != 1 && category != ItemCategory.Consumable)
                throw new ArgumentException(
                    "충전은 소모품만 가진다 — 전리품·재료·장비의 개수는 곧 수량이다.",
                    nameof(chargesPerItem));

            Kind = kind;
            Category = category;
            DisplayName = displayName;
            ShortLabel = shortLabel;
            Description = description;
            GoldValue = goldValue;
            ShopPrice = shopPrice;
            Footprint = footprint;
            ChargesPerItem = chargesPerItem;
        }
    }

    /// <summary>
    /// 아이템 정의의 단일 출처. 새 종류는 이 표에 완전한 정의 하나를 추가해야 하며,
    /// 미등록 enum 값은 소모품 기본값으로 조용히 흘려보내지 않고 즉시 실패한다.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly ItemFootprint OneCell = new ItemFootprint(1, 1);
        private static readonly ItemFootprint Tall = new ItemFootprint(1, 2);
        private static readonly ItemFootprint Large = new ItemFootprint(2, 2);

        private static readonly ItemDefinition[] Definitions =
        {
            // 칸당 2회분. 6회분이 6칸을 먹던 시절엔 회복을 챙길수록 전리품 자리가 사라져
            // "몇 회분 챙길까"가 아니라 "포기할까"만 남았다. 광역 화력(폭탄·냉기·기름)과
            // 탈출 자원(귀환·송출기)은 1로 둔다 — 폭발은 한 발이 판을 뒤집고, 탈출은
            // 익스트랙션 판돈 그 자체다.
            Define(ItemKind.Potion, ItemCategory.Consumable, "응급 키트", "MEDKIT",
                "HP를 회복한다. 쓰는 데 행동 1회를 소비한다.", shopPrice: 15,
                chargesPerItem: 2),
            Define(ItemKind.Bomb, ItemCategory.Consumable, "급조 폭발물", "BOMB",
                "3×3 폭발 — 화상·넉백, 약한 바닥 붕괴와 폭발통 유폭. 본인도 피해를 입는다.", shopPrice: 20),
            Define(ItemKind.FrostBomb, ItemCategory.Consumable, "냉각재 수류탄", "FROST",
                "낮은 피해의 3×3 냉기 폭발 — 맞은 대상을 빙결시킨다. 폭발통은 터뜨리지 않는다.", shopPrice: 15),
            Define(ItemKind.OilFlask, ItemCategory.Consumable, "연료통", "FUEL",
                "3×3 범위에 연료를 뿌린다. 불 폭발이 닿으면 발화해 위에 있는 모두가 불탄다.", 10, footprint: Tall),
            // 칸당 3회분. 1×2라 한 자루가 두 칸을 먹었고, 세 자루면 백팩의 1/4이 칼집이었다 —
            // 단일 대상 한 방짜리 자원에 그 값은 "쓸까"가 아니라 "아예 안 가져간다"로 끝난다.
            // **이건 순수한 칸 산수가 아니다**: 실질 휴대량이 늘어 원거리 연발 횟수가 오른다.
            // 그래도 광역·연쇄가 없어 한 발이 판을 뒤집지 않는 단일 대상 자원이라 허용한다.
            Define(ItemKind.ThrowingKnife, ItemCategory.Consumable, "투척 볼트", "BOLT",
                "적 하나에게 강한 원거리 피해를 준다. 소모품 — 시야선이 필요하다.", 10,
                footprint: Tall, chargesPerItem: 3),
            Define(ItemKind.RecallScroll, ItemCategory.Consumable, "귀환 비컨", "RECALL",
                "현재 층의 입구로 순간이동한다. 행동 1회를 소비한다.", 25, footprint: Tall),
            Define(ItemKind.CoinPouch, ItemCategory.Treasure, "스크랩 파우치", "SCRAP",
                "생환하면 소지금 $10을 얻는다. 죽으면 잃는다.", goldValue: 10),
            Define(ItemKind.Gemstone, ItemCategory.Treasure, "코어 파편", "CORE",
                "생환하면 소지금 $25을 얻는다. 죽으면 잃는다.", goldValue: 25),
            Define(ItemKind.Relic, ItemCategory.Treasure, "시제품 코어", "PROTO",
                "출처가 지워진 군용 시제품. 생환하면 소지금 $60을 얻는다.", goldValue: 60, footprint: Large),
            Define(ItemKind.Herb, ItemCategory.Material, "정화 균사", "SPORE",
                "조합 재료. 2개를 가공하면 응급 키트가 된다.", shopPrice: 6),
            Define(ItemKind.BlastPowder, ItemCategory.Material, "뇌관 화약", "POWDER",
                "조합 재료. 2개를 뭉치면 급조 폭발물이 된다.", shopPrice: 8),
            Define(ItemKind.FrostShard, ItemCategory.Material, "냉매 결정", "SHARD",
                "조합 재료. 폭발물에 섞으면 냉각재 수류탄이 된다.", shopPrice: 5),
            DefineEquipment(ItemKind.PipeSpear, "LANCE", Tall),
            DefineEquipment(ItemKind.HeavyWrench, "WRENCH", Tall),
            DefineEquipment(ItemKind.SignShield, "SHIELD", Large),
            DefineEquipment(ItemKind.PaddedBoots, "BOOTS", OneCell),
            DefineEquipment(ItemKind.ArcCaster, "CASTER", Tall),
            // 칸당 2회분. 사격은 기다리면 공짜로 차므로 셀은 "쏠 수 있게 하는 것"이 아니라
            // **기다리지 않게 하는 것**이다 — 교전 중 한 번에 만충시키는 급속 충전재다.
            // 그래서 상시 휴대품이 아니라 결정적인 순간에 한두 번 쓰는 물건으로 둔다.
            Define(ItemKind.EnergyCell, ItemCategory.Consumable, "에너지 셀", "CELL",
                "쓰면 사격 충전이 즉시 가득 찬다. 기다릴 수 없을 때의 급속 충전재다.",
                shopPrice: 12, chargesPerItem: 2),
            // 칸당 3회분. 배고픔은 판 전체를 관통하는 상시 압박이라(가득 찬 배 100턴)
            // 통조림은 "한 번 챙기고 잊는 것"이 아니라 계속 다시 채우는 소모품이고,
            // 1회분 = 1칸이던 시절엔 그 리듬이 백팩 상시 점유로 나타났다.
            // 물약(2)보다 큰 이유는 회복이 아니라 유지 비용이기 때문이다 —
            // 회복은 판돈이지만 배고픔은 세금이고, 세금을 칸으로 받으면 파밍이 줄어든다.
            Define(ItemKind.CannedFood, ItemCategory.Consumable, "통조림", "FOOD",
                "먹으면 배고픔을 채운다. 먹는 데 행동 1회를 소비한다.", shopPrice: 12,
                chargesPerItem: 3),
            Define(ItemKind.ExtractionBeacon, ItemCategory.Consumable, "비상 송출기", "BEACON",
                "어디서든 즉시 생환한다. 들고 있는 것을 지키고 판을 끝낸다.", shopPrice: 70)
        };

        private static readonly Dictionary<ItemKind, ItemDefinition> ByKind = BuildIndex();

        public static readonly ItemKind[] AllKinds = BuildAllKinds();

        public static IReadOnlyList<ItemDefinition> All => Definitions;

        public static ItemDefinition For(ItemKind kind)
        {
            if (ByKind.TryGetValue(kind, out ItemDefinition definition)) return definition;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "등록되지 않은 아이템 종류다.");
        }

        public static int GoldValue(ItemKind kind) => For(kind).GoldValue;
        public static ItemCategory CategoryOf(ItemKind kind) => For(kind).Category;

        /// <summary>전리품(환금 전용) 여부. 던전 안에서는 사용 불가.</summary>
        public static bool IsTreasure(ItemKind kind) => CategoryOf(kind) == ItemCategory.Treasure;

        /// <summary>던전 인벤토리에서 "사용"할 수 있는가 — 전리품·재료·장비는 아니다.</summary>
        public static bool IsUsable(ItemKind kind) => CategoryOf(kind) == ItemCategory.Consumable;

        /// <summary>상점 구매가. 0 이면 비매품(전리품은 팔지 않는다 — 파밍으로만).</summary>
        public static int ShopPrice(ItemKind kind) => For(kind).ShopPrice;

        /// <summary>
        /// 백팩 한 칸이 담는 사용 횟수(<see cref="ItemDefinition.ChargesPerItem"/>).
        /// 칸수 파생은 여기가 아니라 <see cref="BackpackRules.UnitsFor"/>가 한다 —
        /// 그건 경제가 아니라 패킹 규칙이다.
        /// </summary>
        public static int ChargesPerItem(ItemKind kind) => For(kind).ChargesPerItem;

        /// <summary>한 칸이 여러 회분을 담는 종류인가. UI가 배지 표시 규칙을 가를 때 쓴다.</summary>
        public static bool IsCharged(ItemKind kind) => For(kind).ChargesPerItem > 1;

        /// <summary>
        /// 긴 단위명이나 G 대신 픽셀 HUD에서 즉시 읽히는 달러 기호를 접두사로 사용한다.
        /// HUD·상점·정산 화면이 모두 "$120" 형식을 공유한다.
        /// </summary>
        public static string FormatGold(int amount) => $"${amount}";

        /// <summary>조합 재료 여부. 사용 불가 — 조합 화면에서만 소비된다.</summary>
        public static bool IsMaterial(ItemKind kind) => CategoryOf(kind) == ItemCategory.Material;

        public static string DisplayName(ItemKind kind) => For(kind).DisplayName;

        /// <summary>HUD/피드백용 짧은 영문 라벨. 픽셀 폭이 좁은 액션 문구에서 KR 이름 대신 쓴다.</summary>
        public static string ShortLabel(ItemKind kind) => For(kind).ShortLabel;

        public static string Description(ItemKind kind) => For(kind).Description;

        private static ItemDefinition Define(
            ItemKind kind,
            ItemCategory category,
            string displayName,
            string shortLabel,
            string description,
            int shopPrice = 0,
            int goldValue = 0,
            ItemFootprint? footprint = null,
            int chargesPerItem = 1)
        {
            return new ItemDefinition(
                kind, category, displayName, shortLabel, description,
                goldValue, shopPrice, footprint ?? OneCell, chargesPerItem);
        }

        private static ItemDefinition DefineEquipment(
            ItemKind kind,
            string shortLabel,
            ItemFootprint footprint)
        {
            EquipmentDefinition equipment = EquipmentCatalog.ForItem(kind) ??
                throw new InvalidOperationException($"{kind} 장비 정의가 없다.");
            return Define(
                kind,
                ItemCategory.Equipment,
                equipment.DisplayName,
                shortLabel,
                equipment.Description,
                footprint: footprint);
        }

        private static Dictionary<ItemKind, ItemDefinition> BuildIndex()
        {
            var result = new Dictionary<ItemKind, ItemDefinition>();
            foreach (ItemDefinition definition in Definitions)
            {
                if (result.ContainsKey(definition.Kind))
                    throw new InvalidOperationException($"{definition.Kind} 아이템 정의가 중복됐다.");
                result.Add(definition.Kind, definition);
            }

            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
                if (!result.ContainsKey(kind))
                    throw new InvalidOperationException($"{kind} 아이템 정의가 없다.");
            return result;
        }

        private static ItemKind[] BuildAllKinds()
        {
            var result = new ItemKind[Definitions.Length];
            for (int i = 0; i < Definitions.Length; i++)
                result[i] = Definitions[i].Kind;
            return result;
        }
    }

    /// <summary>던전 생성기가 배치하는 아이템 스폰 지점. 타일이 아니라 타일 위의 오브젝트다.</summary>
    public readonly struct ItemSpawn
    {
        public readonly GridPos Position;
        public readonly ItemKind Kind;

        public ItemSpawn(GridPos position, ItemKind kind)
        {
            Position = position;
            Kind = kind;
        }

        public override string ToString() => $"{Kind}@{Position}";
    }

    /// <summary>
    /// 종류별 수량과 선택적 격자 용량을 함께 관리한다.
    /// 기본 생성자는 테스트/창고용 무제한, 크기를 지정하면 실제 백팩 용량을 적용한다.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<ItemKind, int> _counts = new Dictionary<ItemKind, int>();
        private readonly int _columns;
        private readonly int _rows;

        public bool IsBounded => _columns > 0 && _rows > 0;
        public int Columns => _columns;
        public int Rows => _rows;

        public Inventory()
        {
        }

        public Inventory(int columns, int rows)
        {
            if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            _columns = columns;
            _rows = rows;
        }

        public int Count(ItemKind kind) => _counts.TryGetValue(kind, out int count) ? count : 0;

        public int Add(ItemKind kind, int amount = 1)
        {
            AddUpTo(kind, amount);
            return Count(kind);
        }

        /// <summary>요청 수량 전체가 들어갈 때만 추가한다.</summary>
        public bool TryAdd(ItemKind kind, int amount, out int totalCount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int previous = Count(kind);
            _counts[kind] = previous + amount;
            if (IsBounded && !BackpackRules.TryCreateLayout(this, _columns, _rows, out _))
            {
                RestoreCount(kind, previous);
                totalCount = previous;
                return false;
            }

            totalCount = previous + amount;
            return true;
        }

        public bool TryAdd(ItemKind kind, out int totalCount) => TryAdd(kind, 1, out totalCount);

        /// <summary>공간이 허용하는 만큼만 추가하고 실제 추가된 개수를 반환한다.</summary>
        public int AddUpTo(ItemKind kind, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            if (!IsBounded)
            {
                int next = Count(kind) + amount;
                _counts[kind] = next;
                return amount;
            }

            // 전량을 한 번에 시도한다. 충전이 도입되면서 amount 가 배로 늘었는데
            // TryAdd 는 호출마다 격자를 통째로 다시 팩하므로, 성공하는 경우(대부분)에
            // 팩 횟수가 amount 배가 되는 것을 막는다. 실패할 때만 아래 루프로 폴백한다.
            if (TryAdd(kind, amount, out _)) return amount;

            int added = 0;
            while (added < amount && TryAdd(kind, out _))
                added++;
            return added;
        }

        public BackpackLayout CreateLayout()
        {
            if (!IsBounded)
                throw new InvalidOperationException("무제한 인벤토리에는 백팩 레이아웃이 없다.");
            if (!BackpackRules.TryCreateLayout(this, _columns, _rows, out BackpackLayout layout))
                throw new InvalidOperationException("현재 인벤토리 수량을 백팩에 배치할 수 없다.");
            return layout;
        }

        public bool TryUse(ItemKind kind)
        {
            int count = Count(kind);
            if (count <= 0) return false;
            _counts[kind] = count - 1;
            return true;
        }

        public void Clear() => _counts.Clear();

        private void RestoreCount(ItemKind kind, int count)
        {
            if (count <= 0)
                _counts.Remove(kind);
            else
                _counts[kind] = count;
        }
    }

}
