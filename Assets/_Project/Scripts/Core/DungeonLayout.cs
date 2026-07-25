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

        /// <summary>
        /// 엘리베이터 통로 입구. 상승 던전의 <b>후퇴 동선</b>이며 아래로만 내려간다
        /// (<see cref="ElevatorShaftRules"/>). 하강 던전에는 없다.
        /// </summary>
        public GridPos? ElevatorShaft { get; }

        /// <summary>다른 층의 통로가 이 층으로 내려오는 착지 칸.</summary>
        public GridPos? ElevatorLanding { get; }

        /// <summary>
        /// 이 층 갇힌 방에 있는 동료의 칸. 구출하면 쉘터에 시설이 생긴다
        /// (<see cref="ShelterNpcRoster"/>). 없으면 이 층에 구출 대상이 없다.
        /// </summary>
        public GridPos? RescueNpc { get; }

        /// <summary>구출 대상 동료의 id. <see cref="RescueNpc"/>와 짝이다.</summary>
        public string RescueNpcId { get; }

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
            GridPos? extractionPoint = null,
            GridPos? elevatorShaft = null,
            GridPos? elevatorLanding = null,
            GridPos? rescueNpc = null,
            string rescueNpcId = null)
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
            ElevatorShaft = elevatorShaft;
            ElevatorLanding = elevatorLanding;
            RescueNpc = rescueNpc;
            RescueNpcId = rescueNpcId;
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

        /// <summary>
        /// 이 던전의 진행 방향. <b>던전별 데이터</b>이며 전역 스위치가 아니다.
        /// 중력(<see cref="FallRules"/>·<see cref="SightRules"/>)은 이 값을 타지 않는다.
        /// </summary>
        public DungeonProgressDirection Direction { get; }

        /// <summary>
        /// 이 던전이 시작하는 <b>건물 층 번호</b>(폐병원 = −2 → B2, 지하 던전 = −1 → B1).
        /// 층 라벨을 만들 때 <see cref="Direction"/>과 함께 쓴다.
        /// <para>
        /// <b>레이아웃이 직접 들고 있어야 한다.</b> 전역 선택(`DungeonSelection`)에서 읽으면
        /// 허브를 그릴 때 던전의 값을 쓰거나, 던전 체인의 2번째 던전이 1번째 값을 쓰게 된다 —
        /// 방향과 표기 기준이 서로 다른 출처를 갖는 순간 라벨이 조용히 어긋난다.
        /// </para>
        /// </summary>
        public int FirstBuildingFloor { get; }

        /// <summary>
        /// 이 던전의 지역 프로파일. 적 혼합·밀도·반응 무대 확률이 여기서 갈린다.
        /// <para>
        /// <b>레이아웃이 들고 있어야 하는 이유는 <see cref="Direction"/>과 같다</b> —
        /// 전역 선택에서 읽으면 던전 체인의 2번째 던전이 1번째의 지역 값으로 몬스터를 뽑는다.
        /// 생성기와 런타임 스폰이 같은 출처를 봐야 층 안팎이 어긋나지 않는다.
        /// </para>
        /// </summary>
        public DungeonRegionProfile Region { get; }

        /// <summary>
        /// 다음 층으로 나아가는 계단. 하강 던전에서는 하행, 상승 던전에서는 상행이다.
        /// <b>"출구"를 찾는 코드는 <see cref="DungeonFloorInfo.DownStairs"/>가 아니라 이것을 써야 한다</b> —
        /// 그러지 않으면 상승 던전에서 되돌아가는 계단을 출구로 오인한다.
        /// </summary>
        public GridPos? OnwardStairOf(DungeonFloorInfo floor) =>
            floor == null
                ? null
                : Direction == DungeonProgressDirection.Ascend ? floor.UpStairs : floor.DownStairs;

        /// <summary>이전 층으로 되돌아가는 계단. 첫 층에는 없다.</summary>
        public GridPos? BackStairOf(DungeonFloorInfo floor) =>
            floor == null
                ? null
                : Direction == DungeonProgressDirection.Ascend ? floor.DownStairs : floor.UpStairs;

        public DungeonLayout(
            DungeonHeightModel height,
            List<DungeonFloorInfo> floors,
            DungeonProgressDirection direction = DungeonProgressDirection.Descend,
            int firstBuildingFloor = -1,
            DungeonRegionProfile region = DungeonRegionProfile.Facility)
        {
            Direction = direction;
            FirstBuildingFloor = firstBuildingFloor;
            Region = region;
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
        /// <summary>
        /// 다층 던전을 만든다. <paramref name="direction"/>은 <b>진행</b> 방향이며 공간이 아니다 —
        /// 하강이 주 목적인 던전과 상승이 주 목적인 던전이 함께 존재하므로 전역 스위치가 아니라
        /// 던전별 데이터로 받는다(GDD §10.1). 중력은 이 값을 타지 않는다.
        /// <para>
        /// <paramref name="region"/>은 <b>콘텐츠 정체성</b>이다 — 적 혼합·밀도·반응 무대 확률을
        /// 가른다(<see cref="DungeonBandProfiles"/>). 기본값은 기준 지역이라 기존 호출부의
        /// 생성 결과가 바뀌지 않는다.
        /// </para>
        /// </summary>
        public static DungeonLayout Generate(
            GridMap map,
            int width,
            int height,
            int floorCount,
            int elevationsPerFloor = 4,
            int seed = 1977,
            DungeonProgressDirection direction = DungeonProgressDirection.Descend,
            int firstBuildingFloor = -1,
            DungeonMetaContext meta = default,
            DungeonRegionProfile region = DungeonRegionProfile.Facility)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (width < 9) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 9) throw new ArgumentOutOfRangeException(nameof(height));
            if (floorCount < 1) throw new ArgumentOutOfRangeException(nameof(floorCount));

            map.Clear();
            var heightModel = new DungeonHeightModel(elevationsPerFloor);
            var random = new Random(seed);
            // 갇힌 방을 먼저 정하고 숨은 방 후보에서 뺀다 — 숨은 방은 못 찾을 수 있어서
            // 거기에 NPC 를 두면 진행이 막힌다. 미구출 NPC 가 없으면 빈 집합이므로
            // 후보 목록이 그대로이고 RNG 소비도 예전과 같다(같은 seed = 같은 던전).
            HashSet<int> npcDepths = meta.PendingNpcFloors();
            HashSet<int> secretDepths = PickSecretDepths(random, floorCount, npcDepths);

            // 1) 층 골격을 계획하고 새긴다. 인접 진행 층끼리 북쪽 방 기둥이 겹치도록 제약해
            //    구멍 착지 후보가 항상 남게 한다(방향과 무관하게 인접 층이 공간적으로도 인접하다).
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
                    secretDepths.Contains(depth),
                    direction,
                    meta.PendingNpcAt(depth),
                    region);
                CarveFloor(map, plan, height, direction);
                plans.Add(plan);
            }

            for (int i = 0; i < plans.Count - 1; i++)
                map.Connect(plans[i].Onward.Value, plans[i + 1].Back.Value, bidirectional: true);

            // 2) 구멍은 모든 층이 새겨진 뒤에야 "정확히 한 층 아래에 착지하는" 칸을 고를 수 있다.
            //    낙하 바닥은 공간 최하단이다 — 상승 던전에서는 첫 층이 가장 아래다.
            int bottomFloorIndex = plans[0].FloorIndex;
            foreach (FloorPlan plan in plans)
                if (plan.FloorIndex < bottomFloorIndex) bottomFloorIndex = plan.FloorIndex;
            int bottomElevation = heightModel.Elevation(bottomFloorIndex);

            // 구멍은 방향과 무관하게 **아래로** 떨어지므로 진행 순서가 아니라 공간 순서로 순회한다.
            // 상승 던전에서는 둘이 뒤집혀서, 진행이 앞선 층이 공간적으로는 아래에 있다.
            //
            // 순회는 반드시 **위에서 아래로** 간다. 각 층이 "바로 위층의 구멍 착지 칸"을 피해야
            // 하는데(2층 관통 금지) 그 값은 위층을 먼저 처리해야 생긴다. 하강 던전에서는 이 순서가
            // 예전 진행 순서 순회와 정확히 같아서 **같은 seed 가 같은 던전을 낸다**.
            var stacked = new List<FloorPlan>(plans);
            stacked.Sort((a, b) => a.FloorIndex.CompareTo(b.FloorIndex));
            for (int i = stacked.Count - 1; i >= 1; i--)
            {
                // 보스 아레나에는 구멍을 두지 않는다. 하강 던전에서는 아레나가 공간 최하단이라
                // 자동으로 빠졌고(그래서 이 조건이 없었다), 상승 던전에서는 아레나가 최상층이라
                // 조건 없이는 구멍이 생겨 보스전 중 낙하로 아레나를 벗어날 수 있다.
                // 방향을 바꾸는 변경에 게임플레이 변화를 섞지 않기 위해 불변식을 명시한다.
                if (DungeonBossArenaRules.IsArenaFloor(stacked[i].ProgressIndex, floorCount))
                    continue;

                GridPos? holeAbove = i + 1 < stacked.Count ? stacked[i + 1].Hole : null;
                PlaceHoleAndWeakFloor(map, heightModel, random, stacked[i], holeAbove, bottomElevation);
            }

            // 2-b) 엘리베이터 통로(상승 던전의 후퇴 동선). 스폰보다 **먼저** 놓아야
            //      사다리 타일이 IsFreeForSpawn 에 걸러진다 — 적·아이템이 통로에 갇히지 않는다.
            //      하강 던전에서는 아예 돌지 않으므로 기존 생성이 흔들리지 않는다.
            // 던전당 하나. 탑승구는 보스 아레나 바로 앞 층이라 보스로 가는 길에 반드시 지나가고,
            // 그때는 전원이 없어 멈춰 있다 — 링크는 보스를 잡을 때 Gameplay 가 넣는다.
            if (ElevatorShaftRules.AppliesToDungeon(direction, floorCount))
            {
                var byProgress = new Dictionary<int, FloorPlan>(plans.Count);
                foreach (FloorPlan plan in plans) byProgress[plan.ProgressIndex] = plan;

                if (byProgress.TryGetValue(
                        ElevatorShaftRules.EntranceProgressIndex(floorCount),
                        out FloorPlan entrance) &&
                    byProgress.TryGetValue(
                        ElevatorShaftRules.LandingProgressIndex,
                        out FloorPlan landing))
                    PlaceElevatorShaft(map, random, entrance, landing);
            }

            // 3) 적·아이템 스폰은 구멍·계단이 확정된 최종 타일 상태에서 고른다.
            foreach (FloorPlan plan in plans)
            {
                PlaceRescueNpc(plan);
                PlaceRestSite(map, random, plan, floorCount);
                PlacePuddle(map, random, plan);
                PickEnemySpawns(map, random, plan, floorCount);
                PlaceItems(map, random, plan, meta);
                PlaceEquipment(map, random, plan, meta);
                PlaceExtractionPoint(map, plan, floorCount);
                PlaceBossLandmark(map, plan, floorCount);
                PlaceCatwalk(map, plan, floorCount);
                PlaceWindows(map, heightModel, plan, bottomElevation);
            }

            // plans 는 진행 순서대로 쌓인다(depth 0 = 첫 층). 진행 지수는 계획 단계에서 확정돼
            // plan 이 들고 다니며, 이후 어디서도 elevation 으로 다시 계산하지 않는다.
            var floors = new List<DungeonFloorInfo>(floorCount);
            foreach (FloorPlan plan in plans)
            {
                // 진출/귀환(진행)을 상행/하행(공간)으로 되돌린다 — 맵에 놓인 타일 종류와 맞아야 한다.
                bool onwardGoesUp = direction == DungeonProgressDirection.Ascend;
                floors.Add(new DungeonFloorInfo(
                    plan.FloorIndex,
                    plan.ProgressIndex,
                    plan.Entry,
                    onwardGoesUp ? plan.Onward : plan.Back,
                    onwardGoesUp ? plan.Back : plan.Onward,
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
                    plan.ExtractionPoint,
                    plan.ElevatorShaft,
                    plan.ElevatorLanding,
                    plan.RescueNpc,
                    plan.BranchNpcId));
            }

            return new DungeonLayout(heightModel, floors, direction, firstBuildingFloor, region);
        }

        /// <summary>
        /// 면적 비례 스폰 보정. 기준 11×11(=121)에서 0, 약 60칸 늘 때마다 +1 —
        /// 층을 키웠을 때 방이 텅 비지 않게 적/아이템 밀도를 따라 올린다.
        /// </summary>
        public static int AreaSpawnBonus(int width, int height) =>
            Math.Max(0, (width * height - 121) / 60);

        private static HashSet<int> PickSecretDepths(
            Random random,
            int floorCount,
            HashSet<int> excluded)
        {
            int candidateCount = floorCount > 1 ? floorCount - 1 : floorCount;
            int desired = Math.Min(SecretRoomRules.DesiredCount(floorCount), candidateCount);
            var candidates = new List<int>(candidateCount);
            for (int depth = 0; depth < candidateCount; depth++)
            {
                if (excluded != null && excluded.Contains(depth)) continue;
                candidates.Add(depth);
            }
            if (desired > candidates.Count) desired = candidates.Count;

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

            /// <summary>
            /// 공간 좌표축의 층 인덱스(위로 갈수록 큼). 방향에 따라 부호가 갈린다 —
            /// 하강 던전은 0, −1, −2 …, 상승 던전은 0, +1, +2 … 로 간다.
            /// <b>난이도·구간 판정에 쓰지 말 것</b> — 그건 <see cref="ProgressIndex"/>다.
            /// </summary>
            public int FloorIndex;

            /// <summary>
            /// 진행 지수(첫 층 = 0). 난이도·구간·보상 판정의 유일한 축이다.
            /// <b>고도에서 역산하지 않는다</b> — 예전에 <c>-FloorIndex</c>로 뽑았는데 상승 던전에서
            /// 음수가 되어 모든 밴드 판정이 조용히 첫 층으로 붕괴했다(GDD §5.1).
            /// </summary>
            public int ProgressIndex;

            /// <summary>
            /// 이 층이 속한 지역 프로파일. 밴드 조회는 <b>(지역, 진행 지수)</b> 두 축을 모두 쓴다 —
            /// 계획·배치 단계가 각자 지역을 인자로 받으면 한 곳만 빠뜨려도 층 안에서
            /// 정체성이 갈린다(북쪽 방은 침수인데 웅덩이는 기준값인 식).
            /// </summary>
            public DungeonRegionProfile Region;

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
            /// <summary>이전 층(진행 −1)으로 되돌아가는 계단. 첫 층에는 없다.</summary>
            public GridPos? Back;

            /// <summary>
            /// 다음 층(진행 +1)으로 나아가는 계단. 최종 층에도 둔다 —
            /// 링크가 없는 진출 계단이 "던전 출구"다.
            /// <b>공간 방향이 아니라 진행 방향</b>이므로 상승 던전에서는 이것이 위로 가는 계단이다.
            /// </summary>
            public GridPos? Onward;
            public GridPos? Hole;

            /// <summary>
            /// 이 분기 방에 갇힌 동료의 id. 비어 있으면 평범한 파밍 방이다.
            /// 숨은 방(<c>BranchIsSecret</c>)과는 배타적이다 — 생성기가 층을 갈라 놓는다.
            /// </summary>
            public string BranchNpcId;

            /// <summary>갇힌 동료가 서 있는 칸. 없으면 이 층에 구출 대상이 없다.</summary>
            public GridPos? RescueNpc;

            /// <summary>이 층에 있는 엘리베이터 통로 입구(아래로만 내려간다).</summary>
            public GridPos? ElevatorShaft;

            /// <summary>다른 층의 통로가 이 층으로 내려오는 착지 칸.</summary>
            public GridPos? ElevatorLanding;
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
