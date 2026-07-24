using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 대장간이 파는 영구 장비 강화 한 종류. 순수 데이터다 (Unity 비의존).
    /// 장착 무기 시스템을 새로 만들지 않고, 영웅 기본 스탯 위에 얹는 유한 티어 강화로
    /// GDD §11 의 "영구 스탯 크리프 경계"를 지킨다 — 티어를 낮게 캡한다.
    /// </summary>
    public sealed class SmithyUpgrade
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int MaxTier { get; }
        public int AttackPerTier { get; }
        public int MaxHpPerTier { get; }
        public int RangedPerTier { get; }

        private readonly int _baseCost;
        private readonly int _costStep;

        public SmithyUpgrade(
            string id,
            string displayName,
            string description,
            int maxTier,
            int baseCost,
            int costStep,
            int attackPerTier = 0,
            int maxHpPerTier = 0,
            int rangedPerTier = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 는 비어 있을 수 없다.", nameof(id));
            if (maxTier <= 0) throw new ArgumentOutOfRangeException(nameof(maxTier));
            if (baseCost < 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            if (costStep < 0) throw new ArgumentOutOfRangeException(nameof(costStep));

            Id = id;
            DisplayName = displayName;
            Description = description;
            MaxTier = maxTier;
            _baseCost = baseCost;
            _costStep = costStep;
            AttackPerTier = attackPerTier;
            MaxHpPerTier = maxHpPerTier;
            RangedPerTier = rangedPerTier;
        }

        /// <summary>tier(1..MaxTier) 로 올리는 비용. 범위를 벗어나면 0.</summary>
        public int CostForTier(int tier)
        {
            if (tier < 1 || tier > MaxTier) return 0;
            return _baseCost + (tier - 1) * _costStep;
        }
    }

    public enum SmithyPurchaseResult
    {
        Success = 0,
        MaxedOut = 1,
        InsufficientGold = 2,
        UnknownUpgrade = 3
    }

    /// <summary>
    /// 대장간 강화의 단일 출처. 수치는 밸런스 시뮬/플레이테스트로 튜닝할 자리표시값이다
    /// (근접 3 기준 +2 는 크므로 캡을 낮게 유지 — GDD §11 경고 준수).
    /// </summary>
    public static class SmithyRules
    {
        public static readonly IReadOnlyList<SmithyUpgrade> All = new[]
        {
            new SmithyUpgrade(
                "weapon", "무기 연마", "근접 피해가 티어마다 +1. 대장장이가 날을 벼린다.",
                maxTier: 2, baseCost: 60, costStep: 70, attackPerTier: 1),
            new SmithyUpgrade(
                "armor", "방어구 보강", "최대 HP가 티어마다 +2. 죽지 않고 더 깊이 내려간다.",
                maxTier: 2, baseCost: 45, costStep: 55, maxHpPerTier: 2),
            new SmithyUpgrade(
                "tools", "도구 정비", "원거리·투척 피해가 티어마다 +1. 사냥꾼과 궁합이 좋다.",
                maxTier: 2, baseCost: 50, costStep: 60, rangedPerTier: 1)
        };

        public static SmithyUpgrade ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SmithyUpgrade upgrade in All)
                if (upgrade.Id == id) return upgrade;
            return null;
        }

        public static int TierOf(MetaSaveData meta, string id)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            return meta.GetSmithyTier(id);
        }

        public static bool IsMaxed(MetaSaveData meta, string id)
        {
            SmithyUpgrade upgrade = ById(id);
            return upgrade != null && meta.GetSmithyTier(id) >= upgrade.MaxTier;
        }

        /// <summary>다음 티어 비용. 이미 최대면 0.</summary>
        public static int NextTierCost(MetaSaveData meta, string id)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            SmithyUpgrade upgrade = ById(id);
            if (upgrade == null) return 0;
            int tier = meta.GetSmithyTier(id);
            return tier >= upgrade.MaxTier ? 0 : upgrade.CostForTier(tier + 1);
        }

        /// <summary>골드가 충분하면 한 티어 올린다. 상점 구매 공통 경로.</summary>
        public static SmithyPurchaseResult TryPurchase(MetaSaveData meta, string id)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            SmithyUpgrade upgrade = ById(id);
            if (upgrade == null) return SmithyPurchaseResult.UnknownUpgrade;

            int tier = meta.GetSmithyTier(id);
            if (tier >= upgrade.MaxTier) return SmithyPurchaseResult.MaxedOut;

            int cost = upgrade.CostForTier(tier + 1);
            if (!meta.TrySpend(cost)) return SmithyPurchaseResult.InsufficientGold;

            meta.SetSmithyTier(id, tier + 1);
            return SmithyPurchaseResult.Success;
        }

        // ── 적용 헬퍼: 영웅 기본 스탯 위에 강화를 더한다 ──────────────

        public static int EffectiveAttack(HeroArchetype hero, MetaSaveData meta) =>
            RequireHero(hero).Attack + Bonus(meta, u => u.AttackPerTier);

        public static int EffectiveMaxHp(HeroArchetype hero, MetaSaveData meta) =>
            RequireHero(hero).MaxHp + Bonus(meta, u => u.MaxHpPerTier);

        public static int EffectiveRangedDamage(HeroArchetype hero, MetaSaveData meta) =>
            RequireHero(hero).RangedDamage + Bonus(meta, u => u.RangedPerTier);

        private static HeroArchetype RequireHero(HeroArchetype hero) =>
            hero ?? throw new ArgumentNullException(nameof(hero));

        private static int Bonus(MetaSaveData meta, Func<SmithyUpgrade, int> perTier)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            int total = 0;
            foreach (SmithyUpgrade upgrade in All)
                total += perTier(upgrade) * meta.GetSmithyTier(upgrade.Id);
            return total;
        }
    }
}
