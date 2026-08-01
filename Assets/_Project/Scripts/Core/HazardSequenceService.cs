using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>낙하를 시작한 규칙 원인. 연출 문자열과 분리해 텔레메트리 의미를 고정한다.</summary>
    public enum HazardFallCause
    {
        IntentionalDrop = 0,
        FloorCollapse = 1,
        Knockback = 2
    }

    /// <summary>폭발의 원소 축. 불 여부를 bool로 전달할 때 생기는 호출부 의미 손실을 막는다.</summary>
    public enum ExplosionElement
    {
        Fire = 0,
        Frost = 1
    }

    /// <summary>
    /// 한 위험물 시퀀스가 참조하는 순수 게임 상태.
    /// 참가자 목록의 구성은 생성 시 고정하지만 각 참가자의 위치·HP는 항상 최신 값을 읽는다.
    /// </summary>
    public sealed class HazardSequenceState
    {
        public GridMap Map { get; }
        public DungeonHeightModel Height { get; }
        public int MinimumElevation { get; }
        public IReadOnlyList<CombatantState> Combatants { get; }

        public HazardSequenceState(
            GridMap map,
            DungeonHeightModel height,
            int minimumElevation,
            IReadOnlyList<CombatantState> combatants)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Height = height ?? throw new ArgumentNullException(nameof(height));
            MinimumElevation = minimumElevation;

            if (combatants == null || combatants.Count == 0)
            {
                Combatants = Array.Empty<CombatantState>();
                return;
            }

            var copy = new CombatantState[combatants.Count];
            for (int i = 0; i < combatants.Count; i++)
                copy[i] = combatants[i];
            Combatants = Array.AsReadOnly(copy);
        }

        /// <summary>사망자는 칸을 막지 않는다. 참가자의 이동은 DTO 생성 뒤에도 즉시 반영된다.</summary>
        public bool IsOccupiedExcept(GridPos position, CombatantState except)
        {
            foreach (CombatantState combatant in Combatants)
            {
                if (combatant == null || combatant == except || !combatant.IsAlive) continue;
                if (combatant.Position == position) return true;
            }
            return false;
        }
    }

    /// <summary>낙하 규칙 결과와 그 원인을 함께 보존하는 상태 전이 DTO.</summary>
    public sealed class FallSequenceResolution
    {
        public CombatantState Faller { get; }
        public GridPos Origin { get; }
        public HazardFallCause Cause { get; }
        public FallResult Fall { get; }

        public bool HasLanding => Fall != null;
        public bool IsIntentional => Cause == HazardFallCause.IntentionalDrop;

        internal FallSequenceResolution(
            CombatantState faller,
            GridPos origin,
            HazardFallCause cause,
            FallResult fall)
        {
            Faller = faller ?? throw new ArgumentNullException(nameof(faller));
            Origin = origin;
            Cause = cause;
            Fall = fall;
        }
    }

    /// <summary>
    /// 순차 넉백 한 대상의 계획. 실제 이동은 연출 코루틴 뒤에 적용해야 하므로 여기서는 상태를 바꾸지 않는다.
    /// </summary>
    public readonly struct KnockbackSequenceStep
    {
        public CombatantState Target { get; }
        public GridPos Origin { get; }
        public KnockbackOutcome Outcome { get; }
        public GridPos Destination { get; }
        public bool CollapsesWeakFloor { get; }

        internal KnockbackSequenceStep(
            CombatantState target,
            GridPos origin,
            KnockbackOutcome outcome,
            GridPos destination,
            bool collapsesWeakFloor)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Origin = origin;
            Outcome = outcome;
            Destination = destination;
            CollapsesWeakFloor = collapsesWeakFloor;
        }
    }

    /// <summary>Gameplay가 상태를 적용하고 같은 결과를 연출·텔레메트리에 쓰기 위한 의도 DTO.</summary>
    public readonly struct HazardStatusIntent
    {
        public CombatantState Target { get; }
        public StatusKind Kind { get; }
        public int Turns { get; }

        internal HazardStatusIntent(CombatantState target, StatusKind kind, int turns)
        {
            if (turns <= 0) throw new ArgumentOutOfRangeException(nameof(turns));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Kind = kind;
            Turns = turns;
        }
    }

    /// <summary>폭발 피해·지형 변경까지 끝난 첫 단계의 결과.</summary>
    public sealed class ExplosionSequenceStart
    {
        public GridPos Center { get; }
        public int Damage { get; }
        public ExplosionElement Element { get; }
        public BombResult Blast { get; }
        public IReadOnlyList<GridPos> RevealedSecretDoors { get; }

        internal ExplosionSequenceStart(
            GridPos center,
            int damage,
            ExplosionElement element,
            BombResult blast,
            IReadOnlyList<GridPos> revealedSecretDoors)
        {
            Center = center;
            Damage = damage;
            Element = element;
            Blast = blast ?? throw new ArgumentNullException(nameof(blast));
            RevealedSecretDoors = revealedSecretDoors ?? Array.Empty<GridPos>();
        }
    }

    /// <summary>직접 피격 상태 다음에 처리하는 기름·물 반응 결과.</summary>
    public sealed class ExplosionElementAftermath
    {
        public IReadOnlyList<GridPos> IgnitedOil { get; }
        public IReadOnlyList<GridPos> EvaporatedWater { get; }
        public IReadOnlyList<GridPos> FrozenWater { get; }
        public IReadOnlyList<HazardStatusIntent> SurfaceStatuses { get; }

        internal ExplosionElementAftermath(
            IReadOnlyList<GridPos> ignitedOil,
            IReadOnlyList<GridPos> evaporatedWater,
            IReadOnlyList<GridPos> frozenWater,
            IReadOnlyList<HazardStatusIntent> surfaceStatuses)
        {
            IgnitedOil = ignitedOil ?? Array.Empty<GridPos>();
            EvaporatedWater = evaporatedWater ?? Array.Empty<GridPos>();
            FrozenWater = frozenWater ?? Array.Empty<GridPos>();
            SurfaceStatuses = surfaceStatuses ?? Array.Empty<HazardStatusIntent>();
        }
    }

    /// <summary>
    /// 낙하·폭발의 순수 상태 전이를 단계별로 계산한다.
    /// 연출 사이에 생존 상태가 바뀔 수 있고 넉백은 앞 대상의 이동을 뒤 대상이 봐야 하므로,
    /// 전체 폭발을 한 번에 미리 계산하지 않는다.
    /// </summary>
    public static class HazardSequenceService
    {
        public const int ExplosionStatusTurns = 2;

        public static FallSequenceResolution ResolveFall(
            HazardSequenceState state,
            CombatantState faller,
            GridPos origin,
            HazardFallCause cause,
            int safeFallHeight = FallRules.DefaultSafeFallHeight)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (faller == null) throw new ArgumentNullException(nameof(faller));
            if (safeFallHeight < 0) throw new ArgumentOutOfRangeException(nameof(safeFallHeight));

            FallResult fall = FallRules.TryFall(
                state.Map,
                state.Height,
                faller,
                origin,
                state.MinimumElevation,
                state.Combatants,
                safeFallHeight);
            return new FallSequenceResolution(faller, origin, cause, fall);
        }

        /// <summary>
        /// 현재 위치·점유를 읽어 한 대상의 넉백을 계획한다. 호출자는 이 단계를 적용한 뒤
        /// 다음 대상을 다시 계획해야 동적 점유 순서가 보존된다.
        /// </summary>
        public static KnockbackSequenceStep PlanKnockback(
            HazardSequenceState state,
            GridPos center,
            CombatantState target)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (target == null) throw new ArgumentNullException(nameof(target));

            GridPos origin = target.Position;
            KnockbackOutcome outcome = KnockbackRules.Resolve(
                state.Map,
                center,
                origin,
                pos => state.IsOccupiedExcept(pos, target),
                out GridPos destination);
            bool collapsesWeakFloor = outcome == KnockbackOutcome.Pushed &&
                                      state.Map.Get(destination)?.kind == TileKind.WeakFloor;
            return new KnockbackSequenceStep(
                target,
                origin,
                outcome,
                destination,
                collapsesWeakFloor);
        }

        /// <summary>폭발 피해, 약한 바닥·창문 변화, 비밀문 공개까지 첫 단계를 적용한다.</summary>
        public static ExplosionSequenceStart BeginExplosion(
            HazardSequenceState state,
            GridPos center,
            int damage,
            ExplosionElement element)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            ValidateElement(element);

            BombResult blast = BombRules.Detonate(state.Map, center, state.Combatants, damage);
            List<GridPos> revealed = SecretRoomRules.RevealInBlast(state.Map, center);
            return new ExplosionSequenceStart(center, damage, element, blast, revealed);
        }

        /// <summary>
        /// 피격 연출이 끝난 시점의 생존자를 읽어 직접 화상/빙결 의도를 만든다.
        /// 갓 모드처럼 연출 중 되살아난 대상도 이 단계에서는 포함된다.
        /// </summary>
        public static IReadOnlyList<HazardStatusIntent> PlanBlastStatuses(
            ExplosionSequenceStart start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));

            StatusKind kind = StatusFor(start.Element);
            var intents = new List<HazardStatusIntent>();
            foreach (CombatantState damaged in start.Blast.Damaged)
            {
                if (damaged == null || !damaged.IsAlive) continue;
                intents.Add(new HazardStatusIntent(damaged, kind, ExplosionStatusTurns));
            }
            return intents;
        }

        /// <summary>
        /// 직접 상태 부여 다음 단계의 표면 반응을 적용하고, 그 표면 위 생존자 상태 의도를 반환한다.
        /// 직접 피격자와 표면 위 대상이 같으면 두 의도를 유지한다(재부여/텔레메트리 계약).
        /// </summary>
        public static ExplosionElementAftermath ResolveElementAftermath(
            HazardSequenceState state,
            ExplosionSequenceStart start)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (start == null) throw new ArgumentNullException(nameof(start));

            if (start.Element == ExplosionElement.Fire)
            {
                List<GridPos> ignited = OilRules.Ignite(state.Map, start.Center);
                List<GridPos> evaporated = WaterRules.Evaporate(state.Map, start.Center);
                return new ExplosionElementAftermath(
                    ignited,
                    evaporated,
                    Array.Empty<GridPos>(),
                    PlanSurfaceStatuses(state, ignited, StatusKind.Burn));
            }

            if (start.Element == ExplosionElement.Frost)
            {
                List<GridPos> frozen = WaterRules.ChainFreeze(state.Map, start.Center);
                return new ExplosionElementAftermath(
                    Array.Empty<GridPos>(),
                    Array.Empty<GridPos>(),
                    frozen,
                    PlanSurfaceStatuses(state, frozen, StatusKind.Freeze));
            }

            throw new ArgumentOutOfRangeException(nameof(start), start.Element, "알 수 없는 폭발 원소입니다.");
        }

        private static IReadOnlyList<HazardStatusIntent> PlanSurfaceStatuses(
            HazardSequenceState state,
            IReadOnlyList<GridPos> tiles,
            StatusKind kind)
        {
            if (tiles == null || tiles.Count == 0)
                return Array.Empty<HazardStatusIntent>();

            var region = new HashSet<GridPos>();
            foreach (GridPos tile in tiles) region.Add(tile);

            var intents = new List<HazardStatusIntent>();
            foreach (CombatantState combatant in state.Combatants)
            {
                if (combatant == null || !combatant.IsAlive || !region.Contains(combatant.Position))
                    continue;
                intents.Add(new HazardStatusIntent(combatant, kind, ExplosionStatusTurns));
            }
            return intents;
        }

        private static StatusKind StatusFor(ExplosionElement element)
        {
            if (element == ExplosionElement.Fire) return StatusKind.Burn;
            if (element == ExplosionElement.Frost) return StatusKind.Freeze;
            throw new ArgumentOutOfRangeException(nameof(element), element, "알 수 없는 폭발 원소입니다.");
        }

        private static void ValidateElement(ExplosionElement element)
        {
            if (element != ExplosionElement.Fire && element != ExplosionElement.Frost)
                throw new ArgumentOutOfRangeException(
                    nameof(element), element, "알 수 없는 폭발 원소입니다.");
        }
    }
}
