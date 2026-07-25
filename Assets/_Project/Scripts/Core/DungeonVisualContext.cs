using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 첫 던전의 진행 깊이를 콘텐츠/비주얼 구간으로 묶는다.
    /// 같은 구간 안의 elevation과 LocalHeight는 이 값에 영향을 주지 않는다.
    /// </summary>
    public enum DungeonDepthBand
    {
        Shallow,
        Mid,
        Deep,
        Boss
    }

    public static class DungeonDepthBandRules
    {
        // 구간 경계의 단일 출처. 라벨(RangeLabel)도 이 값에서 만들어 판정과 표기가 어긋나지 않게 한다.
        public const int ShallowLastDepth = 2;
        public const int MidLastDepth = 5;
        public const int DeepLastDepth = 8;

        public static DungeonDepthBand ForDepth(int depthIndex)
        {
            int depth = Math.Max(0, depthIndex);
            if (depth <= ShallowLastDepth) return DungeonDepthBand.Shallow;
            if (depth <= MidLastDepth) return DungeonDepthBand.Mid;
            if (depth <= DeepLastDepth) return DungeonDepthBand.Deep;
            return DungeonDepthBand.Boss;
        }

        public static DungeonDepthBand ForFloor(int floorIndex) =>
            ForDepth(Math.Max(0, -floorIndex));

        /// <summary>계측 리포트에서 구간을 가리키는 사람이 읽는 층 범위. (깊이 0 = B1)</summary>
        public static string RangeLabel(DungeonDepthBand band)
        {
            switch (band)
            {
                case DungeonDepthBand.Shallow:
                    return $"B1~B{ShallowLastDepth + 1}";
                case DungeonDepthBand.Mid:
                    return $"B{ShallowLastDepth + 2}~B{MidLastDepth + 1}";
                case DungeonDepthBand.Deep:
                    return $"B{MidLastDepth + 2}~B{DeepLastDepth + 1}";
                default:
                    return $"B{DeepLastDepth + 2}+";
            }
        }
    }

    /// <summary>
    /// 던전 타일의 시각적 의미를 해석할 때 쓰는 명시적 공간 컨텍스트.
    /// 진행 깊이(FloorIndex/DepthIndex), 연속 공간 좌표(Elevation),
    /// 같은 던전 층 안의 단차(LocalHeight)를 서로 다른 값으로 제공한다.
    /// </summary>
    public readonly struct DungeonVisualContext : IEquatable<DungeonVisualContext>
    {
        public int FloorIndex { get; }
        public int DepthIndex { get; }
        public DungeonDepthBand DepthBand => DungeonDepthBandRules.ForDepth(DepthIndex);
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
