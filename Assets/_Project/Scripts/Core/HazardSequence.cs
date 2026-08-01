using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>연출이 재생해야 할 위험 연쇄의 한 단계.</summary>
    public enum HazardStepKind
    {
        /// <summary>폭발이 터졌다. 판정보다 먼저 나오므로 폭발 연출의 시작점이다.</summary>
        Detonated,
        Damaged,
        WeakFloorsCollapsed,
        SecretDoorsRevealed,
        StatusApplied,
        OilIgnited,
        WaterEvaporated,
        WaterFrozen,

        /// <summary>낙하 없이 한 칸 밀렸다.</summary>
        Knocked,
        Fell,

        /// <summary>낙하 착지점에 서 있던 상대가 함께 맞았다.</summary>
        Crushed,

        /// <summary>폭발통이 유폭한다. 바로 뒤에 그 폭발의 <see cref="Detonated"/>가 이어진다.</summary>
        BarrelChained,
    }

    /// <summary>
    /// 위험 연쇄 한 단계의 기록. 소비자는 <see cref="Kind"/>로 갈라 필요한 필드만 읽는다.
    /// </summary>
    public sealed class HazardStep
    {
        public HazardStepKind Kind;

        /// <summary>피해·상태·넉백·낙하의 대상.</summary>
        public CombatantState Actor;

        /// <summary>폭발 중심 · 낙하 출발 칸.</summary>
        public GridPos Origin;

        /// <summary>밀려간 칸 · 착지 칸.</summary>
        public GridPos Destination;

        /// <summary>피해량 또는 상태이상 지속 턴.</summary>
        public int Amount;

        public int FloorsFallen;
        public bool Fiery;
        public StatusKind Status;
        public StatusApplyResult StatusResult;

        /// <summary>피해 원인(<c>Bomb</c>/<c>FrostBomb</c>/<c>Fall</c>/<c>Crush</c>) 또는 낙하 사유.</summary>
        public string Source;

        /// <summary>붕괴·발화·증발·결빙·숨은 문처럼 칸 묶음으로 일어난 변화.</summary>
        public IReadOnlyList<GridPos> Cells;

        public override string ToString() =>
            Actor != null ? $"{Kind}({Actor.Id})" : Kind.ToString();
    }

    /// <summary>폭발통 하나의 상태. 없으면 <c>null</c>을 넘긴다.</summary>
    public sealed class HazardBarrel
    {
        public GridPos Position;
        public bool Exploded;
        public int Damage;
    }

    /// <summary>위험 연쇄가 읽고 바꾸는 판 상태. 씬을 모른다.</summary>
    public sealed class HazardContext
    {
        public GridMap Map;
        public DungeonHeightModel Height;

        /// <summary>플레이어를 포함한 살아있는·죽은 전투 참가자 전원.</summary>
        public IReadOnlyList<CombatantState> Combatants;

        /// <summary>사망 시 연쇄를 끊는 기준이 되는 플레이어. 없으면 <c>null</c>.</summary>
        public CombatantState Player;

        public int BottomElevation;

        /// <summary>플레이어에게만 적용되는 안전 낙하 높이(장비). 몬스터는 기본값을 쓴다.</summary>
        public int PlayerSafeFallHeight = FallRules.DefaultSafeFallHeight;

        /// <summary>폭발이 부여하는 화상/빙결 지속 턴.</summary>
        public int StatusTurns = 2;

        public HazardBarrel Barrel;
    }

    /// <summary>
    /// 낙하·폭발 연쇄의 <b>순서</b>를 소유한다. 개별 규칙(<see cref="BombRules"/>·
    /// <see cref="KnockbackRules"/>·<see cref="FallRules"/>·<see cref="OilRules"/>·
    /// <see cref="WaterRules"/>)은 그대로 두고, 그것들을 <b>어떤 차례로 엮는가</b>만 여기로 모은다.
    /// <para>
    /// 그 순서가 지금까지 <c>IsoPrototypeDemo</c>의 코루틴 안에만 있었다 — 피해보다 상태가
    /// 먼저인지, 넉백이 원소 반응 뒤인지, 플레이어가 죽으면 폭발통이 유폭하는지 같은 판정이
    /// 연출 코드와 섞여 있어 회귀로 고정할 수 없었다. 순수 C#으로 내려 계약을 테스트가 진다.
    /// </para>
    /// <para>
    /// 규칙들이 판을 실제로 바꾸므로(피해·붕괴·발화) 이 서비스도 <b>같은 변경을 일으키고</b>,
    /// 무엇이 일어났는지를 <see cref="HazardStep"/> 목록으로 남긴다. 호출부는 그 목록을
    /// 재생만 한다 — 애니메이션·피드백 문구·텔레메트리·뷰 동기화는 전부 소비자 몫이고,
    /// 연쇄 판단은 하나도 남지 않는다.
    /// </para>
    /// </summary>
    public static class HazardSequence
    {
        /// <summary>폭발 한 번의 전체 연쇄. 유폭까지 평탄하게 이어 붙인다.</summary>
        public static List<HazardStep> Explode(
            HazardContext context,
            GridPos center,
            int damage,
            bool fiery)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Map == null) throw new ArgumentNullException(nameof(context.Map));

            var steps = new List<HazardStep>();
            Explode(context, center, damage, fiery, steps);
            return steps;
        }

        /// <summary>
        /// 어떤 이유로든 시작된 낙하 하나. <paramref name="cause"/>는 그대로
        /// <see cref="HazardStep.Source"/>에 실려 연출·텔레메트리가 의도 낙하를 구분한다.
        /// </summary>
        public static List<HazardStep> Fall(
            HazardContext context,
            CombatantState faller,
            GridPos from,
            string cause)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (faller == null) throw new ArgumentNullException(nameof(faller));

            var steps = new List<HazardStep>();
            Fall(context, faller, from, cause, steps);
            return steps;
        }

        private static void Explode(
            HazardContext context,
            GridPos center,
            int damage,
            bool fiery,
            List<HazardStep> steps)
        {
            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.Detonated,
                Origin = center,
                Amount = damage,
                Fiery = fiery,
            });

            BombResult blast = BombRules.Detonate(
                context.Map, center, context.Combatants, damage);
            List<GridPos> revealed = SecretRoomRules.RevealInBlast(context.Map, center);
            string source = fiery ? "Bomb" : "FrostBomb";

            foreach (CombatantState damaged in blast.Damaged)
                steps.Add(new HazardStep
                {
                    Kind = HazardStepKind.Damaged,
                    Actor = damaged,
                    Origin = center,
                    Amount = damage,
                    Source = source,
                });

            if (blast.CollapsedWeakFloors.Count > 0)
                steps.Add(new HazardStep
                {
                    Kind = HazardStepKind.WeakFloorsCollapsed,
                    Origin = center,
                    Cells = blast.CollapsedWeakFloors,
                });

            if (revealed.Count > 0)
                steps.Add(new HazardStep
                {
                    Kind = HazardStepKind.SecretDoorsRevealed,
                    Origin = center,
                    Cells = revealed,
                });

            // 상태 부여: 불 폭발은 화상, 냉기 폭발은 빙결. (GDD §5.5)
            StatusKind blastStatus = fiery ? StatusKind.Burn : StatusKind.Freeze;
            foreach (CombatantState survivor in blast.Damaged)
                ApplyStatus(context, survivor, blastStatus, steps);

            // 요소 반응은 넉백보다 먼저다 — 밀려나기 전에 서 있던 칸의 반응을 맞는다.
            if (fiery)
            {
                List<GridPos> ignited = OilRules.Ignite(context.Map, center);
                if (ignited.Count > 0)
                {
                    steps.Add(new HazardStep
                    {
                        Kind = HazardStepKind.OilIgnited,
                        Origin = center,
                        Cells = ignited,
                    });
                    ApplyStatusInRegion(context, ignited, StatusKind.Burn, steps);
                }

                List<GridPos> dried = WaterRules.Evaporate(context.Map, center);
                steps.Add(new HazardStep
                {
                    Kind = HazardStepKind.WaterEvaporated,
                    Origin = center,
                    Cells = dried,
                });
            }
            else
            {
                List<GridPos> frozen = WaterRules.ChainFreeze(context.Map, center);
                if (frozen.Count > 0)
                {
                    steps.Add(new HazardStep
                    {
                        Kind = HazardStepKind.WaterFrozen,
                        Origin = center,
                        Cells = frozen,
                    });
                    ApplyStatusInRegion(context, frozen, StatusKind.Freeze, steps);
                }
            }

            // 넉백: 맞고 살아남은 전원을 중심 반대쪽으로. 플레이어도 예외 없다. (GDD §5.3)
            foreach (CombatantState survivor in blast.Damaged)
            {
                if (!survivor.IsAlive) continue;
                Knockback(context, center, survivor, steps);
                if (PlayerIsDown(context)) break; // 사망 — 남은 넉백만 생략한다
            }

            if (PlayerIsDown(context)) return;
            if (!fiery || context.Barrel == null || context.Barrel.Exploded) return;
            if (!BombRules.InBlast(center, context.Barrel.Position)) return;

            context.Barrel.Exploded = true;
            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.BarrelChained,
                Origin = context.Barrel.Position,
            });
            Explode(context, context.Barrel.Position, context.Barrel.Damage, true, steps);
        }

        private static void Knockback(
            HazardContext context,
            GridPos center,
            CombatantState target,
            List<HazardStep> steps)
        {
            KnockbackOutcome outcome = KnockbackRules.Resolve(
                context.Map,
                center,
                target.Position,
                pos => IsOccupiedExcept(context, pos, target),
                out GridPos destination);
            if (outcome == KnockbackOutcome.None) return;

            if (outcome == KnockbackOutcome.PushedIntoFall)
            {
                Fall(context, target, destination, "KNOCKBACK", steps);
                return;
            }

            GridPos from = target.Position;
            target.MoveTo(destination);
            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.Knocked,
                Actor = target,
                Origin = from,
                Destination = destination,
            });

            // 밀려 떨어진 충격으로 약한 바닥이 무너진다.
            if (context.Map.Get(destination)?.kind == TileKind.WeakFloor)
                Collapse(context, target, destination, steps);
        }

        /// <summary>약한 바닥을 구멍으로 바꾸고 그 위의 대상을 떨어뜨린다.</summary>
        private static void Collapse(
            HazardContext context,
            CombatantState faller,
            GridPos pos,
            List<HazardStep> steps)
        {
            context.Map.Set(pos, TileKind.Hole);
            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.WeakFloorsCollapsed,
                Origin = pos,
                Cells = new[] { pos },
            });
            Fall(context, faller, pos, "COLLAPSE", steps);
        }

        private static void Fall(
            HazardContext context,
            CombatantState faller,
            GridPos from,
            string cause,
            List<HazardStep> steps)
        {
            // 안전 낙하 높이는 장비를 든 플레이어만 받는다.
            int safeFallHeight = faller == context.Player
                ? context.PlayerSafeFallHeight
                : FallRules.DefaultSafeFallHeight;

            FallResult fall = FallRules.TryFall(
                context.Map,
                context.Height,
                faller,
                from,
                context.BottomElevation,
                context.Combatants,
                safeFallHeight);
            if (fall == null) return; // 무저갱 — 생성기가 없다고 보장하지만 방어

            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.Fell,
                Actor = faller,
                Origin = from,
                Destination = fall.FinalPosition,
                FloorsFallen = fall.FloorsFallen,
                Amount = fall.Damage,
                Source = cause,
            });

            if (fall.CrushedOccupant != null)
                steps.Add(new HazardStep
                {
                    Kind = HazardStepKind.Crushed,
                    Actor = fall.CrushedOccupant,
                    Origin = fall.FinalPosition,
                    Amount = fall.Damage,
                    Source = "Crush",
                });
        }

        private static void ApplyStatusInRegion(
            HazardContext context,
            IReadOnlyList<GridPos> cells,
            StatusKind kind,
            List<HazardStep> steps)
        {
            if (context.Combatants == null) return;
            foreach (CombatantState combatant in context.Combatants)
            {
                if (combatant == null || !combatant.IsAlive) continue;
                if (!Contains(cells, combatant.Position)) continue;
                ApplyStatus(context, combatant, kind, steps);
            }
        }

        private static void ApplyStatus(
            HazardContext context,
            CombatantState target,
            StatusKind kind,
            List<HazardStep> steps)
        {
            if (target == null || !target.IsAlive) return;

            StatusApplyResult result = target.Statuses.Apply(kind, context.StatusTurns);
            steps.Add(new HazardStep
            {
                Kind = HazardStepKind.StatusApplied,
                Actor = target,
                Status = kind,
                StatusResult = result,
                Amount = context.StatusTurns,
            });
        }

        private static bool Contains(IReadOnlyList<GridPos> cells, GridPos pos)
        {
            if (cells == null) return false;
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] == pos)
                    return true;
            return false;
        }

        private static bool IsOccupiedExcept(
            HazardContext context,
            GridPos pos,
            CombatantState except)
        {
            if (context.Combatants == null) return false;
            foreach (CombatantState combatant in context.Combatants)
            {
                if (combatant == null || combatant == except) continue;
                if (!combatant.IsAlive) continue;
                if (combatant.Position == pos) return true;
            }
            return false;
        }

        private static bool PlayerIsDown(HazardContext context) =>
            context.Player != null && !context.Player.IsAlive;
    }
}
