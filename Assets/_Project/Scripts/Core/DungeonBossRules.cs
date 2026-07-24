using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>보스 배치와 최심층 출구 봉인을 Unity 비의존 규칙으로 유지한다.</summary>
    public static class DungeonBossRules
    {
        public static bool TrySelectSpawn(
            GridPos entry,
            IReadOnlyList<GridPos> candidates,
            out GridPos spawn)
        {
            spawn = default;
            if (candidates == null || candidates.Count == 0) return false;

            int bestDistance = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                GridPos candidate = candidates[i];
                int distance = entry.ManhattanTo(candidate);
                if (distance <= bestDistance) continue;

                bestDistance = distance;
                spawn = candidate;
            }

            return true;
        }

        public static bool CanUseExit(DungeonDefinition dungeon, bool bossDefeated)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            return dungeon.Boss == null || bossDefeated;
        }
    }
}
