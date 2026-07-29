using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 허브 캠프의 고정 레이아웃. 걸어다니는 로비 — 상인·창고·대장간·의뢰 게시판·기록실이
    /// 모닥불과 포탈을 끼고 맵 위에 있다(영웅 선택은 없앴다 — 원정자는 하나다).
    /// 기존 던전 렌더러를 그대로 태우기 위해 DungeonLayout(층 1개) 형태로 만든다.
    /// </summary>
    public static class HubLayout
    {
        // 9×7에서 확장. 시설이 서로 붙어 있어 오탭이 잦았고, 정착지라기보다 좁은 방으로 읽혔다.
        // 배치의 상대 관계(중앙 통로 = 입구→모닥불→포탈, 좌우로 시설)는 그대로 두고
        // 간격만 넓힌다. 허브 카메라는 던전과 같은 배율로 플레이어를 추종하므로 전체 맵을
        // 한 화면에 넣지 않는다 — 방 크기가 월드 아트 배율을 바꾸면 안 된다.
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
                holeTiles: null,
                restSite: null,
                enemySpawns: new List<GridPos>(),
                items: new List<ItemSpawn>(),
                doors: new List<GridPos>());

            return new DungeonLayout(new DungeonHeightModel(4), new List<DungeonFloorInfo> { floor });
        }
    }
}
