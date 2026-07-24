using System;

namespace ProjectC.Core
{
    /// <summary>동일한 피해 처리 위에 얹는 시각·촉각 계열. 전투 판정과는 분리한다.</summary>
    public enum CombatImpactKind
    {
        Physical = 0,
        Fire = 1,
        Frost = 2,
        Heavy = 3
    }

    /// <summary>
    /// 전투 연출의 데이터 단일 출처. Gameplay는 이 값을 색·스프라이트·애니메이션으로 번역한다.
    /// 문자열 source는 기존 사망 원인/전투 로그와 공유하므로 여기서 한 번만 분류한다.
    /// </summary>
    public static class CombatPresentationRules
    {
        public static CombatImpactKind ImpactForSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return CombatImpactKind.Physical;

            string normalized = source.ToLowerInvariant();
            if (normalized.Contains("frost") || normalized.Contains("freeze"))
                return CombatImpactKind.Frost;
            if (normalized.Contains("burn") || normalized.Contains("bomb") ||
                normalized.Contains("fire") || normalized.Contains("explosion"))
                return CombatImpactKind.Fire;
            if (normalized.Contains("fall") || normalized.Contains("crush") ||
                normalized.Contains("knockback"))
                return CombatImpactKind.Heavy;
            return CombatImpactKind.Physical;
        }

        public static int FlashPulses(CombatImpactKind kind) =>
            kind == CombatImpactKind.Heavy ? 3 : 2;

        public static int BurstRayCount(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire: return 10;
                case CombatImpactKind.Frost: return 8;
                case CombatImpactKind.Heavy: return 12;
                default: return 6;
            }
        }

        public static float ShakeStrength(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire: return 0.045f;
                case CombatImpactKind.Frost: return 0.025f;
                case CombatImpactKind.Heavy: return 0.065f;
                default: return 0.032f;
            }
        }

        public static string StatusCue(StatusKind kind, StatusApplyResult result)
        {
            if (result == StatusApplyResult.CancelledOpposite)
                return kind == StatusKind.Burn ? "THAWED" : "QUENCHED";
            string label = kind == StatusKind.Burn ? "BURN" : "FROZEN";
            return result == StatusApplyResult.Refreshed ? $"{label} +" : label;
        }
    }
}
