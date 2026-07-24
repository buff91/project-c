using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 던전 타일의 시각적 의미를 해석할 때 쓰는 명시적 공간 컨텍스트.
    /// 진행 깊이(FloorIndex/DepthIndex), 연속 공간 좌표(Elevation),
    /// 같은 던전 층 안의 단차(LocalHeight)를 서로 다른 값으로 제공한다.
    /// </summary>
    public readonly struct DungeonVisualContext : IEquatable<DungeonVisualContext>
    {
        public int FloorIndex { get; }
        public int DepthIndex { get; }
        public int Elevation { get; }
        public int LocalHeight { get; }
        public bool IsRaised => LocalHeight > 0;

        private DungeonVisualContext(
            int floorIndex,
            int elevation,
            int localHeight)
        {
            FloorIndex = floorIndex;
            DepthIndex = Math.Max(0, -floorIndex);
            Elevation = elevation;
            LocalHeight = localHeight;
        }

        public static DungeonVisualContext From(
            DungeonHeightModel height,
            int elevation)
        {
            if (height == null) throw new ArgumentNullException(nameof(height));
            return new DungeonVisualContext(
                height.FloorIndex(elevation),
                elevation,
                height.LocalHeight(elevation));
        }

        /// <summary>던전 모델이 아직 없는 에디터 프리뷰용 B1 평면 컨텍스트.</summary>
        public static DungeonVisualContext Preview(int elevation = 0) =>
            new DungeonVisualContext(0, elevation, Math.Max(0, elevation));

        public bool Equals(DungeonVisualContext other) =>
            FloorIndex == other.FloorIndex &&
            DepthIndex == other.DepthIndex &&
            Elevation == other.Elevation &&
            LocalHeight == other.LocalHeight;

        public override bool Equals(object obj) =>
            obj is DungeonVisualContext other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + FloorIndex;
                hash = hash * 31 + Elevation;
                hash = hash * 31 + LocalHeight;
                return hash;
            }
        }

        public static bool operator ==(
            DungeonVisualContext left,
            DungeonVisualContext right) =>
            left.Equals(right);

        public static bool operator !=(
            DungeonVisualContext left,
            DungeonVisualContext right) =>
            !left.Equals(right);
    }
}
