using System;

namespace ProjectC.Core
{
    /// <summary>수직 개구부를 어느 방향으로 들여다보는가.</summary>
    public enum VerticalOpeningView
    {
        None = 0,
        Downward,
        Upward
    }

    /// <summary>
    /// 한 컬럼(x, y)을 관찰자의 눈높이에서 본 결과. 컬럼에는 솔리드 구간이 여럿 있을 수 있으므로
    /// (예: 올라온 단 위에 얹힌 캐치워크) 단순 높이맵이 아니라 <b>지면</b>과 <b>머리 위 구조물</b>을
    /// 나눠 들고, 너머로 시야가 이어지는지도 함께 답한다.
    /// </summary>
    public readonly struct ColumnView
    {
        /// <summary>눈높이 이하에서 가장 높은 타일 — 서 있거나 넘겨다보는 표면.</summary>
        public bool HasGround { get; }
        public GridPos Ground { get; }

        /// <summary>눈높이보다 높은 첫 타일 — 캐치워크·메자닌처럼 머리 위를 지나는 구조물.</summary>
        public bool HasOverhead { get; }
        public GridPos Overhead { get; }

        /// <summary>이 컬럼 너머로 시야가 이어지지 않는가.</summary>
        public bool BlocksBeyond { get; }

        internal ColumnView(
            bool hasGround, GridPos ground, bool hasOverhead, GridPos overhead, bool blocksBeyond)
        {
            HasGround = hasGround;
            Ground = ground;
            HasOverhead = hasOverhead;
            Overhead = overhead;
            BlocksBeyond = blocksBeyond;
        }

        /// <summary>대역에 타일이 하나도 없는 컬럼(void) — 무한 높이 벽으로 취급한다.</summary>
        public static readonly ColumnView Opaque =
            new ColumnView(false, default, false, default, true);
    }

    /// <summary>
    /// 시야·도달 판정의 단일 출처(3D 시야선 1~3단계). 원거리 사격·근접 단차 타격·개구부 투시·
    /// FOV 컬럼 해석이 모두 이 함수들을 공유한다 — 판정이 전투/개구부/FOV 세 곳으로
    /// 흩어지지 않게 하는 자리다.
    ///
    /// - 수평·경사 시선: 2D 브레젠험으로 걷되 각 중간 칸에서 시선 elevation 을 보간해
    ///   그 복셀의 차폐를 본다. void(타일 부재)=불투명 — 이 던전의 벽은 타일 부재로 표현된다.
    /// - 수직 시선(같은 컬럼): 실제 개구부(Hole)만 통과한다. 허공(타일 없음)은 통로다 —
    ///   "void=불투명"은 컬럼을 벽으로 읽는 수평 규칙이라 위·아래 판정에는 적용하지 않는다.
    /// - 컬럼 span 해석(<see cref="ViewColumn"/>): FOV 셰도우캐스팅이 쓰는 지면/머리 위 구분.
    /// </summary>
    public static class SightRules
    {
        /// <summary>
        /// 눈높이보다 이 값을 초과해 높은 타일은 벽처럼 너머 시야를 막는다.
        /// 1단(raised) 단차는 막지 않고, 2단 이상(벽·컨테이너 더미·캐치워크)만 차폐한다.
        /// </summary>
        public const int HeightBlockThreshold = 1;

        /// <summary>이 칸이 위·아래 시야를 막는가. 허공(null)은 통로, 실제 개구부만 뚫려 있다.</summary>
        public static bool BlocksVerticalSight(TileData tile) =>
            tile != null && tile.BlocksVerticalSight;

        /// <summary>
        /// 컬럼을 눈높이 기준으로 해석한다(3D 시야선 3단계 — FOV가 쓰는 span 판정).
        /// 지면은 눈높이 이하에서 가장 높은 타일, 머리 위 구조물은 눈높이보다 높은 첫 타일이다.
        /// 지면보다 아래 타일은 그 지면에 덮여 보이지 않으므로 내지 않는다.
        ///
        /// 차단 규칙: void(대역이 통째로 빈 컬럼) · 머리 위 구조물이 있는 컬럼 ·
        /// 지면이 벽/닫힌 문인 컬럼은 너머로 시야를 넘기지 않는다.
        /// </summary>
        public static ColumnView ViewColumn(
            GridMap map, int x, int y, GridPos origin, int minElevation, int maxElevation)
        {
            if (map == null) return ColumnView.Opaque;

            int eye = origin.elevation + HeightBlockThreshold;

            bool hasGround = false;
            GridPos ground = default;
            bool groundBlocks = false;
            int groundScanTop = Math.Min(maxElevation, eye);
            for (int e = groundScanTop; e >= minElevation; e--)
            {
                var pos = new GridPos(x, y, e);
                if (!map.TryGet(pos, out TileData tile)) continue;
                hasGround = true;
                ground = pos;
                groundBlocks = tile.BlocksSight;
                break;
            }

            bool hasOverhead = false;
            GridPos overhead = default;
            for (int e = Math.Max(minElevation, eye + 1); e <= maxElevation; e++)
            {
                var pos = new GridPos(x, y, e);
                if (!map.Has(pos)) continue;
                hasOverhead = true;
                overhead = pos;
                break;
            }

            if (!hasGround && !hasOverhead) return ColumnView.Opaque;

            return new ColumnView(
                hasGround, ground, hasOverhead, overhead, hasOverhead || groundBlocks);
        }

