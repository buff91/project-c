using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public static partial class DungeonGenerator
    {

        private static void CarveFloor(GridMap map, FloorPlan p, int height)
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
                    for (int x = p.BranchMinX; x <= p.BranchMaxX; x++)
                    for (int y = p.BranchMinY; y <= p.BranchMaxY; y++)
                        p.SecretRoomTiles.Add(new GridPos(x, y, p.BaseElevation));
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

            if (p.Up.HasValue) map.Set(p.Up.Value, TileKind.StairsUp);
            if (p.Down.HasValue) map.Set(p.Down.Value, TileKind.StairsDown);
        }

        private static void PlaceHoleAndWeakFloor(
            GridMap map,
            DungeonHeightModel heightModel,
            Random random,
            FloorPlan p,
            GridPos? holeAbove,
            int bottomElevation)
        {
            var candidates = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                var pos = new GridPos(x, y, p.BaseElevation);
                // 윗층 구멍의 착지 칸을 다시 뚫으면 두 층을 관통하게 된다.
                if (holeAbove.HasValue && holeAbove.Value.x == x && holeAbove.Value.y == y)
                    continue;
                // 복도에서 방으로 들어오는 입구 칸은 막지 않는다.
                if (x == p.VerticalX && y == p.UpperMinY)
                    continue;
                if (map.Get(pos)?.kind != TileKind.Floor)
                    continue;
                if (!LandsOneFloorBelow(map, heightModel, pos, bottomElevation, p.FloorIndex))
                    continue;
                candidates.Add(pos);
            }

            if (candidates.Count == 0) return;

            GridPos hole = candidates[random.Next(candidates.Count)];
            map.Set(hole, TileKind.Hole);
            p.Hole = hole;

            // 약한 바닥: 구멍 옆에 두어 M4 붕괴 때 같은 규칙으로 아래층에 떨어지게 한다.
            var weakOptions = new List<GridPos>();
            foreach (GridPos n in new[] { hole.North, hole.East, hole.South, hole.West })
            {
                if (n.x == p.VerticalX && n.y == p.UpperMinY) continue;
                // 윗층 구멍의 착지 칸을 약한 바닥으로 바꾸면 낙하가 그 층을 뚫고
                // 두 층을 관통한다(약한 바닥은 IsSolidGround 가 아니다).
                if (holeAbove.HasValue && holeAbove.Value.x == n.x && holeAbove.Value.y == n.y)
                    continue;
                if (map.Get(n)?.kind != TileKind.Floor) continue;
                if (!LandsOneFloorBelow(map, heightModel, n, bottomElevation, p.FloorIndex)) continue;
                weakOptions.Add(n);
            }
            if (weakOptions.Count > 0)
                map.Set(weakOptions[random.Next(weakOptions.Count)], TileKind.WeakFloor);
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

        /// <summary>
        /// B4·B7 같은 중간 쉼표 층에 휴식처를 하나 둔다. 막다른 분기 방을 우선하고,
        /// 없으면 북쪽 방을 사용한다. 타일 종류는 Floor를 유지해 프롭/규칙을 지형과 분리한다.
        /// </summary>
        private static void PlaceRestSite(
            GridMap map,
            Random random,
            FloorPlan p,
            int floorCount)
        {
            int depth = -p.FloorIndex;
            if (!DungeonRestRules.ShouldPlace(depth, floorCount)) return;

            var candidates = new List<GridPos>();
            if (p.HasBranch && !p.BranchIsSecret)
            {
                for (int x = p.BranchMinX; x <= p.BranchMaxX; x++)
                for (int y = p.BranchMinY; y <= p.BranchMaxY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (map.Get(pos)?.kind == TileKind.Floor)
                        candidates.Add(pos);
                }
            }

            if (candidates.Count == 0)
            {
                for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
                for (int y = p.UpperMinY; y < p.RaisedY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (map.Get(pos)?.kind == TileKind.Floor)
                        candidates.Add(pos);
                }
            }

            if (candidates.Count > 0)
                p.RestSite = candidates[random.Next(candidates.Count)];
        }
    }
}
