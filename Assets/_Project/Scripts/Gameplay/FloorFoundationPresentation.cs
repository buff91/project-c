using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>현재 화면에서 보이는 B2 바닥 슬래브의 좌·우 하강면.</summary>
    [Flags]
    internal enum FoundationFaces
    {
        None = 0,
        ScreenLeft = 1,
        ScreenRight = 2,
        Both = ScreenLeft | ScreenRight
    }

    /// <summary>한 격자 칸에 붙는 기초 슬래브 면의 순수 프레젠테이션 자료.</summary>
    internal readonly struct FoundationCell
    {
        internal FoundationCell(GridPos position, FoundationFaces faces, int ribPhase)
        {
            Position = position;
            Faces = faces;
            RibPhase = ribPhase;
        }

        internal GridPos Position { get; }
        internal FoundationFaces Faces { get; }
        internal int RibPhase { get; }
    }

    /// <summary>월드 좌표에 고정되는 외곽 지지대의 모서리 방향.</summary>
    internal enum FoundationCorner
    {
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }

    /// <summary>카메라 회전과 무관하게 같은 월드 모서리에 남는 지지대 자료.</summary>
    internal readonly struct FoundationSupport
    {
        internal FoundationSupport(GridPos position, FoundationCorner corner)
        {
            Position = position;
            Corner = corner;
        }

        internal GridPos Position { get; }
        internal FoundationCorner Corner { get; }
    }

    /// <summary>
    /// B2 히어로 룸의 논리 바닥을 화면 방향의 얇은 기초 슬래브로 해석한다.
    /// 지도·FOV를 바꾸지 않고, 호출자가 건넨 현재 표시 상태와 평면 점유만 읽는다.
    /// </summary>
    internal static class FloorFoundationPresentation
    {
        private const int RibPhaseCount = 4;

        /// <summary>
        /// 표시 가능한 후보 칸 중 현재 화면 앞쪽으로 실제 열린 면만 수집한다.
        /// <paramref name="frontA"/>는 화면 오른쪽, <paramref name="frontB"/>는 화면 왼쪽 면이다.
        /// </summary>
        internal static FoundationCell[] Collect(
            IEnumerable<GridPos> candidates,
            Func<GridPos, bool> isRenderable,
            Func<GridPos, bool> hasPlanarTile,
            Vector2Int frontA,
            Vector2Int frontB)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (isRenderable == null) throw new ArgumentNullException(nameof(isRenderable));
            if (hasPlanarTile == null) throw new ArgumentNullException(nameof(hasPlanarTile));
            ValidateFrontDirections(frontA, frontB);

            List<GridPos> ordered = OrderedDistinct(candidates);
            var result = new List<FoundationCell>(ordered.Count);
            foreach (GridPos position in ordered)
            {
                if (!isRenderable(position)) continue;

                FoundationFaces faces = FoundationFaces.None;
                if (!hasPlanarTile(Offset(position, frontA)))
                    faces |= FoundationFaces.ScreenRight;
                if (!hasPlanarTile(Offset(position, frontB)))
                    faces |= FoundationFaces.ScreenLeft;
                if (faces == FoundationFaces.None) continue;

                result.Add(new FoundationCell(position, faces, RibPhaseFor(position)));
            }

            return result.ToArray();
        }

        /// <summary>
        /// 두 직교 월드 방향이 모두 열린 볼록 모서리에만 지지대를 둔다.
        /// 화면 방향을 입력받지 않으므로 회전해도 지지대의 월드 위치가 바뀌지 않는다.
        /// </summary>
        internal static FoundationSupport[] CollectSupports(
            IEnumerable<GridPos> candidates,
            Func<GridPos, bool> isRenderable,
            Func<GridPos, bool> isValidSupportCell,
            Func<GridPos, bool> hasPlanarTile)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (isRenderable == null) throw new ArgumentNullException(nameof(isRenderable));
            if (isValidSupportCell == null)
                throw new ArgumentNullException(nameof(isValidSupportCell));
            if (hasPlanarTile == null) throw new ArgumentNullException(nameof(hasPlanarTile));

            List<GridPos> ordered = OrderedDistinct(candidates);
            var result = new List<FoundationSupport>();
            foreach (GridPos position in ordered)
            {
                if (!isRenderable(position) || !isValidSupportCell(position)) continue;

                bool northOpen = !hasPlanarTile(position.North);
                bool eastOpen = !hasPlanarTile(position.East);
                bool southOpen = !hasPlanarTile(position.South);
                bool westOpen = !hasPlanarTile(position.West);

                if (northOpen && eastOpen)
                    result.Add(new FoundationSupport(position, FoundationCorner.NorthEast));
                if (northOpen && westOpen)
                    result.Add(new FoundationSupport(position, FoundationCorner.NorthWest));
                if (southOpen && eastOpen)
                    result.Add(new FoundationSupport(position, FoundationCorner.SouthEast));
                if (southOpen && westOpen)
                    result.Add(new FoundationSupport(position, FoundationCorner.SouthWest));
            }

            return result.ToArray();
        }

        private static GridPos Offset(GridPos position, Vector2Int direction) =>
            position.Offset(direction.x, direction.y);

        private static int RibPhaseFor(GridPos position)
        {
            // 카메라·열거 순서를 섞지 않는다. 같은 월드 칸은 항상 같은 이음새 위상을 갖는다.
            unchecked
            {
                int hash = position.x * 73856093;
                hash ^= position.y * 19349663;
                hash ^= position.elevation * 83492791;
                hash ^= hash >> 13;
                return hash & (RibPhaseCount - 1);
            }
        }

        private static List<GridPos> OrderedDistinct(IEnumerable<GridPos> candidates)
        {
            var seen = new HashSet<GridPos>();
            var ordered = new List<GridPos>();
            foreach (GridPos candidate in candidates)
            {
                if (seen.Add(candidate)) ordered.Add(candidate);
            }

            ordered.Sort(CompareWorldPosition);
            return ordered;
        }

        private static int CompareWorldPosition(GridPos a, GridPos b)
        {
            int elevation = a.elevation.CompareTo(b.elevation);
            if (elevation != 0) return elevation;
            int y = a.y.CompareTo(b.y);
            return y != 0 ? y : a.x.CompareTo(b.x);
        }

        private static void ValidateFrontDirections(Vector2Int frontA, Vector2Int frontB)
        {
            if (!IsCardinal(frontA) || !IsCardinal(frontB) ||
                frontA.x * frontB.x + frontA.y * frontB.y != 0)
            {
                throw new ArgumentException(
                    "Foundation front directions must be perpendicular cardinal vectors.");
            }
        }

        private static bool IsCardinal(Vector2Int direction) =>
            Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;
    }
}
