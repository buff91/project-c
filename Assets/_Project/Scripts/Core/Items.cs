using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// M1 아이템 최소 셋: 물약(회복), 폭탄(광역 피해 + 약한 바닥 붕괴). (GDD §5.6, §5.8)
    /// </summary>
    public enum ItemKind
    {
        Potion = 0,        // 회복 물약
        Bomb = 1,          // 폭탄: 3×3 피해 + 화상 + 넉백, 약한 바닥 붕괴·폭발통 유폭.
        FrostBomb = 2,     // 냉기 폭탄: 낮은 피해 + 빙결. 불이 아니라 폭발통은 유폭하지 않는다.
        OilFlask = 3,      // 기름 병: 3×3 기름 살포. 불 폭발과 겹치면 발화 → 화상.
        ThrowingKnife = 4, // 투척 단검: 소모형 단일 대상 원거리 피해.
        RecallScroll = 5,  // 귀환 두루마리: 현재 층 입구로 순간이동.
        CoinPouch = 6,     // 전리품: 생환 시 골드로 환산. 던전 안에서는 쓸 수 없다.
        Gemstone = 7,      // 전리품(중): 생환 시 골드로 환산.
        Relic = 8,         // 전리품(대): 희귀. 생환 시 골드로 환산.
        Herb = 9,          // 조합 재료: 약초. 2개로 회복 물약을 만든다.
        BlastPowder = 10,  // 조합 재료: 화약. 2개로 폭탄을 만든다.
        FrostShard = 11,   // 조합 재료: 서리 수정. 폭탄에 섞어 냉기 폭탄을 만든다.
        // 장비 (대장간 제작 — 스탯이 아니라 행동 규칙을 바꾼다. EquipmentCatalog 참조)
        PipeSpear = 12,    // 긴 파이프: 근접 사거리 2(직선).
        HeavyWrench = 13,  // 대형 렌치: 근접 명중 시 1칸 넉백.
        SignShield = 14,   // 표지판 방패: 받는 물리 피해 -1. 2×2 점유.
        PaddedBoots = 15,  // 완충 부츠: 안전 낙하 높이 +2.
        CannedFood = 16    // 통조림: 배고픔을 채운다. (HungerRules)
    }

    /// <summary>아이템 표시 정보의 단일 출처 — 인벤토리/HUD 가 여기서 이름·설명을 읽는다.</summary>
    public static class ItemCatalog
    {
        public static readonly ItemKind[] AllKinds =
        {
            ItemKind.Potion, ItemKind.Bomb, ItemKind.FrostBomb,
            ItemKind.OilFlask, ItemKind.ThrowingKnife, ItemKind.RecallScroll,
            ItemKind.CoinPouch, ItemKind.Gemstone, ItemKind.Relic,
            ItemKind.Herb, ItemKind.BlastPowder, ItemKind.FrostShard,
            ItemKind.PipeSpear, ItemKind.HeavyWrench, ItemKind.SignShield, ItemKind.PaddedBoots,
            ItemKind.CannedFood
        };

        /// <summary>생환 시 골드 환산 가치. 0 이면 소모품(창고 보관 대상).</summary>
        public static int GoldValue(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.CoinPouch: return 10;
                case ItemKind.Gemstone: return 25;
                case ItemKind.Relic: return 60;
                default: return 0;
            }
        }

        /// <summary>전리품(환금 전용) 여부. 던전 안에서는 사용 불가.</summary>
        public static bool IsTreasure(ItemKind kind) => GoldValue(kind) > 0;

        /// <summary>상점 구매가. 0 이면 비매품(전리품은 팔지 않는다 — 파밍으로만).</summary>
        public static int ShopPrice(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return 15;
                case ItemKind.Bomb: return 20;
                case ItemKind.FrostBomb: return 15;
                case ItemKind.OilFlask: return 10;
                case ItemKind.ThrowingKnife: return 10;
                case ItemKind.RecallScroll: return 25;
                case ItemKind.CannedFood: return 12;
                // 조합 재료 — 재료로 만드는 쪽이 완제품 구매보다 싸게 유지한다.
                case ItemKind.Herb: return 6;
                case ItemKind.BlastPowder: return 8;
                case ItemKind.FrostShard: return 5;
                default: return 0;
            }
        }

        /// <summary>
        /// 긴 단위명이나 G 대신 픽셀 HUD에서 즉시 읽히는 달러 기호를 접두사로 사용한다.
        /// HUD·상점·정산 화면이 모두 "$120" 형식을 공유한다.
        /// </summary>
        public static string FormatGold(int amount) => $"${amount}";

        /// <summary>조합 재료 여부. 사용 불가 — 조합 화면에서만 소비된다.</summary>
        public static bool IsMaterial(ItemKind kind) =>
            kind == ItemKind.Herb || kind == ItemKind.BlastPowder || kind == ItemKind.FrostShard;

        public static string DisplayName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return "회복 물약";
                case ItemKind.Bomb: return "폭탄";
                case ItemKind.FrostBomb: return "냉기 폭탄";
                case ItemKind.OilFlask: return "기름 병";
                case ItemKind.ThrowingKnife: return "투척 단검";
                case ItemKind.RecallScroll: return "귀환 두루마리";
                case ItemKind.CoinPouch: return "동전 주머니";
                case ItemKind.Gemstone: return "보석";
                case ItemKind.Relic: return "고대 유물";
                case ItemKind.Herb: return "약초";
                case ItemKind.BlastPowder: return "화약";
                case ItemKind.FrostShard: return "서리 수정";
                case ItemKind.PipeSpear: return "긴 파이프";
                case ItemKind.HeavyWrench: return "대형 렌치";
                case ItemKind.SignShield: return "표지판 방패";
                case ItemKind.PaddedBoots: return "완충 부츠";
                case ItemKind.CannedFood: return "통조림";
                default: return kind.ToString();
            }
        }

        /// <summary>HUD/피드백용 짧은 영문 라벨. 픽셀 폭이 좁은 액션 문구에서 KR 이름 대신 쓴다.</summary>
        public static string ShortLabel(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return "POTION";
                case ItemKind.Bomb: return "BOMB";
                case ItemKind.FrostBomb: return "FROST";
                case ItemKind.OilFlask: return "OIL";
                case ItemKind.ThrowingKnife: return "KNIFE";
                case ItemKind.RecallScroll: return "SCROLL";
                case ItemKind.CoinPouch: return "COIN";
                case ItemKind.Gemstone: return "GEM";
                case ItemKind.Relic: return "RELIC";
                case ItemKind.Herb: return "HERB";
                case ItemKind.BlastPowder: return "POWDER";
                case ItemKind.FrostShard: return "SHARD";
                case ItemKind.PipeSpear: return "SPEAR";
                case ItemKind.HeavyWrench: return "WRENCH";
                case ItemKind.SignShield: return "SHIELD";
                case ItemKind.PaddedBoots: return "BOOTS";
                case ItemKind.CannedFood: return "FOOD";
                default: return kind.ToString();
            }
        }

        public static string Description(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion:
                    return "HP를 회복한다. 마시는 데 행동 1회를 소비한다.";
                case ItemKind.Bomb:
                    return "3×3 폭발 — 화상·넉백, 약한 바닥 붕괴와 폭발통 유폭. 본인도 피해를 입는다.";
                case ItemKind.FrostBomb:
                    return "낮은 피해의 3×3 냉기 폭발 — 맞은 대상을 빙결시킨다. 폭발통은 터뜨리지 않는다.";
                case ItemKind.OilFlask:
                    return "3×3 범위에 기름을 뿌린다. 불 폭발이 닿으면 발화해 위에 있는 모두가 불탄다.";
                case ItemKind.ThrowingKnife:
                    return "적 하나에게 강한 원거리 피해를 준다. 소모품 — 시야선이 필요하다.";
                case ItemKind.RecallScroll:
                    return "현재 층의 입구로 순간이동한다. 행동 1회를 소비한다.";
                case ItemKind.CoinPouch:
                    return "생환하면 소지금 $10을 얻는다. 죽으면 잃는다.";
                case ItemKind.Gemstone:
                    return "생환하면 소지금 $25을 얻는다. 죽으면 잃는다.";
                case ItemKind.Relic:
                    return "깊은 층의 희귀한 유물. 생환하면 소지금 $60을 얻는다.";
                case ItemKind.CannedFood:
                    return "먹으면 배고픔을 크게 채운다. 먹는 데 행동 1회를 소비한다.";
                case ItemKind.PipeSpear:
                case ItemKind.HeavyWrench:
                case ItemKind.SignShield:
                case ItemKind.PaddedBoots:
                    // 장비 설명의 단일 출처는 EquipmentCatalog — 여기서 중복 정의하지 않는다.
                    return EquipmentCatalog.ForItem(kind)?.Description ?? kind.ToString();
                case ItemKind.Herb:
                    return "조합 재료. 2개를 빻으면 회복 물약이 된다.";
                case ItemKind.BlastPowder:
                    return "조합 재료. 2개를 뭉치면 폭탄이 된다.";
                case ItemKind.FrostShard:
                    return "조합 재료. 폭탄에 섞으면 냉기 폭탄이 된다.";
                default: return "";
            }
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