        /// <summary>
        /// 높이 인식 시야선. from.elevation == to.elevation 이면 상수 보간이라
        /// 기존 평면 판정과 완전히 같고, 같은 컬럼이면 수직 개구부 판정으로 넘긴다.
        /// </summary>
        public static bool HasLineOfSight(GridMap map, GridPos from, GridPos to)
        {
            if (map == null) return false;
            if (from.x == to.x && from.y == to.y) return HasVerticalSight(map, from, to);

            int x = from.x;
            int y = from.y;
            int dx = Math.Abs(to.x - from.x);
            int dy = Math.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int error = dx - dy;

            int steps = Math.Max(dx, dy); // 체비셰프 단계 수 = elevation 보간 분모(>=1)
            int k = 0;

            while (x != to.x || y != to.y)
            {
                int twiceError = error * 2;
                if (twiceError > -dy) { error -= dy; x += sx; }
                if (twiceError < dx) { error += dx; y += sy; }
                k++;
                if (x == to.x && y == to.y) break;

                int e = (int)Math.Round(
                    from.elevation + (to.elevation - from.elevation) * (double)k / steps,
                    MidpointRounding.AwayFromZero);

                TileData tile = map.Get(new GridPos(x, y, e));
                if (tile == null || tile.BlocksSight) return false;
            }

            return true;
        }

        /// <summary>
        /// 같은 컬럼의 수직 시야. 낮은 쪽 바로 위부터 <b>높은 쪽 칸까지</b>가 모두 뚫려 있어야 한다.
        /// 높은 쪽 칸 자신을 포함하는 이유: 단단한 바닥 위에 선 대상은 그 바닥에 가려 아래에서
        /// 보이지 않고, 반대로 자기 발밑 바닥을 뚫고 내려다볼 수도 없다. 즉 실제 개구부만 층을 잇는다.
        /// </summary>
        public static bool HasVerticalSight(GridMap map, GridPos from, GridPos to)
        {
            if (map == null || from.x != to.x || from.y != to.y) return false;
            if (from.elevation == to.elevation) return true;

            int upper = Math.Max(from.elevation, to.elevation);
            int lower = Math.Min(from.elevation, to.elevation);
            for (int e = lower + 1; e <= upper; e++)
            {
                if (BlocksVerticalSight(map.Get(new GridPos(from.x, from.y, e))))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 옆칸에 높이차를 감수하고 닿는가 — 근접 단차 타격의 기하 판정.
        /// 마법·특수 사거리도 같은 함수를 쓰도록 순수 기하(맵 비의존)로 둔다.
        ///
        /// <paramref name="maxPlanarReach"/>가 2 이상이면 창처럼 **직선으로만** 뻗는다
        /// (대각 비인접 규칙을 사거리가 길어져도 유지). 사이가 뚫려 있는지는 호출부가
        /// <see cref="HasLineOfSight"/>로 함께 본다 — 기하와 차폐를 섞지 않는다.
        /// </summary>
        public static bool CanReachAcross(
            GridPos from, GridPos to, int maxStepHeight, int maxPlanarReach = 1)
        {
            int planar = from.ManhattanTo(to);
            if (planar < 1 || planar > (maxPlanarReach < 1 ? 1 : maxPlanarReach)) return false;
            if (from.x != to.x && from.y != to.y) return false; // 대각은 근접이 아니다
            return Math.Abs(from.elevation - to.elevation) <= maxStepHeight;
        }

        /// <summary>
        /// 실제로 뚫린 Hole을 통한 층간 시야를 판정한다.
        /// StairsUp/Down은 던전 층 전환 링크일 뿐 시야 포털이 아니다.
        /// </summary>
        public static VerticalOpeningView ViewFromFloor(
            GridMap map,
            DungeonHeightModel height,
            int observerFloorIndex,
            GridPos opening,
            int minimumElevation,
            Func<GridPos, bool> isVisible,
            out GridPos landing)
        {
            landing = default;
            if (map == null || height == null || isVisible == null ||
                map.Get(opening)?.kind != TileKind.Hole)
                return VerticalOpeningView.None;

            GridPos? foundLanding = map.FindLandingBelow(opening, minimumElevation);
            if (!foundLanding.HasValue)
                return VerticalOpeningView.None;

            // 개구부와 착지 사이가 실제로 뚫려 있어야 한다 — 사이에 온전한 바닥이 끼면 못 본다.
            if (!HasVerticalSight(map, opening, foundLanding.Value))
                return VerticalOpeningView.None;

            landing = foundLanding.Value;
            int openingFloor = height.FloorIndex(opening.elevation);
            int landingFloor = height.FloorIndex(landing.elevation);

            if (observerFloorIndex == openingFloor && isVisible(opening))
                return VerticalOpeningView.Downward;
            if (observerFloorIndex == landingFloor && isVisible(landing))
                return VerticalOpeningView.Upward;
            return VerticalOpeningView.None;
        }
    }
}
