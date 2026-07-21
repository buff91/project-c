using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public enum MonsterActionKind
    {
        Wait = 0,
        Step,      // Target 칸으로 한 걸음
        OpenDoor,  // Target 의 닫힌 문을 연다 (행동 1회)
        Attack     // 인접한 플레이어를 근접 공격
    }

    /// <summary>브레인이 반환하는 행동 "의도". 실행·연출은 Gameplay 가 담당한다.</summary>
    public readonly struct MonsterAction
    {
        public readonly MonsterActionKind Kind;
        public readonly GridPos Target;

        private MonsterAction(MonsterActionKind kind, GridPos target)
        {
            Kind = kind;
            Target = target;
        }

        public static MonsterAction Wait() => new MonsterAction(MonsterActionKind.Wait, default);
        public static MonsterAction Step(GridPos to) => new MonsterAction(MonsterActionKind.Step, to);
        public static MonsterAction OpenDoor(GridPos door) => new MonsterAction(MonsterActionKind.OpenDoor, door);
        public static MonsterAction Attack() => new MonsterAction(MonsterActionKind.Attack, default);

        public override string ToString() =>
            Kind == MonsterActionKind.Step || Kind == MonsterActionKind.OpenDoor
                ? $"{Kind}({Target})"
                : Kind.ToString();
    }

    /// <summary>Decide 한 번에 필요한 외부 정보. 콜백 주입으로 Core 순수성을 유지한다.</summary>
    public sealed class MonsterBrainContext
    {
        public GridMap Map;
        public DungeonHeightModel Height;
        public CombatantState Self;
        public CombatantState Player;

        /// <summary>
        /// 해당 칸이 플레이어 시야(FOV)에 들어 있는가. 지각은 플레이어 FOV의 대칭으로 정의한다
        /// ("내가 보이면 상대도 나를 본다"). HasLineOfSight 는 elevation 이 다르면 즉시 false 라
        /// 단 위/계단의 플레이어에게 실명이 되므로 쓰지 않는다.
        /// </summary>
        public Func<GridPos, bool> SeenByPlayer;

        /// <summary>다른 전투 참가자(플레이어 포함)가 점유한 칸인가.</summary>
        public Func<GridPos, bool> IsOccupied;
    }

    public enum MonsterMood
    {
        Patrol = 0,
        Chase = 1,
        Flee = 2
    }

    /// <summary>
    /// 몬스터 한 마리의 순수 로직 상태머신. (GDD §5.7: 순찰→추격→공격)
    /// 위치·HP 를 직접 바꾸지 않고 행동 의도만 반환한다 — M4에서 상태이상(빙결=Decide 스킵,
    /// 화상=사전 틱)과 낙하(TryFall)를 "활성화 → 틱 → Decide → 실행" 파이프라인에 끼울 수 있게.
    /// </summary>
    public sealed class MonsterBrain
    {
        private readonly MonsterArchetype _archetype;
        private GridPos _home;
        private readonly Random _random;

        public MonsterMood Mood { get; private set; } = MonsterMood.Patrol;
        public GridPos? LastSeenPlayerAt { get; private set; }

        public MonsterBrain(MonsterArchetype archetype, GridPos home, int seed)
        {
            _archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
            _home = home;
            _random = new Random(seed);
        }

        /// <summary>
        /// 순찰 기준점을 옮긴다. 낙하 등으로 다른 층에 강제 이동한 몬스터가
        /// 옛 홈 반경 밖에 갇혀 영구 정지하지 않게 한다.
        /// </summary>
        public void Rehome(GridPos home) => _home = home;

        public MonsterAction Decide(MonsterBrainContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Map == null || context.Height == null || context.Self == null)
                throw new ArgumentException("Map/Height/Self 는 필수입니다.", nameof(context));
            if (!context.Self.IsAlive) return MonsterAction.Wait();

            bool seesPlayer = PerceivesPlayer(context);
            if (seesPlayer)
            {
                // HP가 도주 임계 미만이면 추격 대신 도주. (GDD §5.7 순찰→추격→공격→도주)
                bool shouldFlee = _archetype.FleeThreshold > 0f &&
                                  context.Self.Hp < context.Self.MaxHp * _archetype.FleeThreshold;
                Mood = shouldFlee ? MonsterMood.Flee : MonsterMood.Chase;
                LastSeenPlayerAt = context.Player.Position;
            }
            else if (Mood == MonsterMood.Flee)
            {
                // 시야에서 벗어나면 진정하고 순찰로 복귀.
                Mood = MonsterMood.Patrol;
                LastSeenPlayerAt = null;
            }

            if (Mood == MonsterMood.Flee)
                return DecideFlee(context);

            if (Mood == MonsterMood.Chase)
            {
                MonsterAction action = DecideChase(context, seesPlayer);
                if (action.Kind != MonsterActionKind.Wait || seesPlayer)
                    return action;
                // 마지막 목격 지점까지 갔거나 길이 없으면 순찰로 복귀.
                // 복귀 전환 턴은 관망한다 — 같은 턴에 순찰 걸음까지 하면
                // 수색 종착지에서 한 칸 벗어나 "지점 도달" 계약이 깨진다.
                Mood = MonsterMood.Patrol;
                LastSeenPlayerAt = null;
                return MonsterAction.Wait();
            }

            return DecidePatrol(context);
        }

        private bool PerceivesPlayer(MonsterBrainContext context)
        {
            CombatantState player = context.Player;
            if (player == null || !player.IsAlive) return false;

            GridPos self = context.Self.Position;
            return context.Height.SameFloor(self, player.Position) &&
                   self.ChebyshevTo(player.Position) <= _archetype.AggroRange &&
                   (context.SeenByPlayer?.Invoke(self) ?? false);
        }

        private MonsterAction DecideChase(MonsterBrainContext context, bool seesPlayer)
        {
            GridPos self = context.Self.Position;

            if (seesPlayer && CombatRules.AreAdjacent(context.Self, context.Player))
                return MonsterAction.Attack();

            List<GridPos> path = seesPlayer
                ? FindPathToAttackPosition(context)
                : FindPathTo(context, LastSeenPlayerAt ?? self);
            if (path.Count < 2) return MonsterAction.Wait();

            GridPos step = path[1];
            // 층간 링크(계단 점프)나 다른 층으로 새는 걸음은 방어적으로 막는다.
            if (!context.Height.SameFloor(self, step)) return MonsterAction.Wait();

            return context.Map.Get(step)?.kind == TileKind.DoorClosed
                ? MonsterAction.OpenDoor(step)   // 추격 중에만 문을 연다 (순찰은 안 엶)
                : MonsterAction.Step(step);
        }

        /// <summary>공격이 성립하는 칸(플레이어와 같은 elevation 의 4방향 이웃)까지의 최단 경로.</summary>
        private List<GridPos> FindPathToAttackPosition(MonsterBrainContext context)
        {
            GridPos player = context.Player.Position;
            var best = new List<GridPos>();
            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                if (!context.Map.IsWalkable(candidate)) continue;
                if (context.IsOccupied != null && candidate != context.Self.Position &&
                    context.IsOccupied(candidate))
                    continue;

                List<GridPos> path = FindPathTo(context, candidate);
                if (path.Count > 0 && (best.Count == 0 || path.Count < best.Count))
                    best = path;
            }

            return best;
        }

        private List<GridPos> FindPathTo(MonsterBrainContext context, GridPos goal)
        {
            GridPos self = context.Self.Position;
            return GridPathfinder.FindPath(
                context.Map,
                self,
                goal,
                pos => pos != self && context.IsOccupied != null && context.IsOccupied(pos),
                openClosedDoors: true);
        }

        /// <summary>플레이어와의 거리(체비셰프)를 가장 크게 벌리는 이웃 칸으로 물러난다.</summary>
        private MonsterAction DecideFlee(MonsterBrainContext context)
        {
            GridPos self = context.Self.Position;
            GridPos player = context.Player.Position;
            GridPos best = self;
            int bestDistance = self.ChebyshevTo(player);

            foreach (GridPos candidate in new[] { self.North, self.East, self.South, self.West })
            {
                if (!context.Map.IsWalkable(candidate)) continue;
                if (context.IsOccupied != null && context.IsOccupied(candidate)) continue;
                int distance = candidate.ChebyshevTo(player);
                if (distance > bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (best != self) return MonsterAction.Step(best);

            // 궁지에 몰리면 몸부림 — 인접해 있으면 문다.
            return CombatRules.AreAdjacent(context.Self, context.Player)
                ? MonsterAction.Attack()
                : MonsterAction.Wait();
        }

        private MonsterAction DecidePatrol(MonsterBrainContext context)
        {
            // 가끔 제자리에 서서 숨을 돌린다 — 결정론을 위해 항상 한 번 뽑는다.
            bool rest = _random.Next(0, 3) == 0;
            GridPos self = context.Self.Position;

            var options = new List<GridPos>(4);
            foreach (GridPos candidate in new[] { self.North, self.East, self.South, self.West })
            {
                if (!context.Map.IsWalkable(candidate)) continue;
                if (_home.ChebyshevTo(candidate) > _archetype.PatrolRadius) continue;
                if (context.IsOccupied != null && context.IsOccupied(candidate)) continue;
                options.Add(candidate);
            }

            if (rest || options.Count == 0) return MonsterAction.Wait();
            return MonsterAction.Step(options[_random.Next(options.Count)]);
        }
    }
}
