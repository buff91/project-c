using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 실제 개구부를 통과하는 투척 경로. Opening은 위층의 Hole,
    /// Landing은 그 컬럼에서 처음 만나는 아래층의 단단한 바닥이다.
    /// </summary>
    public readonly struct VerticalThrowPath
    {
        public GridPos Opening { get; }
        public GridPos Landing { get; }
        public int Cost { get; }

        public VerticalThrowPath(GridPos opening, GridPos landing, int cost)
        {
            Opening = opening;
            Landing = landing;
            Cost = cost;
        }
    }

    /// <summary>
    /// Hole을 통한 인접 던전 층 투척 판정. 같은 elevation 투척은 기존
    /// <see cref="BombRules"/>가 계속 소유하고, 이 규칙은 실제 개구부를 지나는 경로만 다룬다.
    /// </summary>
    public static class VerticalThrowRules
    {
        /// <summary>구멍 너머로 던질 수 있는 광역 소모품인가.</summary>
        public static bool Supports(ItemKind kind) =>
            kind == ItemKind.Bomb ||
            kind == ItemKind.FrostBomb ||
            kind == ItemKind.OilFlask;

        /// <summary>
        /// 미리보기용 목표 열거. 확정 판정과 어긋나지 않도록 각 후보를
        /// <see cref="CanThrow"/>로 다시 판정한 뒤 결정적 순서로 반환한다.
        /// </summary>
        public static void ForEachThrowTarget(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            ItemKind kind,
            int maxRange,
            Action<GridPos> visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            ForEachThrowTarget(
                map, height, from, kind, maxRange, canUseNearEndpoint: null,
                (target, _) => visit(target));
        }

        /// <summary>
        /// 현재 층에서 쓸 수 있는 개구부 endpoint를 제한하면서 목표만 반환한다.
        /// 아래 투척의 near endpoint는 Hole, 위 투척은 Landing이다.
        /// </summary>
        public static void ForEachThrowTarget(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            ItemKind kind,
            int maxRange,
            Func<GridPos, bool> canUseNearEndpoint,
            Action<GridPos> visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            ForEachThrowTarget(
                map, height, from, kind, maxRange, canUseNearEndpoint,
                (target, _) => visit(target));
        }

        /// <summary>
        /// 목표와 함께 그 목표에 실제로 선택된 최단 개구부 경로를 반환한다.
        /// Gameplay 미리보기는 이 경로를 보관하고 확정 시 같은 predicate로
        /// <see cref="TryResolve"/>를 다시 호출할 수 있다.
        /// </summary>
        public static void ForEachThrowTarget(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            ItemKind kind,
            int maxRange,
            Func<GridPos, bool> canUseNearEndpoint,
            Action<GridPos, VerticalThrowPath> visit)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (height == null) throw new ArgumentNullException(nameof(height));
            if (maxRange < 0) throw new ArgumentOutOfRangeException(nameof(maxRange));
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (!Supports(kind)) return;

            var targets = new List<ResolvedTarget>();
            foreach (KeyValuePair<GridPos, TileData> pair in map.All())
            {
                GridPos target = pair.Key;
                if (pair.Value == null || !pair.Value.IsSolidGround) continue;
                if (TryResolve(
                        map, height, from, target, kind, maxRange, canUseNearEndpoint,
                        out VerticalThrowPath path))
                    targets.Add(new ResolvedTarget(target, path));
            }

            targets.Sort((first, second) => ComparePositions(first.Target, second.Target));
            foreach (ResolvedTarget target in targets)
                visit(target.Target, target.Path);
        }

        public static bool CanThrow(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            GridPos target,
            ItemKind kind,
            int maxRange) =>
            TryResolve(map, height, from, target, kind, maxRange, out _);

        public static bool CanThrow(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            GridPos target,
            ItemKind kind,
            int maxRange,
            Func<GridPos, bool> canUseNearEndpoint) =>
            TryResolve(
                map, height, from, target, kind, maxRange, canUseNearEndpoint, out _);

        /// <summary>
        /// 실제 Hole↔landing 쌍 중 사거리와 시야를 만족하는 최단 경로를 찾는다.
        /// 비용은 진입 평면 맨해튼 거리 + 개구부 통과 1 + 도착 평면 맨해튼 거리다.
        /// 아래로 던질 때는 Hole이 진입점이고, 위로 던질 때는 Landing이 진입점이다.
        /// </summary>
        public static bool TryResolve(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            GridPos target,
            ItemKind kind,
            int maxRange,
            out VerticalThrowPath path)
        {
            return TryResolve(
                map, height, from, target, kind, maxRange,
                canUseNearEndpoint: null, out path);
        }

        public static bool TryResolve(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            GridPos target,
            ItemKind kind,
            int maxRange,
            Func<GridPos, bool> canUseNearEndpoint,
            out VerticalThrowPath path)
        {
            path = default;
            if (map == null || height == null || maxRange < 0 || !Supports(kind))
                return false;
            if (!map.IsSolidGround(target))
                return false;

            int fromFloor = height.FloorIndex(from.elevation);
            int targetFloor = height.FloorIndex(target.elevation);
            if (Math.Abs((long)fromFloor - targetFloor) != 1L)
                return false;
            if (!TryGetMinimumElevation(map, out int minimumElevation))
                return false;

            bool found = false;
            foreach (KeyValuePair<GridPos, TileData> pair in map.All())
            {
                if (pair.Value == null || pair.Value.kind != TileKind.Hole) continue;

                GridPos opening = pair.Key;
                if (opening.elevation <= minimumElevation) continue;

                GridPos? foundLanding = map.FindLandingBelow(opening, minimumElevation);
                if (!foundLanding.HasValue) continue;

                GridPos landing = foundLanding.Value;
                int openingFloor = height.FloorIndex(opening.elevation);
                int landingFloor = height.FloorIndex(landing.elevation);
                if ((long)openingFloor - landingFloor != 1L) continue;
                if (!SightRules.HasVerticalSight(map, opening, landing)) continue;

                bool downward = fromFloor == openingFloor && targetFloor == landingFloor;
                bool upward = fromFloor == landingFloor && targetFloor == openingFloor;
                if (!downward && !upward) continue;

                GridPos entry = downward ? opening : landing;
                GridPos exit = downward ? landing : opening;
                if (canUseNearEndpoint != null && !canUseNearEndpoint(entry)) continue;

                long cost = PlanarDistance(from, entry) + 1L + PlanarDistance(exit, target);
                if (cost > maxRange) continue;

                if (!SightRules.HasLineOfSight(map, from, entry) ||
                    !SightRules.HasLineOfSight(map, exit, target))
                    continue;

                var candidate = new VerticalThrowPath(opening, landing, (int)cost);
                if (!found || IsBetter(candidate, path))
                {
                    path = candidate;
                    found = true;
                }
            }

            return found;
        }

        private readonly struct ResolvedTarget
        {
            public GridPos Target { get; }
            public VerticalThrowPath Path { get; }

            public ResolvedTarget(GridPos target, VerticalThrowPath path)
            {
                Target = target;
                Path = path;
            }
        }

        private static bool TryGetMinimumElevation(GridMap map, out int minimumElevation)
        {
            minimumElevation = 0;
            bool found = false;
            foreach (KeyValuePair<GridPos, TileData> pair in map.All())
            {
                if (!found || pair.Key.elevation < minimumElevation)
                {
                    minimumElevation = pair.Key.elevation;
                    found = true;
                }
            }
            return found;
        }

        private static long PlanarDistance(GridPos first, GridPos second) =>
            Math.Abs((long)first.x - second.x) + Math.Abs((long)first.y - second.y);

        private static bool IsBetter(VerticalThrowPath candidate, VerticalThrowPath current)
        {
            if (candidate.Cost != current.Cost)
                return candidate.Cost < current.Cost;

            int openingOrder = ComparePositions(candidate.Opening, current.Opening);
            if (openingOrder != 0) return openingOrder < 0;
            return ComparePositions(candidate.Landing, current.Landing) < 0;
        }

        private static int ComparePositions(GridPos first, GridPos second)
        {
            int elevationOrder = first.elevation.CompareTo(second.elevation);
            if (elevationOrder != 0) return elevationOrder;
            int xOrder = first.x.CompareTo(second.x);
            return xOrder != 0 ? xOrder : first.y.CompareTo(second.y);
        }
    }
}
