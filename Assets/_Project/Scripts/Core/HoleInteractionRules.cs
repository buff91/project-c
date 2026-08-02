using System;

namespace ProjectC.Core
{
    public enum HoleDropTapDecision
    {
        Arm,
        Confirm
    }

    /// <summary>
    /// 구멍을 통해 보이는 착지 결과. 실제 낙하를 실행하지 않고도 목적지와 피해를
    /// 먼저 설명할 수 있게, <see cref="FallRules"/>와 같은 입력으로 계산한다.
    /// </summary>
    public readonly struct HoleDropPreview
    {
        public GridPos Hole { get; }
        public GridPos Landing { get; }
        public int DestinationFloorIndex { get; }
        public int DropCells { get; }
        public int Damage { get; }
        public FallMeaning Meaning { get; }

        public HoleDropPreview(
            GridPos hole,
            GridPos landing,
            int destinationFloorIndex,
            int dropCells,
            int damage,
            FallMeaning meaning)
        {
            Hole = hole;
            Landing = landing;
            DestinationFloorIndex = destinationFloorIndex;
            DropCells = dropCells;
            Damage = damage;
            Meaning = meaning;
        }
    }

    /// <summary>
    /// 구멍 탭을 미리보기 → 확정의 두 단계로 나누는 순수 규칙.
    /// 첫 탭은 턴을 쓰지 않고 정보를 고정하며, 같은 구멍을 다시 탭할 때만 낙하한다.
    /// </summary>
    public static class HoleInteractionRules
    {
        public static HoleDropTapDecision ResolveTap(GridPos? armedHole, GridPos tappedHole) =>
            armedHole.HasValue && armedHole.Value == tappedHole
                ? HoleDropTapDecision.Confirm
                : HoleDropTapDecision.Arm;

        public static bool TryCreatePreview(
            GridMap map,
            DungeonHeightModel height,
            GridPos hole,
            int minElevation,
            DungeonProgressDirection direction,
            int safeFallHeight,
            out HoleDropPreview preview)
        {
            preview = default;
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (height == null) throw new ArgumentNullException(nameof(height));

            TileData tile = map.Get(hole);
            if (tile == null || tile.kind != TileKind.Hole)
                return false;

            if (!FallRules.TryPreview(
                    map,
                    height,
                    hole,
                    minElevation,
                    safeFallHeight,
                    out FallPreview fall))
                return false;

            preview = new HoleDropPreview(
                hole,
                fall.Landing,
                height.FloorIndex(fall.Landing.elevation),
                fall.DropCells,
                fall.Damage,
                DungeonDirectionRules.FallMeaningFor(direction));
            return true;
        }
    }
}
