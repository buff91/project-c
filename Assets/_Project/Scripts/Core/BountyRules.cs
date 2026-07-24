using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>의뢰 완료를 판정할 계측 축. 전부 RunTelemetry 누적값에 매핑된다.</summary>
    public enum BountyMetric
    {
        Kills = 0,
        BossKills = 1,
        DeepestDepth = 2,
        EnemyFalls = 3,
        IntentionalFalls = 4,
        BurnApplications = 5,
        FreezeApplications = 6,
        OilIgnited = 7,
        WaterFrozen = 8,
        SecretRoomsFound = 9,
        BarrelPushes = 10
    }

    /// <summary>허브 의뢰 게시판의 계약 한 건. 순수 데이터다 (Unity 비의존).</summary>
    public sealed class BountyDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public BountyMetric Metric { get; }
        public int Target { get; }
        public int RewardGold { get; }

        public BountyDefinition(
            string id,
            string displayName,
            string description,
            BountyMetric metric,
            int target,
            int rewardGold)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 는 비어 있을 수 없다.", nameof(id));
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            if (rewardGold < 0) throw new ArgumentOutOfRangeException(nameof(rewardGold));

            Id = id;
            DisplayName = displayName;
            Description = description;
            Metric = metric;
            Target = target;
            RewardGold = rewardGold;
        }
    }

    /// <summary>한 의뢰의 정산 결과.</summary>
    public sealed class BountyClaim
    {
        public BountyDefinition Bounty;
        public int Progress;
        public bool Completed;
        public int RewardGold; // 지급된 골드 (미완료면 0)
    }

    public sealed class BountyClaimResult
    {
        public readonly List<BountyClaim> Claims = new List<BountyClaim>();
        public int CompletedCount;
        public int TotalReward;
    }

    /// <summary>
    /// 의뢰 게시판 규칙의 단일 출처. 완료 판정 백엔드는 RunTelemetry 이므로
    /// 여기서는 목표 정의·시드 선택·텔레메트리 대조·보상 지급만 한다.
    /// 보상은 살아 나갈 때(생환/승리)만 정산한다 — 계약은 무사 귀환이 조건이다.
    /// 목표 수치는 밸런스/플레이테스트로 튜닝할 자리표시값이다.
    /// </summary>
    public static class BountyRules
    {
        public const int OfferCount = 3;

        public static readonly IReadOnlyList<BountyDefinition> Pool = new[]
        {
            new BountyDefinition(
                "cull", "떠도는 위협 소탕",
                "이번 원정에서 적 12마리를 처치한다.",
                BountyMetric.Kills, target: 12, rewardGold: 40),
            new BountyDefinition(
                "leap", "높은 곳에서",
                "낙하를 이용해 3번 아래층으로 뛰어내린다.",
                BountyMetric.IntentionalFalls, target: 3, rewardGold: 35),
            new BountyDefinition(
                "chasm", "구렁으로 밀어라",
                "적을 4번 구멍·허공으로 떨어뜨린다.",
                BountyMetric.EnemyFalls, target: 4, rewardGold: 45),
            new BountyDefinition(
                "pyre", "불의 세례",
                "적에게 화상을 6번 부여한다.",
                BountyMetric.BurnApplications, target: 6, rewardGold: 35),
            new BountyDefinition(
                "frost", "얼어붙은 사냥",
                "적을 6번 빙결시킨다.",
                BountyMetric.FreezeApplications, target: 6, rewardGold: 35),
            new BountyDefinition(
                "wildfire", "기름과 불꽃",
                "기름에 불을 붙여 6칸 이상 태운다.",
                BountyMetric.OilIgnited, target: 6, rewardGold: 40),
            new BountyDefinition(
                "hoarfrost", "서릿발",
                "물 웅덩이를 6칸 이상 얼린다.",
                BountyMetric.WaterFrozen, target: 6, rewardGold: 40),
            new BountyDefinition(
                "descent", "깊이 내려가라",
                "B5(깊이 5층)까지 도달한다.",
                BountyMetric.DeepestDepth, target: 4, rewardGold: 50),
            new BountyDefinition(
                "seeker", "숨겨진 보물",
                "숨은 방 하나를 찾아낸다.",
                BountyMetric.SecretRoomsFound, target: 1, rewardGold: 60),
            new BountyDefinition(
                "demolition", "폭발통 술사",
                "폭발통을 3번 밀어 활용한다.",
                BountyMetric.BarrelPushes, target: 3, rewardGold: 35),
            new BountyDefinition(
                "warden", "묘지기 사냥",
                "최심층 보스 묘지기를 처치한다.",
                BountyMetric.BossKills, target: 1, rewardGold: 100)
        };

        public static BountyDefinition ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (BountyDefinition bounty in Pool)
                if (bounty.Id == id) return bounty;
            return null;
        }

        /// <summary>계측 축의 현재 진행값을 텔레메트리에서 읽는다.</summary>
        public static int ReadMetric(BountyMetric metric, RunTelemetry telemetry)
        {
            if (telemetry == null) return 0;
            switch (metric)
            {
                case BountyMetric.Kills: return telemetry.kills;
                case BountyMetric.BossKills: return telemetry.bossKills;
                // deepestFloorIndex 는 0,-1,-2… 이므로 부호를 뒤집어 도달 깊이(B1=0)로 만든다.
                case BountyMetric.DeepestDepth: return -telemetry.deepestFloorIndex;
                case BountyMetric.EnemyFalls: return telemetry.enemyFalls;
                case BountyMetric.IntentionalFalls: return telemetry.intentionalFalls;
                case BountyMetric.BurnApplications: return telemetry.burnApplications;
                case BountyMetric.FreezeApplications: return telemetry.freezeApplications;
                case BountyMetric.OilIgnited: return telemetry.oilIgnitedTiles;
                case BountyMetric.WaterFrozen: return telemetry.waterFrozenTiles;
                case BountyMetric.SecretRoomsFound: return telemetry.secretRoomsFound;
                case BountyMetric.BarrelPushes: return telemetry.barrelPushes;
                default: return 0;
            }
        }

        public static int Progress(BountyDefinition bounty, RunTelemetry telemetry)
        {
            if (bounty == null) throw new ArgumentNullException(nameof(bounty));
            return ReadMetric(bounty.Metric, telemetry);
        }

        public static bool IsComplete(BountyDefinition bounty, RunTelemetry telemetry) =>
            Progress(bounty, telemetry) >= bounty.Target;

        /// <summary>seed 로 중복 없이 count 개를 고른다. 같은 seed = 같은 의뢰.</summary>
        public static List<BountyDefinition> SelectOffers(int seed, int count)
        {
            var pool = new List<BountyDefinition>(Pool);
            var rng = new Random(seed);
            int take = Math.Min(Math.Max(0, count), pool.Count);

            // 부분 Fisher-Yates: 앞 take 칸만 확정하면 충분하다.
            for (int i = 0; i < take; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                BountyDefinition tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            return pool.GetRange(0, take);
        }

        public static List<BountyDefinition> SelectOffers(int seed) => SelectOffers(seed, OfferCount);

        /// <summary>seed 로 고른 의뢰를 메타의 활성 목록에 세팅하고 그 목록을 돌려준다.</summary>
        public static List<BountyDefinition> AssignOffers(MetaSaveData meta, int seed, int count)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            List<BountyDefinition> offers = SelectOffers(seed, count);
            var ids = new string[offers.Count];
            for (int i = 0; i < offers.Count; i++) ids[i] = offers[i].Id;
            meta.activeBountyIds = ids;
            return offers;
        }

        public static List<BountyDefinition> AssignOffers(MetaSaveData meta, int seed) =>
            AssignOffers(meta, seed, OfferCount);

        public static bool HasActiveBounties(MetaSaveData meta) =>
            meta != null && meta.activeBountyIds != null && meta.activeBountyIds.Length > 0;

        public static List<BountyDefinition> ActiveBounties(MetaSaveData meta)
        {
            var list = new List<BountyDefinition>();
            if (meta?.activeBountyIds == null) return list;
            foreach (string id in meta.activeBountyIds)
            {
                BountyDefinition bounty = ById(id);
                if (bounty != null) list.Add(bounty);
            }
            return list;
        }

        /// <summary>
        /// 활성 의뢰를 텔레메트리로 평가하고, 완료분 보상을 meta.gold 에 지급한다.
        /// 정산 후 활성 목록은 비운다 (완료·미완료 모두 만료 — 허브에서 다시 받는다).
        /// 호출부(Gameplay)가 MetaStore.Save 로 영속화한다.
        /// </summary>
        public static BountyClaimResult Settle(MetaSaveData meta, RunTelemetry telemetry)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            var result = new BountyClaimResult();
            foreach (BountyDefinition bounty in ActiveBounties(meta))
            {
                int progress = ReadMetric(bounty.Metric, telemetry);
                bool completed = progress >= bounty.Target;
                int reward = completed ? bounty.RewardGold : 0;
                if (completed)
                {
                    meta.gold += reward;
                    result.CompletedCount++;
                    result.TotalReward += reward;
                }
                result.Claims.Add(new BountyClaim
                {
                    Bounty = bounty,
                    Progress = progress,
                    Completed = completed,
                    RewardGold = reward
                });
            }

            meta.activeBountyIds = new string[0];
            return result;
        }
    }
}
