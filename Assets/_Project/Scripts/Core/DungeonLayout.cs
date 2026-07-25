using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public sealed class DungeonFloorInfo
    {
        /// <summary>elevation 을 던전 층으로 묶은 <b>공간</b> 구획. 진행 순서와 무관하다.</summary>
        public int FloorIndex { get; }

        /// <summary>
        /// 몇 번째로 방문하는 층인가(0부터). 난이도·콘텐츠 규칙의 유일한 키다 —
        /// 휴식처·탈출구·장비 드랍·숨은 방·적 혼합·구간 변주·보스가 모두 이 값을 쓴다.
        /// <para>
        /// <b>elevation 에서 파생하지 않는다.</b> 던전은 상승·하강·평면이 모두 가능하고
        /// 한 던전 안에서 올라갔다 떨어지는 경로도 가능하므로(GDD §5.1),
        /// <c>1F→3F→2F→5F</c> 같은 경로에서는 고도로 역산할 방법이 없다.
        /// 생성기가 경로를 깔면서 부여한다.
        /// </para>
        /// </summary>
        public int ProgressIndex { get; }

        public GridPos Entry { get; }
        public GridPos? UpStairs { get; }
        public GridPos? DownStairs { get; }
        public GridPos? Hole { get; }
        public GridPos? RestSite { get; }
        public GridPos? ExtractionPoint { get; }
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
            int progressIndex,
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
            IReadOnlyList<GridPos> windows = null,
            GridPos? extractionPoint = null)
        {
            // 던전 생성기는 층마다 적을 보장하지만, 허브 캠프처럼 적 없는 층도 허용한다.
            FloorIndex = floorIndex;
            ProgressIndex = progressIndex < 0 ? 0 : progressIndex;
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
            ExtractionPoint = extractionPoint;
        }
    }

    public sealed class DungeonLayout
    {
        private readonly Dictionary<int, DungeonFloorInfo> _byFloor =
            new Dictionary<int, DungeonFloorInfo>();

        public DungeonHeightModel Height { get; }

        /// <summary>진행 순서(ProgressIndex 오름차순)로 정렬된 층 목록.</summary>
        public IReadOnlyList<DungeonFloorInfo> Floors { get; }
        public GridPos Entry => Floors[0].Entry;

        /// <summary>
        /// <b>공간</b> 최상단 층 인덱스. FOV·입력 픽킹의 elevation 상한처럼
        /// "가장 높은 곳"이 필요한 곳에서만 쓴다 — 진행과 무관하다.
        /// </summary>
        public int TopFloorIndex { get; }

        /// <summary>
        /// <b>공간</b> 최하단 층 인덱스. 낙하 바닥·elevation 하한용이며 진행과 무관하다.
        /// "마지막 층"을 원하면 <see cref="FinalFloorIndex"/>를 쓴다.
        /// </summary>
        public int BottomFloorIndex { get; }

        /// <summary>
        /// <b>진행</b> 최종 층(보스·출구가 있는 곳). 하강 던전에서는 우연히
        /// <see cref="BottomFloorIndex"/>와 같지만, 상승·비단조 던전에서는 다르다.
        /// </summary>
        public int FinalFloorIndex => Floors[Floors.Count - 1].FloorIndex;

        /// <summary>마지막 진행 지수. 층 수 - 1.</summary>
        public int MaxProgressIndex => Floors[Floors.Count - 1].ProgressIndex;

        public DungeonLayout(DungeonHeightModel height, List<DungeonFloorInfo> floors)
        {
            Height = height ?? throw new ArgumentNullException(nameof(height));
            if (floors == null || floors.Count == 0)
                throw new ArgumentException("던전은 한 층 이상이어야 합니다.", nameof(floors));

            Floors = floors;
            int top = floors[0].FloorIndex;
            int bottom = floors[0].FloorIndex;
            foreach (DungeonFloorInfo floor in floors)
            {
                _byFloor.Add(floor.FloorIndex, floor);
                if (floor.FloorIndex > top) top = floor.FloorIndex;
                if (floor.FloorIndex < bottom) bottom = floor.FloorIndex;
            }

            // 공간 극단은 목록 순서가 아니라 실제 최대/최소로 구한다. 하강 던전에서는
            // 목록 순서와 같지만, 상승·비단조 던전에서는 목록 순서가 진행 순서라 다르다.
            TopFloorIndex = top;
            BottomFloorIndex = bottom;
        }

        public bool TryGetFloor(int floorIndex, out DungeonFloorInfo floor) =>
            _byFloor.TryGetValue(floorIndex, out floor);

        /// <summary>
        /// 층의 진행 지수를 찾는다. 난이도·구간 판정은 반드시 이 값을 거쳐야 하며
        /// elevation 이나 floorIndex 부호로 역산해서는 안 된다.
        /// </summary>
        public bool TryGetProgressIndex(int floorIndex, out int progressIndex)
        {
            if (_byFloor.TryGetValue(floorIndex, out DungeonFloorInfo floor))
            {
                progressIndex = floor.ProgressIndex;
                return true;
            }

            progressIndex = 0;
            return false;
        }

        /// <summary>모르는 층은 0(첫 층)으로 본다 — 허브처럼 층이 하나뿐인 레이아웃 포함.</summary>
        public int ProgressIndexFor(int floorIndex) =>
            TryGetProgressIndex(floorIndex, out int progress) ? progress : 0;
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
                PlaceEquipment(map, random, plan);
                PlaceExtractionPoint(map, plan, floorCount);
                PlaceBossLandmark(map, plan, floorCount);
                PlaceCatwalk(map, plan, floorCount);
                PlaceWindows(map, heightModel, plan, bottomElevation);
            }

            // plans 는 진행 순서대로 쌓인다(depth 0 = 첫 층). 진행 지수는 여기서 확정되며
            // 이후 어디서도 elevation 으로 다시 계산하지 않는다.
            var floors = new List<DungeonFloorInfo>(floorCount);
            for (int progress = 0; progress < plans.Count; progress++)
            {
                FloorPlan plan = plans[progress];
                floors.Add(new DungeonFloorInfo(
                    plan.FloorIndex,
                    progress,
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
                    plan.Windows,
                    plan.ExtractionPoint));
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
            public GridPos? ExtractionPoint;
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
