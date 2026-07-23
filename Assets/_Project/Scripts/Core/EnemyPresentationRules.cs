using System;

namespace ProjectC.Core
{
    /// <summary>
    /// FOV에 종속되는 적 전투 피드백과 턴 기반 시체 수명을 결정한다.
    /// 전투 결과 자체와 플레이어에게 공개할 정보를 분리하기 위한 순수 규칙이다.
    /// </summary>
    public static class EnemyPresentationRules
    {
        public static bool ShouldShowFeedback(
            bool debugAll,
            int enemyFloorIndex,
            int activeFloorIndex,
            bool tileVisible)
        {
            return debugAll ||
                   enemyFloorIndex == activeFloorIndex && tileVisible;
        }

        public static bool IsCorpseExpired(
            int currentTurn,
            int deathTurn,
            int lifetimeTurns)
        {
            if (deathTurn < 0) return false;
            int lifetime = Math.Max(1, lifetimeTurns);
            return currentTurn - deathTurn >= lifetime;
        }

        public static float CorpseAlpha(
            int currentTurn,
            int deathTurn,
            int lifetimeTurns)
        {
            int lifetime = Math.Max(1, lifetimeTurns);
            int elapsed = deathTurn < 0 ? 0 : Math.Max(0, currentTurn - deathTurn);
            float remaining = Math.Max(0, lifetime - elapsed) / (float)lifetime;
            return 0.2f + 0.5f * remaining;
        }
    }
}
