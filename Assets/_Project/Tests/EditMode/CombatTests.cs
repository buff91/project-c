using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class CombatantStateTests
    {
        [Test]
        public void Spear_ReachesTwoTilesInStraightLine_ButNotThroughWallsOrDiagonally()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 1, 0), TileKind.Floor);

            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var twoAway = new CombatantState("a", new GridPos(2, 0, 0), 5, 1);
            var diagonal = new CombatantState("b", new GridPos(1, 1, 0), 5, 1);
            var threeAway = new CombatantState("c", new GridPos(3, 0, 0), 5, 1);

            Assert.IsTrue(CombatRules.CanMelee(map, attacker, twoAway, meleeReach: 2));
            Assert.IsFalse(CombatRules.CanMelee(map, attacker, diagonal, meleeReach: 2),
                "사거리가 길어져도 대각은 근접이 아니다");
            Assert.IsFalse(CombatRules.CanMelee(map, attacker, threeAway, meleeReach: 2));
            Assert.IsFalse(CombatRules.CanMelee(map, attacker, twoAway),
                "기본 사거리(1)는 예전 그대로다");

            // 사이가 막히면 찌를 수 없다.
            map.Set(new GridPos(1, 0, 0), TileKind.DoorClosed);
            Assert.IsFalse(CombatRules.CanMelee(map, attacker, twoAway, meleeReach: 2));
        }

        [Test]
        public void Armor_ReducesPhysicalDamage_ButNeverToZero()
        {
            Assert.AreEqual(2, CombatRules.Mitigate(3, 1));
            Assert.AreEqual(1, CombatRules.Mitigate(1, 5), "완전 무효화는 만들지 않는다");
            Assert.AreEqual(3, CombatRules.Mitigate(3, 0));

            var attacker = new CombatantState("a", new GridPos(0, 0, 0), 5, 3);
            var target = new CombatantState("b", new GridPos(1, 0, 0), 10, 1);

            Assert.IsTrue(CombatRules.TryMelee(attacker, target, out int damage, targetArmor: 1));
            Assert.AreEqual(2, damage);
        }

        [Test]
        public void AreAdjacent_OrthogonalNeighbor_WithinMeleeReachHeight()
        {
            var center = new CombatantState("center", new GridPos(2, 2, 0), 5, 1);
            var east = new CombatantState("east", new GridPos(3, 2, 0), 5, 1);
            var diagonal = new CombatantState("diagonal", new GridPos(3, 3, 0), 5, 1);
            var stepUp = new CombatantState("stepUp", new GridPos(3, 2, 1), 5, 1);   // 옆칸 한 단 위 = 단차 타격
            var tooHigh = new CombatantState("tooHigh", new GridPos(3, 2, 2), 5, 1); // 두 단 차 = 사거리 밖

            Assert.IsTrue(CombatRules.AreAdjacent(center, east), "같은 높이 정사각 인접");
            Assert.IsFalse(CombatRules.AreAdjacent(center, diagonal), "대각선은 아님");
            Assert.IsTrue(CombatRules.AreAdjacent(center, stepUp), "단차 1칸은 근접 사거리 안");
            Assert.IsFalse(CombatRules.AreAdjacent(center, tooHigh), "단차 2칸은 근접 사거리 밖");
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
        public void TryMelee_DownStrike_AddsHeightBonus()
        {
            var attacker = new CombatantState("highground", new GridPos(5, 5, 1), 5, 2);
            var target = new CombatantState("below", new GridPos(5, 4, 0), 10, 1); // 옆칸 한 단 아래
            Assert.IsTrue(CombatRules.TryMelee(attacker, target, out int damage));
            Assert.AreEqual(3, damage, "위에서 내려치면 +1 (2→3)");
            Assert.AreEqual(7, target.Hp);
        }

        [Test]
        public void TryMelee_UpStrike_HasNoHeightBonus()
        {
            var attacker = new CombatantState("below", new GridPos(5, 4, 0), 5, 2);
            var target = new CombatantState("above", new GridPos(5, 5, 1), 10, 1);
            Assert.IsTrue(CombatRules.TryMelee(attacker, target, out int damage));
            Assert.AreEqual(2, damage, "올려치기엔 보너스 없음");
        }

        [Test]
        public void TryMelee_RejectsTargetBeyondMeleeReachHeight()
        {
            var attacker = new CombatantState("attacker", new GridPos(5, 5, 2), 5, 2);
            var target = new CombatantState("target", new GridPos(5, 4, 0), 5, 1); // 두 단 차
            Assert.IsFalse(CombatRules.TryMelee(attacker, target, out _));
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
