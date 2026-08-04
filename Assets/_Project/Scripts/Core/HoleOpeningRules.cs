using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Hole 셀 목록을 같은 층에서 4방향으로 이어진 물리 개구부 단위로 나눈다.
    /// "개구부당 라벨 하나" 같은 판정이 층 전체가 아니라 실제 개구부에 붙게 한다.
    /// </summary>
    public static class HoleOpeningRules
    {
        /// <summary>
        /// 입력 순서를 보존한 채 인접 성분(개구부)별로 묶는다.
        /// 인접 판정은 같은 elevation의 4방향 이웃이다.
        /// </summary>
        public static List<List<GridPos>> GroupOpenings(IReadOnlyList<GridPos> holeTiles)
        {
            var openings = new List<List<GridPos>>();
            if (holeTiles == null || holeTiles.Count == 0) return openings;

            var remaining = new List<GridPos>(holeTiles);
            while (remaining.Count > 0)
            {
                var opening = new List<GridPos> { remaining[0] };
                remaining.RemoveAt(0);

                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        if (!IsAdjacentToAny(opening, remaining[i])) continue;
                        opening.Add(remaining[i]);
                        remaining.RemoveAt(i);
                        grew = true;
                        i--;
                    }
                }

                openings.Add(opening);
            }

            return openings;
        }

        /// <summary>
        /// <paramref name="cell"/>이 속한 개구부의 셀들을 입력 순서대로 돌려준다.
        /// 목록에 없는 셀이면 빈 목록.
        /// </summary>
        public static List<GridPos> OpeningContaining(
            IReadOnlyList<GridPos> holeTiles, GridPos cell)
        {
            foreach (List<GridPos> opening in GroupOpenings(holeTiles))
            {
                if (opening.Contains(cell)) return opening;
            }
            return new List<GridPos>();
        }

        private static bool IsAdjacentToAny(List<GridPos> opening, GridPos candidate)
        {
            foreach (GridPos cell in opening)
            {
                if (cell.elevation == candidate.elevation &&
                    cell.ManhattanTo(candidate) == 1)
                    return true;
            }
            return false;
        }
    }
}
