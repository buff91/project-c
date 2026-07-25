using System;

namespace ProjectC.Core
{
    public enum HungerStage
    {
        /// <summary>배부름 — 아무 일도 일어나지 않는다.</summary>
        Fed = 0,
        /// <summary>배고픔 — 아직 피해는 없지만 식량을 찾아야 한다는 신호.</summary>
        Hungry = 1,
        /// <summary>굶주림 — 주기적으로 HP가 깎인다.</summary>
        Starving = 2
    }

    /// <summary>
    /// 배고픔 수치의 단일 출처. (SPD 계보의 부드러운 시계)
    ///
    /// 목적은 **탐색을 죽이지 않으면서 무한 캠핑과 무한 왕복을 막는 것**이다. 그래서
    /// 하드 타이머(제한 턴 안에 탈출)가 아니라 자원으로 관리하는 압박으로 둔다 —
    /// 파밍이 기둥 4인 이 게임에서 시간 제한은 탐색·조합·숨은 방을 통째로 죽인다.
    /// 굶어도 즉사시키지 않고 천천히 깎아 "해결하라"는 신호만 준다.
    ///
    /// 수치는 실플레이 전 임시다. 기준: 한 층 정리에 40~60턴을 가정하면 **가득 찬 배가
    /// 두 층을 채 못 버틴다** — 판마다 한두 번 먹는 게 아니라 중간중간 자주 먹는 리듬이다.
    /// 그래서 한 통이 배를 다 채우지 않고, 통조림을 흔하게 뿌린다.
    /// </summary>
    public static class HungerRules
    {
        public const int MaxSatiation = 100;

        /// <summary>
        /// 이 값 아래면 <see cref="HungerStage.Hungry"/> — **경고만 한다**.
        /// 나중에 이 단계에 별도 상태이상(집중력 저하 등)을 붙일 자리다. HP는 건드리지 않는다.
        /// </summary>
        public const int HungryThreshold = 30;

        /// <summary>굶주림 상태에서 이 턴 수마다 <see cref="StarvingDamage"/> 만큼 깎인다.</summary>
        public const int StarvingDamageInterval = 5;
        public const int StarvingDamage = 1;

        /// <summary>통조림 하나가 채우는 양. 배를 가득 채우지 못한다 — 자주 먹게 만드는 값.</summary>
        public const int RationSatiation = 60;

        public static HungerStage StageFor(int satiation)
        {
            if (satiation <= 0) return HungerStage.Starving;
            return satiation < HungryThreshold ? HungerStage.Hungry : HungerStage.Fed;
        }

        public static string Label(HungerStage stage)
        {
            switch (stage)
            {
                case HungerStage.Starving: return "굶주림";
                case HungerStage.Hungry: return "배고픔";
                default: return "포만";
            }
        }
    }

    /// <summary>
    /// 한 판의 배고픔 상태. 순수 데이터라 체크포인트에 그대로 실린다.
    /// 던전 체인으로 넘어가도 이어진다 — 모닥불에서 쉬어도 배는 채워지지 않는다.
    /// </summary>
    [Serializable]
    public sealed class HungerState
    {
        public int satiation = HungerRules.MaxSatiation;

        /// <summary>굶주린 채로 보낸 턴 — 피해 주기를 세는 카운터.</summary>
        public int starvingTurns;

        public HungerStage Stage => HungerRules.StageFor(satiation);

        /// <summary>
        /// 턴 경과. 배가 남아 있으면 줄이고, 0이면 주기마다 피해를 낸다(반환값 = 이번 턴 피해).
        /// </summary>
        public int Tick(int turns = 1)
        {
            if (turns <= 0) return 0;

            if (satiation > 0)
            {
                satiation = Math.Max(0, satiation - turns);
                if (satiation > 0)
                {
                    starvingTurns = 0;
                    return 0;
                }
            }

            starvingTurns += turns;
            if (starvingTurns < HungerRules.StarvingDamageInterval) return 0;

            starvingTurns -= HungerRules.StarvingDamageInterval;
            return HungerRules.StarvingDamage;
        }

        /// <summary>먹는다. 실제로 채운 양을 반환한다(이미 가득 차 있으면 0).</summary>
        public int Feed(int amount)
        {
            if (amount <= 0) return 0;

            int before = satiation;
            satiation = Math.Min(HungerRules.MaxSatiation, satiation + amount);
            if (satiation > 0) starvingTurns = 0;
            return satiation - before;
        }

        public HungerState Clone() =>
            new HungerState { satiation = satiation, starvingTurns = starvingTurns };
    }
}
