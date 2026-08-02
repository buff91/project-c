using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public sealed class FallResult
    {
        /// <summary>낙하자가 최종적으로 선 칸. 착지점이 점유돼 있으면 인접 칸으로 밀려난다.</summary>
        public GridPos FinalPosition;
        public int FloorsFallen;
        public int Damage;

        /// <summary>착지점에 서 있던 상대. 있으면 같은 낙뎀을 함께 받는다. (GDD §5.3)</summary>
        public CombatantState CrushedOccupant;
    }

    /// <summary>상태를 바꾸지 않고 계산한 낙하 착지와 피해. HUD와 실제 판정이 같은 계산을 쓴다.</summary>
    public readonly struct FallPreview
    {
        public GridPos Landing { get; }
        public int DropCells { get; }
        public int FloorsFallen { get; }
        public int Damage { get; }

        public FallPreview(GridPos landing, int dropCells, int floorsFallen, int damage)
        {
            Landing = landing;
            DropCells = dropCells;
            FloorsFallen = floorsFallen;
            Damage = damage;
        }
    }

    /// <summary>
    /// 모든 낙하 트리거의 수렴점 TryFall(). (GDD §5.3, M4)
    /// 구멍 진입·약한 바닥 붕괴·넉백 등 어떤 이유로든 낙하가 시작되면
    /// 아래 첫 단단한 바닥을 찾아 낙하 층수 → 낙뎀 곡선 → 착지 충돌을 한 곳에서 처리한다.
    /// 플레이어와 몬스터에 동일하게 적용된다.
    /// </summary>
    public static class FallRules
    {
        /// <summary>
        /// 낙하 층수별 낙뎀 누적 곡선: floors × (floors + 1). 1층=2, 2층=6, 3층=12.
        /// <para>
        /// <b>프로덕션 경로는 이 함수를 부르지 않는다</b> — 실제 낙뎀은 칸 기준
        /// <see cref="DamageForDrop"/>이 낸다. 이건 <b>기준 곡선(오라클)</b>이라
        /// 남긴다: 칸 기준 곡선이 층 낙하에서 여전히 2/6/12를 재현하는지
        /// <c>FallRulesTests</c>가 이 값과 대조한다. 호출부가 없다고 지우면 그 대조가 사라진다.
        /// </para>
        /// </summary>
        public static int DamageForFloors(int floorsFallen) =>
            floorsFallen <= 0 ? 0 : floorsFallen * (floorsFallen + 1);

        /// <summary>안전 낙하 높이 기본값(0) — 곡선 자체가 1칸을 무료로 만든다. 이후 액터 스탯으로 상향.</summary>
        public const int DefaultSafeFallHeight = 0;

        /// <summary>
        /// 낙하 칸수 기반 가속 곡선. eff = max(0, dropCells − safeFallHeight),
        /// 데미지 = round( (eff/EPF) × (eff/EPF + 1) ). EPF로 나누므로 한 층 낙하값(2/6/12)을
        /// 그대로 재현하고(EPF=4: 4칸=2·8칸=6·12칸=12), 층 안 낙하(2~3칸)도 작게 반영한다.
        /// 정수 반올림(round-half-up)이라 float 오차가 없다.
        /// </summary>
        public static int DamageForDrop(int dropCells, int elevationsPerFloor,
            int safeFallHeight = DefaultSafeFallHeight)
        {
            if (elevationsPerFloor < 1) throw new ArgumentOutOfRangeException(nameof(elevationsPerFloor));
            int eff = Math.Max(0, dropCells - safeFallHeight);
            if (eff <= 0) return 0;
            long numerator = (long)eff * (eff + elevationsPerFloor);        // eff·(eff+EPF)
            long denom = 2L * elevationsPerFloor * elevationsPerFloor;      // 2·EPF²
            return (int)((2 * numerator + (long)elevationsPerFloor * elevationsPerFloor) / denom);
        }

        /// <summary>
        /// 실제 낙하와 같은 착지·피해를 계산하되 전투 참가자 상태는 바꾸지 않는다.
        /// 의도적 낙하 확인 UI는 반드시 이 경로를 써야 실제 결과와 갈라지지 않는다.
        /// </summary>
        public static bool TryPreview(
            GridMap map,
            DungeonHeightModel height,
            GridPos from,
            int minElevation,
            int safeFallHeight,
            out FallPreview preview)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (height == null) throw new ArgumentNullException(nameof(height));

            preview = default;
            GridPos? landingOrNull = map.FindLandingBelow(from, minElevation);
            if (!landingOrNull.HasValue) return false;

            GridPos landing = landingOrNull.Value;
            int dropCells = from.elevation - landing.elevation;
            preview = new FallPreview(
                landing,
                dropCells,
                height.FloorIndex(from.elevation) - height.FloorIndex(landing.elevation),
                DamageForDrop(dropCells, height.ElevationsPerFloor, safeFallHeight));
            return true;
        }

        /// <summary>
        /// from 칸(구멍/허공)에서 낙하를 처리한다. 착지점이 없으면(무저갱) null.
        /// faller 의 위치·HP, 착지점 점유자의 HP 를 여기서 직접 갱신한다 — 연출은 호출부가.
        /// </summary>
        public static FallResult TryFall(
            GridMap map,
            DungeonHeightModel height,
            CombatantState faller,
            GridPos from,
            int minElevation,
            IReadOnlyList<CombatantState> others,
            int safeFallHeight = DefaultSafeFallHeight)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (height == null) throw new ArgumentNullException(nameof(height));
            if (faller == null) throw new ArgumentNullException(nameof(faller));

            if (!TryPreview(
                    map,
                    height,
                    from,
                    minElevation,
                    safeFallHeight,
                    out FallPreview preview))
                return null;

            GridPos landing = preview.Landing;

            var result = new FallResult
            {
                FloorsFallen = preview.FloorsFallen,
                Damage = preview.Damage
            };

            faller.TakeDamage(preview.Damage);

            CombatantState occupant = FindLivingAt(others, faller, landing);
            if (occupant != null)
            {
                occupant.TakeDamage(preview.Damage);
                result.CrushedOccupant = occupant;
            }

            // 착지점에 산 점유자가 남아 있으면 인접한 빈 바닥으로 밀려난다.
            result.FinalPosition = occupant != null && occupant.IsAlive
                ? FindFreeAdjacent(map, landing, others, faller) ?? landing
                : landing;
            faller.MoveTo(result.FinalPosition);
            return result;
        }

        private static CombatantState FindLivingAt(
            IReadOnlyList<CombatantState> others,
            CombatantState faller,
            GridPos pos)
        {
            if (others == null) return null;
            foreach (CombatantState other in others)
            {
                if (other == null || other == faller || !other.IsAlive) continue;
                if (other.Position == pos) return other;
            }
            return null;
        }

        private static GridPos? FindFreeAdjacent(
            GridMap map,
            GridPos center,
            IReadOnlyList<CombatantState> others,
            CombatantState faller)
        {
            foreach (GridPos candidate in new[] { center.North, center.East, center.South, center.West })
            {
                if (!map.IsWalkable(candidate)) continue;
                if (FindLivingAt(others, faller, candidate) != null) continue;
                return candidate;
            }
            return null;
        }
    }

    public enum KnockbackOutcome
    {
        None = 0,          // 벽/닫힌 문/점유/맵 밖 — 밀리지 않음
        Pushed,            // 한 칸 밀림 (도착 칸이 단단한 바닥)
        PushedIntoFall     // 한 칸 밀렸는데 그 칸이 구멍/허공 — TryFall 로 이어진다
    }

    /// <summary>
    /// 폭발 넉백. 중심에서 바깥으로 한 칸 밀며, 구멍/허공으로 밀리면 낙하 트리거가 된다.
    /// 낙하 처리 자체는 FallRules.TryFall 로 수렴한다. (GDD §5.3 넉백→구멍)
    /// </summary>
    public static class KnockbackRules
    {
        /// <summary>중심→대상의 우세 축 방향 단위 오프셋. 동률이면 x축 우선, 같은 칸이면 0.</summary>
        public static (int dx, int dy) PushDirection(GridPos center, GridPos target)
        {
            int dx = target.x - center.x;
            int dy = target.y - center.y;
            if (dx == 0 && dy == 0) return (0, 0);
            if (Math.Abs(dx) >= Math.Abs(dy)) return (Math.Sign(dx), 0);
            return (0, Math.Sign(dy));
        }

        public static KnockbackOutcome Resolve(
            GridMap map,
            GridPos center,
            GridPos target,
            Func<GridPos, bool> isOccupied,
            out GridPos destination)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            destination = target;
            (int dx, int dy) = PushDirection(center, target);
            if (dx == 0 && dy == 0) return KnockbackOutcome.None;

            GridPos pushed = target.Offset(dx, dy);
            if (isOccupied != null && isOccupied(pushed)) return KnockbackOutcome.None;

            TileData tile = map.Get(pushed);
            if (tile == null)
            {
                // 허공(void) — 같은 컬럼 아래에 바닥이 있으면 밀려 떨어진다.
                destination = pushed;
                return KnockbackOutcome.PushedIntoFall;
            }
            if (tile.CausesFall)
            {
                destination = pushed;
                return KnockbackOutcome.PushedIntoFall;
            }
            if (tile.IsWalkable)
            {
                destination = pushed;
                return KnockbackOutcome.Pushed;
            }
            return KnockbackOutcome.None; // 벽/닫힌 문
        }
    }
}
