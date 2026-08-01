using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 낙하·폭발 연쇄의 <b>순서 계약</b>. 개별 규칙(피해량·낙뎀 곡선·넉백 방향)은 각자의
    /// 테스트가 지고, 여기서는 그것들이 어떤 차례로 엮이는지만 고정한다 — 지금까지 코루틴
    /// 안에 있어 회귀가 없던 부분이다.
    /// </summary>
    public class HazardSequenceTests
    {
        private static readonly DungeonHeightModel Height = new DungeonHeightModel(4);

        private static GridMap FloorAround(GridPos center, int radius = 2)
        {
            var map = new GridMap();
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                map.Set(center.Offset(dx, dy), TileKind.Floor);
            return map;
        }

        private static HazardContext Context(
            GridMap map,
            CombatantState player,
            params CombatantState[] others)
        {
            var all = new List<CombatantState>();
            if (player != null) all.Add(player);
            all.AddRange(others);
            return new HazardContext
            {
                Map = map,
                Height = Height,
                Combatants = all,
                Player = player,
                BottomElevation = 0,
            };
        }

        private static List<HazardStepKind> Kinds(IEnumerable<HazardStep> steps) =>
            steps.Select(step => step.Kind).ToList();

        [Test]
        public void Explode_OrdersDamageThenStatusThenKnockback()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            var enemy = new CombatantState("goblin", new GridPos(1, 0, 0), 20, 2);
            HazardContext context = Context(map, null, enemy);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);

            Assert.AreEqual(
                new[]
                {
                    HazardStepKind.Detonated,
                    HazardStepKind.Damaged,
                    HazardStepKind.StatusApplied,
                    HazardStepKind.WaterEvaporated,
                    HazardStepKind.Knocked,
                },
                Kinds(steps).ToArray());
        }

        [Test]
        public void Explode_DetonatedStepComesBeforeAnyJudgement()
        {
            var center = new GridPos(0, 0, 0);
            HazardContext context = Context(FloorAround(center), null);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);

            // 연출은 이 스텝에서 폭발 애니메이션을 시작한다 — 판정보다 뒤로 가면 피해가
            // 먼저 뜨고 폭발이 나중에 터진다.
            Assert.AreEqual(HazardStepKind.Detonated, steps[0].Kind);
            Assert.AreEqual(center, steps[0].Origin);
            Assert.IsTrue(steps[0].Fiery);
        }

        [Test]
        public void FieryExplosion_Burns_FrostExplosion_Freezes()
        {
            var center = new GridPos(0, 0, 0);

            foreach ((bool fiery, StatusKind expected) in
                     new[] { (true, StatusKind.Burn), (false, StatusKind.Freeze) })
            {
                var enemy = new CombatantState("goblin", new GridPos(1, 0, 0), 20, 2);
                HazardContext context = Context(FloorAround(center), null, enemy);

                List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery);

                HazardStep status = steps.Single(
                    step => step.Kind == HazardStepKind.StatusApplied);
                Assert.AreEqual(expected, status.Status, $"fiery={fiery}");
                Assert.AreEqual(enemy, status.Actor);
            }
        }

        [Test]
        public void FieryExplosion_IgnitesOil_AndBurnsAgainAfterIgnition()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            var oiled = new GridPos(1, 1, 0);
            map.Get(oiled).oiled = true;
            var enemy = new CombatantState("goblin", oiled, 20, 2);
            HazardContext context = Context(map, null, enemy);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);
            List<HazardStepKind> kinds = Kinds(steps);

            Assert.IsFalse(map.Get(oiled).oiled, "발화한 기름은 소모된다");
            // 기름은 블라스트 안에서만 붙으므로(ForEachBlastCell) 기름 위의 대상은 이미 폭발
            // 화상을 받은 뒤다. 발화가 주는 것은 두 번째 부여 = 갱신이고, 그게 발화 스텝
            // '뒤'에 와야 연출이 "불이 번져서 다시 붙었다"로 읽힌다.
            Assert.Less(
                kinds.IndexOf(HazardStepKind.OilIgnited),
                kinds.LastIndexOf(HazardStepKind.StatusApplied));
            Assert.IsTrue(
                steps.Where(step => step.Kind == HazardStepKind.StatusApplied)
                    .All(step => step.Actor == enemy && step.Status == StatusKind.Burn));
        }

        [Test]
        public void FrostExplosion_FreezesConnectedPuddle_BeyondTheBlast()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center, radius: 4);
            // 블라스트에 닿은 웅덩이에서 시작해 밖으로 이어진다 — 결빙은 블라스트를 넘는다.
            foreach (int x in new[] { 1, 2, 3 })
                map.Get(new GridPos(x, 1, 0)).wet = true;
            var far = new GridPos(3, 1, 0);
            var enemy = new CombatantState("goblin", far, 20, 2);
            HazardContext context = Context(map, null, enemy);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: false);

            HazardStep frozen = steps.Single(step => step.Kind == HazardStepKind.WaterFrozen);
            CollectionAssert.Contains(frozen.Cells, far);
            // 폭발 피해권 밖이라 상태는 오직 결빙 전파로만 온다.
            HazardStep freeze = steps.Single(step => step.Kind == HazardStepKind.StatusApplied);
            Assert.AreEqual(enemy, freeze.Actor);
            Assert.AreEqual(StatusKind.Freeze, freeze.Status);
            Assert.AreEqual(20, enemy.Hp, "블라스트 밖은 폭발 피해를 받지 않는다");
        }

        [Test]
        public void Knockback_IntoHole_BecomesFall()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            map.Set(new GridPos(2, 0, 0), TileKind.Hole);
            map.Set(new GridPos(2, 0, -4), TileKind.Floor);
            var enemy = new CombatantState("goblin", new GridPos(1, 0, 0), 40, 2);
            HazardContext context = new HazardContext
            {
                Map = map,
                Height = Height,
                Combatants = new[] { enemy },
                BottomElevation = -4,
            };

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);

            HazardStep fell = steps.Single(step => step.Kind == HazardStepKind.Fell);
            Assert.AreEqual("KNOCKBACK", fell.Source);
            Assert.AreEqual(new GridPos(2, 0, -4), fell.Destination);
            Assert.IsFalse(
                steps.Any(step => step.Kind == HazardStepKind.Knocked),
                "구멍으로 밀린 것은 이동이 아니라 낙하다");
        }

        [Test]
        public void Knockback_OntoWeakFloor_CollapsesThenFalls()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            map.Set(new GridPos(2, 0, 0), TileKind.WeakFloor);
            map.Set(new GridPos(2, 0, -4), TileKind.Floor);
            var enemy = new CombatantState("goblin", new GridPos(1, 0, 0), 40, 2);
            HazardContext context = new HazardContext
            {
                Map = map,
                Height = Height,
                Combatants = new[] { enemy },
                BottomElevation = -4,
            };

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);
            List<HazardStepKind> kinds = Kinds(steps);

            // 밀림 → 붕괴 → 낙하 순서가 지켜져야 연출이 "밟자마자 꺼진다"로 읽힌다.
            Assert.Less(
                kinds.IndexOf(HazardStepKind.Knocked),
                kinds.IndexOf(HazardStepKind.WeakFloorsCollapsed));
            Assert.Less(
                kinds.IndexOf(HazardStepKind.WeakFloorsCollapsed),
                kinds.IndexOf(HazardStepKind.Fell));
            Assert.AreEqual("COLLAPSE", steps.Single(s => s.Kind == HazardStepKind.Fell).Source);
            Assert.AreEqual(TileKind.Hole, map.Get(new GridPos(2, 0, 0)).kind);
        }

        [Test]
        public void PlayerKilledMidKnockback_StopsRemainingKnockbacks()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            map.Set(new GridPos(2, 0, 0), TileKind.Hole);
            map.Set(new GridPos(2, 0, -16), TileKind.Floor); // 치명적인 높이
            var player = new CombatantState("player", new GridPos(1, 0, 0), 10, 2);
            var enemy = new CombatantState("goblin", new GridPos(0, 1, 0), 40, 2);
            HazardContext context = Context(map, player, enemy);
            context.BottomElevation = -16;

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);

            Assert.IsFalse(player.IsAlive, "낙뎀으로 죽어야 하는 배치다");
            Assert.IsTrue(steps.Any(step => step.Kind == HazardStepKind.Fell));
            // 플레이어가 넉백 도중 죽으면 남은 대상의 넉백은 생략된다.
            Assert.IsFalse(steps.Any(step => step.Kind == HazardStepKind.Knocked));
            Assert.AreEqual(new GridPos(0, 1, 0), enemy.Position);
        }

        [Test]
        public void PlayerKilledByTheBlastItself_DoesNotStopOthers()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            var player = new CombatantState("player", new GridPos(1, 0, 0), 3, 2);
            var enemy = new CombatantState("goblin", new GridPos(0, 1, 0), 40, 2);
            HazardContext context = Context(map, player, enemy);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 5, fiery: true);

            // 폭발 자체로 죽은 플레이어는 넉백 루프에서 `continue`로 건너뛰므로 중단 조건에
            // 걸리지 않는다 — 남은 몬스터는 그대로 밀린다. 기존 코루틴 동작 그대로다.
            Assert.IsFalse(player.IsAlive);
            Assert.IsTrue(steps.Any(step => step.Kind == HazardStepKind.Knocked));
        }

        [Test]
        public void PlayerDeath_CancelsBarrelChain()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            var player = new CombatantState("player", new GridPos(0, 0, 0), 2, 2);
            HazardContext context = Context(map, player);
            context.Barrel = new HazardBarrel
            {
                Position = new GridPos(1, 0, 0),
                Damage = 4,
            };

            List<HazardStep> steps = HazardSequence.Explode(context, center, 5, fiery: true);

            Assert.IsFalse(player.IsAlive);
            Assert.IsFalse(steps.Any(step => step.Kind == HazardStepKind.BarrelChained));
            Assert.IsFalse(context.Barrel.Exploded);
        }

        [Test]
        public void BarrelChain_AppendsItsOwnExplosion_AndOnlyOnce()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            HazardContext context = Context(map, null);
            context.Barrel = new HazardBarrel
            {
                Position = new GridPos(1, 0, 0),
                Damage = 4,
            };

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);
            List<HazardStepKind> kinds = Kinds(steps);

            Assert.AreEqual(1, kinds.Count(kind => kind == HazardStepKind.BarrelChained));
            Assert.AreEqual(
                HazardStepKind.Detonated,
                steps[kinds.IndexOf(HazardStepKind.BarrelChained) + 1].Kind,
                "유폭 스텝 바로 뒤에 그 폭발이 이어져야 연출이 끊기지 않는다");
            Assert.IsTrue(context.Barrel.Exploded);

            // 같은 폭발통은 다시 터지지 않는다.
            List<HazardStep> again = HazardSequence.Explode(context, center, 3, fiery: true);
            Assert.IsFalse(again.Any(step => step.Kind == HazardStepKind.BarrelChained));
        }

        [Test]
        public void FrostExplosion_DoesNotChainTheBarrel()
        {
            var center = new GridPos(0, 0, 0);
            HazardContext context = Context(FloorAround(center), null);
            context.Barrel = new HazardBarrel
            {
                Position = new GridPos(1, 0, 0),
                Damage = 4,
            };

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: false);

            Assert.IsFalse(steps.Any(step => step.Kind == HazardStepKind.BarrelChained));
            Assert.IsFalse(context.Barrel.Exploded);
        }

        [Test]
        public void Fall_ReportsCrushedOccupant()
        {
            var map = new GridMap();
            var from = new GridPos(0, 0, 4);
            map.Set(from, TileKind.Hole);
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            var faller = new CombatantState("goblin", from, 30, 2);
            var victim = new CombatantState("victim", new GridPos(0, 0, 0), 30, 2);
            var context = new HazardContext
            {
                Map = map,
                Height = Height,
                Combatants = new[] { faller, victim },
                BottomElevation = 0,
            };

            List<HazardStep> steps = HazardSequence.Fall(context, faller, from, "DROP");

            Assert.AreEqual(
                new[] { HazardStepKind.Fell, HazardStepKind.Crushed },
                Kinds(steps).ToArray());
            Assert.AreEqual("DROP", steps[0].Source);
            Assert.AreEqual(victim, steps[1].Actor);
            Assert.AreEqual(steps[0].Amount, steps[1].Amount, "압사는 같은 낙뎀을 나눠 받는다");
        }

        [Test]
        public void SafeFallHeight_AppliesToThePlayerOnly()
        {
            var from = new GridPos(0, 0, 4);

            int DamageFor(bool asPlayer)
            {
                var map = new GridMap();
                map.Set(from, TileKind.Hole);
                map.Set(new GridPos(0, 0, 0), TileKind.Floor);
                var faller = new CombatantState("faller", from, 30, 2);
                var context = new HazardContext
                {
                    Map = map,
                    Height = Height,
                    Combatants = new[] { faller },
                    Player = asPlayer ? faller : null,
                    BottomElevation = 0,
                    PlayerSafeFallHeight = 3,
                };
                return HazardSequence.Fall(context, faller, from, "DROP")[0].Amount;
            }

            Assert.Less(
                DamageFor(asPlayer: true),
                DamageFor(asPlayer: false),
                "장비의 안전 낙하 높이는 몬스터에게 새지 않는다");
        }

        [Test]
        public void Explode_RevealsSecretDoorsInBlast()
        {
            var center = new GridPos(0, 0, 0);
            GridMap map = FloorAround(center);
            var secret = new GridPos(1, 0, 0);
            map.Set(secret, TileKind.SecretDoor);
            HazardContext context = Context(map, null);

            List<HazardStep> steps = HazardSequence.Explode(context, center, 3, fiery: true);

            HazardStep revealed = steps.Single(
                step => step.Kind == HazardStepKind.SecretDoorsRevealed);
            CollectionAssert.Contains(revealed.Cells, secret);
        }
    }
}
