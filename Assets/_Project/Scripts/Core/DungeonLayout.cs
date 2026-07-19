using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public sealed class DungeonFloorInfo
    {
        public int FloorIndex { get; }
        public GridPos Entry { get; }
        public GridPos? UpStairs { get; }
        public GridPos? DownStairs { get; }
        public GridPos? Hole { get; }
        public GridPos EnemySpawn { get; }
        public IReadOnlyList<GridPos> Doors { get; }

        public DungeonFloorInfo(
            int floorIndex,
            GridPos entry,
            GridPos? upStairs,
            GridPos? downStairs,
            GridPos? hole,
            GridPos enemySpawn,
            IReadOnlyList<GridPos> doors)
        {
            FloorIndex = floorIndex;
            Entry = entry;
            UpStairs = upStairs;
            DownStairs = downStairs;
            Hole = hole;
            EnemySpawn = enemySpawn;
            Doors = doors ?? Array.Empty<GridPos>();
        }
    }

    public sealed class DungeonLayout
    {
        private readonly Dictionary<int, DungeonFloorInfo> _byFloor =
            new Dictionary<int, DungeonFloorInfo>();

        public DungeonHeightModel Height { get; }
        public IReadOnlyList<DungeonFloorInfo> Floors { get; }
        public GridPos Entry => Floors[0].Entry;
        public int TopFloorIndex => Floors[0].FloorIndex;
        public int BottomFloorIndex => Floors[Floors.Count - 1].FloorIndex;

        public DungeonLayout(DungeonHeightModel height, List<DungeonFloorInfo> floors)
        {
            Height = height ?? throw new ArgumentNullException(nameof(height));
            if (floors == null || floors.Count == 0)
                throw new ArgumentException("던전은 한 층 이상이어야 합니다.", nameof(floors));

            Floors = floors;
            foreach (DungeonFloorInfo floor in floors)
                _byFloor.Add(floor.FloorIndex, floor);
        }

        public bool TryGetFloor(int floorIndex, out DungeonFloorInfo floor) =>
            _byFloor.TryGetValue(floorIndex, out floor);
    }

    /// <summary>
    /// 검증용 다층 던전 생성기. 각 층을 세 방 + 두 복도 + 문으로 구성하고
    /// 높은 단·일반 계단·구멍·층간 계단을 함께 배치한다.
    /// </summary>
    public static class DungeonGenerator
    {
        public static DungeonLayout Generate(
            GridMap map,
            int width,
            int height,
            int floorCount,
            int elevationsPerFloor = 4,
            int seed = 1977)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (width < 9) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 9) throw new ArgumentOutOfRangeException(nameof(height));
            if (floorCount < 1) throw new ArgumentOutOfRangeException(nameof(floorCount));

            map.Clear();
            var heightModel = new DungeonHeightModel(elevationsPerFloor);
            var floors = new List<DungeonFloorInfo>(floorCount);
            var random = new Random(seed);

            for (int depth = 0; depth < floorCount; depth++)
            {
                int floorIndex = -depth;
                int baseElevation = heightModel.Elevation(floorIndex);
                int leftMaxX = Math.Max(3, width / 3);
                int rightMinX = width - Math.Max(4, width / 3 + 1);
                int lowerMaxY = Math.Max(3, height / 3);
                int upperMinY = height - Math.Max(4, height / 3 + 1);
                int upperMinX = Math.Max(1, width / 2 - 2);
                int upperMaxX = Math.Min(width - 2, upperMinX + 4);
                int horizontalY = Math.Min(2, lowerMaxY - 1);
                int verticalX = Math.Min(upperMaxX, rightMinX + 1);

                void SetBase(int x, int y, TileKind kind = TileKind.Floor) =>
                    map.Set(new GridPos(x, y, baseElevation), kind);

                void CarveRect(int minX, int minY, int maxX, int maxY)
                {
                    for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        SetBase(x, y);
                }

                // 입구 방, 동쪽 방, 북쪽 방.
                CarveRect(0, 0, leftMaxX, lowerMaxY);
                CarveRect(rightMinX, 0, width - 1, lowerMaxY + 1);
                CarveRect(upperMinX, upperMinY, upperMaxX, height - 1);

                // 1칸 폭 복도. 목에 문을 놓아 방 단위 탐험과 FOV 차단이 가능하다.
                for (int x = leftMaxX + 1; x < rightMinX; x++) SetBase(x, horizontalY);
                for (int y = lowerMaxY + 2; y < upperMinY; y++) SetBase(verticalX, y);
                SetBase(verticalX, lowerMaxY + 1);

                var doors = new List<GridPos>(2)
                {
                    new GridPos(leftMaxX + 1, horizontalY, baseElevation),
                    new GridPos(verticalX, lowerMaxY + 1, baseElevation)
                };
                foreach (GridPos door in doors) map.Set(door, TileKind.DoorClosed);

                // 북쪽 방의 뒤쪽을 한 단 올리고 중앙 계단으로 연결한다.
                int raisedY = height - 2;
                for (int x = upperMinX; x <= upperMaxX; x++)
                for (int y = raisedY; y < height; y++)
                {
                    map.Remove(new GridPos(x, y, baseElevation));
                    map.Set(new GridPos(x, y, baseElevation + 1), TileKind.Floor);
                }
                int internalStairX = (upperMinX + upperMaxX) / 2;
                map.Set(new GridPos(internalStairX, raisedY - 1, baseElevation), TileKind.Stairs);

                GridPos At(int x, int y)
                {
                    int elevation = y >= raisedY && x >= upperMinX && x <= upperMaxX
                        ? baseElevation + 1
                        : baseElevation;
                    return new GridPos(x, y, elevation);
                }

                // 층간 링크는 같은 x/y의 수직 샤프트를 공유한다.
                // 중간층에서는 좌·우 샤프트를 번갈아 써 Up/Down이 한 칸에 겹치지 않게 한다.
                int upX = (depth - 1) % 2 == 0 ? width - 2 : 1;
                int downX = depth % 2 == 0 ? width - 2 : 1;
                GridPos? up = depth == 0 ? (GridPos?)null : At(upX, 1);
                GridPos? down = depth == floorCount - 1 ? (GridPos?)null : At(downX, 1);
                GridPos entry = up ?? At(1, 1);

                if (up.HasValue) map.Set(up.Value, TileKind.StairsUp);
                if (down.HasValue) map.Set(down.Value, TileKind.StairsDown);

                GridPos? hole = null;
                if (depth < floorCount - 1)
                {
                    int holeX = upperMinX + 1 + random.Next(0, Math.Max(1, upperMaxX - upperMinX - 1));
                    int holeY = upperMinY + 1;
                    hole = new GridPos(holeX, holeY, baseElevation);
                    map.Set(hole.Value, TileKind.Hole);

                    var weak = new GridPos(Math.Min(upperMaxX, holeX + 1), holeY, baseElevation);
                    map.Set(weak, TileKind.WeakFloor);
                }

                floors.Add(new DungeonFloorInfo(
                    floorIndex,
                    entry,
                    up,
                    down,
                    hole,
                    At(upperMaxX, upperMinY + 1),
                    doors));
            }

            for (int i = 0; i < floors.Count - 1; i++)
            {
                GridPos down = floors[i].DownStairs.Value;
                GridPos up = floors[i + 1].UpStairs.Value;
                map.Connect(down, up, bidirectional: true);
            }

            return new DungeonLayout(heightModel, floors);
        }
    }
}
