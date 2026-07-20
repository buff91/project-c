using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public enum StatusKind
    {
        Burn = 0,   // 화상: 매 턴 틱 피해
        Freeze = 1  // 빙결: 해당 턴 행동 불가
    }

    /// <summary>한 번의 턴 틱 결과. 행동 파이프라인의 "틱" 단계가 소비한다.</summary>
    public readonly struct StatusTick
    {
        public readonly int BurnDamage;
        public readonly bool Frozen;

        public StatusTick(int burnDamage, bool frozen)
        {
            BurnDamage = burnDamage;
            Frozen = frozen;
        }
    }

    /// <summary>
    /// 요소 반응 테이블. (GDD §5.5 — 반응은 코드가 아니라 데이터로 확장한다)
    /// 지금은 상태끼리의 상쇄만 있고, M5+에서 타일 원소(기름/물)와의 반응이 추가된다.
    /// </summary>
    public static class StatusReactions
    {
        /// <summary>(들어오는 상태, 지워지는 기존 상태): 불↔얼음은 서로 상쇄된다.</summary>
        public static readonly (StatusKind incoming, StatusKind cancels)[] CancelPairs =
        {
            (StatusKind.Burn, StatusKind.Freeze),
            (StatusKind.Freeze, StatusKind.Burn)
        };
    }

    /// <summary>
    /// 전투 참가자 하나에 붙은 상태이상 집합. (GDD §5.5, M4)
    /// 턴 파이프라인("활성화 → 틱 → 행동")의 틱 단계에서 Tick() 한 번 호출한다.
    /// 플레이어와 몬스터가 같은 규칙을 쓴다.
    /// </summary>
    public sealed class StatusEffects
    {
        public const int BurnDamagePerTurn = 1;

        private readonly Dictionary<StatusKind, int> _remainingTurns =
            new Dictionary<StatusKind, int>();

        public bool Has(StatusKind kind) => RemainingTurns(kind) > 0;

        public int RemainingTurns(StatusKind kind) =>
            _remainingTurns.TryGetValue(kind, out int turns) ? turns : 0;

        /// <summary>
        /// 상태를 부여한다. 이미 있으면 남은 턴을 연장(최댓값)하고,
        /// 반응 테이블의 상쇄 상대는 지운다 (예: 빙결 중 화상 → 해동).
        /// </summary>
        public void Apply(StatusKind kind, int turns)
        {
            if (turns <= 0) throw new ArgumentOutOfRangeException(nameof(turns));

            foreach ((StatusKind incoming, StatusKind cancels) in StatusReactions.CancelPairs)
            {
                if (incoming == kind && Has(cancels))
                {
                    _remainingTurns.Remove(cancels);
                    return; // 상쇄가 일어나면 새 상태는 소모된 것으로 본다.
                }
            }

            _remainingTurns[kind] = Math.Max(RemainingTurns(kind), turns);
        }

        /// <summary>턴 시작 틱: 화상 피해량과 빙결 여부를 반환하고 지속 턴을 줄인다.</summary>
        public StatusTick Tick()
        {
            int burnDamage = Has(StatusKind.Burn) ? BurnDamagePerTurn : 0;
            bool frozen = Has(StatusKind.Freeze);

            foreach (StatusKind kind in new[] { StatusKind.Burn, StatusKind.Freeze })
            {
                int remaining = RemainingTurns(kind);
                if (remaining <= 0) continue;
                if (remaining == 1) _remainingTurns.Remove(kind);
                else _remainingTurns[kind] = remaining - 1;
            }

            return new StatusTick(burnDamage, frozen);
        }

        public void Clear() => _remainingTurns.Clear();
    }
}
