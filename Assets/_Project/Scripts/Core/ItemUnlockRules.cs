using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 해금 조건 한 건. 계측 축은 <see cref="BountyMetric"/>을 재사용한다 —
    /// 의뢰와 같은 값을 읽으므로 계측 시스템을 새로 만들지 않는다.
    /// </summary>
    public sealed class ItemUnlockCondition
    {
        public ItemUnlockCondition(
            ItemKind kind,
            BountyMetric metric,
            int target,
            string requirement)
        {
            Kind = kind;
            Metric = metric;
            Target = target;
            Requirement = requirement;
        }

        public ItemKind Kind { get; }
        public BountyMetric Metric { get; }
        public int Target { get; }

        /// <summary>플레이어에게 보여줄 조건 문장. 기록실·판 종료 화면이 같은 문장을 쓴다.</summary>
        public string Requirement { get; }
    }

    /// <summary>
    /// 조건 달성 기반 도구 해금 — 한 판에서 조건을 채우면 <b>다음 판부터</b> 드랍 풀에 들어온다.
    ///
    /// <para>
    /// <b>왜 필요한가.</b> 지금은 모든 도구가 첫 판부터 나와서 판을 거듭해도 세계가 넓어지지
    /// 않는다. 죽어도 해금은 남으므로 <b>실패한 판도 전진</b>이 된다 — 그게 재도전 동력이다.
    /// </para>
    /// <para>
    /// <b>영구 스탯 강화가 아니다.</b> GDD §11이 경계한 것은 숫자를 올리는 해금이고,
    /// 여기서 늘어나는 것은 선택지다 — 같은 층에서 쓸 수 있는 수가 는다.
    /// </para>
    /// <para>
    /// <b>조건에 쓸 수 있는 계측이 제한된다(순환 금지).</b> 잠긴 도구로만 오르는 계측을
    /// 조건으로 걸면 영원히 못 연다 — <see cref="BountyMetric.FreezeApplications"/>는 냉기
    /// 폭탄/서리 수정이, <see cref="BountyMetric.OilIgnited"/>는 기름 병이,
    /// <see cref="BountyMetric.WaterFrozen"/>은 냉기가 있어야 오른다.
    /// 그래서 <see cref="StarterReachableMetrics"/>에 있는 축만 쓴다(테스트로 고정).
    /// </para>
    /// </summary>
    public static class ItemUnlockRules
    {
        /// <summary>
        /// 시작 풀(물약·폭탄·통조림·전리품·약초·화약)만으로 올릴 수 있는 계측.
        /// 화상이 들어 있는 이유는 <b>폭탄이 화상을 준다</b>는 것이다.
        /// </summary>
        public static readonly IReadOnlyList<BountyMetric> StarterReachableMetrics = new[]
        {
            BountyMetric.Kills,
            BountyMetric.BossKills,
            BountyMetric.DeepestDepth,
            BountyMetric.EnemyFalls,
            BountyMetric.IntentionalFalls,
            BountyMetric.BurnApplications,
            BountyMetric.SecretRoomsFound,
            BountyMetric.BarrelPushes
        };

        /// <summary>
        /// 도구 5종의 해금 조건. <b>표시 순서가 기록실의 칸 순서</b>이므로 중간에 끼워넣지 말고
        /// 뒤에 붙인다(플레이어가 외운 자리가 밀리지 않게).
        /// 수치는 실플레이 리포트로 조정할 값이다.
        /// </summary>
        public static readonly IReadOnlyList<ItemUnlockCondition> Conditions = new[]
        {
            // 불을 충분히 다뤘으니 반대 원소를 준다. 폭탄만으로도 오른다.
            new ItemUnlockCondition(
                ItemKind.FrostBomb, BountyMetric.BurnApplications, 12,
                "한 판에서 적에게 화상 12회"),
            // 환경을 무기로 쓴 플레이어에게 환경 도구를.
            new ItemUnlockCondition(
                ItemKind.OilFlask, BountyMetric.BarrelPushes, 3,
                "한 판에서 폭발통 3번 밀기"),
            new ItemUnlockCondition(
                ItemKind.ThrowingKnife, BountyMetric.Kills, 20,
                "한 판에서 적 20마리 처치"),
            // 탐색 성향 보상.
            new ItemUnlockCondition(
                ItemKind.RecallScroll, BountyMetric.SecretRoomsFound, 2,
                "한 판에서 숨은 방 2곳 발견"),
            new ItemUnlockCondition(
                ItemKind.FrostShard, BountyMetric.EnemyFalls, 4,
                "한 판에서 적을 4번 떨어뜨리기")
        };

        /// <summary>이 종류가 해금 게이트를 타는가.</summary>
        public static bool RequiresUnlock(ItemKind kind) => Find(kind) != null;

        /// <summary>해금 조건을 찾는다. 게이트를 타지 않는 종류면 null.</summary>
        public static ItemUnlockCondition Find(ItemKind kind)
        {
            foreach (ItemUnlockCondition condition in Conditions)
                if (condition.Kind == kind) return condition;
            return null;
        }

        /// <summary>
        /// 미해금 도구가 굴렸을 때 대신 나올 <b>가장 가까운 형제</b>.
        /// 통조림 같은 하나로 몰면 초반이 그 아이템으로 뒤덮이므로 역할이 비슷한 것으로 흩는다.
        /// </summary>
        public static ItemKind FallbackFor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.FrostBomb: return ItemKind.Bomb;
                case ItemKind.OilFlask: return ItemKind.Bomb;
                case ItemKind.ThrowingKnife: return ItemKind.Potion;
                case ItemKind.RecallScroll: return ItemKind.CannedFood;
                case ItemKind.FrostShard: return ItemKind.BlastPowder;
                default: return kind;
            }
        }

        /// <summary>이 종류가 지금 드랍 풀에 있는가(게이트를 안 타거나 이미 해금했거나).</summary>
        public static bool IsAvailable(ItemKind kind, IReadOnlyCollection<ItemKind> unlocked) =>
            !RequiresUnlock(kind) || Contains(unlocked, kind);

        /// <summary>
        /// 굴린 결과를 실제로 놓을 종류로 바꾼다. <b>롤은 호출부가 이미 소비했다</b> —
        /// 풀을 다시 짜지 않고 결과만 치환하는 이유는 RNG 스트림을 보존해
        /// "같은 seed + 같은 해금 상태 = 같은 던전"을 유지하기 위해서다.
        /// </summary>
        public static ItemKind Resolve(ItemKind rolled, IReadOnlyCollection<ItemKind> unlocked) =>
            IsAvailable(rolled, unlocked) ? rolled : FallbackFor(rolled);

        /// <summary>
        /// 이번 판 계측으로 <b>새로 열리는</b> 것들. 이미 해금한 것은 돌려주지 않으므로
        /// 두 번 불러도 중복이 생기지 않는다.
        /// </summary>
        public static List<ItemUnlockCondition> EvaluateUnlocks(
            RunTelemetry telemetry,
            IReadOnlyCollection<ItemKind> unlocked)
        {
            var opened = new List<ItemUnlockCondition>();
            if (telemetry == null) return opened;

            foreach (ItemUnlockCondition condition in Conditions)
            {
                if (Contains(unlocked, condition.Kind)) continue;
                if (BountyRules.ReadMetric(condition.Metric, telemetry) < condition.Target) continue;
                opened.Add(condition);
            }

            return opened;
        }

        /// <summary>
        /// 아직 못 연 것 중 <b>가장 가까운</b> 하나. 판 종료 화면이 다음 목표를 알리는 데 쓴다.
        /// <para>
        /// 이 안내가 필요한 이유: 해금이 막혔을 때 <b>의뢰로 안내할 수 없다</b> —
        /// 의뢰 게시판 자체가 잠기는 시설이라 순환이 된다. 그래서 잠기지 않는 경로
        /// (판 종료 화면·기록실)가 안내를 맡는다.
        /// </para>
        /// 전부 열렸으면 null.
        /// </summary>
        public static ItemUnlockCondition ClosestPending(
            RunTelemetry telemetry,
            IReadOnlyCollection<ItemKind> unlocked)
        {
            ItemUnlockCondition best = null;
            int bestRemaining = int.MaxValue;

            foreach (ItemUnlockCondition condition in Conditions)
            {
                if (Contains(unlocked, condition.Kind)) continue;

                int current = telemetry != null
                    ? BountyRules.ReadMetric(condition.Metric, telemetry)
                    : 0;
                int remaining = condition.Target - current;
                if (remaining <= 0) remaining = 0;
                if (remaining >= bestRemaining) continue;

                bestRemaining = remaining;
                best = condition;
            }

            return best;
        }

        /// <summary>기록실 표시용 — 몇 종을 열었는가.</summary>
        public static int UnlockedCount(IReadOnlyCollection<ItemKind> unlocked)
        {
            int found = 0;
            foreach (ItemUnlockCondition condition in Conditions)
                if (Contains(unlocked, condition.Kind)) found++;
            return found;
        }

        public static int TotalCount => Conditions.Count;

        private static bool Contains(IReadOnlyCollection<ItemKind> unlocked, ItemKind kind)
        {
            if (unlocked == null) return false;
            foreach (ItemKind found in unlocked)
                if (found == kind) return true;
            return false;
        }
    }
}
