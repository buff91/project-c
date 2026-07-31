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
            CollectionAssert.DoesNotContain(shallow, "Skeleton", "초반 구간엔 경비 드론이 없다");

            List<string> deep = Pick(60, 3, seed: 3);
            CollectionAssert.Contains(deep, "Skeleton", "후반 구간엔 경비 드론이 섞인다");
        }

        private static List<string> Pick(int count, int depth, int seed)
        {
            var random = new System.Random(seed);
            var picks = new List<string>(count);
            for (int i = 0; i < count; i++)
                picks.Add(MonsterRoster.PickForDepth(
                    DungeonRegionProfile.Facility, depth, random).Id);
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

        private static MonsterArchetype Slinger() =>
            new MonsterArchetype("Slinger", 4, 1, aggroRange: 7, patrolRadius: 2,
                rangedRange: 4, rangedPower: 2, keepAwayRange: 2);

        [Test]
        public void Slinger_FiresFromRange_InsteadOfClosing()
        {
            GridMap map = Flat(11);
            var self = new CombatantState("s", new GridPos(4, 0, 0), 4, 1);
            var player = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var brain = new MonsterBrain(Slinger(), self.Position, seed: 2);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.RangedAttack, action.Kind,
                "사거리·사선이 잡히면 붙지 않고 쏜다");
        }

        [Test]
        public void Slinger_BacksAwayWhenPlayerCloses()
        {
            GridMap map = Flat(11);
            var self = new CombatantState("s", new GridPos(5, 5, 0), 4, 1);
            var player = new CombatantState("p", new GridPos(4, 5, 0), 10, 3);
            var brain = new MonsterBrain(Slinger(), self.Position, seed: 2);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.Step, action.Kind, "붙으면 먼저 거리를 벌린다");
            Assert.Greater(
                action.Target.ChebyshevTo(player.Position),
                self.Position.ChebyshevTo(player.Position),
                "물러선 칸은 더 멀어야 한다");
        }

        [Test]
        public void Slinger_Cornered_FallsBackToMelee()
        {
            // 물러설 칸이 없는 막다른 자리에서는 붙어서라도 싸운다(무한 대기 방지).
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 0), TileKind.Floor);
            var self = new CombatantState("s", new GridPos(0, 0, 0), 4, 1);
            var player = new CombatantState("p", new GridPos(1, 0, 0), 10, 3);
            var brain = new MonsterBrain(Slinger(), self.Position, seed: 2);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.Attack, action.Kind);
        }

        [Test]
        public void Slinger_MovesToRegainLineOfSight_WhenBlocked()
        {
            // 사선이 벽에 막히면 쏘지도 붙지도 않고 사격 가능한 자리로 움직인다.
            GridMap map = Flat(11);
            for (int y = 0; y < 11; y++)
                if (y != 5) map.Set(new GridPos(3, y, 0), TileKind.Wall);
            // 거리는 벌어져 있어(체비셰프 4 > keepAway 2) 물러서기 가지는 타지 않는다.
            var self = new CombatantState("s", new GridPos(1, 8, 0), 4, 1);
            var player = new CombatantState("p", new GridPos(5, 8, 0), 10, 3);
            var brain = new MonsterBrain(Slinger(), self.Position, seed: 2);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.Step, action.Kind);
            Assert.IsTrue(map.IsWalkable(action.Target));
        }

        [Test]
        public void RangedRepositioning_LadderLink_UsesArchetypeClimbPolicy()
        {
            var map = new GridMap();
            var lowerLadder = new GridPos(0, 0, 0);
            var upperLadder = new GridPos(0, 0, 2);
            var playerPos = new GridPos(3, 0, 2);
            map.Set(lowerLadder, TileKind.Ladder);
            map.Set(upperLadder, TileKind.Ladder);
            for (int x = 1; x <= playerPos.x; x++)
                map.Set(new GridPos(x, 0, 2), TileKind.Floor);
            map.Connect(lowerLadder, upperLadder);

            var player = new CombatantState("p", playerPos, 10, 3);
            var drone = new CombatantState("arc", lowerLadder, 5, 1);
            var slinger = new CombatantState("slinger", lowerLadder, 4, 1);

            MonsterAction droneAction = new MonsterBrain(
                MonsterRoster.ArcDrone, lowerLadder, seed: 2).Decide(
                Context(map, drone, player));
            MonsterAction slingerAction = new MonsterBrain(
                MonsterRoster.Slinger, lowerLadder, seed: 2).Decide(
                Context(map, slinger, player));

            Assert.AreEqual(
                MonsterActionKind.Wait,
                droneAction.Kind,
                "기계 원거리 적은 사격 자리를 찾기 위해 사다리를 타면 안 된다.");
            Assert.AreEqual(MonsterActionKind.Step, slingerAction.Kind);
            Assert.AreEqual(
                upperLadder,
                slingerAction.Target,
                "등반 가능한 인간형 사수는 같은 사다리 링크를 사격 경로로 쓴다.");
        }

        [Test]
        public void MeleeArchetype_IsUnaffectedByRangedBranch()
        {
            GridMap map = Flat(11);
            var self = new CombatantState("g", new GridPos(4, 0, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 2);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.Step, action.Kind, "근접 몬스터는 그대로 붙는다");
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
        public void PlanarAdjacent_WithinMeleeReach_Attacks_ButBeyondReachDoesNot()
        {
            // 건물형 수직성 v0.3: 옆칸이 한 단(≤MeleeReachHeight) 위면 단차 타격으로 공격한다.
            // 두 단 이상이면 근접 사거리 밖이라 공격을 반환하지 않는다(헛턴 방지).
            GridMap map = Flat(5);
            map.Set(new GridPos(2, 3, 0), TileKind.Stairs);
            map.Set(new GridPos(2, 4, 1), TileKind.Floor);
            var self = new CombatantState("g", new GridPos(2, 3, 0), 5, 1);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            var nearPlayer = new CombatantState("p", new GridPos(2, 4, 1), 8, 2); // 한 단 위 옆칸
            Assert.AreEqual(MonsterActionKind.Attack,
                brain.Decide(Context(map, self, nearPlayer)).Kind, "단차 1칸은 단차 타격");

            map.Set(new GridPos(2, 4, 2), TileKind.Floor);
            var farPlayer = new CombatantState("p", new GridPos(2, 4, 2), 8, 2); // 두 단 위
            Assert.AreNotEqual(MonsterActionKind.Attack,
                brain.Decide(Context(map, self, farPlayer)).Kind, "두 단 차는 근접 사거리 밖");
        }

        [Test]
        public void Patrol_DoesNotWanderOntoWeakFloor()
        {
            GridMap map = Flat(11);
            map.Set(new GridPos(6, 5, 0), TileKind.WeakFloor);
            var self = new CombatantState("g", new GridPos(5, 5, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(0, 0, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 7);

            for (int i = 0; i < 40; i++)
            {
                MonsterAction action = brain.Decide(Context(map, self, player, playerSeesMonster: false));
                if (action.Kind == MonsterActionKind.Step)
                {
                    Assert.AreNotEqual(new GridPos(6, 5, 0), action.Target, "순찰이 약한 바닥에 들어가지 않는다");
                    self.MoveTo(action.Target);
                }
            }
        }

        [Test]
        public void Flee_DoesNotBackOntoWeakFloor_StandsInstead()
        {
            GridMap map = Flat(11);
            map.Set(new GridPos(6, 5, 0), TileKind.WeakFloor); // 유일한 후퇴 방향이 약한 바닥
            var coward = new MonsterArchetype("Coward", 5, 1, aggroRange: 6, patrolRadius: 2, fleeThreshold: 1f);
            var self = new CombatantState("g", new GridPos(5, 5, 0), 5, 1);
            self.TakeDamage(1); // Hp 4 < Max → 도주 발동
            var player = new CombatantState("p", new GridPos(0, 5, 0), 8, 2);
            var brain = new MonsterBrain(coward, self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterMood.Flee, brain.Mood);
            Assert.AreNotEqual(new GridPos(6, 5, 0), action.Target, "도망치다 약한 바닥으로 자멸하지 않는다");
        }

        [Test]
        public void Chase_RoutesAroundWeakFloor_DoesNotStepOnIt()
        {
            GridMap map = Flat(11);
            map.Set(new GridPos(4, 5, 0), TileKind.WeakFloor); // 추격 직선 경로 위
            var self = new CombatantState("g", new GridPos(5, 5, 0), 5, 1);
            var player = new CombatantState("p", new GridPos(2, 5, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterMood.Chase, brain.Mood);
            if (action.Kind == MonsterActionKind.Step)
                Assert.AreNotEqual(new GridPos(4, 5, 0), action.Target, "추격이 약한 바닥을 우회한다");
        }

        [Test]
        public void BurningMonster_SeeksWater_OverChasingPlayer()
        {
            GridMap map = Flat(11);
            map.Get(new GridPos(2, 5, 0)).wet = true;                 // 물웅덩이 서쪽
            var self = new CombatantState("g", new GridPos(5, 5, 0), 5, 1);
            self.Statuses.Apply(StatusKind.Burn, 3);
            var player = new CombatantState("p", new GridPos(9, 5, 0), 8, 2); // 물 반대(동)쪽
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            MonsterAction action = brain.Decide(Context(map, self, player));

            Assert.AreEqual(MonsterActionKind.Step, action.Kind);
            Assert.AreEqual(new GridPos(4, 5, 0), action.Target, "불붙으면 추격 대신 물(서)로 한 걸음");
        }

        [Test]
        public void BurningMonster_OnWater_StandsToDouse()
        {
            GridMap map = Flat(11);
            map.Get(new GridPos(5, 5, 0)).wet = true;
            var self = new CombatantState("g", new GridPos(5, 5, 0), 5, 1);
            self.Statuses.Apply(StatusKind.Burn, 3);
            var player = new CombatantState("p", new GridPos(7, 5, 0), 8, 2);
            var brain = new MonsterBrain(Goblin(), self.Position, seed: 1);

            Assert.AreEqual(MonsterActionKind.Wait,
                brain.Decide(Context(map, self, player)).Kind, "이미 물 위면 서서 끈다");
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

        /// <summary>
        /// 원거리 무기가 생긴 뒤(M4) 지각 반경 밖 저격이 무저항 처형이 되지 않아야 한다 —
        /// 맞으면 쏜 자리를 향해 추격으로 전환한다.
        /// </summary>
        [Test]
        public void OnDamaged_FromOutsideAggro_SwitchesToChaseTowardTheShooter()
        {
            var brain = new MonsterBrain(Goblin(), new GridPos(0, 0, 0), seed: 5);
            Assert.AreEqual(MonsterMood.Patrol, brain.Mood);

            var shooter = new GridPos(9, 0, 0); // aggroRange 6 밖
            brain.OnDamaged(shooter);

            Assert.AreEqual(MonsterMood.Chase, brain.Mood);
            Assert.AreEqual(shooter, brain.LastSeenPlayerAt);
        }

        /// <summary>
        /// 도주 중에는 덮어쓰지 않는다 — 한 대 맞고 되돌아서면 도주 규칙(GDD §5.7)이 죽는다.
        /// </summary>
        [Test]
        public void OnDamaged_WhileFleeing_KeepsFleeing()
        {
            GridMap map = Flat(11);
            // HP를 도주 임계 아래로 두고 플레이어를 보여 Flee 로 진입시킨다.
            var self = new CombatantState("s", new GridPos(5, 0, 0), 10, 1);
            self.TakeDamage(8);
            var player = new CombatantState("p", new GridPos(4, 0, 0), 10, 3);
            var archetype = new MonsterArchetype(
                "Coward", 10, 1, aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.5f);
            var brain = new MonsterBrain(archetype, self.Position, seed: 3);

            brain.Decide(Context(map, self, player));
            Assert.AreEqual(MonsterMood.Flee, brain.Mood, "도주 상태 전제가 성립하지 않았다");

            brain.OnDamaged(new GridPos(9, 0, 0));

            Assert.AreEqual(MonsterMood.Flee, brain.Mood);
        }
    }
}
