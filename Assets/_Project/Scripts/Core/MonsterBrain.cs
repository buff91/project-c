using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public enum MonsterActionKind
    {
        Wait = 0,
        Step,        // Target 칸으로 한 걸음
        OpenDoor,    // Target 의 닫힌 문을 연다 (행동 1회)
        Attack,      // 인접한 플레이어를 근접 공격
        RangedAttack // 사거리·사선이 잡힌 플레이어를 원거리 공격
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
        public static MonsterAction RangedAttack() =>
            new MonsterAction(MonsterActionKind.RangedAttack, default);

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
        private readonly BehaviorNode<MonsterBrainContext, MonsterAction> _tree;
        private bool _seesPlayer;

        public MonsterMood Mood { get; private set; } = MonsterMood.Patrol;
        public GridPos? LastSeenPlayerAt { get; private set; }

        public MonsterBrain(MonsterArchetype archetype, GridPos home, int seed)
        {
            _archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
            _home = home;
            _random = new Random(seed);
            _tree = BuildTree();
        }

        /// <summary>
        /// 행동 트리(BT): 위에서 아래로 우선순위 Selector. 새 행동은 여기에 가지(When/Do)를
        /// 선언적으로 추가·재배치한다 — 콘텐츠가 늘어도 분기 가독성을 유지한다. (FSM → BT)
        /// </summary>
        private BehaviorNode<MonsterBrainContext, MonsterAction> BuildTree()
        {
            Selector<MonsterBrainContext, MonsterAction> Sel(
                params BehaviorNode<MonsterBrainContext, MonsterAction>[] children) =>
                new Selector<MonsterBrainContext, MonsterAction>(children);
            Condition<MonsterBrainContext, MonsterAction> When(
                Func<MonsterBrainContext, bool> predicate,
                BehaviorNode<MonsterBrainContext, MonsterAction> child) =>
                new Condition<MonsterBrainContext, MonsterAction>(predicate, child);
            Leaf<MonsterBrainContext, MonsterAction> Do(Func<MonsterBrainContext, MonsterAction?> behavior) =>
                new Leaf<MonsterBrainContext, MonsterAction>(behavior);

            return Sel(
                // 죽었으면 대기.
                When(c => !c.Self.IsAlive, Do(_ => MonsterAction.Wait())),

                // 불이 붙으면 물을 찾아 끈다(정상 행동보다 우선). 갈 물이 없으면 이 가지는 결정하지 않는다.
                When(c => c.Self.Statuses.Has(StatusKind.Burn),
                    Sel(
                        When(c => c.Map.Get(c.Self.Position)?.wet == true, Do(_ => MonsterAction.Wait())),
                        Do(TrySeekWater))),

                // 지각·기분(FSM 상태) 갱신 — 부수효과 후 항상 다음 가지로 흘려보낸다.
                Do(c => { UpdatePerception(c); return null; }),

                // 기분별 행동: 도주 → 추격 → 순찰.
                When(c => Mood == MonsterMood.Flee, Do(c => DecideFlee(c))),
                When(c => Mood == MonsterMood.Chase, Do(c => ChaseOrRelent(c))),
                Do(c => DecidePatrol(c)));
        }

        /// <summary>
        /// 순찰 기준점을 옮긴다. 낙하 등으로 다른 층에 강제 이동한 몬스터가
        /// 옛 홈 반경 밖에 갇혀 영구 정지하지 않게 한다.
        /// </summary>
        public void Rehome(GridPos home) => _home = home;

        /// <summary>
        /// 맞았다 — 시야 밖에서 날아온 사격에도 반응한다. 원거리 무기가 생긴 뒤로
        /// 이게 없으면 지각 반경 밖에서 쏘는 저격이 무저항 처형이 된다(카이팅 최적해 부활).
        /// <para>
        /// 도주 중이면 기분을 덮어쓰지 않는다 — 낮은 HP로 도망치던 개체가 한 대 맞고
        /// 되돌아서면 도주 규칙(GDD §5.7)이 무의미해진다.
        /// </para>
        /// </summary>
        public void OnDamaged(GridPos attackerAt)
        {
            if (Mood == MonsterMood.Flee) return;
            Mood = MonsterMood.Chase;
            LastSeenPlayerAt = attackerAt;
        }

        public MonsterAction Decide(MonsterBrainContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Map == null || context.Height == null || context.Self == null)
                throw new ArgumentException("Map/Height/Self 는 필수입니다.", nameof(context));

            return _tree.Tick(context) ?? MonsterAction.Wait();
        }

        /// <summary>지각과 기분(FSM 상태)을 갱신한다 — 트리의 부수효과 노드가 매 틱 호출한다.</summary>
        private void UpdatePerception(MonsterBrainContext context)
        {
            _seesPlayer = PerceivesPlayer(context);
            if (_seesPlayer)
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
        }

        /// <summary>추격 걸음을 내되, 목격 지점 도달/막힘이면 순찰로 복귀하고 이번 턴은 관망한다.</summary>
        private MonsterAction ChaseOrRelent(MonsterBrainContext context)
        {
            MonsterAction action = DecideChase(context, _seesPlayer);
            if (action.Kind != MonsterActionKind.Wait || _seesPlayer)
                return action;
            // 복귀 전환 턴은 관망 — 같은 턴에 순찰 걸음까지 하면 "지점 도달" 계약이 깨진다.
            Mood = MonsterMood.Patrol;
            LastSeenPlayerAt = null;
            return MonsterAction.Wait();
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

            // 사수는 붙기 전에 쏜다. 결정하지 못하면(사선도 자리도 없으면) 일반 추격으로 흘린다.
            if (seesPlayer && _archetype.IsRanged)
            {
                MonsterAction? ranged = DecideRanged(context);
                if (ranged.HasValue) return ranged.Value;
            }

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

        /// <summary>
        /// 사수의 교전 순서. ① 너무 붙었으면 거리를 벌린다(막히면 붙어서라도 싸운다)
        /// ② 사거리·사선이 잡히면 쏜다 ③ 아니면 사격 가능한 자리로 한 걸음.
        /// 셋 다 못 하면 null 을 돌려 일반 추격(붙어서 때리기)으로 넘긴다.
        ///
        /// 판정은 전부 플레이어와 같은 <see cref="CombatRules"/>를 쓴다 — 사거리 예산에 높이차를
        /// 물리는 규칙(고지대는 비쌈)도 그대로 적용돼, 사수와 플레이어가 같은 기하를 공유한다.
        /// </summary>
        private MonsterAction? DecideRanged(MonsterBrainContext context)
        {
            GridPos self = context.Self.Position;
            GridPos player = context.Player.Position;

            if (_archetype.KeepAwayRange > 0 &&
                self.ChebyshevTo(player) <= _archetype.KeepAwayRange)
            {
                // 거리 벌리기는 도주와 같은 규칙(가장 멀어지는 이웃 칸)을 재사용한다.
                MonsterAction retreat = DecideFlee(context);
                if (retreat.Kind == MonsterActionKind.Step) return retreat;
                // 물러설 곳이 없으면 붙어서라도 싸운다.
                if (CombatRules.AreAdjacent(context.Self, context.Player))
                    return MonsterAction.Attack();
            }

            if (CombatRules.CanFireFrom(context.Map, self, player, _archetype.RangedRange))
                return MonsterAction.RangedAttack();

            if (CombatRules.FindFiringPosition(
                    context.Map,
                    self,
                    player,
                    _archetype.RangedRange,
                    out List<GridPos> path,
                    pos => pos != self &&
                           (IsFallHazard(context.Map, pos) ||
                            (context.IsOccupied != null && context.IsOccupied(pos))),
                    canClimb: _archetype.CanClimb) &&
                path.Count >= 2 &&
                context.Height.SameFloor(self, path[1]))
                return MonsterAction.Step(path[1]);

            return null;
        }

        /// <summary>공격이 성립하는 칸(플레이어와 같은 elevation 의 4방향 이웃)까지의 최단 경로.</summary>
        private List<GridPos> FindPathToAttackPosition(MonsterBrainContext context)
        {
            GridPos player = context.Player.Position;
            var best = new List<GridPos>();
            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                if (!context.Map.IsWalkable(candidate)) continue;
                if (IsFallHazard(context.Map, candidate)) continue; // 약한 바닥 위에서 때리려 서지 않는다
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
                pos => pos != self &&
                       (IsFallHazard(context.Map, pos) ||
                        (context.IsOccupied != null && context.IsOccupied(pos))),
                openClosedDoors: true,
                // 종마다 다르다 — 못 오르는 적은 사다리 위 플레이어를 따라가지 못한다.
                // 명시적으로 넘기지 않으면 기본값(true)이라 전부 올라 이 축이 죽는다.
                canClimb: _archetype.CanClimb);
        }

        /// <summary>
        /// 이 칸에 발을 디디면 낙하하는가(약한 바닥). 몬스터는 궁지가 아니면 자진해서 밟지 않는다 —
        /// 낙하는 플레이어의 밀기/넉백으로 유도하는 게 정석이다. (GDD §5.3 넉백→낙하)
        /// </summary>
        private static bool IsFallHazard(GridMap map, GridPos pos) =>
            map.Get(pos)?.kind == TileKind.WeakFloor;

        private const int WaterSeekRadius = 6;

        /// <summary>불붙은 몬스터가 같은 층의 가장 가까운(도달 가능한) 젖은 칸으로 한 걸음. 없으면 null(결정 안 함).</summary>
        private MonsterAction? TrySeekWater(MonsterBrainContext context)
        {
            GridPos self = context.Self.Position;

            var candidates = new List<GridPos>();
            foreach (KeyValuePair<GridPos, TileData> pair in context.Map.All())
            {
                if (pair.Value?.wet != true) continue;
                if (!context.Height.SameFloor(self, pair.Key)) continue;
                int d = self.ChebyshevTo(pair.Key);
                if (d >= 1 && d <= WaterSeekRadius) candidates.Add(pair.Key);
            }

            candidates.Sort((a, b) =>
            {
                int da = self.ChebyshevTo(a), db = self.ChebyshevTo(b);
                if (da != db) return da.CompareTo(db);
                if (a.x != b.x) return a.x.CompareTo(b.x);
                return a.y.CompareTo(b.y);
            });

            foreach (GridPos wet in candidates)
            {
                List<GridPos> path = FindPathTo(context, wet);
                if (path.Count >= 2 && context.Height.SameFloor(self, path[1]))
                    return MonsterAction.Step(path[1]);
            }

            return null;
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
                if (IsFallHazard(context.Map, candidate)) continue; // 도망치다 스스로 약한 바닥에 안 떨어진다
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
                if (IsFallHazard(context.Map, candidate)) continue; // 순찰 중 약한 바닥엔 들어가지 않는다
                if (_home.ChebyshevTo(candidate) > _archetype.PatrolRadius) continue;
                if (context.IsOccupied != null && context.IsOccupied(candidate)) continue;
                options.Add(candidate);
            }

            if (rest || options.Count == 0) return MonsterAction.Wait();
            return MonsterAction.Step(options[_random.Next(options.Count)]);
        }
    }
}
