using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 다층 격자의 논리 좌표. (GDD §5.1)
    /// x, y : 평면 격자 좌표
    /// elevation : 연속적인 높이값. 단순 "층 번호"가 아니라 한 층 안에서도 높이차가 존재.
    /// </summary>
    [Serializable]
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int x;
        public readonly int y;
        public readonly int elevation;

        public GridPos(int x, int y, int elevation = 0)
        {
            this.x = x;
            this.y = y;
            this.elevation = elevation;
        }

        /// <summary>같은 elevation 유지한 채 (dx, dy) 이동.</summary>
        public GridPos Offset(int dx, int dy) => new GridPos(x + dx, y + dy, elevation);

        /// <summary>elevation 만 변경.</summary>
        public GridPos WithElevation(int newElevation) => new GridPos(x, y, newElevation);

        /// <summary>같은 층(elevation) 내 4방향 이웃.</summary>
        public GridPos North => Offset(0, 1);
        public GridPos South => Offset(0, -1);
        public GridPos East  => Offset(1, 0);
        public GridPos West  => Offset(-1, 0);

        /// <summary>elevation 무시한 평면 맨해튼 거리. (같은 층 이동 비용 근사)</summary>
        public int ManhattanTo(GridPos other) => Math.Abs(x - other.x) + Math.Abs(y - other.y);

        /// <summary>elevation 무시한 평면 체비셰프 거리. (8방향 이동/시야 근사)</summary>
        public int ChebyshevTo(GridPos other) => Math.Max(Math.Abs(x - other.x), Math.Abs(y - other.y));

        public bool Equals(GridPos other) => x == other.x && y == other.y && elevation == other.elevation;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode()
        {
            // 좌표 3개를 섞은 해시. 격자맵의 Dictionary 키로 쓰이므로 분포 중요.
            unchecked
            {
                int h = 17;
                h = h * 31 + x;
                h = h * 31 + y;
                h = h * 31 + elevation;
                return h;
            }
        }

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({x}, {y}, e{elevation})";
    }
}
