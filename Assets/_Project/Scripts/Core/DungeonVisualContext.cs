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

        // ForFloor(floorIndex) 는 제거했다. floorIndex 부호로 진행을 역산하던 함수라
        // 상승 던전에서 전부 0(첫 구간)으로 붕괴했고, 비단조 경로에서는 애초에 성립하지 않는다.
        // 구간이 필요하면 ForDepth(진행 지수)를 쓰고, 진행 지수는
        // DungeonLayout.ProgressIndexFor(floorIndex) 로 얻는다.

        /// <summary>
        /// 계측 리포트에서 구간을 가리키는 사람이 읽는 <b>진행</b> 범위.
        /// <para>
        /// 예전에는 <c>B1~B3</c>처럼 지하 층 표기를 썼는데, 던전이 상승·평면일 수도 있으므로
        /// (GDD §10.1) 화면에 거짓이 된다. 진행 순서는 방향과 무관하므로 "몇 번째 층"으로 쓴다 —
        /// 아케이드 타워에서 1~3번째는 B2·B1·1F이고, 하강 던전에서는 B1~B3이다.
        /// </para>
        /// </summary>
        public static string RangeLabel(DungeonDepthBand band)
        {
            switch (band)
            {
                case DungeonDepthBand.Shallow:
                    return $"1~{ShallowLastDepth + 1}번째";
                case DungeonDepthBand.Mid:
                    return $"{ShallowLastDepth + 2}~{MidLastDepth + 1}번째";
                case DungeonDepthBand.Deep:
                    return $"{MidLastDepth + 2}~{DeepLastDepth + 1}번째";
                default:
                    return $"{DeepLastDepth + 2}번째+";
            }
        }

        /// <summary>
        /// 구간의 사람이 읽는 이름. 열거자 이름(Shallow/Mid/Deep)은 깊이 어휘라
        /// 상승 던전에서 어긋나므로, 화면·리포트에는 방향 중립어를 쓴다.
        /// 열거자 자체는 코드/JSON 키로 그대로 둔다(과거 리포트 호환).
        /// </summary>
        public static string BandLabel(DungeonDepthBand band)
        {
            switch (band)
            {
                case DungeonDepthBand.Shallow: return "초반";
                case DungeonDepthBand.Mid: return "중반";
                case DungeonDepthBand.Deep: return "후반";
                default: return "보스";
            }
        }
    }

    /// <summary>
    /// 던전 타일의 시각적 의미를 해석할 때 쓰는 명시적 공간 컨텍스트.
    /// 진행(ProgressIndex), 공간 구획(FloorIndex), 연속 공간 좌표(Elevation),
    /// 같은 던전 층 안의 단차(LocalHeight)를 서로 다른 값으로 제공한다.
    /// <para>
    /// <b>ProgressIndex 는 반드시 호출자가 넘긴다.</b> 예전에는 <c>Max(0, -floorIndex)</c>로
    /// 파생했지만, 그러면 상승 던전에서 전부 0으로 붕괴하고 비단조 경로에서는 성립하지 않는다.
    /// 값의 출처는 <see cref="DungeonLayout.ProgressIndexFor"/> 하나다.
    /// </para>
    /// </summary>
    public readonly struct DungeonVisualContext : IEquatable<DungeonVisualContext>
    {
        public int FloorIndex { get; }

        /// <summary>몇 번째로 방문하는 층인가(0부터). 구간·변주 판정의 유일한 키.</summary>
        public int ProgressIndex { get; }

        public DungeonDepthBand DepthBand => DungeonDepthBandRules.ForDepth(ProgressIndex);
        public int Elevation { get; }
        public int LocalHeight { get; }
        public bool IsRaised => LocalHeight > 0;

        private DungeonVisualContext(
            int floorIndex,
            int progressIndex,
            int elevation,
            int localHeight)
        {
            FloorIndex = floorIndex;
            ProgressIndex = progressIndex < 0 ? 0 : progressIndex;
            Elevation = elevation;
            LocalHeight = localHeight;
        }

        public static DungeonVisualContext From(
            DungeonHeightModel height,
            int elevation,
            int progressIndex)
        {
            if (height == null) throw new ArgumentNullException(nameof(height));
            return new DungeonVisualContext(
                height.FloorIndex(elevation),
                progressIndex,
                elevation,
                height.LocalHeight(elevation));
        }

        /// <summary>레이아웃에서 진행 지수를 직접 찾아 만든다(권장 경로).</summary>
        public static DungeonVisualContext From(DungeonLayout layout, int elevation)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            int floorIndex = layout.Height.FloorIndex(elevation);
            return From(layout.Height, elevation, layout.ProgressIndexFor(floorIndex));
        }

        /// <summary>던전 모델이 아직 없는 에디터 프리뷰용 첫 층 평면 컨텍스트.</summary>
        public static DungeonVisualContext Preview(int elevation = 0, int progressIndex = 0) =>
            new DungeonVisualContext(0, progressIndex, elevation, Math.Max(0, elevation));

        public bool Equals(DungeonVisualContext other) =>
            FloorIndex == other.FloorIndex &&
            ProgressIndex == other.ProgressIndex &&
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
                // ProgressIndex 는 이제 파생값이 아니라 독립 필드라 해시에도 넣는다.
                hash = hash * 31 + ProgressIndex;
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
