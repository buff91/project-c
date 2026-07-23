using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 허브 캠프의 고정 레이아웃. 걸어다니는 로비 — 상인/영웅/창고/포탈이 맵 위에 있다.
    /// 기존 던전 렌더러를 그대로 태우기 위해 DungeonLayout(층 1개) 형태로 만든다.
    /// </summary>
    public static class HubLayout
    {
        public const int Width = 9;
        public const int Height = 7;

        public static readonly GridPos Entry = new GridPos(4, 0, 0);
        public static readonly GridPos Portal = new GridPos(4, 6, 0);
        public static readonly GridPos Campfire = new GridPos(4, 3, 0);
        public static readonly GridPos Merchant = new GridPos(1, 4, 0);
        public static readonly GridPos Stash = new GridPos(7, 4, 0);

        /// <summary>HeroRoster.All 순서와 짝을 이룬다.</summary>
        public static readonly IReadOnlyList<GridPos> HeroPositions = new[]
        {
            new GridPos(2, 2, 0),
            new GridPos(4, 1, 0),
            new GridPos(6, 2, 0)
        };

        /// <summary>
        /// 선택 영웅은 플레이어로 캠프에 서 있으므로 대기 위치에서는 숨긴다.
        /// 선택이 바뀌면 이전 영웅은 다시 자신의 대기 위치에 나타난다.
        /// </summary>
        public static bool ShouldShowHeroAtRosterPosition(string heroId, string selectedHeroId)
        {
            if (string.IsNullOrEmpty(heroId)) return false;
            return heroId != HeroRoster.ById(selectedHeroId).Id;
        }

        public static DungeonLayout Build(GridMap map)
        {
            map.Clear();
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);

            var floor = new DungeonFloorInfo(
                0,
                Entry,
                upStairs: null,
                downStairs: null,
                hole: null,
                enemySpawns: new List<GridPos>(),
                items: new List<ItemSpawn>(),
                doors: new List<GridPos>());

            return new DungeonLayout(new DungeonHeightModel(4), new List<DungeonFloorInfo> { floor });
        }
    }
}
