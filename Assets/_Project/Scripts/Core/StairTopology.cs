namespace ProjectC.Core
{
    /// <summary>
    /// 같은 층 내부의 계단 타일과 한 단 높은 착지 타일 사이의 연결을 해석한다.
    /// </summary>
    public static class StairTopology
    {
        private static readonly (int x, int y)[] Directions =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        public static bool TryGetHigherLanding(GridMap map, GridPos stairs, out GridPos landing)
        {
            landing = default;
            if (map == null || map.Get(stairs)?.kind != TileKind.Stairs)
                return false;

            foreach ((int x, int y) direction in Directions)
            {
                var candidate = new GridPos(
                    stairs.x + direction.x,
                    stairs.y + direction.y,
                    stairs.elevation + 1);
                if (!map.IsWalkable(candidate)) continue;

                landing = candidate;
                return true;
            }

            return false;
        }
    }
}
