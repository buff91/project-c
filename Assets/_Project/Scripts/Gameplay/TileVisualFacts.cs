using ProjectC.Core;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 타일 하나를 그리기 위해 **격자를 아는 쪽(호스트)이 미리 풀어 놓은 사실들**.
    ///
    /// 스프라이트 팩토리가 격자·던전·플레이어를 직접 보지 않게 만드는 경계다. 팩토리는
    /// "이 타일이 전면인가", "평면이 오른쪽으로 오르는가"를 계산할 방법이 없고, 알 필요도 없다.
    ///
    /// 채우는 비용에 주의한다 — <c>GetTileSprite</c>는 FOV 갱신마다 타일 수만큼 불린다.
    /// 그래서 호스트는 <see cref="PlaneRisesRight"/>·<see cref="SecretHinted"/>처럼
    /// 특정 종류에만 필요한 값을 **그 종류일 때만** 계산한다(원본의 지연 평가를 보존).
    /// </summary>
    internal readonly struct TileVisualFacts
    {
        internal TileVisualFacts(
            DungeonVisualContext context,
            bool extruded,
            bool planeRisesRight,
            bool secretHinted,
            bool hubMode,
            bool hospitalDressing)
        {
            Context = context;
            Extruded = extruded;
            PlaneRisesRight = planeRisesRight;
            SecretHinted = secretHinted;
            HubMode = hubMode;
            HospitalDressing = hospitalDressing;
        }

        /// <summary>진행 지수·국소 높이 등 깊이 맥락. 진행 지수는 레이아웃이 소유한다(고도로 역산하지 않는다).</summary>
        internal DungeonVisualContext Context { get; }

        /// <summary>전면 모서리이거나 높여진 타일 — 48px 텍스처에 측면 두께를 그린다.</summary>
        internal bool Extruded { get; }

        /// <summary>
        /// 문/계단의 아이소 평면이 화면 오른쪽으로 오르는지. 종류에 맞는 질의를 호스트가 고른다
        /// (문은 통로 축, 계단은 상단 착지점). 해당 없는 종류에서는 의미 없는 값이다.
        /// </summary>
        internal bool PlaneRisesRight { get; }

        /// <summary>비밀문이 인접 조사 가능 거리라 균열이 은은히 빛나는 상태인지.</summary>
        internal bool SecretHinted { get; }

        /// <summary>허브 씬인지 — 바닥 분기와 측면 두께 색이 달라진다.</summary>
        internal bool HubMode { get; }

        /// <summary>Facility 지역의 폐병원 전용 바닥·벽 드레싱을 사용할지.</summary>
        internal bool HospitalDressing { get; }
    }
}
