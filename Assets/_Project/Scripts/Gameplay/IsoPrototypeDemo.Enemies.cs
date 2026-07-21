using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 적 페이즈 처리부. (M2)
    /// 파이프라인: 활성화 판정 → (M4: 상태이상 틱 자리) → Brain.Decide → 의도 실행.
    /// 브레인은 행동 의도만 반환하고, 위치·타일·연출 변경은 전부 여기서 일어난다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private const int TrickleSpawnIntervalTurns = 12;

        /// <summary>진행 중 추가 스폰 상한. 층이 커지면 생성기 밀도 보정과 같이 올라간다.</summary>
        private int ActiveFloorMonsterCap => 4 + DungeonGenerator.AreaSpawnBonus(roomSize, roomSize);

        private readonly List<EnemyAgent> _enemyTurnOrder = new List<EnemyAgent>();
        private bool _enemyPhaseMapChanged;
        private System.Random _spawnRng;
        private int _lastTrickleSpawnTurn;

        /// <summary>활성 반경: 시야보다 약간 넓게 잡아 시야 밖에서도 접근을 준비한다. (GDD §5.7 컬링)</summary>
        private int MonsterActiveRadius => fieldOfViewRadius + 2;

        private IEnumerator ResolveEnemyPhase()
        {
            if (!_turns.TryBeginEnemyPhase()) yield break;
            _enemyPhaseMapChanged = false;

            // 플레이어 턴이 끝난 시점의 상태이상 틱.
            // (플레이어 빙결은 아직 소스가 없다 — 소스가 생기면 행동 차단으로 확장)
            StatusTick playerTick = _playerState.Statuses.Tick();
            if (playerTick.BurnDamage > 0)
            {
                _playerState.TakeDamage(playerTick.BurnDamage);
                InteractionFeedback?.Invoke($"BURNING -{playerTick.BurnDamage} HP");
                yield return ShowPlayerHit(playerTick.BurnDamage, "Burn");
                if (!_playerState.IsAlive)
                {
                    _turns.TryCompleteEnemyPhase();
                    yield break;
                }
            }

            // 플레이어와 가까운 순으로 순차 decide→execute.
            // 앞 몬스터의 이동이 뒤 몬스터의 점유 판정에 반영돼 겹침/콩가라인 잼이 없다.
            _enemyTurnOrder.Clear();
            _enemyTurnOrder.AddRange(_enemies);
            GridPos playerPos = _playerState.Position;
            _enemyTurnOrder.Sort((a, b) =>
                a.State.Position.ChebyshevTo(playerPos)
                    .CompareTo(b.State.Position.ChebyshevTo(playerPos)));

            foreach (EnemyAgent enemy in _enemyTurnOrder)
            {
                if (!_playerState.IsAlive) break;
                if (!enemy.State.IsAlive || enemy.Brain == null) continue;

                // 활성화/컬링: 반경 밖·다른 층은 사고 자체를 건너뛴다(휴면 — 시간도 멈춘다).
                if (!MonsterActivation.IsActive(
                        _dungeon.Height,
                        _playerState.Position,
                        enemy.State.Position,
                        MonsterActiveRadius))
                    continue;

                // 상태이상 틱: 파이프라인 = 활성화 → 틱 → Decide → 실행. (GDD §5.5)
                StatusTick tick = enemy.State.Statuses.Tick();
                if (tick.BurnDamage > 0)
                {
                    enemy.State.TakeDamage(tick.BurnDamage);
                    yield return ShowEnemyHit(enemy, tick.BurnDamage, "Burn");
                    if (!enemy.State.IsAlive) continue;
                }
                ApplyEnemyVisuals(enemy); // 틱으로 상태가 풀렸으면 틴트 원복
                if (tick.Frozen) continue; // 빙결: 이번 턴 행동 불가

                MonsterAction action = enemy.Brain.Decide(BuildBrainContext(enemy));
                NotifyMoodTransition(enemy);
                yield return ExecuteEnemyAction(enemy, action);
            }

            // 문 개방·바닥 붕괴로 맵이 변했으면 플레이어 시야도 변한다 — 이때만 전체 갱신.
            if (_enemyPhaseMapChanged)
                RefreshFloorVisibility();

            TrySpawnReinforcement();
            _turns.TryCompleteEnemyPhase();
        }

        /// <summary>몬스터 한 마리를 만들어 씬과 목록에 붙인다. 초기 배치와 런타임 스폰이 공유.</summary>
        private EnemyAgent SpawnEnemy(MonsterArchetype archetype, GridPos spawn, int floorIndex)
        {
            var enemy = new EnemyAgent
            {
                Archetype = archetype,
                State = new CombatantState(
                    $"{archetype.Id} {FloorLabel(floorIndex)}-{_enemies.Count + 1}",
                    spawn,
                    archetype.MaxHp,
                    archetype.AttackPower),
                Brain = new MonsterBrain(archetype, spawn, dungeonSeed * 31 + _enemies.Count)
            };
            enemy.Root = CreateStandingSprite(
                enemy.State.Id, MonsterSpriteFor(archetype), spawn, out SpriteRenderer renderer);
            enemy.Renderer = renderer;
            enemy.HpFill = CreateHealthBar(enemy.Root, $"{enemy.State.Id} HP");
            UpdateHealthBar(enemy.HpFill, enemy.State);
            enemy.MoodIcon = CreateMoodIcon(enemy.Root);
            enemy.LastMood = enemy.Brain.Mood;
            _enemies.Add(enemy);
            ApplyEnemyVisuals(enemy);
            return enemy;
        }

        private Sprite MonsterSpriteFor(MonsterArchetype archetype)
        {
            Sprite mapped = visualCatalog != null ? visualCatalog.MonsterFor(archetype.Id) : null;
            return mapped != null ? mapped : GetMonsterSprite(archetype.Id);
        }

        /// <summary>
        /// 진행 중 추가 스폰: 일정 턴 간격으로, 활성 층의 몬스터가 적으면
        /// 플레이어 시야 밖 스폰 지점에 하나 보충한다. (GDD §5.7 난이도·깊이 연동)
        /// </summary>
        private void TrySpawnReinforcement()
        {
            if (_turns.TurnNumber - _lastTrickleSpawnTurn < TrickleSpawnIntervalTurns) return;
            if (!_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor)) return;

            int living = 0;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive &&
                    _dungeon.Height.FloorIndex(enemy.State.Position.elevation) == _activeFloorIndex)
                    living++;
            }
            if (living >= ActiveFloorMonsterCap) return;

            foreach (GridPos spawn in floor.EnemySpawns)
            {
                if (_visibleTiles.Contains(spawn)) continue;   // 눈앞 스폰 금지
                if (!_grid.Map.IsWalkable(spawn)) continue;    // 붕괴로 구멍이 됐을 수 있다
                if (IsPositionOccupiedExcept(spawn, null)) continue;

                _lastTrickleSpawnTurn = _turns.TurnNumber;
                EnemyAgent reinforcement = SpawnEnemy(
                    MonsterRoster.PickForDepth(GlobalDepth(_activeFloorIndex), _spawnRng),
                    spawn,
                    _activeFloorIndex);
                InteractionFeedback?.Invoke("SOMETHING STIRS IN THE DARK...");
                Debug.Log($"[Spawn] 추가 스폰: {reinforcement.State.Id} @ {spawn}");
                return;
            }
        }

        private MonsterBrainContext BuildBrainContext(EnemyAgent self)
        {
            return new MonsterBrainContext
            {
                Map = _grid.Map,
                Height = _dungeon.Height,
                Self = self.State,
                Player = _playerState,
                SeenByPlayer = pos => _visibleTiles.Contains(pos),
                IsOccupied = pos => IsOccupiedForEnemy(self, pos)
            };
        }

        private bool IsOccupiedForEnemy(EnemyAgent self, GridPos pos)
        {
            if (_playerState != null && _playerState.IsAlive && pos == _playerState.Position)
                return true;
            foreach (EnemyAgent other in _enemies)
            {
                if (other == self || !other.State.IsAlive) continue;
                if (other.State.Position == pos) return true;
            }
            return false;
        }

        private IEnumerator ExecuteEnemyAction(EnemyAgent enemy, MonsterAction action)
        {
            switch (action.Kind)
            {
                case MonsterActionKind.Attack:
                    if (CombatRules.TryMelee(enemy.State, _playerState, out int damage))
                        yield return ShowPlayerHit(damage, enemy.State.Id);
                    break;

                case MonsterActionKind.Step:
                    yield return MoveEnemyStep(enemy, action.Target);
                    break;

                case MonsterActionKind.OpenDoor:
                    TileData door = _grid.Map.Get(action.Target);
                    if (door != null && door.CanOpen)
                    {
                        yield return SetDoorState(action.Target, TileKind.DoorOpen);
                        _enemyPhaseMapChanged = true;
                        InteractionFeedback?.Invoke($"{enemy.State.Id} OPENED A DOOR!");
                        Debug.Log($"[Door] {enemy.State.Id} 가 {action.Target} 문을 열었다");
                    }
                    break;
            }
        }

        private IEnumerator MoveEnemyStep(EnemyAgent enemy, GridPos next)
        {
            // 의도와 실행 사이에 상태가 변했을 수 있으니 실행 시점에 재검증한다.
            if (!_grid.Map.IsWalkable(next) || IsOccupiedForEnemy(enemy, next)) yield break;

            bool animate = _visibleTiles.Contains(enemy.State.Position) || _visibleTiles.Contains(next);
            Vector3 start = enemy.Root != null ? enemy.Root.transform.position : Vector3.zero;

            enemy.State.MoveTo(next);
            ApplyEnemyVisuals(enemy);

            // 플레이어 시야 밖 이동은 연출 없이 즉시 배치 — 걸음마다 도는 적 페이즈가
            // 활성 몬스터 수만큼 느려지지 않게 한다.
            if (animate && enemy.Root != null)
            {
                Vector3 end = enemy.Root.transform.position;
                float duration = secondsPerStep * 0.75f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    enemy.Root.transform.position = Vector3.Lerp(start, end, SmoothStep(t));
                    yield return null;
                }
                enemy.Root.transform.position = end;
            }

            // 약한 바닥은 몬스터가 밟아도 무너진다 — 플레이어와 동일 규칙. (GDD §5.3)
            if (_grid.Map.Get(next)?.kind == TileKind.WeakFloor)
            {
                _enemyPhaseMapChanged = true;
                yield return CollapseUnderEnemy(enemy, next);
            }
        }

        /// <summary>몬스터 한 마리의 위치·정렬·틴트·가시성만 갱신하는 가벼운 경로. (전체 리빌드 금지)</summary>
        private void ApplyEnemyVisuals(EnemyAgent enemy)
        {
            if (enemy.Root == null) return;
            GridPos pos = enemy.State.Position;
            enemy.Root.transform.position = _grid.GridToWorld(pos);
            enemy.Renderer.sortingOrder = _grid.iso.SortingOrder(SortingAnchor(pos), 1);
            float dim = ElevationDim(pos);
            Color tint = EnemyTint(enemy.State);
            enemy.Renderer.color = new Color(tint.r * dim, tint.g * dim, tint.b * dim, tint.a);
            bool visibleToPlayer =
                _dungeon.Height.FloorIndex(pos.elevation) == _activeFloorIndex &&
                (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(pos));
            SetSpriteHierarchyVisible(enemy.Root, visibleToPlayer);
            UpdateMoodIcon(enemy, visibleToPlayer);
        }

        /// <summary>머리 위 인지 상태 아이콘 (SPD 관례): 추격 "!", 도주 "…", 순찰은 없음.</summary>
        private static TextMesh CreateMoodIcon(GameObject owner)
        {
            var icon = new GameObject("Mood Icon");
            icon.transform.SetParent(owner.transform, false);
            icon.transform.localPosition = new Vector3(0.24f, 1.02f, 0f);

            var text = icon.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 44;
            text.characterSize = 0.05f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;

            var renderer = icon.GetComponent<MeshRenderer>();
            renderer.material = text.font.material;
            renderer.sortingOrder = 30003;
            icon.SetActive(false);
            return text;
        }

        private void UpdateMoodIcon(EnemyAgent enemy, bool visibleToPlayer)
        {
            if (enemy.MoodIcon == null) return;

            MonsterMood mood = enemy.Brain != null ? enemy.Brain.Mood : MonsterMood.Patrol;
            bool show = visibleToPlayer && enemy.State.IsAlive && mood != MonsterMood.Patrol;
            enemy.MoodIcon.gameObject.SetActive(show);
            if (!show) return;

            if (mood == MonsterMood.Chase)
            {
                enemy.MoodIcon.text = "!";
                enemy.MoodIcon.color = new Color32(255, 96, 80, 255);
            }
            else // Flee
            {
                enemy.MoodIcon.text = "…";
                enemy.MoodIcon.color = new Color32(160, 208, 255, 255);
            }
        }

        /// <summary>인지 상태 전환 연출: 발견 "!" 팝업 + 상태칩, 수색 포기 "?", 도주 시작 안내.</summary>
        private void NotifyMoodTransition(EnemyAgent enemy)
        {
            MonsterMood mood = enemy.Brain.Mood;
            if (mood == enemy.LastMood)
            {
                UpdateMoodIcon(enemy, IsEnemyVisibleToPlayer(enemy));
                return;
            }

            enemy.LastMood = mood;
            bool visible = IsEnemyVisibleToPlayer(enemy);
            UpdateMoodIcon(enemy, visible);
            if (!visible) return;

            Vector3 popupPos = enemy.Root != null
                ? enemy.Root.transform.position
                : _grid.GridToWorld(enemy.State.Position);
            string name = RunSummary.FormatCause(enemy.State.Id);

            switch (mood)
            {
                case MonsterMood.Chase:
                    FloatingText?.Show(popupPos, "!", FloatingTextKind.Alert);
                    InteractionFeedback?.Invoke($"{name}이(가) 당신을 발견했다!");
                    break;
                case MonsterMood.Flee:
                    FloatingText?.Show(popupPos, "…", FloatingTextKind.Alert);
                    InteractionFeedback?.Invoke($"{name}이(가) 달아난다!");
                    break;
                default: // 추격 → 순찰: 수색 포기
                    FloatingText?.Show(popupPos, "?", FloatingTextKind.Alert);
                    break;
            }
        }

        private bool IsEnemyVisibleToPlayer(EnemyAgent enemy) =>
            _dungeon.Height.FloorIndex(enemy.State.Position.elevation) == _activeFloorIndex &&
            (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(enemy.State.Position));

        /// <summary>몬스터 틴트의 단일 출처: 사망 회색 > 빙결 하늘색 > 화상 주황 > 기본.</summary>
        private static Color EnemyTint(CombatantState state)
        {
            if (!state.IsAlive) return new Color32(60, 64, 66, 180);
            if (state.Statuses.Has(StatusKind.Freeze)) return new Color32(140, 210, 235, 255);
            if (state.Statuses.Has(StatusKind.Burn)) return new Color32(255, 168, 112, 255);
            return Color.white;
        }
    }
}
