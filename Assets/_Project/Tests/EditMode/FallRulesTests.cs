using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class FallRulesTests
    {
        private static readonly DungeonHeightModel Height = new DungeonHeightModel(4);

        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(2, 6)]
        [TestCase(3, 12)]
        public void DamageCurve_IsCumulative(int floors, int expected)
        {
            Assert.AreEqual(expected, FallRules.DamageForFloors(floors));
        }

        [Test]
        public void TryFall_LandsOneFloorBelow_AndAppliesDamage()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 2, 0), TileKind.Hole);
            map.Set(new GridPos(2, 2, -4), TileKind.Floor);
            var faller = new CombatantState("hero", new GridPos(2, 2, 0), 10, 1);

            FallResult result = FallRules.TryFall(
                map, Height, faller, new GridPos(2, 2, 0), -4, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.FloorsFallen);
            Assert.AreEqual(2, result.Damage);
            Assert.AreEqual(new GridPos(2, 2, -4), faller.Position);
            Assert.AreEqual(8, faller.Hp);
        }

        [Test]
        public void TryFall_TwoFloors_UsesSteeperDamage()
        {
            var map = new GridMap();
            map.Set(new GridPos(1, 1, 0), TileKind.Hole);
            map.Set(new GridPos(1, 1, -8), TileKind.Floor); // 한 층 건너뛰고 두 층 아래 착지
            var faller = new CombatantState("hero", new GridPos(1, 1, 0), 10, 1);

            FallResult result = FallRules.TryFall(
                map, Height, faller, new GridPos(1, 1, 0), -8, null);

            Assert.AreEqual(2, result.FloorsFallen);
            Assert.AreEqual(6, result.Damage);
            Assert.AreEqual(4, faller.Hp);
        }

        [Test]
        public void TryFall_Abyss_ReturnsNull_AndLeavesFallerUntouched()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Hole);
            var faller = new CombatantState("hero", new GridPos(0, 0, 0), 10, 1);

            Assert.IsNull(FallRules.TryFall(map, Height, faller, new GridPos(0, 0, 0), -8, null));
            Assert.AreEqual(10, faller.Hp);
            Assert.AreEqual(new GridPos(0, 0, 0), faller.Position);
        }

        [Test]
        public void TryFall_OntoOccupant_DamagesBoth_AndShuntsFallerAside()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 2, 0), TileKind.Hole);
            for (int x = 1; x <= 3; x++)
            for (int y = 1; y <= 3; y++)
                map.Set(new GridPos(x, y, -4), TileKind.Floor);
            var faller = new CombatantState("hero", new GridPos(2, 2, 0), 10, 1);
            var occupant = new CombatantState("goblin", new GridPos(2, 2, -4), 10, 1);

            FallResult result = FallRules.TryFall(
                map, Height, faller, new GridPos(2, 2, 0), -4, new[] { occupant });

            Assert.AreEqual(occupant, result.CrushedOccupant);
            Assert.AreEqual(8, occupant.Hp, "착지 충돌은 양쪽 데미지");
            Assert.AreEqual(8, faller.Hp);
            Assert.AreNotEqual(occupant.Position, faller.Position, "산 점유자 위에 겹치지 않는다");
            Assert.IsTrue(map.IsWalkable(faller.Position));
            Assert.AreEqual(1, occupant.Position.ManhattanTo(faller.Position));
        }

        [Test]
        public void TryFall_OccupantKilledByImpact_FallerTakesTheTile()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 2, 0), TileKind.Hole);
            map.Set(new GridPos(2, 2, -4), TileKind.Floor);
            var faller = new CombatantState("hero", new GridPos(2, 2, 0), 10, 1);
            var occupant = new CombatantState("goblin", new GridPos(2, 2, -4), 2, 1); // 낙뎀 2에 즉사

            FallResult result = FallRules.TryFall(
                map, Height, faller, new GridPos(2, 2, 0), -4, new[] { occupant });

            Assert.IsFalse(occupant.IsAlive);
            Assert.AreEqual(new GridPos(2, 2, -4), result.FinalPosition);
        }
    }

    public class KnockbackRulesTests
    {
        [Test]
        public void PushDirection_UsesDominantAxis_AwayFromCenter()
        {
            var center = new GridPos(5, 5, 0);
            Assert.AreEqual((1, 0), KnockbackRules.PushDirection(center, new GridPos(6, 5, 0)));
            Assert.AreEqual((0, -1), KnockbackRules.PushDirection(center, new GridPos(5, 3, 0)));
            Assert.AreEqual((1, 0), KnockbackRules.PushDirection(center, new GridPos(6, 6, 0)), "동률은 x 우선");
            Assert.AreEqual((0, 0), KnockbackRules.PushDirection(center, center));
        }

        [Test]
        public void Resolve_PushesOntoFloor_BlocksOnWallOrOccupied()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(4, 0, 0), TileKind.Wall);
            var center = new GridPos(1, 0, 0);

            Assert.AreEqual(
                KnockbackOutcome.Pushed,
                KnockbackRules.Resolve(map, center, new GridPos(2, 0, 0), null, out GridPos dest));
            Assert.AreEqual(new GridPos(3, 0, 0), dest);

            Assert.AreEqual(
                KnockbackOutcome.None,
                KnockbackRules.Resolve(map, center, new GridPos(3, 0, 0), null, out _),
                "벽으로는 못 밀린다");

            Assert.AreEqual(
                KnockbackOutcome.None,
                KnockbackRules.Resolve(map, center, new GridPos(2, 0, 0), pos => pos == new GridPos(3, 0, 0), out _),
                "점유 칸으로는 못 밀린다");
        }

        [Test]
        public void Resolve_PushIntoHoleOrVoid_SignalsFall()
        {
            var map = new GridMap();
            for (int x = 0; x < 4; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(3, 0, 0), TileKind.Hole);

            Assert.AreEqual(
                KnockbackOutcome.PushedIntoFall,
                KnockbackRules.Resolve(map, new GridPos(1, 0, 0), new GridPos(2, 0, 0), null, out GridPos dest));
            Assert.AreEqual(new GridPos(3, 0, 0), dest);

            // 맵 밖(void)으로 밀림 — 아래 바닥 유무는 TryFall 이 판정한다.
            Assert.AreEqual(
                KnockbackOutcome.PushedIntoFall,
                KnockbackRules.Resolve(map, new GridPos(2, 0, 0), new GridPos(3, 0, 0), null, out _));
        }
    }
}
