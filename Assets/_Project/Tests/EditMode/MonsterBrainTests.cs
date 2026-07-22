using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class GridPathfinderOptionTests
    {
        [Test]
        public void IsBlocked_RoutesAroundOccupiedTile()
        {
            var map = new GridMap();
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 2; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            var occupied = new GridPos(1, 0, 0);

            var path = GridPathfinder.FindPath(
                map, new GridPos(0, 0, 0), new GridPos(3, 0, 0),
                pos => pos == occupied);

            Assert.Greater(path.Count, 0);
            Assert.IsFalse(path.Contains(occupied));
        }

        [Test]
        public void FindPath_RoutesThroughStairLink_ToAnotherFloorTarget()
        {
            var map = new GridMap();
            for (int x = 0; x <= 3; x++)
            {
                map.Set(new GridPos(x, 0, 0), TileKind.Floor);
                map.Set(new GridPos(x, 0, -4), TileKind.Floor);
            }
            map.Set(new GridPos(3, 0, 0), TileKind.StairsDown);
            map.Set(new GridPos(0, 0, -4), TileKind.StairsUp);
            map.Connect(new GridPos(3, 0, 0), new GridPos(0, 0, -4));

            // 다른 층 목적지 — 경로가 하행 계단 링크를 자동 경유해야 한다.
            var path = GridPathfinder.FindPath(map, new GridPos(0, 0, 0), new GridPos(2, 0, -4));

            Assert.Greater(path.Count, 0, "층을 넘는 경로가 있어야 한다");
            CollectionAssert.Contains(path, new GridPos(3, 0, 0), "하행 계단 경유");
            CollectionAssert.Contains(path, new GridPos(0, 0, -4), "링크 착지");
            Assert.AreEqual(new GridPos(2, 0, -4), path[path.Count - 1]);
        }

        [Test]
        public void OpenClosedDoors_AllowsPathThroughDoor()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.DoorClosed);
            var start = new GridPos(0, 0, 0);
            var goal = new GridPos(4, 0, 0);

            Assert.AreEqual(0, GridPathfinder.FindPath(map, start, goal).Count, "기본값은 닫힌 문 차단");
            var path = GridPathfinder.FindPath(map, start, goal, openClosedDoors: true);
            Assert.Greater(path.Count, 0);
            Assert.IsTrue(path.Contains(new GridPos(2, 0, 0)));
        }
    }

    public class MonsterActivationTests
    {
        private static readonly DungeonHeightModel Height = new DungeonHeightModel(4);

        [Test]
        public void Active_RequiresSameFloorAndRadius()
        {
            var player = new GridPos(2, 2, 0);

            Assert.IsTrue(MonsterActivation.IsActive(Height, player, new GridPos(6, 2, 0), 8));
            Assert.IsFalse(MonsterActivation.IsActive(Height, player, new GridPos(12, 2, 0), 8), "반경 밖 휴면");
            Assert.IsFalse(MonsterActivation.IsActive(Height, player, new GridPos(2, 3, -4), 8), "다른 층 휴면");
            Assert.IsTrue(MonsterActivation.IsActive(Height, player, new GridPos(2, 3, 1), 8), "층 내부 높이차는 같은 층");
        }
    }

    public class MonsterRosterTests
    {
        [Test]
        public void PickForDepth_IsDeterministic_AndDepthShiftsMix()
        {
            CollectionAssert.AreEqual(Pick(30, 0, seed: 5), Pick(30, 0, seed: 5));

            List<string> shallow = Pick(60, 0, seed: 3);
            CollectionAssert.DoesNotContain(shallow, "Skeleton", "최상층(B1)엔 해골이 없다");

            List<string> deep = Pick(60, 3, seed: 3);
            CollectionAssert.Contains(deep, "Skeleton", "깊은 층엔 해골이 섞인다");
        }

        private static List<string> Pick(int count, int depth, int seed)
        {
            var random = new System.Random(seed);
            var picks = new List<string>(count);
            for (int i = 0; i < count; i++)
                picks.Add(MonsterRoster.PickForDepth(depth, random).Id);
            return picks;
        }
    }

    public class MonsterBrainTests
    {
        private static GridMap Flat(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        private static MonsterArchetype Goblin() =>
            new MonsterArchetype("Goblin", 5, 1, aggroRange: 6, patrolRadius: 2);

        private static MonsterBrainContext Context(
            GridMap map,
            CombatantState self,
            CombatantState player,
            bool playerSeesMonster = true,
            System.Func<GridPos, bool> occupied = null)
        {
            return new MonsterBrainContext
            {
                Map = map,
                Height = new DungeonHeightModel(4),
                Self = self,
                Player = player,
                SeenByPlayer = _ => playerSeesMonster,
                IsOccupied = occupied ?? (pos => player != null && pos == player.Position)
            };
        }

        [Test]
        public void OutOfSight_StaysPatrol_AndKeepsPatrolRadius()
        {
            GridMap map = Flat(11);
            var self = new CombatantState("g", new GridPos(8, 8, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(1, 1, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 3);

            for (int i = 0; i < 20; i++)
            {
                MonsterAction action = brain.Decide(Context(map, self, player, playerSeesMonster: false));
                Assert.AreEqual(MonsterMood.Patrol, brain.Mood);
                Assert.AreNotEqual(MonsterActionKind.Attack, action.Kind);
                Assert.AreNotEqual(MonsterActionKind.OpenDoor, action.Kind, "순찰 중 개문 금지");
                if (action.Kind == MonsterActionKind.Step)
                {
                    Assert.IsTrue(map.IsWalkable(action.Target));
                    Assert.LessOrEqual(new GridPos(8, 8, 0).ChebyshevTo(action.Target), 2, "순찰 반경 이탈");
                    self.MoveTo(action.Target);
                }
            }
        }

        [Test]
        public void SeenWithinAggro_ChasesAndClosesDistance()
        {
            GridMap map = Flat(11);
            var self = new CombatantState("g", new GridPos(7, 7, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(2, 2, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            int previous = self.Position.ChebyshevTo(player.Position);
            bool attacked = false;
            for (int i = 0; i < 12; i++)
            {
                MonsterAction action = brain.Decide(Context(map, self, player));
                if (action.Kind == MonsterActionKind.Attack)
                {
                    attacked = true;
                    break;
                }

                Assert.AreEqual(MonsterActionKind.Step, action.Kind);
                self.MoveTo(action.Target);
                int distance = self.Position.ChebyshevTo(player.Position);
                Assert.LessOrEqual(distance, previous, "추격 중 거리 증가");
                previous = distance;
            }

            Assert.IsTrue(attacked, "추격이 공격까지 수렴하지 않음");
            Assert.AreEqual(MonsterMood.Chase, brain.Mood);
        }

        [Test]
        public void AdjacentPlayer_Attacks()
        {
            GridMap map = Flat(5);
            var self = new CombatantState("g", new GridPos(2, 2, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(2, 3, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            Assert.AreEqual(MonsterActionKind.Attack, brain.Decide(Context(map, self, player)).Kind);
        }

        [Test]
        public void PlanarAdjacent_ButDifferentElevation_DoesNotAttack()
        {
            // 계단 위 플레이어(elevation 1)와 평면상 인접 — AreAdjacent 가 아니므로
            // Attack 을 반환하면 실행이 항상 실패하는 헛턴 루프가 된다.
            GridMap map = Flat(5);
            map.Set(new GridPos(2, 3, 0), TileKind.Stairs);
            map.Set(new GridPos(2, 4, 1), TileKind.Floor);
            var self = new CombatantState("g", new GridPos(2, 3, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(2, 4, 1), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreNotEqual(MonsterActionKind.Attack, action.Kind);
        }

        [Test]
        public void ClosedDoorOnChasePath_ReturnsOpenDoor()
        {
            var map = new GridMap();
            for (int x = 0; x < 7; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(3, 0, 0), TileKind.DoorClosed);
            var self = new CombatantState("g", new GridPos(4, 0, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(0, 0, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.OpenDoor, action.Kind);
            Assert.AreEqual(new GridPos(3, 0, 0), action.Target);
        }

        [Test]
        public void OccupiedCorridorTile_IsNotSteppedInto()
        {
            var map = new GridMap();
            for (int x = 0; x < 6; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            var blockerPos = new GridPos(3, 0, 0);
            var self = new CombatantState("g", new GridPos(5, 0, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(0, 0, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(
                map, self, player,
                occupied: pos => pos == blockerPos || pos == player.Position));

            // 1칸 복도가 막혔으니 그 칸으로는 못 들어간다 (Wait 또는 다른 유효 걸음만).
            if (action.Kind == MonsterActionKind.Step)
                Assert.AreNotEqual(blockerPos, action.Target);
        }

        [Test]
        public void SameSeed_ProducesSameActionSequence()
        {
            List<string> Run()
            {
                GridMap map = Flat(9);
                var self = new CombatantState("g", new GridPos(4, 4, 0), 5, 1);
                var player = new CombatantState("p", new GridPos(0, 0, 0), 8, 2);
                var brain = new MonsterBrain(Goblin(), self.Position, seed: 77);
                var actions = new List<string>();
                for (int i = 0; i < 15; i++)
                {
                    MonsterAction action = brain.Decide(Context(map, self, player, playerSeesMonster: false));
                    actions.Add(action.ToString());
                    if (action.Kind == MonsterActionKind.Step) self.MoveTo(action.Target);
                }
                return actions;
            }

            CollectionAssert.AreEqual(Run(), Run());
        }

        [Test]
        public void Rehome_MovesPatrolAnchor_ToNewPosition()
        {
            // 낙하로 강제 이동한 몬스터가 옛 홈 반경 밖에 갇혀 영구 정지하지 않아야 한다.
            GridMap map = Flat(12);
            var self = new CombatantState("g", new GridPos(2, 2, 0), 5, 1);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 4);

            self.MoveTo(new GridPos(9, 9, 0)); // 낙하로 먼 곳에 착지했다고 가정
            brain.Rehome(self.Position);

            bool stepped = false;
            for (int i = 0; i < 20; i++)
            {
                MonsterAction action = brain.Decide(Context(map, self, null, playerSeesMonster: false));
                if (action.Kind != MonsterActionKind.Step) continue;
                stepped = true;
                Assert.LessOrEqual(
                    new GridPos(9, 9, 0).ChebyshevTo(action.Target), 2, "새 홈 반경에서 순찰해야 한다");
                self.MoveTo(action.Target);
            }

            Assert.IsTrue(stepped, "재홈 후에도 순찰 걸음이 나와야 한다");
        }

        [Test]
        public void LowHp_WithFleeThreshold_StepsAwayFromPlayer()
        {
            GridMap map = Flat(9);
            var coward = new MonsterArchetype("Coward", 5, 1, aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.5f);
            var self = new CombatantState("c", new GridPos(4, 4, 0), 5, 1);
            self.TakeDamage(4); // HP 1 < 2.5 → 도주
            var player = new CombatantState("p", new GridPos(2, 4, 0), 8, 2);
            var brain = new MonsterBrain(coward, self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterMood.Flee, brain.Mood);
            Assert.AreEqual(MonsterActionKind.Step, action.Kind);
            Assert.Greater(
                action.Target.ChebyshevTo(player.Position),
                self.Position.ChebyshevTo(player.Position),
                "도주 걸음은 거리를 벌려야 한다");
        }

        [Test]
        public void CorneredWhileFleeing_AdjacentPlayer_BitesBack()
        {
            // 두 칸짜리 골방 — 물러날 곳이 없고 플레이어가 인접해 있으면 문다.
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 0), TileKind.Floor);
            var coward = new MonsterArchetype("Coward", 5, 1, aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.5f);
            var self = new CombatantState("c", new GridPos(0, 0, 0), 5, 1);
            self.TakeDamage(4);
            var player = new CombatantState("p", new GridPos(1, 0, 0), 8, 2);
            var brain = new MonsterBrain(coward, self.Position, seed: 1);

            Assert.AreEqual(MonsterActionKind.Attack, brain.Decide(Context(map, self, player)).Kind);
            Assert.AreEqual(MonsterMood.Flee, brain.Mood);
        }

        [Test]
        public void LostSight_WalksToLastSeen_ThenReturnsToPatrol()
        {
            GridMap map = Flat(9);
            var self = new CombatantState("g", new GridPos(6, 6, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(3, 6, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 5);

            // 한 번 목격 → Chase. 이후 플레이어는 자리를 떠난다.
            brain.Decide(Context(map, self, player));
            Assert.AreEqual(MonsterMood.Chase, brain.Mood);
            GridPos lastSeen = player.Position;
            player.MoveTo(new GridPos(0, 6, 0));

            // 시야 상실 후에도 마지막 목격 지점으로 이동하다가, 도달하면 순찰 복귀
            for (int i = 0; i < 12 && brain.Mood == MonsterMood.Chase; i++)
            {
                MonsterAction action = brain.Decide(Context(map, self, player, playerSeesMonster: false));
                if (action.Kind == MonsterActionKind.Step) self.MoveTo(action.Target);
            }

            Assert.AreEqual(MonsterMood.Patrol, brain.Mood);
            Assert.AreEqual(lastSeen, self.Position, "마지막 목격 지점까지 이동했어야 함");
        }
    }
}
