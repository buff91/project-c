using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>B2 시작방에서 기존 바닥 자산을 군집으로 묶는 역할.</summary>
    public enum B2HeroFloorPatchKind
    {
        Service,
        Grate,
        Cracked,
        Macro0,
        Macro1,
        Macro2,
        Macro3,
    }

    /// <summary>
    /// 첫 던전 B2의 프레젠테이션 전용 배치 결과.
    /// 지형을 바꾸지 않고 폭발통·낮은 드레싱·서비스 벽 군집이 공유할 좌표만 소유한다.
    /// </summary>
    public sealed class B2HeroRoomLayout
    {
        private static readonly (int dx, int dy)[] BarrelBayDirections =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        private readonly HashSet<GridPos> _roomCells;
        private readonly HashSet<GridPos> _clearSpine;
        private readonly Dictionary<GridPos, B2HeroFloorPatchKind> _floorPatches;

        internal B2HeroRoomLayout(
            IReadOnlyList<GridPos> roomCells,
            IReadOnlyList<GridPos> clearSpine,
            GridPos? barrel,
            GridPos? parkingStop,
            int parkingStopWorldFacingQuarterTurns,
            GridPos? fallenSign,
            int fallenSignWorldFacingQuarterTurns,
            Dictionary<GridPos, B2HeroFloorPatchKind> floorPatches)
        {
            RoomCells = roomCells;
            ClearSpine = clearSpine;
            Barrel = barrel;
            ParkingStop = parkingStop;
            ParkingStopWorldFacingQuarterTurns = parkingStopWorldFacingQuarterTurns;
            FallenSign = fallenSign;
            FallenSignWorldFacingQuarterTurns = fallenSignWorldFacingQuarterTurns;
            var accentPositions = new List<GridPos>(2);
            if (parkingStop.HasValue) accentPositions.Add(parkingStop.Value);
            if (fallenSign.HasValue) accentPositions.Add(fallenSign.Value);
            AccentPositions = accentPositions;
            _roomCells = new HashSet<GridPos>(roomCells);
            _clearSpine = new HashSet<GridPos>(clearSpine);
            _floorPatches = floorPatches;

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            foreach (GridPos pos in roomCells)
            {
                minX = Math.Min(minX, pos.x);
                maxX = Math.Max(maxX, pos.x);
                minY = Math.Min(minY, pos.y);
                maxY = Math.Max(maxY, pos.y);
            }

            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public IReadOnlyList<GridPos> RoomCells { get; }
        public IReadOnlyList<GridPos> ClearSpine { get; }
        public GridPos? Barrel { get; }
        public GridPos? ParkingStop { get; }
        public int ParkingStopWorldFacingQuarterTurns { get; }
        public GridPos? FallenSign { get; }
        public int FallenSignWorldFacingQuarterTurns { get; }
        /// <summary>이전 소비자를 위한 named low-prop 좌표의 parking → fallen 순서 뷰.</summary>
        public IReadOnlyList<GridPos> AccentPositions { get; }
        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }

        public bool ContainsRoomCell(GridPos pos) => _roomCells.Contains(pos);
        public bool IsClearSpine(GridPos pos) => _clearSpine.Contains(pos);

        public bool TryGetFloorPatch(
            GridPos pos,
            out B2HeroFloorPatchKind kind) =>
            _floorPatches.TryGetValue(pos, out kind);

        /// <summary>
        /// 월드 좌표에 고정된 2×2 연속 바닥 역할. 카메라가 돌아도 role은 바뀌지 않고
        /// 카탈로그가 같은 role의 view만 바꿔 연결 무늬를 보존한다.
        /// </summary>
        public bool TryGetMacroFloorRole(GridPos pos, out int role)
        {
            role = -1;
            if (!_floorPatches.TryGetValue(pos, out B2HeroFloorPatchKind kind) ||
                kind < B2HeroFloorPatchKind.Macro0 ||
                kind > B2HeroFloorPatchKind.Macro3)
                return false;

            role = (int)kind - (int)B2HeroFloorPatchKind.Macro0;
            return true;
        }

        /// <summary>
        /// 배럴 service 셀과 인접 grate 셀이 실제 한 쌍일 때만 전용 바닥의 월드 방향을 돌려준다.
        /// 0..3은 +Y, +X, -Y, -X 순서라 DungeonDressingPlacementRules의 시점 합성과 맞는다.
        /// </summary>
        public bool TryGetBarrelBay(
            out GridPos service,
            out GridPos drain,
            out int worldFacingQuarterTurns)
        {
            service = default;
            drain = default;
            worldFacingQuarterTurns = 0;
            if (!Barrel.HasValue ||
                !_floorPatches.TryGetValue(Barrel.Value, out B2HeroFloorPatchKind serviceKind) ||
                serviceKind != B2HeroFloorPatchKind.Service)
                return false;

            service = Barrel.Value;
            for (int view = 0; view < BarrelBayDirections.Length; view++)
            {
                (int dx, int dy) = BarrelBayDirections[view];
                GridPos candidate = service.Offset(dx, dy);
                if (_floorPatches.TryGetValue(candidate, out B2HeroFloorPatchKind kind) &&
                    kind == B2HeroFloorPatchKind.Grate)
                {
                    drain = candidate;
                    worldFacingQuarterTurns = view;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// B2 물리 -X 벽의 가운데 세 칸을 하나의 authored 서비스 스파인으로 묶는다.
        /// q0 화면의 좌→우가 segment 0..2가 되도록 물리 y는 역순((0,3) → (0,1))이다.
        /// segment 0의 호스 릴이 좌측 배럴 베이와 붙고 두 코너 셀은 조용한 벽으로 남는다.
        /// </summary>
        public bool TryGetServiceWallSegment(
            GridPos pos,
            int outwardX,
            int outwardY,
            out int segment)
        {
            segment = -1;
            if (!_roomCells.Contains(pos) || outwardX != -1 || outwardY != 0)
                return false;
            if (pos.x != MinX)
                return false;

            segment = MinY + 3 - pos.y;
            if (segment >= 0 && segment <= 2)
                return true;

            segment = -1;
            return false;
        }

        /// <summary>
        /// 시작방의 물리 벽면을 랜덤 패널로 흩뿌리지 않고 서비스 스파인과 반대쪽 단말로 묶는다.
        /// true + -1은 기본 재질, true + 1은 같은 벽체의 비점등 보조 재질이다. 둘은 물리 좌표
        /// parity로 고정되어 시점을 돌려도 같은 벽면이 남는다.
        /// </summary>
        public bool TryGetWallDecoration(
            GridPos pos,
            int outwardX,
            int outwardY,
            out int decoration)
        {
            decoration = -1;
            if (!_roomCells.Contains(pos) ||
                Math.Abs(outwardX) + Math.Abs(outwardY) != 1)
                return false;

            GridPos outside = pos.Offset(outwardX, outwardY);
            if (_roomCells.Contains(outside)) return false;

            // 서비스 스파인은 -X 벽의 전용 3셀 master가 소유한다. -Y 벽에는
            // 뒤쪽 중심의 봉인 단말과 반대 끝 결제·티켓 단말만 남겨 q0의 삼각 구도를 만든다.
            // 둘 다 바닥 footprint가 없는 벽 매립형이라 이동·문·세이브 규칙에는 관여하지 않는다.
            if (outwardX == 0 && outwardY == -1 &&
                pos.y == MinY && (pos.x == MinX || pos.x == MaxX))
                decoration = 2;

            // 정면 시안에 보이지 않는 두 반대 벽도 회전 시 전부 같은 무지 패널로
            // 늘어서지 않게 한다. 낮은 설비 패널 하나씩만 두어 q2/q3의 빈 면을 깨되,
            // 발광·상호작용·바닥 footprint는 추가하지 않는다.
            if ((outwardX == 1 && outwardY == 0 &&
                 pos.x == MaxX && pos.y == MinY + 2) ||
                (outwardX == 0 && outwardY == 1 &&
                 pos.y == MaxY && pos.x == MinX + 2))
                decoration = 0;

            // 긴 무지 벽에서 같은 중앙 패널이 셀마다 복사돼 보이지 않게, 기능 장식과
            // 서비스 master가 없는 홀수 bay만 legacy window 슬롯의 비점등 재질로 바꾼다.
            // 좌표·outward만 사용하므로 seed와 카메라 회전에 독립적이다.
            int faceIndex = outwardX == 0
                ? pos.x - MinX
                : pos.y - MinY;
            if (decoration < 0 &&
                !TryGetServiceWallSegment(pos, outwardX, outwardY, out _) &&
                (faceIndex & 1) == 1)
                decoration = 1;

            return true;
        }

        /// <summary>
        /// B2 시작방은 랜덤 비상등 대신 authored 서비스 스파인의 작은 작업등 하나를 쓴다.
        /// true는 이 방이 해당 타일의 sconce 결정을 소유한다는 뜻이며, false 값은 명시적인 소등이다.
        /// </summary>
        public bool TryGetWallSconce(GridPos pos, out bool isSconce)
        {
            isSconce = false;
            if (!_roomCells.Contains(pos)) return false;

            isSconce = pos.x == MinX && pos.y == MinY + 2;
            return true;
        }
    }

    /// <summary>
    /// 첫 던전 B2의 6×5 시작방을 히어로 룸처럼 읽히게 하는 순수 좌표 계획.
    /// map/RNG를 수정하지 않으며 생성 지형·세이브 지문과 독립이다.
    /// </summary>
    public static class B2HeroRoomLayoutRules
    {
        private static readonly (int dx, int dy)[] CardinalDirections =
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        public static bool AppliesTo(string dungeonId, int progressIndex) =>
            dungeonId == DungeonCatalog.DefaultId && progressIndex == 0;

        public static bool TryCreate(
            string dungeonId,
            int progressIndex,
            int seed,
            GridMap map,
            DungeonFloorInfo floor,
            GridPos? onward,
            ISet<GridPos> occupied,
            out B2HeroRoomLayout layout)
        {
            layout = null;
            if (!AppliesTo(dungeonId, progressIndex)) return false;
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (floor == null) throw new ArgumentNullException(nameof(floor));

            List<GridPos> roomCells = CollectStartingRoom(map, floor.Entry);
            if (roomCells.Count == 0) return false;
            var room = new HashSet<GridPos>(roomCells);
            GetRoomRightEdge(
                roomCells,
                out int roomMaxX,
                out int roomMinY);
            int roomElevation = roomCells[0].elevation;

            List<GridPos> clearSpine = onward.HasValue
                ? GridPathfinder.FindPath(
                    map,
                    floor.Entry,
                    onward.Value,
                    openClosedDoors: true)
                : new List<GridPos> { floor.Entry };
            if (clearSpine.Count == 0)
                clearSpine.Add(floor.Entry);

            var reserved = occupied != null
                ? new HashSet<GridPos>(occupied)
                : new HashSet<GridPos>();
            foreach (GridPos pos in clearSpine) reserved.Add(pos);
            reserved.Add(floor.Entry);
            // (MaxX, MinY)는 벽 매립형 kiosk 아래의 음영 여백이다. 낮은 소품이나
            // 특수 바닥이 들어오면 단말이 다시 독립 standing prop처럼 보이므로 비워 둔다.
            reserved.Add(new GridPos(roomMaxX, roomMinY, roomElevation));
            // 두 배치 규칙 모두 Entry 맨해튼 2칸 미만을 후보에서 제외한다. 인접 칸까지
            // reserved(경로 차단)로 넣으면 안전 영역이 아니라 폭발통 접근 경로까지 막힌다.

            var barrelCandidates = new List<GridPos>(roomCells);
            barrelCandidates.Sort((left, right) =>
            {
                int distance = left.ManhattanTo(floor.Entry)
                    .CompareTo(right.ManhattanTo(floor.Entry));
                if (distance != 0) return distance;
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });

            GridPos? barrel = DungeonPropPlacementRules.TrySelectSafePosition(
                map,
                floor.Entry,
                barrelCandidates,
                reserved,
                out GridPos selectedBarrel)
                    ? selectedBarrel
                    : (GridPos?)null;

            var dressingReserved = new HashSet<GridPos>(reserved);
            if (barrel.HasValue)
            {
                dressingReserved.Add(barrel.Value);
                foreach ((int dx, int dy) in CardinalDirections)
                    dressingReserved.Add(barrel.Value.Offset(dx, dy));
            }

            var accentCandidates = new List<GridPos>(roomCells);
            accentCandidates.Sort((left, right) =>
            {
                int distance = right.ManhattanTo(floor.Entry)
                    .CompareTo(left.ManhattanTo(floor.Entry));
                if (distance != 0) return distance;
                int order = StableOrder(left, seed).CompareTo(StableOrder(right, seed));
                if (order != 0) return order;
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });

            // 오른쪽 군집의 역할·방향은 카탈로그 슬롯 수나 seed 정렬이 아니라 layout이
            // 직접 소유한다. q0 기준 둘 다 view-0 실루엣이며 카메라 회전만 여기에 더해진다.
            GridPos preferredParking = new GridPos(
                roomMaxX,
                roomMinY + 2,
                roomElevation);
            GridPos? parkingStop = TrySelectPreferredDressing(
                map,
                room,
                floor.Entry,
                preferredParking,
                dressingReserved,
                out GridPos selectedParking)
                    ? selectedParking
                    : (GridPos?)null;
            if (parkingStop.HasValue) dressingReserved.Add(parkingStop.Value);

            GridPos preferredFallen = new GridPos(
                roomMaxX,
                roomMinY + 1,
                roomElevation);
            GridPos? fallenSign = TrySelectPreferredDressing(
                map,
                room,
                floor.Entry,
                preferredFallen,
                dressingReserved,
                out GridPos selectedFallen)
                    ? selectedFallen
                    : (GridPos?)null;
            if (fallenSign.HasValue) dressingReserved.Add(fallenSign.Value);

            // 선호 좌표가 실제 점유·진출선·비Floor라서 막힐 때만 기존 외곽 안전 선택을 쓴다.
            // 이미 선호 좌표 하나를 썼다면 fallback은 기존 최소 간격을 지켜 반대 군집으로 간다.
            accentCandidates.RemoveAll(candidate =>
                (parkingStop.HasValue &&
                 candidate.ManhattanTo(parkingStop.Value) <
                    DungeonDressingPlacementRules.MinimumDressingSpacing) ||
                (fallenSign.HasValue &&
                 candidate.ManhattanTo(fallenSign.Value) <
                    DungeonDressingPlacementRules.MinimumDressingSpacing));
            int missingAccentCount =
                (parkingStop.HasValue ? 0 : 1) +
                (fallenSign.HasValue ? 0 : 1);
            IReadOnlyList<GridPos> fallbackAccents =
                DungeonDressingPlacementRules.SelectSafePositions(
                    map,
                    floor.Entry,
                    accentCandidates,
                    dressingReserved,
                    missingAccentCount);
            int fallbackIndex = 0;
            if (!parkingStop.HasValue && fallbackIndex < fallbackAccents.Count)
                parkingStop = fallbackAccents[fallbackIndex++];
            if (!fallenSign.HasValue && fallbackIndex < fallbackAccents.Count)
                fallenSign = fallbackAccents[fallbackIndex];

            var floorPatches = new Dictionary<GridPos, B2HeroFloorPatchKind>();
            // 낮은 바닥 패치는 프롭의 접근 칸을 막지 않는다. 진짜 점유/주동선만 피하고,
            // 폭발통 주변은 오히려 하나의 설비 구역으로 묶는다.
            var unavailable = new HashSet<GridPos>(reserved);
            if (parkingStop.HasValue) unavailable.Add(parkingStop.Value);
            if (fallenSign.HasValue) unavailable.Add(fallenSign.Value);

            if (barrel.HasValue)
            {
                floorPatches[barrel.Value] = B2HeroFloorPatchKind.Service;
                unavailable.Add(barrel.Value);
                AddAdjacentPatch(
                    map,
                    roomCells,
                    unavailable,
                    floorPatches,
                    barrel.Value,
                    B2HeroFloorPatchKind.Grate);
            }

            GridPos preferredCracked = new GridPos(
                roomMaxX,
                roomMinY + 3,
                roomElevation);
            if (!TryAddPreferredPatch(
                    map,
                    room,
                    unavailable,
                    floorPatches,
                    preferredCracked,
                    B2HeroFloorPatchKind.Cracked))
            {
                GridPos? crackedFallbackAnchor = fallenSign ?? parkingStop;
                if (crackedFallbackAnchor.HasValue)
                {
                    AddAdjacentPatch(
                        map,
                        roomCells,
                        unavailable,
                        floorPatches,
                        crackedFallbackAnchor.Value,
                        B2HeroFloorPatchKind.Cracked);
                }
            }

            // 연결 무늬는 네 셀이 모두 깨끗할 때만 원자적으로 배치한다. 기존 특수
            // 바닥·소품·진출선 중 하나라도 닿으면 부분 조각을 남기지 않고 일반 바닥으로 둔다.
            if (barrel.HasValue)
            {
                foreach ((int dx, int dy) in CardinalDirections)
                    unavailable.Add(barrel.Value.Offset(dx, dy));
            }
            AddMacroFloorBlock(
                map,
                roomCells,
                unavailable,
                floorPatches,
                seed);

            layout = new B2HeroRoomLayout(
                roomCells,
                clearSpine,
                barrel,
                parkingStop,
                0,
                fallenSign,
                0,
                floorPatches);
            return true;
        }

        private static bool TrySelectPreferredDressing(
            GridMap map,
            ISet<GridPos> room,
            GridPos entry,
            GridPos preferred,
            ISet<GridPos> reserved,
            out GridPos selected)
        {
            selected = default;
            if (!room.Contains(preferred)) return false;

            IReadOnlyList<GridPos> result =
                DungeonDressingPlacementRules.SelectSafePositions(
                    map,
                    entry,
                    new[] { preferred },
                    reserved,
                    maximumCount: 1);
            if (result.Count == 0) return false;

            selected = result[0];
            return true;
        }

        private static bool TryAddPreferredPatch(
            GridMap map,
            ISet<GridPos> room,
            ISet<GridPos> unavailable,
            IDictionary<GridPos, B2HeroFloorPatchKind> patches,
            GridPos preferred,
            B2HeroFloorPatchKind kind)
        {
            if (!room.Contains(preferred) ||
                unavailable.Contains(preferred) ||
                patches.ContainsKey(preferred) ||
                map.Get(preferred)?.kind != TileKind.Floor)
                return false;

            patches[preferred] = kind;
            unavailable.Add(preferred);
            return true;
        }

        private static void GetRoomRightEdge(
            IReadOnlyList<GridPos> roomCells,
            out int maxX,
            out int minY)
        {
            maxX = int.MinValue;
            minY = int.MaxValue;
            foreach (GridPos pos in roomCells)
            {
                maxX = Math.Max(maxX, pos.x);
                minY = Math.Min(minY, pos.y);
            }
        }

        private static List<GridPos> CollectStartingRoom(GridMap map, GridPos entry)
        {
            var cells = new List<GridPos>();
            if (map.Get(entry)?.kind != TileKind.Floor) return cells;

            var seen = new HashSet<GridPos> { entry };
            var queue = new Queue<GridPos>();
            queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                cells.Add(current);
                foreach ((int dx, int dy) in CardinalDirections)
                {
                    GridPos next = current.Offset(dx, dy);
                    if (next.elevation != entry.elevation ||
                        map.Get(next)?.kind != TileKind.Floor ||
                        !seen.Add(next))
                        continue;
                    queue.Enqueue(next);
                }
            }

            cells.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });
            return cells;
        }

        private static void AddAdjacentPatch(
            GridMap map,
            IReadOnlyList<GridPos> roomCells,
            ISet<GridPos> unavailable,
            IDictionary<GridPos, B2HeroFloorPatchKind> patches,
            GridPos anchor,
            B2HeroFloorPatchKind kind)
        {
            var room = new HashSet<GridPos>(roomCells);
            foreach ((int dx, int dy) in CardinalDirections)
            {
                GridPos candidate = anchor.Offset(dx, dy);
                if (!room.Contains(candidate) ||
                    unavailable.Contains(candidate) ||
                    patches.ContainsKey(candidate) ||
                    map.Get(candidate)?.kind != TileKind.Floor)
                    continue;

                patches[candidate] = kind;
                unavailable.Add(candidate);
                return;
            }
        }

        private static void AddMacroFloorBlock(
            GridMap map,
            IReadOnlyList<GridPos> roomCells,
            ISet<GridPos> unavailable,
            IDictionary<GridPos, B2HeroFloorPatchKind> patches,
            int seed)
        {
            var room = new HashSet<GridPos>(roomCells);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            int elevation = roomCells[0].elevation;
            foreach (GridPos pos in roomCells)
            {
                minX = Math.Min(minX, pos.x);
                maxX = Math.Max(maxX, pos.x);
                minY = Math.Min(minY, pos.y);
                maxY = Math.Max(maxY, pos.y);
            }

            var preferred = new GridPos(maxX - 2, minY + 1, elevation);
            var anchors = new List<GridPos>();
            for (int y = minY; y < maxY; y++)
            for (int x = minX; x < maxX; x++)
                anchors.Add(new GridPos(x, y, elevation));

            anchors.Sort((left, right) =>
            {
                int distance = left.ManhattanTo(preferred)
                    .CompareTo(right.ManhattanTo(preferred));
                if (distance != 0) return distance;
                int order = StableOrder(left, seed).CompareTo(StableOrder(right, seed));
                if (order != 0) return order;
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });

            foreach (GridPos anchor in anchors)
            {
                GridPos[] block =
                {
                    anchor,
                    anchor.Offset(1, 0),
                    anchor.Offset(0, 1),
                    anchor.Offset(1, 1),
                };
                bool clean = true;
                foreach (GridPos pos in block)
                {
                    if (!room.Contains(pos) ||
                        unavailable.Contains(pos) ||
                        patches.ContainsKey(pos) ||
                        map.Get(pos)?.kind != TileKind.Floor)
                    {
                        clean = false;
                        break;
                    }
                }
                if (!clean) continue;

                for (int role = 0; role < block.Length; role++)
                {
                    patches.Add(
                        block[role],
                        (B2HeroFloorPatchKind)((int)B2HeroFloorPatchKind.Macro0 + role));
                    unavailable.Add(block[role]);
                }
                return;
            }
        }

        private static int StableOrder(GridPos pos, int seed)
        {
            unchecked
            {
                int hash = pos.x * 73856093;
                hash ^= pos.y * 19349663;
                hash ^= pos.elevation * 83492791;
                hash ^= seed * 486187739;
                return hash & int.MaxValue;
            }
        }
    }
}
