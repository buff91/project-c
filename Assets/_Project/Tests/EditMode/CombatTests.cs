using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class CombatantStateTests
    {
        [Test]
        public void AreAdjacent_RequiresOrthogonalNeighborOnSameElevation()
        {
            var center = new CombatantState("center", new GridPos(2, 2, 0), 5, 1);
            var east = new CombatantState("east", new GridPos(3, 2, 0), 5, 1);
            var diagonal = new CombatantState("diagonal", new GridPos(3, 3, 0), 5, 1);
            var raised = new CombatantState("raised", new GridPos(3, 2, 1), 5, 1);

            Assert.IsTrue(CombatRules.AreAdjacent(center, east));
            Assert.IsFalse(CombatRules.AreAdjacent(center, diagonal));
            Assert.IsFalse(CombatRules.AreAdjacent(center, raised));
        }

        [Test]
        public void TryMelee_ReducesHpAndClampsAtZero()
        {
            var attacker = new CombatantState("attacker", new GridPos(0, 0, 0), 5, 3);
            var target = new CombatantState("target", new GridPos(1, 0, 0), 2, 1);

            bool attacked = CombatRules.TryMelee(attacker, target, out int damage);

            Assert.IsTrue(attacked);
            Assert.AreEqual(2, damage);
            Assert.AreEqual(0, target.Hp);
            Assert.IsFalse(target.IsAlive);
        }

        [Test]
        public void TryMelee_RejectsNonAdjacentTarget()
        {
            var attacker = new CombatantState("attacker", new GridPos(0, 0, 0), 5, 2);
            var target = new CombatantState("target", new GridPos(2, 0, 0), 5, 1);

            Assert.IsFalse(CombatRules.TryMelee(attacker, target, out int damage));
            Assert.AreEqual(0, damage);
            Assert.AreEqual(target.MaxHp, target.Hp);
        }

        [Test]
        public void TryRanged_HitsVisibleTargetWithinRange()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            var attacker = new CombatantState("archer", new GridPos(0, 0, 0), 5, 2);
            var target = new CombatantState("target", new GridPos(4, 0, 0), 5, 1);

            Assert.IsTrue(CombatRules.TryRanged(attacker, target, map, 5, out int damage));
            Assert.AreEqual(2, damage);
        }

        [Test]
        public void TryRanged_IsBlockedByClosedDoor()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.DoorClosed);
            var attacker = new CombatantState("archer", new GridPos(0, 0, 0), 5, 2);
            var target = new CombatantState("target", new GridPos(4, 0, 0), 5, 1);

            Assert.IsFalse(CombatRules.TryRanged(attacker, target, map, 5, out _));
            Assert.AreEqual(target.MaxHp, target.Hp);
        }

        [Test]
        public void TryRanged_RejectsTargetBeyondRange()
        {
            var map = new GridMap();
            for (int x = 0; x < 6; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            var attacker = new CombatantState("archer", new GridPos(0, 0, 0), 5, 2);
            var target = new CombatantState("target", new GridPos(5, 0, 0), 5, 1);

            Assert.IsFalse(CombatRules.TryRanged(attacker, target, map, 4, out _));
        }
    }

    public class TurnManagerTests
    {
        [Test]
        public void PlayerAndEnemyPhases_AdvanceOneTurn()
        {
            var turns = new TurnManager();

            Assert.AreEqual(1, turns.TurnNumber);
            Assert.AreEqual(TurnPhase.Player, turns.Phase);
            Assert.IsTrue(turns.TryBeginEnemyPhase());
            Assert.IsFalse(turns.TryBeginEnemyPhase());
            Assert.AreEqual(TurnPhase.Enemies, turns.Phase);
            Assert.IsTrue(turns.TryCompleteEnemyPhase());
            Assert.AreEqual(2, turns.TurnNumber);
            Assert.AreEqual(TurnPhase.Player, turns.Phase);
        }
    }
}
