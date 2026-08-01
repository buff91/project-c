using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class HazardSequenceServiceTests
    {
        private static readonly DungeonHeightModel Height = new DungeonHeightModel(4);

        [Test]
        public void State_FreezesMembership_ButReadsLatestLivingPositions()
        {
            GridMap map = FlatMap(7);
            var first = new CombatantState("first", new GridPos(2, 2, 0), 10, 1);
            var second = new CombatantState("second", new GridPos(3, 2, 0), 10, 1);
            var source = new List<CombatantState> { first, second };
            var state = State(map, source);

            source.Clear();
            second.MoveTo(new GridPos(4, 2, 0));

            Assert.AreEqual(2, state.Combatants.Count, "호출자의 목록 변경이 시퀀스 구성원을 바꾸면 안 된다");
            Assert.IsFalse(state.IsOccupiedExcept(new GridPos(3, 2, 0), first));
            Assert.IsTrue(state.IsOccupiedExcept(new GridPos(4, 2, 0), first), "최신 위치는 읽어야 한다");

            second.TakeDamage(second.MaxHp);
            Assert.IsFalse(state.IsOccupiedExcept(second.Position, first), "사망자는 점유를 막지 않는다");
        }

        [Test]
        public void ResolveFall_PreservesTypedCauseSafeHeightAndLandingCollision()
        {
            var map = new GridMap();
            var origin = new GridPos(3, 3, 0);
            var landing = new GridPos(3, 3, -4);
            map.Set(origin, TileKind.Hole);
            for (int x = 2; x <= 4; x++)
            for (int y = 2; y <= 4; y++)
                map.Set(new GridPos(x, y, -4), TileKind.Floor);

            var faller = new CombatantState("hero", origin, 10, 1);
            var occupant = new CombatantState("guard", landing, 10, 1);
            var state = new HazardSequenceState(map, Height, -4, new[] { faller, occupant });

            FallSequenceResolution resolution = HazardSequenceService.ResolveFall(
                state,
                faller,
                origin,
                HazardFallCause.IntentionalDrop,
                safeFallHeight: 2);

            Assert.IsTrue(resolution.HasLanding);
            Assert.IsTrue(resolution.IsIntentional);
            Assert.AreEqual(HazardFallCause.IntentionalDrop, resolution.Cause);
            Assert.AreEqual(origin, resolution.Origin);
            Assert.AreEqual(1, resolution.Fall.Damage, "4칸 낙하에서 안전 높이 2를 적용한다");
            Assert.AreEqual(9, faller.Hp);
            Assert.AreEqual(9, occupant.Hp, "착지 충돌도 같은 피해를 받는다");
            Assert.AreSame(occupant, resolution.Fall.CrushedOccupant);
            Assert.AreNotEqual(landing, faller.Position, "산 점유자와 같은 칸에 남지 않는다");
        }

        [Test]
        public void ResolveFall_AbyssKeepsStateAndStillReportsCause()
        {
            var map = new GridMap();
            var origin = new GridPos(1, 1, 0);
            map.Set(origin, TileKind.Hole);
            var faller = new CombatantState("hero", origin, 10, 1);

            FallSequenceResolution resolution = HazardSequenceService.ResolveFall(
                new HazardSequenceState(map, Height, -8, new[] { faller }),
                faller,
                origin,
                HazardFallCause.FloorCollapse);

            Assert.IsFalse(resolution.HasLanding);
            Assert.AreEqual(HazardFallCause.FloorCollapse, resolution.Cause);
            Assert.AreEqual(10, faller.Hp);
            Assert.AreEqual(origin, faller.Position);
        }

        [Test]
        public void PlanKnockback_ReportsWeakFloorAndFallWithoutMutatingTarget()
        {
            GridMap map = FlatMap(7);
            var center = new GridPos(2, 3, 0);
            var target = new CombatantState("target", new GridPos(3, 3, 0), 10, 1);
            var state = State(map, new[] { target });

            var weak = new GridPos(4, 3, 0);
            map.Set(weak, TileKind.WeakFloor);
            KnockbackSequenceStep weakStep = HazardSequenceService.PlanKnockback(state, center, target);

            Assert.AreEqual(KnockbackOutcome.Pushed, weakStep.Outcome);
            Assert.AreEqual(weak, weakStep.Destination);
            Assert.IsTrue(weakStep.CollapsesWeakFloor);
            Assert.AreEqual(new GridPos(3, 3, 0), target.Position, "계획 단계는 이동을 적용하지 않는다");

            map.Set(weak, TileKind.Hole);
            KnockbackSequenceStep fallStep = HazardSequenceService.PlanKnockback(state, center, target);

            Assert.AreEqual(KnockbackOutcome.PushedIntoFall, fallStep.Outcome);
            Assert.AreEqual(weak, fallStep.Destination);
            Assert.IsFalse(fallStep.CollapsesWeakFloor);
        }

        [Test]
        public void PlanKnockback_ReplansAgainstMovementAppliedByPriorTarget()
        {
            GridMap map = FlatMap(7);
            var center = new GridPos(1, 3, 0);
            var near = new CombatantState("near", new GridPos(2, 3, 0), 10, 1);
            var far = new CombatantState("far", new GridPos(3, 3, 0), 10, 1);
            HazardSequenceState state = State(map, new[] { near, far });

            Assert.AreEqual(
                KnockbackOutcome.None,
                HazardSequenceService.PlanKnockback(state, center, near).Outcome,
                "앞 대상이 아직 서 있으면 가까운 대상은 밀리지 않는다");

            KnockbackSequenceStep farStep =
                HazardSequenceService.PlanKnockback(state, center, far);
            Assert.AreEqual(KnockbackOutcome.Pushed, farStep.Outcome);
            far.MoveTo(farStep.Destination); // Gameplay가 첫 대상 이동 코루틴을 끝낸 시점

            KnockbackSequenceStep replannedNear =
                HazardSequenceService.PlanKnockback(state, center, near);
            Assert.AreEqual(KnockbackOutcome.Pushed, replannedNear.Outcome);
            Assert.AreEqual(new GridPos(3, 3, 0), replannedNear.Destination,
                "앞 대상이 비운 칸을 다음 계획이 최신 위치로 읽어야 한다");
        }

        [Test]
        public void BeginFireExplosion_ResolvesBlastTopologySecretAndSurfaceAftermath()
        {
            GridMap map = FlatMap(7);
            var center = new GridPos(3, 3, 0);
            var weak = new GridPos(2, 2, 0);
            var window = new GridPos(2, 3, 0);
            var secret = new GridPos(2, 4, 0);
            var oil = new GridPos(3, 4, 0);
            var water = new GridPos(4, 3, 0);
            map.Set(weak, TileKind.WeakFloor);
            map.Set(window, TileKind.Window);
            map.Set(secret, TileKind.SecretDoor);
            map.Get(oil).oiled = true;
            map.Get(water).wet = true;

            var hero = new CombatantState("hero", center, 10, 1);
            var oilTarget = new CombatantState("oil-target", oil, 10, 1);
            HazardSequenceState state = State(map, new[] { hero, oilTarget });

            ExplosionSequenceStart start = HazardSequenceService.BeginExplosion(
                state, center, damage: 2, ExplosionElement.Fire);

            Assert.AreEqual(8, hero.Hp);
            Assert.AreEqual(8, oilTarget.Hp);
            CollectionAssert.Contains(start.Blast.CollapsedWeakFloors, weak);
            CollectionAssert.Contains(start.Blast.ShatteredWindows, window);
            CollectionAssert.Contains(start.RevealedSecretDoors, secret);
            Assert.AreEqual(TileKind.Hole, map.Get(weak).kind);
            Assert.AreEqual(TileKind.WindowBroken, map.Get(window).kind);
            Assert.AreEqual(TileKind.SecretPassage, map.Get(secret).kind);

            IReadOnlyList<HazardStatusIntent> direct =
                HazardSequenceService.PlanBlastStatuses(start);
            ExplosionElementAftermath aftermath =
                HazardSequenceService.ResolveElementAftermath(state, start);

            CollectionAssert.AreEqual(new[] { hero, oilTarget }, direct.Select(intent => intent.Target));
            Assert.IsTrue(direct.All(intent => intent.Kind == StatusKind.Burn));
            CollectionAssert.Contains(aftermath.IgnitedOil, oil);
            CollectionAssert.Contains(aftermath.EvaporatedWater, water);
            Assert.IsFalse(map.Get(oil).oiled);
            Assert.IsFalse(map.Get(water).wet);
            Assert.AreEqual(1, aftermath.SurfaceStatuses.Count);
            Assert.AreSame(oilTarget, aftermath.SurfaceStatuses[0].Target);

            Assert.AreEqual(StatusApplyResult.Applied, Apply(direct[1]));
            Assert.AreEqual(StatusApplyResult.Refreshed, Apply(aftermath.SurfaceStatuses[0]),
                "직접 피격과 기름 발화의 중복은 두 번째 상태 갱신으로 보존한다");
        }

        [Test]
        public void FrostAftermath_FreezesConnectedPuddleOutsideBlastWithoutExtraDamage()
        {
            GridMap map = FlatMap(9);
            var center = new GridPos(3, 3, 0);
            var seed = new GridPos(4, 3, 0);
            var outside = new GridPos(5, 3, 0);
            map.Get(seed).wet = true;
            map.Get(outside).wet = true;
            var directTarget = new CombatantState("direct", center, 10, 1);
            var puddleTarget = new CombatantState("puddle", outside, 10, 1);
            HazardSequenceState state = State(map, new[] { directTarget, puddleTarget });

            ExplosionSequenceStart start = HazardSequenceService.BeginExplosion(
                state, center, damage: 1, ExplosionElement.Frost);
            IReadOnlyList<HazardStatusIntent> direct = HazardSequenceService.PlanBlastStatuses(start);
            ExplosionElementAftermath aftermath =
                HazardSequenceService.ResolveElementAftermath(state, start);

            Assert.AreEqual(9, directTarget.Hp);
            Assert.AreEqual(10, puddleTarget.Hp, "블라스트 밖 연결 웅덩이는 추가 피해를 받지 않는다");
            CollectionAssert.AreEqual(new[] { directTarget }, direct.Select(intent => intent.Target));
            CollectionAssert.Contains(aftermath.FrozenWater, outside);
            Assert.IsTrue(map.Get(outside).wet, "결빙은 젖음을 소모하지 않는다");
            Assert.AreEqual(1, aftermath.SurfaceStatuses.Count);
            Assert.AreSame(puddleTarget, aftermath.SurfaceStatuses[0].Target);
            Assert.AreEqual(StatusKind.Freeze, aftermath.SurfaceStatuses[0].Kind);
        }

        [Test]
        public void BlastStatuses_ArePlannedAfterHitPresentationCanReviveTarget()
        {
            GridMap map = FlatMap(5);
            var center = new GridPos(2, 2, 0);
            var hero = new CombatantState("hero", center, 1, 1);
            HazardSequenceState state = State(map, new[] { hero });

            ExplosionSequenceStart start = HazardSequenceService.BeginExplosion(
                state, center, damage: 1, ExplosionElement.Fire);

            Assert.IsFalse(hero.IsAlive);
            Assert.IsEmpty(HazardSequenceService.PlanBlastStatuses(start));

            hero.OverrideHpForDebug(1); // Gameplay의 갓 모드 피격 연출과 같은 복구 시점

            IReadOnlyList<HazardStatusIntent> afterPresentation =
                HazardSequenceService.PlanBlastStatuses(start);
            Assert.AreEqual(1, afterPresentation.Count);
            Assert.AreSame(hero, afterPresentation[0].Target);
            Assert.AreEqual(StatusKind.Burn, afterPresentation[0].Kind);
            Assert.AreEqual(HazardSequenceService.ExplosionStatusTurns, afterPresentation[0].Turns);
        }

        [Test]
        public void BeginExplosion_InvalidElementRejectsBeforeMutatingState()
        {
            GridMap map = FlatMap(5);
            var center = new GridPos(2, 2, 0);
            var weak = new GridPos(2, 3, 0);
            map.Set(weak, TileKind.WeakFloor);
            var hero = new CombatantState("hero", center, 10, 1);
            HazardSequenceState state = State(map, new[] { hero });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HazardSequenceService.BeginExplosion(
                    state, center, damage: 3, (ExplosionElement)999));

            Assert.AreEqual(10, hero.Hp);
            Assert.AreEqual(TileKind.WeakFloor, map.Get(weak).kind);
        }

        private static StatusApplyResult Apply(HazardStatusIntent intent) =>
            intent.Target.Statuses.Apply(intent.Kind, intent.Turns);

        private static HazardSequenceState State(
            GridMap map,
            IReadOnlyList<CombatantState> combatants) =>
            new HazardSequenceState(map, Height, minimumElevation: -8, combatants);

        private static GridMap FlatMap(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }
    }
}
