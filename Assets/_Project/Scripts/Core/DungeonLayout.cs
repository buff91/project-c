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
        public GridPos? RestSite { get; }
        public GridPos? Landmark { get; }
        public IReadOnlyList<GridPos> Windows { get; }
        public IReadOnlyList<GridPos> EnemySpawns { get; }
        public IReadOnlyList<ItemSpawn> Items { get; }
        public IReadOnlyList<GridPos> Doors { get; }
        public GridPos? SecretDoor { get; }
        public IReadOnlyList<GridPos> SecretRoomTiles { get; }
        public GridPos? SecretReward { get; }
        public bool HasSecretRoom => SecretDoor.HasValue;

        /// <summary>첫 번째 적 스폰. (단일 적을 쓰던 호출부 호환용 축약)</summary>
        public GridPos EnemySpawn => EnemySpawns[0];

        public DungeonFloorInfo(
            int floorIndex,
            GridPos entry,
            GridPos? upStairs,
            GridPos? downStairs,
            GridPos? hole,
            GridPos? restSite,
            IReadOnlyList<GridPos> enemySpawns,
            IReadOnlyList<ItemSpawn> items,
            IReadOnlyList<GridPos> doors,
            GridPos? secretDoor = null,
            IReadOnlyList<GridPos> secretRoomTiles = null,
            GridPos? secretReward = null,
            GridPos? landmark = null,
            IReadOnlyList<GridPos> windows = null)
        {
            // 던전 생성기는 층마다 적을 보장하지만, 허브 캠프처럼 적 없는 층도 허용한다.
            FloorIndex = floorIndex;
            Entry = entry;
            UpStairs = upStairs;
            DownStairs = downStairs;
            Hole = hole;
            RestSite = restSite;
            EnemySpawns = enemySpawns;
            Items = items ?? Array.Empty<ItemSpawn>();
            Doors = doors ?? Array.Empty<GridPos>();
            SecretDoor = secretDoor;
            SecretRoomTiles = secretRoomTiles ?? Array.Empty<GridPos>();
            SecretReward = secretReward;
            Landmark = landmark;
            Windows = windows ?? Array.Empty<GridPos>();
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
    public static partial class DungeonGenerator
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
            HashSet<int> secretDepths = PickSecretDepths(random, floorCount);

            // 1) 층 골격을 계획하고 새긴다. 아래층 북쪽 방은 윗층 북쪽 방과 기둥이
            //    겹치도록 제약해 구멍 착지 후보가 항상 남게 한다.
            var plans = new List<FloorPlan>(floorCount);
            for (int depth = 0; depth < floorCount; depth++)
            {
                FloorPlan previous = depth > 0 ? plans[depth - 1] : null;
                FloorPlan plan = PlanFloor(
                    random,
                    width,
                    height,
                    depth,
                    floorCount,
                    heightModel,
                    previous,
                    secretDepths.Contains(depth));
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
                PlaceRestSite(map, random, plan, floorCount);
                PlacePuddle(map, random, plan);
                PickEnemySpawns(map, random, plan, floorCount);
                PlaceItems(map, random, plan);
                PlaceBossLandmark(map, plan, floorCount);
                PlaceCatwalk(map, plan, floorCount);
                PlaceWindows(map, heightModel, plan, bottomElevation);
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
                    plan.RestSite,
                    plan.EnemySpawns,
                    plan.Items,
                    plan.Doors,
                    plan.SecretDoor,
                    plan.SecretRoomTiles,
                    plan.SecretReward,
                    plan.Landmark,
                    plan.Windows));
            }

            return new DungeonLayout(heightModel, floors);
        }

        /// <summary>
        /// 면적 비례 스폰 보정. 기준 11×11(=121)에서 0, 약 60칸 늘 때마다 +1 —
        /// 층을 키웠을 때 방이 텅 비지 않게 적/아이템 밀도를 따라 올린다.
        /// </summary>
        public static int AreaSpawnBonus(int width, int height) =>
            Math.Max(0, (width * height - 121) / 60);

        private static HashSet<int> PickSecretDepths(Random random, int floorCount)
        {
            int candidateCount = floorCount > 1 ? floorCount - 1 : floorCount;
            int desired = Math.Min(SecretRoomRules.DesiredCount(floorCount), candidateCount);
            var candidates = new List<int>(candidateCount);
            for (int depth = 0; depth < candidateCount; depth++)
                candidates.Add(depth);

            var selected = new HashSet<int>();
            for (int i = 0; i < desired; i++)
            {
                int index = random.Next(candidates.Count);
                selected.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            return selected;
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
            public int Height;
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
            public int LadderX;
            public int RaisedY;
            public bool HasBranch;
            public bool BranchIsSecret;
            public int BranchDoorX;
            public int BranchMinX;
            public int BranchMaxX;
            public int BranchMinY;
            public int BranchMaxY;
            public GridPos? Up;
            public GridPos? Down;
            public GridPos? Hole;
            public GridPos? RestSite;
            public GridPos? Landmark;
            public GridPos? SecretDoor;
            public GridPos? SecretReward;
            public GridPos Entry;
            public readonly List<GridPos> EnemySpawns = new List<GridPos>();
            public readonly List<ItemSpawn> Items = new List<ItemSpawn>();
            public readonly List<GridPos> Doors = new List<GridPos>();
            public readonly List<GridPos> SecretRoomTiles = new List<GridPos>();
            public readonly List<GridPos> Windows = new List<GridPos>();

            public GridPos At(int x, int y)
            {
                bool raised = y >= RaisedY && x >= UpperMinX && x <= UpperMaxX;
                return new GridPos(x, y, raised ? BaseElevation + 1 : BaseElevation);
            }
        }
    }
}
