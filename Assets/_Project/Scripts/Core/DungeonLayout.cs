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
        public IReadOnlyList<GridPos> EnemySpawns { get; }
        public IReadOnlyList<ItemSpawn> Items { get; }
        public IReadOnlyList<GridPos> Doors { get; }

        /// <summary>첫 번째 적 스폰. (단일 적을 쓰던 호출부 호환용 축약)</summary>
        public GridPos EnemySpawn => EnemySpawns[0];

        public DungeonFloorInfo(
            int floorIndex,
            GridPos entry,
            GridPos? upStairs,
            GridPos? downStairs,
            GridPos? hole,
            IReadOnlyList<GridPos> enemySpawns,
            IReadOnlyList<ItemSpawn> items,
            IReadOnlyList<GridPos> doors)
        {
            if (enemySpawns == null || enemySpawns.Count == 0)
                throw new ArgumentException("층마다 적 스폰이 하나 이상 필요합니다.", nameof(enemySpawns));

            FloorIndex = floorIndex;
            Entry = entry;
            UpStairs = upStairs;
            DownStairs = downStairs;
            Hole = hole;
            EnemySpawns = enemySpawns;
            Items = items ?? Array.Empty<ItemSpawn>();
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
    /// 다층 던전 생성기. 방–복도–문 연결 그래프와 층간 샤프트 규칙은 유지한 채
    /// 방 크기/위치·복도·문·내부 계단·구멍·막다른 분기 방을 seed로 변형한다.
    /// 같은 seed 는 항상 같은 던전을 만든다.
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
            var random = new Random(seed);

            // 1) 층 골격을 계획하고 새긴다. 아래층 북쪽 방은 윗층 북쪽 방과 기둥이
            //    겹치도록 제약해 구멍 착지 후보가 항상 남게 한다.
            var plans = new List<FloorPlan>(floorCount);
            for (int depth = 0; depth < floorCount; depth++)
            {
                FloorPlan previous = depth > 0 ? plans[depth - 1] : null;
                FloorPlan plan = PlanFloor(random, width, height, depth, floorCount, heightModel, previous);
                CarveFloor(map, plan, height);
                plans.Add(plan);
            }

            for (int i = 0; i < plans.Count - 1; i++)
                map.Connect(plans[i].Down.Value, plans[i + 1].Up.Value, bidirectional: true);

            // 2) 구멍은 모든 층이 새겨진 뒤에야 "정확히 한 층 아래에 착지하는" 칸을 고를 수 있다.
            int bottomElevation = heightModel.Elevation(-(floorCount - 1));
            for (int depth = 0; depth < floorCount - 1; depth++)
            {
                GridPos? holeAbove = depth > 0 ? plans[depth - 1].Hole : null;
                PlaceHoleAndWeakFloor(map, heightModel, random, plans[depth], holeAbove, bottomElevation);
            }

            // 3) 적·아이템 스폰은 구멍·계단이 확정된 최종 타일 상태에서 고른다.
            foreach (FloorPlan plan in plans)
            {
                PickEnemySpawns(map, random, plan);
                PlaceItems(map, random, plan);
            }

            var floors = new List<DungeonFloorInfo>(floorCount);
            foreach (FloorPlan plan in plans)
            {
                floors.Add(new DungeonFloorInfo(
                    plan.FloorIndex,
                    plan.Entry,
                    plan.Up,
                    plan.Down,
                    plan.Hole,
                    plan.EnemySpawns,
                    plan.Items,
                    plan.Doors));
            }

            return new DungeonLayout(heightModel, floors);
        }

        /// <summary>층 하나의 골격 치수를 seed 로 뽑는다. 방 최소 폭/간격 제약은 범위로 보장한다.</summary>
        private static FloorPlan PlanFloor(
            Random random,
            int width,
            int height,
            int depth,
            int floorCount,
            DungeonHeightModel heightModel,
            FloorPlan previous)
        {
            var p = new FloorPlan
            {
                Width = width,
                FloorIndex = -depth
            };
            p.BaseElevation = heightModel.Elevation(p.FloorIndex);

            // 남쪽 두 방: 입구 방(남서)과 동쪽 방(남동). 사이에 1칸 이상 복도 공간을 남긴다.
            p.LeftMaxX = 3 + random.Next(0, Math.Max(1, width - 9));
            p.RightMinX = random.Next(p.LeftMaxX + 2, width - 2);
            p.LowerMaxY = 3 + random.Next(0, Math.Max(1, height - 8));

            // 북쪽 방. 동쪽 방(rows 0..LowerMaxY)과 행 간격을 두어 문을 우회하는 인접을 막는다.
            p.UpperMinY = random.Next(p.LowerMaxY + 2, height - 3);
            int upperMinCap = Math.Min(
                p.RightMinX - 2,
                previous != null ? previous.UpperMaxX - 1 : int.MaxValue);
            p.UpperMinX = random.Next(1, Math.Max(2, upperMinCap));
            int upperMaxFloor = Math.Max(
                Math.Max(p.UpperMinX + 3, p.RightMinX),
                previous != null ? previous.UpperMinX + 2 : 0);
            p.UpperMaxX = random.Next(upperMaxFloor, width - 1);

            p.RaisedY = height - 2;
            p.StairX = random.Next(p.UpperMinX, p.UpperMaxX + 1);
            p.HorizontalY = random.Next(1, p.LowerMaxY + 1);
            p.VerticalX = random.Next(p.RightMinX, Math.Min(p.UpperMaxX, width - 2) + 1);

            p.Doors.Add(new GridPos(p.LeftMaxX + 1, p.HorizontalY, p.BaseElevation));
            p.Doors.Add(new GridPos(p.VerticalX, p.LowerMaxY + 1, p.BaseElevation));

            // 확률적 막다른 분기 방: 북서쪽 빈 공간이 충분할 때만 문 하나로 매달린다.
            bool wantBranch = random.Next(0, 2) == 1;
            int branchDoorCap = Math.Min(p.LeftMaxX, p.UpperMinX - 2);
            if (wantBranch && p.UpperMinX >= 3 && branchDoorCap >= 0)
            {
                p.HasBranch = true;
                p.BranchDoorX = random.Next(0, branchDoorCap + 1);
                p.BranchMinX = Math.Max(0, p.BranchDoorX - 1);
                p.BranchMaxX = Math.Min(p.UpperMinX - 2, p.BranchMinX + 1 + random.Next(0, 2));
                p.BranchMinY = p.LowerMaxY + 2;
                p.BranchMaxY = Math.Min(height - 2, p.BranchMinY + 1 + random.Next(0, 2));
                p.Doors.Add(new GridPos(p.BranchDoorX, p.LowerMaxY + 1, p.BaseElevation));
            }

            // 층간 링크는 같은 x/y의 수직 샤프트를 공유한다.
            // 중간층에서는 좌·우 샤프트를 번갈아 써 Up/Down이 한 칸에 겹치지 않게 한다.
            int upX = (depth - 1) % 2 == 0 ? width - 2 : 1;
            int downX = depth % 2 == 0 ? width - 2 : 1;
            p.Up = depth == 0 ? (GridPos?)null : p.At(upX, 1);
            p.Down = depth == floorCount - 1 ? (GridPos?)null : p.At(downX, 1);
            p.Entry = p.Up ?? p.At(1, 1);
            return p;
        }

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
            }

            foreach (GridPos door in p.Doors) map.Set(door, TileKind.DoorClosed);

            // 북쪽 방의 뒤쪽을 한 단 올리고 계단으로 연결한다.
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.RaisedY; y < height; y++)
            {
                map.Remove(new GridPos(x, y, p.BaseElevation));
                map.Set(new GridPos(x, y, p.BaseElevation + 1), TileKind.Floor);
            }
            map.Set(new GridPos(p.StairX, p.RaisedY - 1, p.BaseElevation), TileKind.Stairs);

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
        /// 적 스폰은 문 뒤(북쪽 방)에만 둔다 — 입구·동쪽 방에 두면 층 진입 즉시 인접 전투가
        /// 강제되고, "문을 열기 전에는 차단" 불변식도 깨진다. 수는 깊이에 따라 1~4.
        /// </summary>
        private static void PickEnemySpawns(GridMap map, Random random, FloorPlan p)
        {
            var candidates = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                if (x == p.VerticalX && y == p.UpperMinY) continue;
                var pos = new GridPos(x, y, p.BaseElevation);
                if (map.Get(pos)?.kind == TileKind.Floor)
                    candidates.Add(pos);
            }

            int depth = -p.FloorIndex;
            int desired = 1 + random.Next(0, 2) + depth / 2;
            p.EnemySpawns.AddRange(TakeRandom(candidates, desired, random));

            // 하행 계단 경비병: 완주 동선(남쪽 방→하행 계단)이 전투를 완전히
            // 우회하지 못하게 한다. 계단 인접(체비셰프 3)에 배치해 카이팅 거리를 줄이고,
            // 수는 1+depth 로 깊을수록 무거운 관문. 샤프트 교대 규칙상 하행 방은 도착
            // 방과 항상 다르고 입구에서 수평 문 뒤라 "문 열기 전 차단" 불변식 유지.
            // (밸런스 시뮬 600판 근거 — 직행 정책 완주율 94%)
            if (p.Down.HasValue)
            {
                bool eastRoom = p.Down.Value.x != 1;
                int guardMinX = eastRoom ? p.RightMinX : 0;
                int guardMaxX = eastRoom ? p.Width - 1 : p.LeftMaxX;
                var guardPool = new List<GridPos>();
                for (int x = guardMinX; x <= guardMaxX; x++)
                for (int y = 0; y <= p.LowerMaxY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (pos == p.Entry) continue;
                    if (pos.ChebyshevTo(p.Down.Value) > 3) continue;
                    if (map.Get(pos)?.kind != TileKind.Floor) continue;
                    if (p.EnemySpawns.Contains(pos)) continue;
                    guardPool.Add(pos);
                }
                p.EnemySpawns.AddRange(TakeRandom(guardPool, 1 + depth, random));
            }

            // 북쪽 방 바닥이 전부 특수 타일로 채워지는 경우는 없지만, 방어적으로 높은 단을 쓴다.
            if (p.EnemySpawns.Count == 0)
                p.EnemySpawns.Add(p.At(p.StairX, p.RaisedY));
        }

        /// <summary>
        /// 아이템 스폰. 막다른 분기 방이 있으면 보상 아이템 하나를 보장하고,
        /// 나머지는 북쪽·동쪽 방의 빈 바닥에 1~2개 흩뿌린다. 적 스폰과는 겹치지 않는다.
        /// </summary>
        private static void PlaceItems(GridMap map, Random random, FloorPlan p)
        {
            ItemKind RollKind()
            {
                int roll = random.Next(0, 10);
                if (roll < 4) return ItemKind.Potion;
                if (roll < 8) return ItemKind.Bomb;
                return ItemKind.FrostBomb;
            }

            bool IsFree(GridPos pos) =>
                map.Get(pos)?.kind == TileKind.Floor &&
                pos != p.Entry &&
                !p.EnemySpawns.Contains(pos);

            if (p.HasBranch)
            {
                var branchTiles = new List<GridPos>();
                for (int x = p.BranchMinX; x <= p.BranchMaxX; x++)
                for (int y = p.BranchMinY; y <= p.BranchMaxY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (IsFree(pos)) branchTiles.Add(pos);
                }
                foreach (GridPos pos in TakeRandom(branchTiles, 1, random))
                    p.Items.Add(new ItemSpawn(pos, RollKind()));
            }

            var scatter = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                if (x == p.VerticalX && y == p.UpperMinY) continue;
                var pos = new GridPos(x, y, p.BaseElevation);
                if (IsFree(pos)) scatter.Add(pos);
            }
            for (int x = p.RightMinX; x < p.Width; x++)
            for (int y = 0; y <= p.LowerMaxY; y++)
            {
                var pos = new GridPos(x, y, p.BaseElevation);
                if (IsFree(pos)) scatter.Add(pos);
            }

            int scatterCount = 1 + random.Next(0, 2);
            foreach (GridPos pos in TakeRandom(scatter, scatterCount, random))
                p.Items.Add(new ItemSpawn(pos, RollKind()));
        }

        /// <summary>후보 목록에서 서로 다른 위치를 최대 count개 뽑는다. 목록은 소모된다.</summary>
        private static List<GridPos> TakeRandom(List<GridPos> pool, int count, Random random)
        {
            var result = new List<GridPos>(Math.Min(count, pool.Count));
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = random.Next(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        /// <summary>층 하나의 골격 치수. Carve/Hole/Spawn 단계가 공유한다.</summary>
        private sealed class FloorPlan
        {
            public int Width;
            public int FloorIndex;
            public int BaseElevation;
            public int LeftMaxX;
            public int RightMinX;
            public int LowerMaxY;
            public int UpperMinX;
            public int UpperMaxX;
            public int UpperMinY;
            public int HorizontalY;
            public int VerticalX;
            public int StairX;
            public int RaisedY;
            public bool HasBranch;
            public int BranchDoorX;
            public int BranchMinX;
            public int BranchMaxX;
            public int BranchMinY;
            public int BranchMaxY;
            public GridPos? Up;
            public GridPos? Down;
            public GridPos? Hole;
            public GridPos Entry;
            public readonly List<GridPos> EnemySpawns = new List<GridPos>();
            public readonly List<ItemSpawn> Items = new List<ItemSpawn>();
            public readonly List<GridPos> Doors = new List<GridPos>();

            public GridPos At(int x, int y)
            {
                bool raised = y >= RaisedY && x >= UpperMinX && x <= UpperMaxX;
                return new GridPos(x, y, raised ? BaseElevation + 1 : BaseElevation);
            }
        }
    }
}
