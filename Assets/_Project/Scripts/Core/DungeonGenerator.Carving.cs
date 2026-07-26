using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public static partial class DungeonGenerator
    {

        private static void CarveFloor(
            GridMap map,
            FloorPlan p,
            int height,
            DungeonProgressDirection direction)
        {
            void SetBase(int x, int y, TileKind kind = TileKind.Floor) =>
                map.Set(new GridPos(x, y, p.BaseElevation), kind);

            void CarveRect(int minX, int minY, int maxX, int maxY)
            {
                for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    SetBase(x, y);
            }

            // 입구 방, 동쪽 방, 북쪽 방.
            CarveRect(0, 0, p.LeftMaxX, p.LowerMaxY);
            CarveRect(p.RightMinX, 0, p.Width - 1, p.LowerMaxY);
            CarveRect(p.UpperMinX, p.UpperMinY, p.UpperMaxX, height - 1);

            // 1칸 폭 복도. 목에 문을 놓아 방 단위 탐험과 FOV 차단이 가능하다.
            for (int x = p.LeftMaxX + 1; x < p.RightMinX; x++) SetBase(x, p.HorizontalY);
            for (int y = p.LowerMaxY + 2; y < p.UpperMinY; y++) SetBase(p.VerticalX, y);
            SetBase(p.VerticalX, p.LowerMaxY + 1);

            if (p.HasBranch)
            {
                CarveRect(p.BranchMinX, p.BranchMinY, p.BranchMaxX, p.BranchMaxY);
                SetBase(p.BranchDoorX, p.LowerMaxY + 1);
                if (p.BranchIsSecret)
                {
                    foreach (GridPos pos in p.BranchCells())
                        p.SecretRoomTiles.Add(pos);
                }
            }

            foreach (GridPos door in p.Doors) map.Set(door, TileKind.DoorClosed);
            if (p.SecretDoor.HasValue)
                map.Set(p.SecretDoor.Value, TileKind.SecretDoor);

            // 북쪽 방의 뒤쪽을 한 단 올리고 계단으로 연결한다.
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.RaisedY; y < height; y++)
            {
                map.Remove(new GridPos(x, y, p.BaseElevation));
                map.Set(new GridPos(x, y, p.BaseElevation + 1), TileKind.Floor);
            }
            map.Set(new GridPos(p.StairX, p.RaisedY - 1, p.BaseElevation), TileKind.Stairs);

            // 계단과 떨어진 위치에 사다리를 하나 더 둔다. 아래/위 발판을 모두 금색 사다리
            // 타일로 표시하고 명시적 링크로 연결해, 같은 층 높이 이동임을 데이터로도 구분한다.
            var ladderBottom = new GridPos(p.LadderX, p.RaisedY - 1, p.BaseElevation);
            var ladderTop = new GridPos(p.LadderX, p.RaisedY, p.BaseElevation + 1);
            map.Set(ladderBottom, TileKind.Ladder);
            map.Set(ladderTop, TileKind.Ladder);
            map.Connect(ladderBottom, ladderTop);

            // 캐치워크(+2단)는 밴드 길이와 아레나 여부를 알아야 하므로 배치 단계(PlaceCatwalk)에서 얹는다.

            // StairsUp/Down 은 **공간** 이름이라 고정이고, 진출·귀환 중 무엇이 되는지가
            // 방향을 탄다. 상승 던전에서는 진출 계단이 StairsUp 이다.
            if (p.Back.HasValue)
                map.Set(p.Back.Value, DungeonDirectionRules.BackStair(direction));
            if (p.Onward.HasValue)
                map.Set(p.Onward.Value, DungeonDirectionRules.OnwardStair(direction));
        }

        /// <summary>
        /// 개구부가 자랄 수 있는 최대 칸 수. <b>보장이 아니라 상한</b>이다 —
        /// 자리가 안 나오면 1칸으로 끝난다.
        /// </summary>
        private const int MaxHoleCells = 3;

        /// <summary>
        /// 층의 개구부와 그 옆 약한 바닥을 놓는다.
        /// <para>
        /// <b>왜 여러 칸인가.</b> GDD 는 층을 잇는 것을 "개구부(opening)"라고 부르는데
        /// 1칸짜리는 개구부라기보다 점이라, 정찰 창구·전술 무대·공간의 인상·지름길
        /// 네 역할을 전부 얕게 수행했다. 넉백으로 떨어뜨리는 것도 1칸이면 운에 가깝다.
        /// </para>
        /// <para>
        /// <b>2×2 를 강제하지 않는 이유.</b> 북쪽 방 밴드는 최소 크기 던전에서 아주 얕고
        /// (<c>UpperMinY..RaisedY</c>), Y축 층간 겹침은 보장되지 않는다(X축만 제약한다).
        /// 모양을 강제하면 후보가 0이 되거나 방이 잘려 도달성이 깨진다.
        /// 그래서 앵커 한 칸을 고른 뒤 <b>한 칸씩 자라며, 방이 잘리면 거기서 멈춘다</b>.
        /// </para>
        /// </summary>
        private static void PlaceHoleAndWeakFloor(
            GridMap map,
            DungeonHeightModel heightModel,
            Random random,
            FloorPlan p,
            IReadOnlyList<GridPos> holeAbove,
            int bottomElevation)
        {
            var candidates = new List<GridPos>();
            foreach (GridPos pos in p.UpperRoomCells())
            {
                if (IsHoleCandidate(map, heightModel, p, holeAbove, bottomElevation, pos))
                    candidates.Add(pos);
            }

            if (candidates.Count == 0) return;

            GridPos anchor = candidates[random.Next(candidates.Count)];
            map.Set(anchor, TileKind.Hole);
            p.HoleTiles.Add(anchor);

            // 앵커에서 이어 붙인다. 자리가 없거나 방이 잘리면 그 자리에서 멈춘다 —
            // RNG 를 더 쓰지 않으므로(결정론적 순회) 생성 스트림이 칸 수에 흔들리지 않는다.
            while (p.HoleTiles.Count < MaxHoleCells)
            {
                GridPos? grown = FindHoleGrowth(map, heightModel, p, holeAbove, bottomElevation);
                if (!grown.HasValue) break;
                map.Set(grown.Value, TileKind.Hole);
                p.HoleTiles.Add(grown.Value);
            }

            // 약한 바닥: 개구부 **둘레**에 두어 M4 붕괴 때 같은 규칙으로 아래층에 떨어지게 한다.
            var weakOptions = new List<GridPos>();
            foreach (GridPos hole in p.HoleTiles)
            foreach (GridPos n in new[] { hole.North, hole.East, hole.South, hole.West })
            {
                if (weakOptions.Contains(n)) continue;
                if (!IsHoleCandidate(map, heightModel, p, holeAbove, bottomElevation, n)) continue;
                weakOptions.Add(n);
            }
            if (weakOptions.Count > 0)
                map.Set(weakOptions[random.Next(weakOptions.Count)], TileKind.WeakFloor);
        }

        /// <summary>
        /// 이 칸을 뚫어도 되는가. 개구부와 약한 바닥이 <b>같은 조건</b>을 쓴다 —
        /// 약한 바닥은 밟으면 개구부가 되므로 기준이 갈리면 붕괴가 불변식을 깬다.
        /// </summary>
        private static bool IsHoleCandidate(
            GridMap map,
            DungeonHeightModel heightModel,
            FloorPlan p,
            IReadOnlyList<GridPos> holeAbove,
            int bottomElevation,
            GridPos pos)
        {
            // 윗층 개구부의 착지 칸을 다시 뚫으면 두 층을 관통하게 된다.
            // 윗층 개구부가 여러 칸이므로 **집합 전체**와 비교해야 한다.
            foreach (GridPos above in holeAbove)
                if (above.x == pos.x && above.y == pos.y) return false;
            // 복도에서 방으로 들어오는 입구 칸은 막지 않는다.
            if (p.IsUpperRoomEntrance(pos)) return false;
            if (map.Get(pos)?.kind != TileKind.Floor) return false;
            return LandsOneFloorBelow(map, heightModel, pos, bottomElevation, p.FloorIndex);
        }

        /// <summary>
        /// 개구부에 붙일 다음 칸. 결정론적 순회(북→동→남→서)로 첫 번째 안전한 칸을 고른다.
        /// <b>방이 잘리는 칸은 고르지 않는다</b> — 개구부는 걸어서 지날 수 없으므로
        /// 넓히다가 북쪽 방을 두 조각으로 나눌 수 있다.
        /// </summary>
        private static GridPos? FindHoleGrowth(
            GridMap map,
            DungeonHeightModel heightModel,
            FloorPlan p,
            IReadOnlyList<GridPos> holeAbove,
            int bottomElevation)
        {
            foreach (GridPos hole in p.HoleTiles)
            foreach (GridPos n in new[] { hole.North, hole.East, hole.South, hole.West })
            {
                if (!IsHoleCandidate(map, heightModel, p, holeAbove, bottomElevation, n)) continue;
                if (!KeepsUpperRoomConnected(map, p, n)) continue;
                return n;
            }
            return null;
        }

        /// <summary>
        /// 이 칸을 뚫어도 북쪽 방 바닥이 한 덩어리로 남는가.
        /// 방 입구에서 플러드 필 해 걸을 수 있는 칸 수가 그대로인지 본다.
        /// </summary>
        private static bool KeepsUpperRoomConnected(GridMap map, FloorPlan p, GridPos removed)
        {
            var walkable = new HashSet<GridPos>();
            foreach (GridPos pos in p.UpperRoomCells())
            {
                if (pos == removed) continue;
                if (map.Get(pos)?.IsWalkable == true) walkable.Add(pos);
            }
            if (walkable.Count == 0) return true;

            GridPos start = p.UpperRoomEntrance;
            if (!walkable.Contains(start)) return false;

            var seen = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                foreach (GridPos n in new[] { current.North, current.East, current.South, current.West })
                    if (walkable.Contains(n) && seen.Add(n)) queue.Enqueue(n);
            }

            return seen.Count == walkable.Count;
        }

        private static bool LandsOneFloorBelow(
            GridMap map,
            DungeonHeightModel heightModel,
            GridPos pos,
            int bottomElevation,
            int floorIndex)
        {
            GridPos? landing = map.FindLandingBelow(pos, bottomElevation);
            return landing.HasValue &&
                   heightModel.FloorIndex(landing.Value.elevation) == floorIndex - 1 &&
                   map.Get(landing.Value).IsWalkable;
        }

    }
}
