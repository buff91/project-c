using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 허브 캠프의 고정 레이아웃. 걸어다니는 로비 — 상인/영웅/창고/포탈이 맵 위에 있다.
    /// 기존 던전 렌더러를 그대로 태우기 위해 DungeonLayout(층 1개) 형태로 만든다.
    /// </summary>
    public static class HubLayout
    {
        // 9×7에서 확장. 시설이 서로 붙어 있어 오탭이 잦았고, 정착지라기보다 좁은 방으로 읽혔다.
        // 배치의 상대 관계(중앙 통로 = 입구→모닥불→포탈, 좌우로 시설, 앞쪽에 영웅)는 그대로 두고
        // 간격만 넓힌다. 허브 카메라는 맵 경계를 자동으로 맞추므로 크기 변경이 안전하다.
        public const int Width = 13;
        public const int Height = 9;

        public static readonly GridPos Entry = new GridPos(6, 0, 0);
        public static readonly GridPos Portal = new GridPos(6, 8, 0);
        public static readonly GridPos Campfire = new GridPos(6, 4, 0);
        public static readonly GridPos Merchant = new GridPos(2, 5, 0);
        public static readonly GridPos Stash = new GridPos(10, 5, 0);
        public static readonly GridPos Smith = new GridPos(2, 2, 0);
        public static readonly GridPos BountyBoard = new GridPos(10, 2, 0);

        /// <summary>
        /// 기록실 — 해금 조건과 진행값을 보는 곳(<see cref="ItemUnlockRules"/>).
        /// <b>항상 열려 있어야 한다</b>: 무엇을 해야 하는지 배우는 유일한 창구이고,
        /// 해금 안내를 의뢰로 줄 수 없기 때문이다(의뢰 게시판은 잠기는 시설이라 순환이 된다).
        /// 자리는 중앙 통로(x=6, 입구→모닥불→포탈)를 비켜 왼쪽 앞쪽에 둔다.
        /// </summary>
        public static readonly GridPos Codex = new GridPos(4, 6, 0);

        /// <summary>HeroRoster.All 순서와 짝을 이룬다.</summary>
        public static readonly IReadOnlyList<GridPos> HeroPositions = new[]
        {
            new GridPos(4, 2, 0),
            new GridPos(6, 1, 0),
            new GridPos(8, 2, 0)
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
                progressIndex: 0,
                Entry,
                upStairs: null,
                downStairs: null,
                hole: null,
                restSite: null,
                enemySpawns: new List<GridPos>(),
                items: new List<ItemSpawn>(),
                doors: new List<GridPos>());

            return new DungeonLayout(new DungeonHeightModel(4), new List<DungeonFloorInfo> { floor });
        }
    }
}
