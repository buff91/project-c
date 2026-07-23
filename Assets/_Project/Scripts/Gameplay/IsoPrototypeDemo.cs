using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public enum DungeonViewMode
    {
        Play = 0,
        DebugAll = 1
    }

    public enum CombatActionMode
    {
        Melee = 0,
        Ranged = 1
    }

    /// <summary>
    /// 64×32 픽셀 규격을 검증하는 교체 가능한 아이소메트릭 프로토타입.
    /// 외부 아트가 없어도 런타임 픽셀 스프라이트로 지형과 탭 이동을 확인할 수 있다.
    ///
    /// partial 구성: 이 파일(상태·입력·턴/전투·연출) +
    /// IsoPrototypeDemo.Visibility.cs(FOV·수직 포털·후면 벽·가림) +
    /// IsoPrototypeDemo.Sprites.cs(런타임 임시 스프라이트 생성).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridManager), typeof(IsoTapInput))]
    public partial class IsoPrototypeDemo : MonoBehaviour
    {
        public const int TilePixelWidth = 64;
        public const int TilePixelHeight = 32;
        public const int PixelsPerUnit = 64;

        [Header("프로토타입")]
        [Tooltip("층 한 변 크기. 키우면 방·복도가 넓어지고 적/아이템 밀도가 면적 비례로 따라 오른다.")]
        [Range(9, 20)] public int roomSize = 13;
        [Range(0.03f, 0.3f)] public float secondsPerStep = 0.09f;
        public bool buildOnStart = true;
        public bool configureMainCamera = true;

        [Header("카메라 구도")]
        [Range(4f, 7f)] public float playCameraSize = 5.2f;
        [Range(7f, 16f)] public float debugCameraSize = 8.8f;
        [Min(0f)] public float hubCameraHorizontalPadding = 0.75f;
        [Min(0f)] public float hubCameraVerticalPadding = 1.5f;

        [Header("M1 전투")]
        [Min(1)] public int playerMaxHp = 8;
        [Min(1)] public int playerAttack = 2;
        [Tooltip("원거리는 근접보다 약하다 — 같은 피해면 카이팅으로 접근전이 성립하지 않는다.")]
        [Min(1)] public int rangedAttackDamage = 1;
        [Range(2, 8)] public int rangedAttackRange = 4;
        public CombatActionMode combatMode = CombatActionMode.Melee;
        [Tooltip("적이 죽은 뒤 시체가 월드에 남는 턴 수. 지나면 탭 대상과 시뮬레이션 목록에서도 제거한다.")]
        [Range(1, 8)] public int corpseLifetimeTurns = 3;

        [Header("M1 아이템")]
        [Min(1)] public int potionHealAmount = 5;
        [Min(1)] public int bombDamage = 3;
        [Min(0)] public int frostBombDamage = 1;
        [Range(2, 8)] public int bombThrowRange = 4;
        [Tooltip("투척 단검 피해. 소모품이므로 상시 원거리보다 강하다.")]
        [Min(1)] public int knifeDamage = 3;

        [Header("던전 체인")]
        [Tooltip("한 판에 완주해야 하는 던전 수. 각 던전의 최심층 출구가 다음 던전으로 이어진다.")]
        [Range(1, 5)] public int stageCount = 3;

        [Header("허브 모드")]
        [Tooltip("켜면 던전 대신 허브 캠프(상인/영웅/창고/포탈)를 만든다. Hub 씬 전용.")]
        public bool hubMode;

        [Header("M3 다층 던전")]
        [Range(2, 5)] public int floorCount = 3;
        [Range(3, 6)] public int elevationsPerFloor = 4;
        [Tooltip("절차 생성 seed. 같은 값이면 같은 던전이 재현된다.")]
        public int dungeonSeed = 1977;
        [Tooltip("검증 장면에서 시작할 깊이. 1이면 B2에서 시작한다.")]
        [Range(0, 2)] public int previewStartDepth = 0;
        public DungeonViewMode viewMode = DungeonViewMode.Play;
        [Range(3, 10)] public int fieldOfViewRadius = 6;
        [Range(1, 2)] public int verticalPreviewRadius = 2;
        [Range(0.05f, 0.4f)] public float exploredAlpha = 0.16f;
        [Range(0.1f, 0.7f)] public float verticalPreviewAlpha = 0.54f;
        [Range(0.2f, 1.2f)] public float playAdjacentFloorSeparation = 0.6f;
        [Range(0.8f, 3f)] public float debugFloorSeparation = 1.6f;
        [Range(0.15f, 0.8f)] public float debugAdjacentAlpha = 0.48f;
        [Tooltip("자동 검증용: Play 시작 후 첫 하행 계단을 타고 B2로 이동한다.")]
        public bool autoDescendOnStart;

        [Header("안개 / 맵 경계")]
        [Tooltip("실제 방 구조는 숨기고, 현재 층이 놓이는 전체 영역만 어두운 안개로 구분한다.")]
        public bool showDungeonFogBackdrop = true;
        public Color32 unknownFogColor = new Color32(8, 13, 20, 210);
        public Color32 unknownFogEdge = new Color32(18, 27, 38, 228);

        [Header("플레이어 가림 처리")]
        [Tooltip("플레이어와 화면상 겹치는 앞쪽 타일·벽을 자동으로 투명하게 만든다.")]
        public bool fadePlayerOccluders = true;
        [Range(0.12f, 0.7f)] public float playerOccluderAlpha = 0.3f;
        [Range(2f, 16f)] public float playerOccluderFadeSpeed = 8f;
        [Range(0f, 0.25f)] public float playerOcclusionPadding = 0.06f;

        [Header("4방향 시점")]
        public bool showRearWalls = true;

        [Tooltip("Aseprite에서 만든 실제 스프라이트를 연결한다. 비어 있으면 런타임 임시 아트를 사용한다.")]
        public IsoVisualCatalog visualCatalog;

        [Header("팔레트")]
        public Color32 floorTop = new Color32(72, 78, 82, 255);
        public Color32 raisedTop = new Color32(102, 88, 70, 255);
        public Color32 lowerTop = new Color32(43, 55, 59, 255);
        public Color32 tileSeam = new Color32(31, 38, 42, 255);
        public Color32 outline = new Color32(12, 16, 20, 255);
        public Color32 accent = new Color32(84, 211, 197, 255);

        public GridPos PlayerPos => _playerPos;
        public int ActiveFloorIndex => _activeFloorIndex;
        public int ViewQuarterTurns => _grid != null ? _grid.iso.viewQuarterTurns : 0;
        public DungeonViewMode ViewMode => viewMode;
        public CombatActionMode CombatMode => combatMode;
        public int RangedAttackRange => rangedAttackRange;
        public string VerticalHintLabel => BuildVerticalHintLabel();
        public string LocationLabel => _dungeon == null
            ? "--"
            : $"{FloorLabel(_activeFloorIndex)} · HEIGHT {_dungeon.Height.LocalHeight(_playerPos.elevation)} · ({_playerPos.x},{_playerPos.y})";
        public string ActiveFloorLabel => FloorLabel(_activeFloorIndex);
        public string AboveFloorLabel => _dungeon != null && _dungeon.TryGetFloor(_activeFloorIndex + 1, out _)
            ? FloorLabel(_activeFloorIndex + 1)
            : "--";
        public string BelowFloorLabel => _dungeon != null && _dungeon.TryGetFloor(_activeFloorIndex - 1, out _)
            ? FloorLabel(_activeFloorIndex - 1)
            : "--";
        public int PotionCount => _inventory.Count(ItemKind.Potion);
        public int BombCount => _inventory.Count(ItemKind.Bomb);
        public int FrostBombCount => _inventory.Count(ItemKind.FrostBomb);
        public int ItemCount(ItemKind kind) => _inventory.Count(kind);
        public BackpackLayout CurrentBackpackLayout => _inventory.CreateLayout();

        /// <summary>지금 들고 있는 전리품의 골드 환산 가치 (출구 선택지 표시용).</summary>
        public int CarriedTreasureGold()
        {
            int gold = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
                gold += ItemCatalog.GoldValue(kind) * _inventory.Count(kind);
            return gold;
        }
        public int StageIndex => _stageIndex;
        public string StageLabel => $"던전 {_stageIndex}/{stageCount}";
        public bool BombAiming => _bombAiming;
        public ItemKind AimedBombKind => _bombAimKind;
        public CombatantState PlayerState => _playerState;
        public RunSummary RunSummary => _runSummary;
        public event System.Action<int> ViewRotationChanged;
        public event System.Action<int> ActiveFloorChanged;
        public event System.Action<DungeonViewMode> ViewModeChanged;
        public event System.Action<CombatActionMode> CombatModeChanged;
        public event System.Action<string> InteractionFeedback;
        public event System.Action<VerticalRouteCue> VerticalRouteDiscovered;
        public event System.Action PlayerPositionChanged;
        public event System.Action VerticalContextChanged;
        public event System.Action InventoryChanged;
        public event System.Action<bool> BombAimingChanged;
        public event System.Action PlayerHpChanged;
        public event System.Action<RunSummary> RunEnded;
        /// <summary>던전 출구 도착 — HUD 가 "다음 던전 vs 생환" 선택지를 띄운다.</summary>
        public event System.Action ExitChoiceRequested;
        /// <summary>허브 상호작용 — id: "merchant" | "stash" | "hero:{heroId}".</summary>
        public event System.Action<string> HubInteractionRequested;
        /// <summary>플레이어 자신을 탭 — HUD 가 액션 휠을 토글한다.</summary>
        public event System.Action PlayerTapped;

        private GridManager _grid;
        private IsoTapInput _input;
        private Transform _visualRoot;
        private GameObject _player;
        private SpriteRenderer _playerRenderer;
        private GridSortingObject _playerSorting;
        private Transform _playerLocator;
        private Transform _playerFootprint;
        private GridPos _playerPos;
        private GameObject _barrel;
        private SpriteRenderer _barrelRenderer;
        private GridPos _barrelPos;
        private readonly List<EnemyAgent> _enemies = new List<EnemyAgent>();
        private readonly List<ItemAgent> _items = new List<ItemAgent>();
        private readonly Inventory _inventory =
            new Inventory(BackpackRules.Columns, BackpackRules.Rows);
        private bool _bombAiming;
        private ItemKind _bombAimKind = ItemKind.Bomb;
        private GameObject _selection;
        private GridPos _selectionPos;
        private Transform _wallRoot;
        private Transform _shaftRoot;
        private Coroutine _moveRoutine;
        private Transform _playerHpFill;
        private CombatantState _playerState;
        private readonly TurnManager _turns = new TurnManager();
        private bool _resolvingAction;
        private bool _travelCancelRequested;
        private bool _godMode;
        private int _stageIndex = 1;
        private HeroArchetype _hero;
        private RunSummary _runSummary = new RunSummary();
        private FloatingTextSpawner _floatingText;
        private readonly HashSet<string> _travelVisibleEnemyIds = new HashSet<string>();
        private readonly HashSet<GridPos> _travelVisibleItemTiles = new HashSet<GridPos>();
        private readonly Dictionary<GridPos, string> _hubInteractables =
            new Dictionary<GridPos, string>();
        private readonly Dictionary<string, SpriteRenderer> _hubHeroProps =
            new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, GridPos> _hubHeroPositions =
            new Dictionary<string, GridPos>();
        private DungeonLayout _dungeon;
        private int _activeFloorIndex;
        private readonly Dictionary<GridPos, SpriteRenderer> _tileRenderers =
            new Dictionary<GridPos, SpriteRenderer>();
        private readonly Dictionary<SpriteRenderer, GridPos> _rearWallRenderers =
            new Dictionary<SpriteRenderer, GridPos>();
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private readonly HashSet<GridPos> _visibleTiles = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _exploredTiles = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _verticalPreviewTiles = new HashSet<GridPos>();
        private readonly List<VerticalLandmarkAgent> _verticalLandmarks =
            new List<VerticalLandmarkAgent>();
        private readonly HashSet<VerticalRouteRole> _discoveredVerticalRoutes =
            new HashSet<VerticalRouteRole>();
        private SpriteRenderer _dungeonFogBackdrop;
        private Camera _configuredCamera;
        private float _lastCameraAspect = -1f;

        private void Awake()
        {
            _grid = GetComponent<GridManager>();
            _input = GetComponent<IsoTapInput>();
        }

        private void OnEnable()
        {
            if (_input == null) _input = GetComponent<IsoTapInput>();
            _input.TileTapped += HandleTileTapped;
            _input.ViewRotationRequested += RotateView;
            _input.StepRequested += HandleStepRequested;
            _input.InteractRequested += InteractAdjacent;
            _input.WaitRequested += WaitTurn;
            _input.ActorPicker = PickEnemyTileAt;
            _input.TilePicker = PickVisibleTileAt;

            // 생성된 임시 스프라이트는 씬에 저장하지 않는다. 대신 씬을 열 때마다
            // 편집 모드 미리보기를 다시 만들어 Game 뷰가 비어 보이지 않게 한다.
            if (!Application.isPlaying && buildOnStart)
                BuildPrototype();
        }

        private void Start()
        {
            if (Application.isPlaying && buildOnStart)
            {
                // Unity 창이 포커스를 잃어도 MCP 자동 검증과 턴 코루틴이 진행되게 한다.
                Application.runInBackground = true;
                BuildPrototype();
                if (autoDescendOnStart)
                    StartCoroutine(AutoDescend());
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.TileTapped -= HandleTileTapped;
                _input.ViewRotationRequested -= RotateView;
                _input.StepRequested -= HandleStepRequested;
                _input.InteractRequested -= InteractAdjacent;
                _input.WaitRequested -= WaitTurn;
                if (_input.ActorPicker == PickEnemyTileAt)
                    _input.ActorPicker = null;
                if (_input.TilePicker == PickVisibleTileAt)
                    _input.TilePicker = null;
            }
        }

        public void BuildPrototype()
        {
            if (_grid == null) _grid = GetComponent<GridManager>();
            if (_input == null) _input = GetComponent<IsoTapInput>();

            // 이전 8×8 프로토타입 씬을 열어도 세 방 레이아웃의 최소 규격으로 자동 이행한다.
            roomSize = Mathf.Max(9, roomSize);

            // 이어하기: 저장된 seed/규격으로 같은 던전을 재생성하고 해당 층 입구에서 시작한다.
            RunSaveData continueData = null;
            if (Application.isPlaying && !hubMode && RunSaveStore.ContinueRequested)
            {
                RunSaveStore.ContinueRequested = false;
                if (RunSaveStore.TryLoad(out continueData))
                {
                    dungeonSeed = continueData.seed;
                    roomSize = Mathf.Max(9, continueData.roomSize);
                    floorCount = continueData.floorCount;
                    elevationsPerFloor = continueData.elevationsPerFloor;
                    _stageIndex = Mathf.Max(1, continueData.stageIndex);
                }
            }
            if (Application.isPlaying && !hubMode)
                previewStartDepth = RunStartRules.ResolvePreviewDepth(continueData);
            if (Application.isPlaying && !hubMode && continueData == null && _stageIndex == 1)
                dungeonSeed = DungeonSelection.Selected.Seed;

            // 영웅 프리셋: 새 판은 메뉴 선택, 이어하기는 저장된 영웅. 편집 모드 미리보기는 인스펙터 값 유지.
            if (Application.isPlaying)
            {
                _hero = HeroRoster.ById(continueData != null ? continueData.heroId : HeroSelection.SelectedId);
                playerMaxHp = _hero.MaxHp;
                playerAttack = _hero.Attack;
                rangedAttackDamage = _hero.RangedDamage;
            }

            if (Application.isPlaying && _moveRoutine != null)
                StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _resolvingAction = false;
            _travelCancelRequested = false;
            _turns.Reset();
            _visibleTiles.Clear();
            _exploredTiles.Clear();
            _verticalPreviewTiles.Clear();
            _verticalLandmarks.Clear();
            _discoveredVerticalRoutes.Clear();
            _enemies.Clear();
            _items.Clear();
            _inventory.Clear();
            _bombAiming = false;
            _barrelExploded = false;
            _lastTrickleSpawnTurn = 0;

            _grid.buildDemoOnStart = false;
            _grid.iso.tileWidth = 1f;
            _grid.iso.tileHeight = 0.5f;
            _grid.iso.elevationStep = 0.25f;
            _grid.iso.viewPivotX = (roomSize - 1) * 0.5f;
            _grid.iso.viewPivotY = (roomSize - 1) * 0.5f;
            _grid.iso.SetViewRotation(0);

            ClearVisuals();
            BuildRoomData();
            CreateRoomVisuals();
            CreateActorsAndProps();
            if (continueData != null)
                ApplyContinueData(continueData);
            else if (Application.isPlaying && !hubMode && _hero != null && _stageIndex == 1)
            {
                // 시작 키트는 첫 던전에서만 — 던전 전환은 ApplyCarriedState 가 이월한다.
                if (_hero.StartPotions > 0) _inventory.AddUpTo(ItemKind.Potion, _hero.StartPotions);
                if (_hero.StartBombs > 0) _inventory.AddUpTo(ItemKind.Bomb, _hero.StartBombs);
                if (_hero.StartFrostBombs > 0) _inventory.AddUpTo(ItemKind.FrostBomb, _hero.StartFrostBombs);

                // 허브에서 선택한 출정 백팩만 반입한다. 창고의 나머지 물품은 안전하게 유지한다.
                MetaSaveData meta = MetaStore.LoadOrNew();
                int selected = 0;
                foreach (ItemKind kind in ItemCatalog.AllKinds)
                    selected += meta.GetLoadoutCount(kind);

                int carried = ExpeditionLoadoutRules.ConsumeLoadout(meta, _inventory);
                if (selected > 0)
                {
                    MetaStore.Save(meta);
                    int returned = selected - carried;
                    string leftover = returned > 0 ? $" · {returned}개는 창고 복귀" : "";
                    InteractionFeedback?.Invoke(
                        $"{_hero.DisplayName} — 출정 물품 {carried}개 반입{leftover}");
                }
                else
                {
                    InteractionFeedback?.Invoke($"{_hero.DisplayName} — 기본 지급품으로 던전 진입");
                }
            }

            if (configureMainCamera)
                ConfigureCamera(Camera.main);

            ViewRotationChanged?.Invoke(_grid.iso.viewQuarterTurns);
            ActiveFloorChanged?.Invoke(_activeFloorIndex);
            ViewModeChanged?.Invoke(viewMode);
            CombatModeChanged?.Invoke(combatMode);
            PlayerPositionChanged?.Invoke();
            InventoryChanged?.Invoke();
            BombAimingChanged?.Invoke(false);
            PlayerHpChanged?.Invoke();
        }

        // ── 디버그 창 전용 API (에디터/개발빌드에서 DebugPanelController 가 호출) ──

        public bool DebugGodMode => _godMode;
        public int DebugSeed => dungeonSeed;
        public int DebugTurnNumber => _turns.TurnNumber;

        public int DebugLivingEnemiesOnFloor()
        {
            int count = 0;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive &&
                    _dungeon != null &&
                    _dungeon.Height.FloorIndex(enemy.State.Position.elevation) == _activeFloorIndex)
                    count++;
            }
            return count;
        }

        public void DebugToggleGodMode()
        {
            if (!Application.isPlaying) return;
            _godMode = !_godMode;
            InteractionFeedback?.Invoke(_godMode ? "CHEAT: GOD MODE ON" : "CHEAT: GOD MODE OFF");
        }

        public void DebugHealFull()
        {
            if (!Application.isPlaying || _playerState == null) return;
            _playerState.OverrideHpForDebug(_playerState.MaxHp);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            InteractionFeedback?.Invoke("CHEAT: HP FULL");
        }

        public void DebugDamageSelf(int amount)
        {
            if (!Application.isPlaying || _playerState == null || !_playerState.IsAlive) return;
            int dealt = _playerState.TakeDamage(amount);
            StartCoroutine(ShowPlayerHit(dealt, "Debug"));
        }

        public void DebugGiveItem(ItemKind kind)
        {
            if (!Application.isPlaying) return;
            if (!_inventory.TryAdd(kind, out int count))
            {
                InteractionFeedback?.Invoke(
                    $"CHEAT: 백팩 공간 부족 · {ItemCatalog.DisplayName(kind)} " +
                    $"{BackpackRules.Footprint(kind)}칸 필요");
                return;
            }
            InventoryChanged?.Invoke();
            InteractionFeedback?.Invoke($"CHEAT: {ItemLabel(kind)} +1 (×{count})");
        }

        public void DebugKillAllOnFloor()
        {
            if (!Application.isPlaying || _dungeon == null) return;
            int killed = 0;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (!enemy.State.IsAlive ||
                    _dungeon.Height.FloorIndex(enemy.State.Position.elevation) != _activeFloorIndex)
                    continue;
                enemy.State.TakeDamage(9999);
                UpdateHealthBar(enemy.HpFill, enemy.State);
                ApplyEnemyVisuals(enemy);
                killed++;
            }
            InteractionFeedback?.Invoke($"CHEAT: 몬스터 {killed}마리 제거");
        }

        public void DebugJumpFloor(int delta)
        {
            if (!Application.isPlaying || _dungeon == null || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (!_dungeon.TryGetFloor(_activeFloorIndex + delta, out DungeonFloorInfo floor))
            {
                InteractionFeedback?.Invoke("CHEAT: 그 방향에 층이 없다");
                return;
            }

            _playerState.MoveTo(floor.Entry);
            _player.transform.position = _grid.GridToWorld(floor.Entry);
            SyncPlayerView(floor.Entry, floorChanged: true);
            _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
            InteractionFeedback?.Invoke($"CHEAT: {FloorLabel(_activeFloorIndex)} 로 점프");
        }

        public void DebugClearSave()
        {
            if (!Application.isPlaying) return;
            RunSaveStore.Clear();
            InteractionFeedback?.Invoke("CHEAT: 세이브 삭제");
        }

        /// <summary>이어하기 데이터의 HP·인벤토리·전적을 새로 만든 판에 덧입힌다.</summary>
        private void ApplyContinueData(RunSaveData data)
        {
            ApplyCarriedState(data, $"이어하기 — {FloorLabel(_activeFloorIndex)} 입구에서 재개");
            Debug.Log($"[Save] 이어하기: {StageLabel} {FloorLabel(_activeFloorIndex)}, " +
                      $"HP {_playerState.Hp}, 처치 {data.kills}");
        }

        /// <summary>이어하기와 던전 전환이 공유하는 상태 이월(HP·인벤토리·전적).</summary>
        private void ApplyCarriedState(RunSaveData data, string feedback)
        {
            int hp = Mathf.Clamp(data.hp, 1, _playerState.MaxHp);
            if (hp < _playerState.MaxHp)
                _playerState.TakeDamage(_playerState.MaxHp - hp);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();

            if (data.potions > 0) _inventory.Add(ItemKind.Potion, data.potions);
            if (data.bombs > 0) _inventory.Add(ItemKind.Bomb, data.bombs);
            if (data.frostBombs > 0) _inventory.Add(ItemKind.FrostBomb, data.frostBombs);
            if (data.oilFlasks > 0) _inventory.Add(ItemKind.OilFlask, data.oilFlasks);
            if (data.knives > 0) _inventory.Add(ItemKind.ThrowingKnife, data.knives);
            if (data.scrolls > 0) _inventory.Add(ItemKind.RecallScroll, data.scrolls);
            if (data.coinPouches > 0) _inventory.Add(ItemKind.CoinPouch, data.coinPouches);
            if (data.gemstones > 0) _inventory.Add(ItemKind.Gemstone, data.gemstones);
            if (data.relics > 0) _inventory.Add(ItemKind.Relic, data.relics);
            if (data.herbs > 0) _inventory.Add(ItemKind.Herb, data.herbs);
            if (data.powders > 0) _inventory.Add(ItemKind.BlastPowder, data.powders);
            if (data.frostShards > 0) _inventory.Add(ItemKind.FrostShard, data.frostShards);
            InventoryChanged?.Invoke();

            _runSummary = new RunSummary(data.deepestFloorIndex, data.kills);
            _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
            InteractionFeedback?.Invoke(feedback);
        }

        /// <summary>층 도착 시점의 체크포인트 저장. 판이 끝났으면 저장하지 않는다.</summary>
        private void SaveCheckpoint()
        {
            if (!Application.isPlaying || hubMode || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;

            RunSaveStore.Save(new RunSaveData
            {
                heroId = _hero != null ? _hero.Id : null,
                seed = dungeonSeed,
                roomSize = roomSize,
                floorCount = floorCount,
                elevationsPerFloor = elevationsPerFloor,
                stageIndex = _stageIndex,
                currentFloorIndex = _activeFloorIndex,
                hp = _playerState.Hp,
                potions = PotionCount,
                bombs = BombCount,
                frostBombs = FrostBombCount,
                oilFlasks = ItemCount(ItemKind.OilFlask),
                knives = ItemCount(ItemKind.ThrowingKnife),
                scrolls = ItemCount(ItemKind.RecallScroll),
                coinPouches = ItemCount(ItemKind.CoinPouch),
                gemstones = ItemCount(ItemKind.Gemstone),
                relics = ItemCount(ItemKind.Relic),
                herbs = ItemCount(ItemKind.Herb),
                powders = ItemCount(ItemKind.BlastPowder),
                frostShards = ItemCount(ItemKind.FrostShard),
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex
            });
        }

        /// <summary>플로팅 텍스트 스포너를 지연 생성한다. 편집 모드 미리보기에는 만들지 않는다.</summary>
        private FloatingTextSpawner FloatingText
        {
            get
            {
                if (_floatingText == null && Application.isPlaying)
                {
                    var host = new GameObject("Floating Text");
                    host.transform.SetParent(transform, false);
                    _floatingText = host.AddComponent<FloatingTextSpawner>();
                }
                return _floatingText;
            }
        }

        private void BuildRoomData()
        {
            if (hubMode)
            {
                _dungeon = HubLayout.Build(_grid.Map);
                _activeFloorIndex = 0;
                _runSummary = new RunSummary();
                UpdateInputFloorRange();
                return;
            }

            _dungeon = DungeonGenerator.Generate(
                _grid.Map,
                roomSize,
                roomSize,
                floorCount,
                elevationsPerFloor,
                dungeonSeed);
            int startDepth = Mathf.Clamp(previewStartDepth, 0, _dungeon.Floors.Count - 1);
            _activeFloorIndex = _dungeon.Floors[startDepth].FloorIndex;
            _runSummary = new RunSummary(GlobalFloorIndex(_activeFloorIndex));
            UpdateInputFloorRange();
        }

        private void CreateRoomVisuals()
        {
            var root = new GameObject("Generated Visuals");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(transform, false);
            _visualRoot = root.transform;
            _tileRenderers.Clear();

            foreach (var pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                TileKind kind = pair.Value.kind;
                var tile = new GameObject($"Tile {pos} {kind}");
                tile.transform.SetParent(_visualRoot, false);
                tile.transform.position = VisualPosition(pos);

                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = GetTileSprite(kind, pos);
                renderer.sortingOrder = _grid.iso.SortingOrder(
                    TileVisualSortingPos(pos, kind),
                    TileSortOffset(kind));
                _tileRenderers.Add(pos, renderer);
            }

            CreateVerticalLandmarks();
            RefreshFloorVisibility();
        }

        private void CreateActorsAndProps()
        {
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo activeFloor);
            _playerPos = activeFloor.Entry;
            _playerState = new CombatantState("Player", _playerPos, playerMaxHp, playerAttack);
            Sprite playerSprite = visualCatalog != null
                ? visualCatalog.HeroFor(_hero != null ? _hero.Id : HeroSelection.SelectedId)
                : null;
            if (playerSprite == null)
                playerSprite = GetCharacterSprite(false);
            _player = CreateStandingSprite("Player", playerSprite, _playerPos, out _playerRenderer);
            _playerSorting = _player.AddComponent<GridSortingObject>();
            _playerSorting.grid = _grid;
            _playerSorting.microOffset = 1;
            _playerSorting.Pos = _playerPos;

            var locator = new GameObject("Player Locator");
            locator.transform.SetParent(_player.transform, false);
            locator.transform.localPosition = new Vector3(0f, 1.02f, 0f);
            var locatorRenderer = locator.AddComponent<SpriteRenderer>();
            locatorRenderer.sprite = GetPlayerLocatorSprite();
            locatorRenderer.sortingOrder = 30002;
            _playerLocator = locator.transform;

            var footprint = new GameObject("Player Footprint");
            footprint.transform.SetParent(_player.transform, false);
            footprint.transform.localPosition = Vector3.zero;
            var footprintRenderer = footprint.AddComponent<SpriteRenderer>();
            footprintRenderer.sprite = visualCatalog != null && visualCatalog.playerFootprint != null
                ? visualCatalog.playerFootprint
                : GetPlayerFootprintSprite();
            footprintRenderer.sortingOrder = 29990;
            _playerFootprint = footprint.transform;

            Sprite barrelSprite = visualCatalog != null && visualCatalog.explosiveBarrel != null
                ? visualCatalog.explosiveBarrel
                : GetBarrelSprite();

            if (hubMode)
            {
                // 허브: 폭발통 대신 캠프 프롭(상인/영웅/창고/포탈/모닥불)을 세운다.
                _barrelExploded = true; // 폭발통 분기 비활성
                CreateHubProps();
                _playerHpFill = CreateHealthBar(_player, "Player HP");
                UpdateHealthBar(_playerHpFill, _playerState);
                _selection = new GameObject("Selection Marker");
                _selection.transform.SetParent(_visualRoot, false);
                var hubSelection = _selection.AddComponent<SpriteRenderer>();
                hubSelection.sprite = visualCatalog != null && visualCatalog.selection != null
                    ? visualCatalog.selection
                    : GetSelectionSprite();
                hubSelection.sortingOrder = _grid.iso.SortingOrder(_playerPos, -1);
                _selection.transform.position = _grid.GridToWorld(_playerPos);
                _selectionPos = _playerPos;
                RefreshFloorVisibility();
                return;
            }

            // 생성기가 배치한 스폰대로 모든 층의 적과 아이템을 만든다.
            // 몬스터 종류는 깊이 비례 혼합 — 스탯·혼합표는 MonsterRoster 한 곳에서. (M5)
            _spawnRng = new System.Random(dungeonSeed * 17);
            foreach (DungeonFloorInfo floor in _dungeon.Floors)
            {
                foreach (GridPos spawn in floor.EnemySpawns)
                    SpawnEnemy(
                        MonsterRoster.PickForDepth(GlobalDepth(floor.FloorIndex), _spawnRng),
                        spawn,
                        floor.FloorIndex);

                foreach (ItemSpawn itemSpawn in floor.Items)
                {
                    Sprite mapped = visualCatalog != null ? visualCatalog.ItemFor(itemSpawn.Kind) : null;
                    var item = new ItemAgent { Spawn = itemSpawn };
                    item.Root = CreateStandingSprite(
                        $"Item {itemSpawn.Kind} {itemSpawn.Position}",
                        mapped != null ? mapped : GetItemSprite(itemSpawn.Kind),
                        itemSpawn.Position,
                        out SpriteRenderer itemRenderer,
                        microOffset: 0);
                    item.Renderer = itemRenderer;
                    _items.Add(item);
                }
            }

            _barrelPos = FindPreviewPropPosition();
            _barrel = CreateStandingSprite("Explosive Barrel", barrelSprite, _barrelPos, out _barrelRenderer);

            _playerHpFill = CreateHealthBar(_player, "Player HP");
            UpdateHealthBar(_playerHpFill, _playerState);

            _selection = new GameObject("Selection Marker");
            _selection.transform.SetParent(_visualRoot, false);
            var selectionRenderer = _selection.AddComponent<SpriteRenderer>();
            selectionRenderer.sprite = visualCatalog != null && visualCatalog.selection != null
                ? visualCatalog.selection
                : GetSelectionSprite();
            selectionRenderer.sortingOrder = _grid.iso.SortingOrder(_playerPos, -1);
            _selection.transform.position = _grid.GridToWorld(_playerPos);
            _selectionPos = _playerPos;
            RefreshFloorVisibility();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || _playerLocator == null) return;

            if (hubMode && configureMainCamera)
            {
                Camera mainCamera = _configuredCamera != null ? _configuredCamera : Camera.main;
                if (mainCamera != null &&
                    !Mathf.Approximately(mainCamera.aspect, _lastCameraAspect))
                    ConfigureCamera(mainCamera);
            }

            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.1f;
            _playerLocator.localScale = new Vector3(pulse, pulse, 1f);
            if (_playerFootprint != null)
            {
                float footprintPulse = 1f + Mathf.Sin(Time.time * 5f) * 0.04f;
                _playerFootprint.localScale = new Vector3(footprintPulse, footprintPulse, 1f);
            }

            UpdatePlayerOccluders(Time.deltaTime);
        }

        private GameObject CreateStandingSprite(
            string objectName,
            Sprite sprite,
            GridPos pos,
            out SpriteRenderer renderer,
            int microOffset = 1)
        {
            var instance = new GameObject(objectName);
            instance.transform.SetParent(_visualRoot, false);
            instance.transform.position = _grid.GridToWorld(pos);
            renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = _grid.iso.SortingOrder(pos, microOffset);
            return instance;
        }

        public void RotateView(int direction)
        {
            if (_grid == null || _dungeon == null || _resolvingAction || direction == 0)
                return;

            _grid.iso.RotateView(direction);
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            ViewRotationChanged?.Invoke(_grid.iso.viewQuarterTurns);
            Debug.Log($"[View] 아이소 시점 회전: {_grid.iso.viewQuarterTurns * 90}°");
        }

        public void ToggleViewMode()
        {
            viewMode = viewMode == DungeonViewMode.Play
                ? DungeonViewMode.DebugAll
                : DungeonViewMode.Play;
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            ViewModeChanged?.Invoke(viewMode);
            Debug.Log($"[View] 던전 표시 모드: {viewMode}");
        }

        public void ToggleCombatMode()
        {
            combatMode = combatMode == CombatActionMode.Melee
                ? CombatActionMode.Ranged
                : CombatActionMode.Melee;
            CombatModeChanged?.Invoke(combatMode);
            InteractionFeedback?.Invoke(combatMode == CombatActionMode.Melee
                ? "MELEE: 적을 탭해 접근 공격"
                : $"RANGED: 사거리 {rangedAttackRange}, 문/벽에 차단");
        }

        /// <summary>물약을 마셔 HP를 회복한다. 행동 1회를 소비한다.</summary>
        public void UsePotion()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (PotionCount <= 0)
            {
                InteractionFeedback?.Invoke("NO POTIONS");
                return;
            }
            if (_playerState.Hp >= _playerState.MaxHp)
            {
                InteractionFeedback?.Invoke("HP FULL");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(DrinkPotion()));
        }

        /// <summary>
        /// 컨텍스트 상호작용 버튼 라벨 (M4~M5 이월분). 인접한 문/폭발통이 있으면
        /// "OPEN"/"CLOSE"/"PUSH", 없으면 null — HUD 가 매 프레임 폴링해 버튼을 숨긴다.
        /// </summary>
        public string ContextInteractionLabel =>
            !Application.isPlaying || _resolvingAction ||
            _playerState == null || !_playerState.IsAlive || _runSummary.Ended
                ? null
                : TryGetCurrentConnectorInteraction(out string connectorLabel)
                    ? connectorLabel
                    : TryFindAdjacentInteraction(out _, out string label) ? label : null;

        /// <summary>상호작용 버튼 실행 — 스페이스바/액션 휠과 같은 경로.</summary>
        public void PerformContextInteraction() => InteractAdjacent();

        /// <summary>폭탄/냉기 폭탄 조준 모드. 켠 상태에서 타일을 탭하면 투척한다.</summary>
        public void ToggleBombAim() => ToggleThrowAim(ItemKind.Bomb);

        public void ToggleFrostBombAim() => ToggleThrowAim(ItemKind.FrostBomb);

        /// <summary>투척류 아이템 공통 조준 진입점 (인벤토리 화면이 호출).</summary>
        public void ToggleAim(ItemKind kind)
        {
            if (kind == ItemKind.Bomb || kind == ItemKind.FrostBomb ||
                kind == ItemKind.OilFlask || kind == ItemKind.ThrowingKnife)
                ToggleThrowAim(kind);
        }

        private void ToggleThrowAim(ItemKind kind)
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;

            bool alreadyAimingThis = _bombAiming && _bombAimKind == kind;
            if (!alreadyAimingThis && _inventory.Count(kind) <= 0)
            {
                InteractionFeedback?.Invoke($"NO {ItemLabel(kind)}S");
                return;
            }

            _bombAimKind = kind;
            SetBombAiming(!alreadyAimingThis);
            string aimHint = kind == ItemKind.ThrowingKnife
                ? $"KNIFE: 적을 탭 · 사거리 {rangedAttackRange} · 피해 {knifeDamage}"
                : $"{ItemLabel(kind)}: 목표 타일 탭 · 사거리 {bombThrowRange} · 3×3";
            InteractionFeedback?.Invoke(_bombAiming ? aimHint : "AIM CANCELED");
        }

        private void SetBombAiming(bool aiming)
        {
            _bombAiming = aiming;
            // 조준 종류 전환도 HUD 하이라이트에 반영돼야 하므로 상태가 같아도 알린다.
            BombAimingChanged?.Invoke(aiming);
        }

        private static string ItemLabel(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return "POTION";
                case ItemKind.Bomb: return "BOMB";
                case ItemKind.FrostBomb: return "FROST";
                case ItemKind.OilFlask: return "OIL";
                case ItemKind.ThrowingKnife: return "KNIFE";
                case ItemKind.RecallScroll: return "SCROLL";
                case ItemKind.CoinPouch: return "COIN";
                case ItemKind.Gemstone: return "GEM";
                case ItemKind.Relic: return "RELIC";
                case ItemKind.Herb: return "HERB";
                case ItemKind.BlastPowder: return "POWDER";
                case ItemKind.FrostShard: return "SHARD";
                default: return kind.ToString();
            }
        }

        public void ApplyVisualSettings()
        {
            exploredAlpha = Mathf.Clamp(exploredAlpha, 0.05f, 0.4f);
            verticalPreviewAlpha = Mathf.Clamp(verticalPreviewAlpha, 0.1f, 0.8f);
            playerOccluderAlpha = Mathf.Clamp(playerOccluderAlpha, 0.12f, 0.7f);

            if (_dungeon == null) return;
            RefreshFloorVisibility();
            UpdatePlayerOccluders(0f, instant: true);
        }

        private void ApplyViewToVisuals()
        {
            foreach (var pair in _tileRenderers)
            {
                pair.Value.transform.position = VisualPosition(pair.Key);
                TileKind kind = _grid.Map.Get(pair.Key).kind;
                pair.Value.sortingOrder = _grid.iso.SortingOrder(
                    TileVisualSortingPos(pair.Key, kind),
                    TileSortOffset(kind));
                pair.Value.sprite = GetTileSprite(kind, pair.Key);
            }

            if (_playerSorting != null)
            {
                _playerSorting.Apply();
                ApplyPlayerVisualSorting(_playerSorting.Pos);
            }
            foreach (EnemyAgent enemy in _enemies)
                ApplyEnemyVisuals(enemy);
            foreach (ItemAgent item in _items)
            {
                if (item.Root == null) continue;
                item.Root.transform.position = _grid.GridToWorld(item.Spawn.Position);
                item.Renderer.sortingOrder = _grid.iso.SortingOrder(item.Spawn.Position, 0);
            }
            if (_barrelRenderer != null)
            {
                _barrel.transform.position = _grid.GridToWorld(_barrelPos);
                _barrelRenderer.sortingOrder = _grid.iso.SortingOrder(_barrelPos, 1);
            }
            if (_selection != null)
                PositionSelection(_selectionPos);

            RefreshFloorVisibility();
        }

        /// <summary>
        /// 탭 지점이 적(남아 있는 시체 포함) 스프라이트 안이면 그 발밑 타일을 반환한다.
        /// 아이소 몸통은 타일보다 화면상 위에 그려져, 평면 역변환만 쓰면
        /// 몸통 탭이 뒤 타일 이동으로 새는 문제의 보정이다. 겹치면 앞(정렬 위) 우선.
        /// 살아있으면 공격, 시체면 그 칸으로 이동 — 분기는 HandleTileTapped 가 한다.
        /// </summary>
        private GridPos? PickEnemyTileAt(Vector2 screenPoint)
        {
            if (!Application.isPlaying || _playerState == null || Camera.main == null)
                return null;

            Vector3 world = Camera.main.ScreenToWorldPoint(screenPoint);
            EnemyAgent best = null;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.Renderer == null || !enemy.Renderer.enabled)
                    continue;
                Bounds bounds = enemy.Renderer.bounds;
                if (world.x < bounds.min.x || world.x > bounds.max.x ||
                    world.y < bounds.min.y || world.y > bounds.max.y)
                    continue;
                if (best == null || enemy.Renderer.sortingOrder > best.Renderer.sortingOrder)
                    best = enemy;
            }

            return best?.State.Position;
        }

        /// <summary>
        /// 화면에 실제 그려진 타일 다이아몬드만 집는다. 전체 elevation 평면을 역산하면
        /// 같은 화면 좌표에 우연히 놓인 아래층 타일이 선택될 수 있으므로, 현재 활성 층을
        /// 최우선으로 하고 실제 개구부를 통해 표시된 인접 층은 그다음 순위로 둔다.
        /// </summary>
        private GridPos? PickVisibleTileAt(Vector2 screenPoint)
        {
            if (!Application.isPlaying || _dungeon == null || Camera.main == null)
                return null;

            Vector3 world = Camera.main.ScreenToWorldPoint(screenPoint);
            var candidates = new List<WorldInputCandidate>();
            foreach (var pair in _tileRenderers)
            {
                SpriteRenderer renderer = pair.Value;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                GridPos position = pair.Key;
                int floorIndex = _dungeon.Height.FloorIndex(position.elevation);
                bool activeFloor = floorIndex == _activeFloorIndex;
                bool inputVisible = viewMode == DungeonViewMode.DebugAll ||
                                    (activeFloor &&
                                     (_visibleTiles.Contains(position) ||
                                      _exploredTiles.Contains(position))) ||
                                    _verticalPreviewTiles.Contains(position);
                if (!inputVisible) continue;

                Vector3 center = VisualPosition(position);
                int layerPriority = activeFloor ? 2 : 1;
                candidates.Add(new WorldInputCandidate(
                    position,
                    center.x,
                    center.y,
                    layerPriority,
                    renderer.sortingOrder));
            }

            return WorldInputRules.TryPickProjectedTile(
                candidates,
                world.x,
                world.y,
                _grid.iso.tileWidth,
                _grid.iso.tileHeight,
                out GridPos picked)
                ? picked
                : (GridPos?)null;
        }

        /// <summary>
        /// 방향키 한 칸 이동 (PC). 탭 파이프라인을 재사용해 인접 적 공격·문 열기·
        /// 계단 오르내림이 그대로 성립한다. 조준 중엔 오발 방지를 위해 무시.
        /// </summary>
        private void HandleStepRequested(int dx, int dy)
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            int x = _playerPos.x + dx;
            int y = _playerPos.y + dy;
            // 계단 승강 커버: 위 → 같은 높이 → 아래 순으로 존재하는 타일을 고른다.
            for (int deltaElevation = 1; deltaElevation >= -1; deltaElevation--)
            {
                var candidate = new GridPos(x, y, _playerPos.elevation + deltaElevation);
                if (!_grid.Map.Has(candidate)) continue;

                // 이동 입력은 이동이 우선: 열린 문은 닫기 토글이 아니라 그냥 지나간다.
                // (문을 닫고 싶으면 Space/탭으로.)
                TileData candidateTile = _grid.Map.Get(candidate);
                if (candidateTile != null && candidateTile.kind == TileKind.DoorOpen &&
                    !IsLivingEnemyAt(candidate))
                {
                    List<GridPos> stepPath = GridPathfinder.FindPath(_grid.Map, _playerPos, candidate);
                    if (stepPath.Count > 1)
                    {
                        StartPlayerAction(candidate, MovePlayerPath(stepPath));
                        return;
                    }
                }

                HandleTileTapped(candidate, tileExists: true);
                return;
            }
            HandleTileTapped(new GridPos(x, y, _playerPos.elevation), tileExists: false);
        }

        /// <summary>
        /// 인접 상호작용 (스페이스바/액션 휠): 적 공격 > 문 > 허브 오브젝트 > 폭발통.
        /// 대상이 없으면 아무 일도 하지 않는다 — 오입력이 턴을 낭비하지 않게.
        /// </summary>
        public void InteractAdjacent()
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            if (TryActivateHubPortal() || TryActivateCurrentConnector())
                return;

            if (TryFindAdjacentInteraction(out GridPos target, out _))
                HandleTileTapped(target, tileExists: true);
        }

        private bool TryGetCurrentConnectorInteraction(out string label)
        {
            label = null;
            if (_grid == null || _dungeon == null) return false;

            TileKind? kind = _grid.Map.Get(_playerPos)?.kind;
            if (kind == TileKind.Ladder)
            {
                IReadOnlyList<GridPos> ladderLinks = _grid.Map.LinksFrom(_playerPos);
                if (ladderLinks.Count == 0) return false;
                label = ladderLinks[0].elevation > _playerPos.elevation
                    ? "사다리 오르기"
                    : "사다리 내려가기";
                return true;
            }

            if (kind == TileKind.StairsUp)
            {
                label = $"{AboveFloorLabel}로 이동";
                return _grid.Map.LinksFrom(_playerPos).Count > 0;
            }

            if (kind == TileKind.StairsDown)
            {
                bool hasDestination = _grid.Map.LinksFrom(_playerPos).Count > 0 ||
                                      (_activeFloorIndex == _dungeon.BottomFloorIndex &&
                                       _stageIndex < stageCount);
                if (!hasDestination) return false;
                label = _activeFloorIndex == _dungeon.BottomFloorIndex && _stageIndex < stageCount
                    ? "다음 던전으로"
                    : $"{BelowFloorLabel}로 이동";
                return true;
            }

            return false;
        }

        private bool TryActivateHubPortal()
        {
            if (!hubMode || _playerPos != HubLayout.Portal) return false;
            InteractionFeedback?.Invoke("포탈 활성화 — 목적지를 선택하세요");
            HubInteractionRequested?.Invoke("dungeon-select");
            return true;
        }

        private bool TryActivateCurrentConnector()
        {
            if (!TryGetCurrentConnectorInteraction(out string label)) return false;

            TileKind kind = _grid.Map.Get(_playerPos).kind;
            IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(_playerPos);
            if (links.Count > 0)
            {
                var path = new List<GridPos> { _playerPos, links[0] };
                InteractionFeedback?.Invoke(label);
                StartPlayerAction(links[0], MovePlayerPath(path));
                return true;
            }

            if (kind == TileKind.StairsDown &&
                _activeFloorIndex == _dungeon.BottomFloorIndex &&
                _stageIndex < stageCount)
            {
                var path = new List<GridPos> { _playerPos };
                InteractionFeedback?.Invoke(label);
                StartPlayerAction(_playerPos, MoveAndAdvanceStage(path, _playerPos));
                return true;
            }

            return false;
        }

        /// <summary>인접 상호작용 대상 탐색. 액션 휠 라벨에도 쓴다.</summary>
        public bool TryFindAdjacentInteraction(out GridPos target, out string label)
        {
            target = default;
            label = null;
            if (_playerState == null || _grid == null || _dungeon == null) return false;

            GridPos player = _playerPos;
            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                EnemyAgent enemy = FindLivingEnemyAt(candidate);
                if (enemy != null && (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(candidate)))
                {
                    target = candidate;
                    label = "공격";
                    return true;
                }
            }

            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                TileData tile = _grid.Map.Get(candidate);
                if (tile != null && (tile.CanOpen || tile.CanClose))
                {
                    target = candidate;
                    label = tile.CanOpen ? "문 열기" : "문 닫기";
                    return true;
                }
                if (hubMode && _hubInteractables.TryGetValue(candidate, out string hubId))
                {
                    target = candidate;
                    label = hubId == "merchant" ? "상인" : hubId == "stash" ? "창고" : "영웅";
                    return true;
                }
                if (!hubMode && !_barrelExploded && candidate == _barrelPos)
                {
                    target = candidate;
                    label = "밀기";
                    return true;
                }
            }
            return false;
        }

        /// <summary>대기(턴 스킵): 제자리에서 행동 1회를 소비하고 적 턴만 돌린다.</summary>
        public void WaitTurn()
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            InteractionFeedback?.Invoke("대기 — 주변을 살핀다");
            _moveRoutine = StartCoroutine(RunPlayerAction(ResolveEnemyPhase()));
        }

        /// <summary>게임 포기: 소지품을 전부 잃고(창고는 유지) 허브로 돌아간다.</summary>
        public void AbandonRun()
        {
            if (!Application.isPlaying || hubMode) return;
            RunSaveStore.Clear();
            Debug.Log("[Run] 게임 포기 — 소지품 소실, 허브 복귀");
            UnityEngine.SceneManagement.SceneManager.LoadScene(FrontEndFlow.HubScene);
        }

        private void HandleTileTapped(GridPos target, bool tileExists)
        {
            if (!Application.isPlaying || _playerState == null || !_playerState.IsAlive ||
                _runSummary.Ended)
                return;

            // 화면의 검은 여백(void)은 미탐색 타일이 아니다. 이 검사를 FOV/자동 이동보다
            // 먼저 해야 맵 밖 탭이 가까운 탐색 경계로 걷는 명령으로 바뀌지 않는다.
            // IsoTapInput에서도 차단하지만 키보드/테스트 등 직접 호출 경로를 위해 재검증한다.
            if (!tileExists || !WorldInputRules.IsMapTile(_grid.Map, target))
                return;

            // 자동 이동 중 재탭 = 취소 요청. 다음 스텝 경계에서 멈춘다.
            if (_resolvingAction)
            {
                _travelCancelRequested = true;
                return;
            }

            if (viewMode == DungeonViewMode.Play &&
                !_visibleTiles.Contains(target) &&
                !_exploredTiles.Contains(target) &&
                !_verticalPreviewTiles.Contains(target))
            {
                TryTravelTowardUnexplored(target);
                return;
            }

            if (_bombAiming)
            {
                HandleBombAimTap(target);
                return;
            }

            // 사다리/층 전환 타일 위에서는 자기 자신 탭이 곧 사용이다.
            // 그 외 타일에서만 액션 휠을 연다.
            if (target == _playerPos)
            {
                if (TryActivateHubPortal() || TryActivateCurrentConnector())
                    return;
                PlayerTapped?.Invoke();
                return;
            }

            // 허브: NPC/오브젝트 탭 → 옆까지 걸어가 상호작용.
            if (hubMode && _hubInteractables.TryGetValue(target, out string hubId))
            {
                if (TryFindApproach(target, out List<GridPos> hubPath))
                    StartPlayerAction(target, ApproachAndInteract(hubPath, target, hubId));
                return;
            }

            // 적 판정을 문/구멍보다 먼저: 열린 문 위에 선 적을 탭하면 공격이지
            // 문 토글이 아니다. 시야 밖(explored 기억)의 적은 이동 탭으로만 취급.
            EnemyAgent tappedEnemy = FindLivingEnemyAt(target);
            if (tappedEnemy != null &&
                (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(target)))
            {
                if (combatMode == CombatActionMode.Ranged)
                    StartPlayerAction(target, RangedAttack(tappedEnemy));
                else if (TryFindApproach(tappedEnemy.State.Position, out List<GridPos> attackPath))
                    StartPlayerAction(target, ApproachAndAttack(attackPath, tappedEnemy));
                return;
            }

            TileData targetTile = _grid.Map.Get(target);
            if (targetTile != null && targetTile.kind == TileKind.Hole)
            {
                if (TryFindApproach(target, out List<GridPos> dropPath))
                    StartPlayerAction(target, ApproachAndDrop(dropPath, target));
                return;
            }

            if (targetTile != null && (targetTile.CanOpen || targetTile.CanClose))
            {
                if (TryFindApproach(target, out List<GridPos> doorPath))
                    StartPlayerAction(target, ApproachAndToggleDoor(doorPath, target));
                return;
            }

            // 폭발통 밀기 (오브젝트 상호작용). 인접까지 접근한 뒤 민다.
            if (!_barrelExploded && target == _barrelPos &&
                _dungeon.Height.FloorIndex(target.elevation) == _activeFloorIndex)
            {
                if (TryFindApproach(_barrelPos, out List<GridPos> pushPath))
                    StartPlayerAction(target, ApproachAndPushBarrel(pushPath));
                return;
            }

            if (!_grid.Map.IsWalkable(target))
            {
                // 조용히 무시하면 "보이는 것과 갈 수 있는 곳"이 헷갈린다 — 즉시 알려준다.
                InteractionFeedback?.Invoke(
                    _grid.Map.Get(target)?.kind == TileKind.Wall
                        ? "벽이다 — 지나갈 수 없다"
                        : "갈 수 없는 곳");
                return;
            }

            // 다른 층의 탐색된 칸도 목적지로 허용 — 경로 탐색이 계단 링크를 자동 경유한다.
            List<GridPos> path = GridPathfinder.FindPath(_grid.Map, _playerPos, target);
            if (path.Count == 0)
            {
                if (_dungeon.Height.FloorIndex(target.elevation) != _activeFloorIndex)
                    InteractionFeedback?.Invoke("그 층으로 가는 길을 아직 모른다");
                return;
            }

            // 적이 시야에 있는 동안엔 탭당 1스텝만 — 카이팅/오토무브 남용 방지. (SPD 관례)
            int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), path.Count - 1);
            if (allowedSteps < path.Count - 1)
                path.RemoveRange(allowedSteps + 1, path.Count - allowedSteps - 1);

            // 사다리/층 전환 링크는 경로가 실제로 입구까지 닿을 때만 잇는다(절단되면 생략).
            // 링크를 타고 반대편 끝을 목적지로 탭한 경로에는 되돌아가는 링크를 덧붙이지 않는다.
            bool usesExplicitConnector =
                targetTile.kind == TileKind.Ladder ||
                targetTile.kind == TileKind.StairsUp ||
                targetTile.kind == TileKind.StairsDown;
            bool arrivedThroughSameLink = false;
            if (path.Count >= 2)
            {
                GridPos previous = path[path.Count - 2];
                foreach (GridPos linked in _grid.Map.LinksFrom(target))
                {
                    if (linked != previous) continue;
                    arrivedThroughSameLink = true;
                    break;
                }
            }
            if (usesExplicitConnector &&
                !arrivedThroughSameLink &&
                path[path.Count - 1] == target)
            {
                IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(target);
                if (links.Count > 0)
                {
                    path.Add(links[0]);
                }
                else if (targetTile.kind == TileKind.StairsDown &&
                         _activeFloorIndex == _dungeon.BottomFloorIndex &&
                         _stageIndex < stageCount)
                {
                    // 링크 없는 최심층 하행 계단 = 다음 던전 출구.
                    StartPlayerAction(target, MoveAndAdvanceStage(path, target));
                    return;
                }
            }

            StartPlayerAction(target, MovePlayerPath(path));
        }

        private void HandleBombAimTap(GridPos target)
        {
            // 단검은 타일이 아니라 적을 조준한다.
            if (_bombAimKind == ItemKind.ThrowingKnife)
            {
                EnemyAgent knifeTarget = FindLivingEnemyAt(target);
                if (knifeTarget == null || !_visibleTiles.Contains(target))
                {
                    InteractionFeedback?.Invoke("KNIFE: 보이는 적을 탭해라");
                    return;
                }
                StartPlayerAction(target, ThrowKnife(knifeTarget));
                return;
            }

            if (!BombRules.CanThrow(_grid.Map, _playerPos, target, bombThrowRange))
            {
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, target);
                InteractionFeedback?.Invoke(blocked
                    ? "THROW BLOCKED"
                    : $"OUT OF THROW RANGE · MAX {bombThrowRange}");
                return;
            }

            if (_bombAimKind == ItemKind.OilFlask)
            {
                StartPlayerAction(target, ThrowOil(target));
                return;
            }

            StartPlayerAction(target, ThrowBomb(target, _bombAimKind));
        }

        /// <summary>선택 마커를 옮기고 행동 코루틴을 잠금 래퍼로 시작한다. (탭 분기 공통 꼬리)</summary>
        private void StartPlayerAction(GridPos target, IEnumerator action)
        {
            PositionSelection(target);
            _moveRoutine = StartCoroutine(RunPlayerAction(action));
        }

        private bool TryFindApproach(GridPos target, out List<GridPos> path)
        {
            path = FindPathToAdjacent(target);
            return path.Count > 0;
        }

        /// <summary>
        /// 플레이어 행동 코루틴 공통 래퍼. 진행 중 입력 잠금(_resolvingAction)과
        /// 핸들 해제를 한 곳에서 처리해, 개별 행동이 잠금 해제를 잊을 수 없게 한다.
        /// </summary>
        private IEnumerator RunPlayerAction(IEnumerator action)
        {
            _resolvingAction = true;
            _travelCancelRequested = false;
            yield return action;
            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator AutoDescend()
        {
            yield return null;
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
            if (floor.DownStairs.HasValue)
                HandleTileTapped(floor.DownStairs.Value, tileExists: true);
        }

        private IEnumerator ApproachAndAttack(IReadOnlyList<GridPos> path, EnemyAgent enemy)
        {
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && enemy.State.IsAlive &&
                CombatRules.TryMelee(_playerState, enemy.State, out int damage))
            {
                yield return ShowEnemyHit(enemy, damage, "Melee");
                yield return ResolveEnemyPhase();
            }
        }

        private IEnumerator RangedAttack(EnemyAgent enemy)
        {
            if (CombatRules.TryRanged(
                    _playerState,
                    enemy.State,
                    _grid.Map,
                    rangedAttackRange,
                    out int damage,
                    rangedAttackDamage))
            {
                yield return AnimateProjectile(_playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke($"RANGED HIT · {damage} DAMAGE");
                yield return ShowEnemyHit(enemy, damage, "Ranged");
                yield return ResolveEnemyPhase();
            }
            else
            {
                // 쏠 수 없으면 사격 가능 위치까지 접근한다 (SPD식). 탭당 1스텝 규칙 유지.
                RangedBlockReason reason = CombatRules.DiagnoseRanged(
                    _grid.Map, _playerPos, enemy.State.Position, rangedAttackRange);
                if (!CombatRules.FindFiringPosition(
                        _grid.Map, _playerPos, enemy.State.Position, rangedAttackRange,
                        out List<GridPos> firingPath,
                        pos => pos != _playerPos &&
                               (IsLivingEnemyAt(pos) || _grid.Map.Get(pos)?.kind == TileKind.WeakFloor)))
                {
                    InteractionFeedback?.Invoke("사선을 잡을 위치가 없다");
                    yield break;
                }

                InteractionFeedback?.Invoke(reason switch
                {
                    RangedBlockReason.ElevationMismatch => "높이가 다르다 — 계단으로 접근한다",
                    RangedBlockReason.Blocked => "사선이 막혔다 — 접근한다",
                    _ => $"사거리 밖(MAX {rangedAttackRange}) — 접근한다"
                });

                int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), firingPath.Count - 1);
                if (allowedSteps < firingPath.Count - 1)
                    firingPath.RemoveRange(allowedSteps + 1, firingPath.Count - allowedSteps - 1);
                yield return MovePlayerPath(firingPath);

                // 접근이 끝난 그 탭에서 조건이 갖춰졌으면 즉시 발사.
                if (_playerState.IsAlive && enemy.State.IsAlive &&
                    CombatRules.TryRanged(
                        _playerState, enemy.State, _grid.Map, rangedAttackRange,
                        out int approachDamage, rangedAttackDamage))
                {
                    yield return AnimateProjectile(_playerPos, enemy.State.Position);
                    InteractionFeedback?.Invoke($"RANGED HIT · {approachDamage} DAMAGE");
                    yield return ShowEnemyHit(enemy, approachDamage, "Ranged");
                    yield return ResolveEnemyPhase();
                }
            }
        }

        private IEnumerator DrinkPotion()
        {
            _inventory.TryUse(ItemKind.Potion);
            InventoryChanged?.Invoke();

            int healed = _playerState.Heal(potionHealAmount);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            FloatingText?.ShowDamage(_player.transform.position, healed, FloatingTextKind.Heal);
            InteractionFeedback?.Invoke($"POTION +{healed} HP");
            Debug.Log($"[Item] 물약 사용: +{healed} HP → {_playerState.Hp}/{_playerState.MaxHp}");
            yield return FlashColor(_playerRenderer, new Color32(96, 224, 128, 255));

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowOil(GridPos target)
        {
            SetBombAiming(false);
            _inventory.TryUse(ItemKind.OilFlask);
            InventoryChanged?.Invoke();

            yield return AnimateProjectile(_playerPos, target);
            List<GridPos> splashed = OilRules.Splash(_grid.Map, target);
            InteractionFeedback?.Invoke($"OIL SPLASHED ×{splashed.Count} — 불이 닿으면 발화한다");
            Debug.Log($"[Item] 기름 살포 {target}: {splashed.Count}칸");
            RefreshFloorVisibility();

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowKnife(EnemyAgent enemy)
        {
            SetBombAiming(false);
            _inventory.TryUse(ItemKind.ThrowingKnife);
            InventoryChanged?.Invoke();

            if (CombatRules.TryRanged(
                    _playerState, enemy.State, _grid.Map, rangedAttackRange,
                    out int damage, knifeDamage))
            {
                yield return AnimateProjectile(_playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke($"KNIFE HIT · {damage} DAMAGE");
                yield return ShowEnemyHit(enemy, damage, "Knife");
            }
            else
            {
                // 소모는 이미 됐다 — 빗나간 투척도 손해라는 감각 유지.
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke(blocked ? "KNIFE BLOCKED" : $"OUT OF RANGE · MAX {rangedAttackRange}");
            }

            yield return ResolveEnemyPhase();
        }

        /// <summary>조합 실행 (인벤토리 화면이 호출). 행동 1회를 소비한다.</summary>
        public void CraftRecipe(int recipeIndex)
        {
            if (!Application.isPlaying || hubMode || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (recipeIndex < 0 || recipeIndex >= CraftingRules.Recipes.Length) return;

            Recipe recipe = CraftingRules.Recipes[recipeIndex];
            if (!CraftingRules.CanCraft(_inventory, recipe))
            {
                InteractionFeedback?.Invoke("재료가 모자라다");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(CraftAction(recipe)));
        }

        private IEnumerator CraftAction(Recipe recipe)
        {
            CraftingRules.TryCraft(_inventory, recipe);
            InventoryChanged?.Invoke();
            InteractionFeedback?.Invoke(
                $"조합: {ItemCatalog.DisplayName(recipe.Output)} 완성!");
            Debug.Log($"[Craft] {recipe} 조합 완료");
            yield return FlashColor(_playerRenderer, new Color32(196, 150, 90, 255));

            yield return ResolveEnemyPhase();
        }

        /// <summary>귀환 두루마리: 현재 층 입구로 순간이동. 행동 1회 소비.</summary>
        public void UseRecallScroll()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (_inventory.Count(ItemKind.RecallScroll) <= 0)
            {
                InteractionFeedback?.Invoke("NO SCROLLS");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(RecallToEntry()));
        }

        private IEnumerator RecallToEntry()
        {
            _inventory.TryUse(ItemKind.RecallScroll);
            InventoryChanged?.Invoke();

            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
            GridPos destination = floor.Entry;
            if (IsLivingEnemyAt(destination))
            {
                // 입구가 막혔으면 걷기 가능한 인접 칸으로 비껴 착지한다.
                foreach (GridPos candidate in new[]
                         { destination.North, destination.East, destination.South, destination.West })
                {
                    if (_grid.Map.IsWalkable(candidate) && !IsLivingEnemyAt(candidate))
                    {
                        destination = candidate;
                        break;
                    }
                }
            }

            InteractionFeedback?.Invoke("RECALL — 층 입구로 귀환");
            Debug.Log($"[Item] 귀환 두루마리: {_playerPos} → {destination}");
            yield return AnimateFloorTransition(_grid.GridToWorld(destination));
            _playerState.MoveTo(destination);
            SyncPlayerView(destination, floorChanged: false);

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowBomb(GridPos target, ItemKind kind)
        {
            SetBombAiming(false);
            _inventory.TryUse(kind);
            InventoryChanged?.Invoke();

            bool fiery = kind != ItemKind.FrostBomb;
            yield return AnimateProjectile(_playerPos, target);
            yield return ResolveExplosion(target, fiery ? bombDamage : frostBombDamage, fiery);

            if (_playerState.IsAlive)
                yield return ResolveEnemyPhase();
        }

        private IEnumerator AnimateBlast(GridPos center, bool fiery = true)
        {
            var blast = new GameObject("Bomb Blast");
            blast.transform.SetParent(_visualRoot, false);
            blast.transform.position = _grid.GridToWorld(center) + Vector3.up * 0.18f;
            var renderer = blast.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBlastSprite(fiery);
            renderer.sortingOrder = 31001;

            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.5f, 2.6f, SmoothStep(t));
                blast.transform.localScale = new Vector3(scale, scale, 1f);
                Color color = renderer.color;
                color.a = 1f - t * t;
                renderer.color = color;
                yield return null;
            }

            Destroy(blast);
        }

        private IEnumerator AnimateProjectile(GridPos from, GridPos to)
        {
            var projectile = new GameObject("Ranged Projectile");
            projectile.transform.SetParent(_visualRoot, false);
            var renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = GetProjectileSprite();
            renderer.sortingOrder = 31000;

            Vector3 start = _grid.GridToWorld(from) + Vector3.up * 0.42f;
            Vector3 end = _grid.GridToWorld(to) + Vector3.up * 0.42f;
            float elapsed = 0f;
            const float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                projectile.transform.position = Vector3.Lerp(start, end, t) +
                                                Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.24f);
                yield return null;
            }

            Destroy(projectile);
        }

        private IEnumerator ApproachAndToggleDoor(IReadOnlyList<GridPos> path, GridPos door)
        {
            yield return MovePlayerPath(path);

            TileData tile = _grid.Map.Get(door);
            if (IsPlayerAdjacentTo(door) && tile != null && (tile.CanOpen || tile.CanClose))
            {
                TileKind nextKind = tile.CanOpen ? TileKind.DoorOpen : TileKind.DoorClosed;
                if (nextKind == TileKind.DoorClosed && IsLivingEnemyAt(door))
                {
                    InteractionFeedback?.Invoke("무언가 문을 막고 있다!");
                    yield break;
                }
                yield return SetDoorState(door, nextKind);
                RefreshFloorVisibility();
                string feedback = nextKind == TileKind.DoorOpen ? "DOOR OPENED" : "DOOR CLOSED";
                InteractionFeedback?.Invoke(feedback);
                Debug.Log($"[Door] {door} {feedback}");
                yield return ResolveEnemyPhase();
            }
        }

        private IEnumerator ApproachAndDrop(IReadOnlyList<GridPos> path, GridPos hole)
        {
            yield return MovePlayerPath(path);

            // 의도적 낙하도 TryFall 하나로 수렴 — 낙뎀을 감수하는 하강 수단이다. (GDD §5.3)
            GridPos? landing = _grid.Map.FindLandingBelow(hole, BottomElevation);
            if (_playerState.IsAlive && landing.HasValue && IsPlayerAdjacentTo(hole))
            {
                yield return FallPlayer(hole, "DROP");
                if (_playerState.IsAlive)
                    yield return ResolveEnemyPhase();
            }
        }

        private IEnumerator AnimateHoleDrop(GridPos hole, GridPos landing)
        {
            Vector3 start = _player.transform.position;
            Vector3 holeWorld = _grid.GridToWorld(hole);
            Vector3 landingWorld = _grid.GridToWorld(landing);
            Color original = _playerRenderer.color;

            float elapsed = 0f;
            const float hopDuration = 0.14f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hopDuration);
                _player.transform.position = Vector3.Lerp(start, holeWorld, t) +
                                             Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.18f;
                yield return null;
            }

            elapsed = 0f;
            const float fallDuration = 0.34f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                _player.transform.position = Vector3.Lerp(holeWorld, landingWorld, SmoothStep(t));
                _player.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, Mathf.Sin(t * Mathf.PI));
                _playerRenderer.color = new Color(original.r, original.g, original.b, Mathf.Lerp(1f, 0.35f, Mathf.Sin(t * Mathf.PI)));
                yield return null;
            }

            _player.transform.position = landingWorld;
            _player.transform.localScale = Vector3.one;
            _playerRenderer.color = original;
        }

        private IEnumerator AnimateDoorTransition(SpriteRenderer renderer, GridPos door, TileKind nextKind)
        {
            float duration = 0.11f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.08f, t), 1f, 1f);
                yield return null;
            }

            _grid.Map.Set(door, nextKind);
            renderer.sprite = GetTileSprite(nextKind, door);
            renderer.sortingOrder = _grid.iso.SortingOrder(door, TileSortOffset(nextKind));

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.transform.localScale = new Vector3(Mathf.Lerp(0.08f, 1f, t), 1f, 1f);
                yield return null;
            }
            renderer.transform.localScale = Vector3.one;
            yield return AnimateDoorInteractionFx(door, nextKind == TileKind.DoorOpen);
        }

        private IEnumerator AnimateDoorInteractionFx(GridPos door, bool opening)
        {
            var effect = new GameObject(opening ? "Door Open Burst" : "Door Close Burst");
            effect.transform.SetParent(_visualRoot, false);
            effect.transform.position = _grid.GridToWorld(door) + Vector3.up * 0.42f;
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDoorInteractionSprite(opening);
            renderer.sortingOrder = 30003;

            float elapsed = 0f;
            const float duration = 0.16f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.45f, 1.25f, SmoothStep(t));
                effect.transform.localScale = new Vector3(scale, scale, 1f);
                Color color = renderer.color;
                color.a = 1f - t;
                renderer.color = color;
                yield return null;
            }

            Destroy(effect);
        }

        private IEnumerator MovePlayerPath(IReadOnlyList<GridPos> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                GridPos next = path[i];
                if (IsLivingEnemyAt(next))
                    yield break;

                // 스텝 전 스냅샷 — 이 스텝(내 이동+적 턴)으로 "새로" 보이게 된 것만 인터럽트한다.
                SnapshotTravelSight();
                int hpBeforeStep = _playerState.Hp;

                Vector3 start = _player.transform.position;
                Vector3 end = _grid.GridToWorld(next);
                ApplyPlayerVisualSorting(next);

                bool changesDungeonFloor = !_dungeon.Height.SameFloor(_playerState.Position, next);
                if (changesDungeonFloor)
                {
                    yield return AnimateFloorTransition(end);
                }
                else
                {
                    float elapsed = 0f;
                    while (elapsed < secondsPerStep)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / secondsPerStep);
                        _player.transform.position = Vector3.LerpUnclamped(start, end, SmoothStep(t));
                        yield return null;
                    }
                }

                _player.transform.position = end;

                // 던전 층 전환 계단은 "입구 칸에 서기"와 "링크 이동"을 한 행동으로 묶는다.
                // 둘 사이에 적 턴/자동 이동 인터럽트가 끼면 계단 위에 남아 층이 안 바뀐 것처럼
                // 보이므로, 같은 층에서 입구를 밟은 즉시 반대편 출구까지 이동한다.
                bool enteredFromSameFloor =
                    _dungeon.Height.SameFloor(_playerState.Position, next);
                if (enteredFromSameFloor &&
                    VerticalTraversalRules.TryGetAutomaticFloorDestination(
                        _grid.Map, next, out GridPos floorDestination))
                {
                    InteractionFeedback?.Invoke(
                        $"{FloorLabel(_dungeon.Height.FloorIndex(floorDestination.elevation))}로 이동");
                    yield return AnimateFloorTransition(_grid.GridToWorld(floorDestination));

                    if (i + 1 < path.Count && path[i + 1] == floorDestination)
                        i++; // 경로 탐색이 넣은 링크 목적지는 이미 함께 소비했다.
                    next = floorDestination;
                    end = _grid.GridToWorld(next);
                }

                _playerPos = next;
                _playerState.MoveTo(next);
                PlayerPositionChanged?.Invoke();
                _playerSorting.x = next.x;
                _playerSorting.y = next.y;
                _playerSorting.elevation = next.elevation;
                _player.transform.position = end;
                ConfigureCamera(Camera.main);
                TryCollectItemAt(next);

                // 허브 포탈은 밟는 순간 목적지 선택을 연다. 던전 진입은 확인 버튼에서만 확정한다.
                if (hubMode && next == HubLayout.Portal)
                {
                    TryActivateHubPortal();
                    yield break;
                }

                // 약한 바닥은 밟는 순간 무너진다 — 낙하로 경로가 무효화된다. (GDD §5.3)
                if (_grid.Map.Get(next)?.kind == TileKind.WeakFloor)
                {
                    yield return CollapseUnderPlayer(next);
                    if (_playerState.IsAlive)
                        yield return ResolveEnemyPhase();
                    yield break;
                }

                int nextFloor = _dungeon.Height.FloorIndex(next.elevation);
                if (nextFloor != _activeFloorIndex)
                {
                    _activeFloorIndex = nextFloor;
                    UpdateInputFloorRange();
                    RefreshFloorVisibility();
                    PositionSelection(next);
                    ActiveFloorChanged?.Invoke(_activeFloorIndex);
                    Debug.Log($"[Dungeon] 층 이동: {FloorLabel(_activeFloorIndex)} / " +
                              $"층 내부 높이 {_dungeon.Height.LocalHeight(next.elevation)}");
                    _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
                    TryDeclareVictory();
                    if (_runSummary.Ended) yield break;
                    SaveCheckpoint();
                }
                else
                {
                    RefreshFloorVisibility();
                }

                yield return ResolveEnemyPhase();
                if (!_playerState.IsAlive)
                    yield break;

                if (i >= path.Count - 1) continue; // 마지막 스텝 뒤엔 멈출 이동이 없다

                if (_travelCancelRequested)
                {
                    InteractionFeedback?.Invoke("MOVE CANCELED");
                    yield break;
                }

                TravelInterrupt interrupt = TravelRules.Evaluate(
                    _travelVisibleEnemyIds,
                    EnemySightStates(),
                    AnyNewVisibleItem(),
                    _playerState.Hp < hpBeforeStep);
                if (interrupt != TravelInterrupt.None)
                {
                    FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
                    InteractionFeedback?.Invoke(interrupt switch
                    {
                        TravelInterrupt.PlayerDamaged => "INTERRUPTED — 피해를 입어 멈췄다",
                        TravelInterrupt.EnemySighted => "ENEMY SIGHTED — 적 발견!",
                        _ => "ITEM SIGHTED — 무언가 보인다"
                    });
                    yield break;
                }
            }
        }

        /// <summary>스텝 시작 전 시야 스냅샷: 보이는 살아있는 적 ID + 보이는 미수집 아이템 칸.</summary>
        private void SnapshotTravelSight()
        {
            _travelVisibleEnemyIds.Clear();
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive && _visibleTiles.Contains(enemy.State.Position))
                    _travelVisibleEnemyIds.Add(enemy.State.Id);
            }

            _travelVisibleItemTiles.Clear();
            foreach (ItemAgent item in _items)
            {
                if (!item.Collected && _visibleTiles.Contains(item.Spawn.Position))
                    _travelVisibleItemTiles.Add(item.Spawn.Position);
            }
        }

        private IEnumerable<(string, bool, bool)> EnemySightStates()
        {
            foreach (EnemyAgent enemy in _enemies)
                yield return (
                    enemy.State.Id,
                    _visibleTiles.Contains(enemy.State.Position),
                    enemy.State.IsAlive);
        }

        private bool AnyEnemyVisible()
        {
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive && _visibleTiles.Contains(enemy.State.Position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 미탐색 칸 탭: 아는(탐색된) 타일 중 목표에 평면 거리로 가장 가까운 칸까지
        /// 아는 타일만 밟아 이동한다. (SPD의 미탐색 탭 관례)
        /// </summary>
        private void TryTravelTowardUnexplored(GridPos target)
        {
            static int PlanarDistance(GridPos a, GridPos b) =>
                Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

            var candidates = new List<GridPos>();
            foreach (GridPos pos in _exploredTiles)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) continue;
                if (!_grid.Map.IsWalkable(pos) || pos == _playerPos) continue;
                if (IsLivingEnemyAt(pos)) continue;
                if (PlanarDistance(pos, target) >= PlanarDistance(_playerPos, target)) continue;
                candidates.Add(pos);
            }

            if (candidates.Count == 0)
            {
                InteractionFeedback?.Invoke("UNEXPLORED — 아는 길이 없다");
                return;
            }

            candidates.Sort((a, b) =>
            {
                int byTarget = PlanarDistance(a, target).CompareTo(PlanarDistance(b, target));
                return byTarget != 0
                    ? byTarget
                    : PlanarDistance(a, _playerPos).CompareTo(PlanarDistance(b, _playerPos));
            });

            // 최상위 후보 몇 개만 경로 검증 — 후보 전수 탐색은 탭마다 너무 비싸다.
            bool Unknown(GridPos pos) => !_exploredTiles.Contains(pos) && !_visibleTiles.Contains(pos);
            int attempts = Mathf.Min(8, candidates.Count);
            for (int i = 0; i < attempts; i++)
            {
                List<GridPos> path = GridPathfinder.FindPath(
                    _grid.Map, _playerPos, candidates[i], pos => Unknown(pos));
                if (path.Count < 2) continue;

                int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), path.Count - 1);
                if (allowedSteps < path.Count - 1)
                    path.RemoveRange(allowedSteps + 1, path.Count - allowedSteps - 1);
                InteractionFeedback?.Invoke("미탐색 방향으로 이동...");
                StartPlayerAction(candidates[i], MovePlayerPath(path));
                return;
            }

            InteractionFeedback?.Invoke("UNEXPLORED — 아는 길이 없다");
        }

        private bool AnyNewVisibleItem()
        {
            foreach (ItemAgent item in _items)
            {
                if (!item.Collected &&
                    _visibleTiles.Contains(item.Spawn.Position) &&
                    !_travelVisibleItemTiles.Contains(item.Spawn.Position))
                    return true;
            }
            return false;
        }

        private IEnumerator AnimateFloorTransition(Vector3 destination)
        {
            Color original = _playerRenderer.color;
            _playerRenderer.color = new Color(original.r, original.g, original.b, 0.2f);
            yield return new WaitForSeconds(0.12f);
            _player.transform.position = destination;
            _playerRenderer.color = original;
            yield return new WaitForSeconds(0.12f);
        }

        private IEnumerator FlashDamage(SpriteRenderer renderer) =>
            FlashColor(renderer, new Color32(255, 92, 72, 255));

        /// <summary>
        /// 적 피격 공통 처리. 전투 결과와 로그는 항상 반영하되,
        /// 플로팅 피해·사망 안내·플래시는 현재 FOV 안에서만 공개한다.
        /// </summary>
        private IEnumerator ShowEnemyHit(EnemyAgent enemy, int damage, string source)
        {
            UpdateHealthBar(enemy.HpFill, enemy.State);
            bool visibleToPlayer = IsEnemyVisibleToPlayer(enemy);
            if (visibleToPlayer)
            {
                FloatingText?.ShowDamage(
                    enemy.Root != null
                        ? enemy.Root.transform.position
                        : _grid.GridToWorld(enemy.State.Position),
                    damage,
                    FloatingTextKind.EnemyDamage);
            }
            Debug.Log($"[{source}] {enemy.State.Id}에게 {damage} 피해. " +
                      $"HP {enemy.State.Hp}/{enemy.State.MaxHp}");

            if (visibleToPlayer && enemy.Renderer != null)
                yield return FlashDamage(enemy.Renderer);

            bool newlyDead = !enemy.State.IsAlive && enemy.DeathTurn < 0;
            if (newlyDead)
            {
                enemy.DeathTurn = _turns.TurnNumber;
                _runSummary.RecordKill();
                Debug.Log($"[Combat] {enemy.State.Id} 처치");
                if (visibleToPlayer)
                    InteractionFeedback?.Invoke("ENEMY DEFEATED");
            }

            ApplyEnemyVisuals(enemy);
        }

        /// <summary>플레이어 피격 공통 연출. 사망 시 붉은 처리와 재시작 안내.</summary>
        private IEnumerator ShowPlayerHit(int damage, string source)
        {
            // 무적(디버그): 이미 깎인 피해를 되돌린다 — 모든 플레이어 피해가 이 경로를 지난다.
            if (_godMode && damage > 0)
            {
                _playerState.OverrideHpForDebug(_playerState.Hp + damage);
                UpdateHealthBar(_playerHpFill, _playerState);
                PlayerHpChanged?.Invoke();
                InteractionFeedback?.Invoke($"CHEAT: GOD — {source} 피해 {damage} 무시");
                yield break;
            }

            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            FloatingText?.ShowDamage(
                _player.transform.position, damage, FloatingTextKind.PlayerDamage);
            Debug.Log($"[{source}] 플레이어가 {damage} 피해. " +
                      $"HP {_playerState.Hp}/{_playerState.MaxHp}");
            yield return FlashDamage(_playerRenderer);

            if (!_playerState.IsAlive)
            {
                _playerRenderer.color = new Color32(120, 42, 42, 220);
                _runSummary.EndInDefeat(source);
                RunSaveStore.Clear();
                Debug.Log($"[Combat] 플레이어 사망 — 사인 {source}, " +
                          $"최심층 {FloorLabel(_runSummary.DeepestFloorIndex)}");
                RunEnded?.Invoke(_runSummary);
            }
        }

        // ── 허브 캠프 ─────────────────────────────────────────

        /// <summary>캠프 프롭 생성: 상인/영웅 3명/창고/포탈/모닥불. 탭 상호작용 좌표도 등록한다.</summary>
        private void CreateHubProps()
        {
            _hubInteractables.Clear();
            _hubHeroProps.Clear();
            _hubHeroPositions.Clear();

            Sprite campfire = visualCatalog != null ? visualCatalog.hubCampfire : null;
            Sprite portal = visualCatalog != null ? visualCatalog.hubPortal : null;
            Sprite merchantSprite = visualCatalog != null ? visualCatalog.merchant : null;
            Sprite stash = visualCatalog != null ? visualCatalog.hubStash : null;

            CreateHubProp("Campfire", campfire != null ? campfire : GetHubPropSprite("campfire"), HubLayout.Campfire);
            CreateHubProp("Portal", portal != null ? portal : GetHubPropSprite("portal"), HubLayout.Portal);

            CreateHubProp(
                "Merchant",
                merchantSprite != null ? merchantSprite : GetCharacterSprite(true),
                HubLayout.Merchant);
            _hubInteractables[HubLayout.Merchant] = "merchant";

            CreateHubProp("Stash", stash != null ? stash : GetHubPropSprite("stash"), HubLayout.Stash);
            _hubInteractables[HubLayout.Stash] = "stash";

            for (int i = 0; i < HeroRoster.All.Count && i < HubLayout.HeroPositions.Count; i++)
            {
                HeroArchetype hero = HeroRoster.All[i];
                Sprite heroSprite = visualCatalog != null ? visualCatalog.HeroFor(hero.Id) : null;
                var prop = CreateHubProp(
                    $"Hero {hero.Id}",
                    heroSprite != null ? heroSprite : GetCharacterSprite(false),
                    HubLayout.HeroPositions[i]);
                _hubHeroProps[hero.Id] = prop;
                _hubHeroPositions[hero.Id] = HubLayout.HeroPositions[i];
            }

            RefreshHubHeroLocks();
        }

        private SpriteRenderer CreateHubProp(string objectName, Sprite sprite, GridPos pos)
        {
            CreateStandingSprite(objectName, sprite, pos, out SpriteRenderer renderer);
            return renderer;
        }

        /// <summary>
        /// 선택 영웅은 플레이어로 표시하고 대기 위치에서는 숨긴다.
        /// 나머지 영웅은 자기 위치에 복귀하며, 잠긴 영웅만 회색으로 표시한다.
        /// </summary>
        public void RefreshHubHeroLocks()
        {
            if (!hubMode) return;
            MetaSaveData meta = MetaStore.LoadOrNew();
            HeroArchetype selectedHero = HeroRoster.ById(HeroSelection.SelectedId);

            _hero = selectedHero;
            playerMaxHp = selectedHero.MaxHp;
            playerAttack = selectedHero.Attack;
            rangedAttackDamage = selectedHero.RangedDamage;

            if (_playerRenderer != null)
            {
                Sprite selectedSprite = visualCatalog != null
                    ? visualCatalog.HeroFor(selectedHero.Id)
                    : null;
                _playerRenderer.sprite = selectedSprite != null
                    ? selectedSprite
                    : GetCharacterSprite(false);
                _playerRenderer.color = Color.white;
            }

            if (_playerState != null)
            {
                _playerState = new CombatantState(
                    "Player", _playerState.Position, selectedHero.MaxHp, selectedHero.Attack);
                UpdateHealthBar(_playerHpFill, _playerState);
                PlayerHpChanged?.Invoke();
            }

            foreach (KeyValuePair<string, SpriteRenderer> pair in _hubHeroProps)
            {
                HeroArchetype hero = HeroRoster.ById(pair.Key);
                bool unlocked = hero.UnlockCost <= 0 || meta.IsHeroUnlocked(hero.Id);
                bool showAtRosterPosition = HubLayout.ShouldShowHeroAtRosterPosition(
                    hero.Id, selectedHero.Id);

                pair.Value.gameObject.SetActive(showAtRosterPosition);
                pair.Value.color = !unlocked
                    ? (Color)new Color32(96, 100, 104, 255)
                    : Color.white;

                if (!_hubHeroPositions.TryGetValue(hero.Id, out GridPos position))
                    continue;
                if (showAtRosterPosition)
                    _hubInteractables[position] = $"hero:{hero.Id}";
                else
                    _hubInteractables.Remove(position);
            }
        }

        /// <summary>NPC/오브젝트 옆까지 걸어간 뒤 상호작용 이벤트를 쏜다. (허브 전용)</summary>
        private IEnumerator ApproachAndInteract(IReadOnlyList<GridPos> path, GridPos target, string id)
        {
            yield return MovePlayerPath(path);
            if (_playerState.IsAlive && IsPlayerAdjacentTo(target))
                HubInteractionRequested?.Invoke(id);
        }

        /// <summary>던전 선택 확인 — 허브에서 새 판을 시작한다.</summary>
        public void BeginSelectedDungeon()
        {
            if (!Application.isPlaying || !hubMode) return;
            RunSaveStore.Clear();
            RunSaveStore.ContinueRequested = false;
            InteractionFeedback?.Invoke($"{DungeonSelection.Selected.DisplayName}(으)로 출발");
            UnityEngine.SceneManagement.SceneManager.LoadScene(FrontEndFlow.DungeonScene);
        }

        /// <summary>던전 체인 좌표계: 스테이지 누적 깊이(몬스터 혼합용, 0부터 증가).</summary>
        private int GlobalDepth(int floorIndex) => (_stageIndex - 1) * floorCount - floorIndex;

        /// <summary>스테이지 누적 층 인덱스(기록/표시용, 아래로 갈수록 음수).</summary>
        private int GlobalFloorIndex(int floorIndex) => floorIndex - (_stageIndex - 1) * floorCount;

        /// <summary>
        /// 마지막 던전의 최심층에 살아서 도달했으면 승리. (GDD: 한 판 목표 = 최심층 도달)
        /// 앞 던전에서는 출구 계단 안내만 한다 — 다음 던전 진입은 출구 탭으로.
        /// </summary>
        private void TryDeclareVictory()
        {
            if (hubMode || _runSummary.Ended || _playerState == null || !_playerState.IsAlive) return;
            if (_activeFloorIndex != _dungeon.BottomFloorIndex) return;

            if (_stageIndex < stageCount)
            {
                InteractionFeedback?.Invoke($"{StageLabel} 최심층 — 출구 계단(▼)을 찾아라");
                return;
            }

            int victoryGold = BankInventoryToStash();
            _runSummary.EndInVictory(victoryGold);
            RunSaveStore.Clear();
            InteractionFeedback?.Invoke("DEEPEST FLOOR REACHED!");
            Debug.Log($"[Run] 최심층 {FloorLabel(GlobalFloorIndex(_activeFloorIndex))} 도달 — 승리");
            RunEnded?.Invoke(_runSummary);
        }

        /// <summary>출구 계단까지 걸어간 뒤 "다음 던전 vs 생환" 선택지를 띄운다.</summary>
        private IEnumerator MoveAndAdvanceStage(IReadOnlyList<GridPos> path, GridPos exit)
        {
            yield return MovePlayerPath(path);
            if (_playerState.IsAlive && _playerPos == exit)
                ExitChoiceRequested?.Invoke();
        }

        /// <summary>출구 선택지 — 다음 던전으로. (HUD 버튼이 호출)</summary>
        public void ConfirmAdvanceStage()
        {
            if (!Application.isPlaying || _resolvingAction || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;
            AdvanceToNextStage();
        }

        /// <summary>출구 선택지 — 생환. 전리품 환산 + 소모품 창고 보관 후 판 종료.</summary>
        public void ExtractRun()
        {
            if (!Application.isPlaying || _resolvingAction || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;

            int gold = BankInventoryToStash();
            RunSaveStore.Clear();
            _runSummary.EndInExtraction(gold);
            InteractionFeedback?.Invoke($"생환 — +{ItemCatalog.FormatGold(gold)} 적립");
            Debug.Log(
                $"[Run] 생환: +{ItemCatalog.FormatGold(gold)}, " +
                $"최심층 {FloorLabel(_runSummary.DeepestFloorIndex)}");
            RunEnded?.Invoke(_runSummary);
        }

        /// <summary>
        /// 정산: 전리품은 골드로 환산, 소모품은 창고에 보관한다.
        /// 살아 나갈 때(생환/승리)만 불린다 — 사망은 전부 소실. (extraction 규칙)
        /// </summary>
        private int BankInventoryToStash()
        {
            MetaSaveData meta = MetaStore.LoadOrNew();
            int gold = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = _inventory.Count(kind);
                if (count <= 0) continue;
                if (ItemCatalog.IsTreasure(kind)) gold += ItemCatalog.GoldValue(kind) * count;
                else meta.AddCount(kind, count);
            }
            meta.gold += gold;
            MetaStore.Save(meta);
            _inventory.Clear();
            InventoryChanged?.Invoke();
            return gold;
        }

        /// <summary>
        /// 다음 던전 진입: HP·인벤토리·전적을 들고 새 seed 던전을 생성한다.
        /// 던전 간 이동은 층 이동과 달리 씬 상태를 전부 리빌드한다.
        /// </summary>
        private void AdvanceToNextStage()
        {
            // 모닥불: 던전 사이에서 잃은 HP의 절반을 회복한다.
            // (밸런스 시뮬: 회복 0%면 완주 18%, 50%면 49% — 체인 소모전 보정)
            int restedHp = Mathf.Min(
                _playerState.MaxHp,
                _playerState.Hp + Mathf.CeilToInt((_playerState.MaxHp - _playerState.Hp) * 0.5f));

            var carry = new RunSaveData
            {
                heroId = _hero != null ? _hero.Id : null,
                hp = restedHp,
                potions = PotionCount,
                bombs = BombCount,
                frostBombs = FrostBombCount,
                oilFlasks = ItemCount(ItemKind.OilFlask),
                knives = ItemCount(ItemKind.ThrowingKnife),
                scrolls = ItemCount(ItemKind.RecallScroll),
                coinPouches = ItemCount(ItemKind.CoinPouch),
                gemstones = ItemCount(ItemKind.Gemstone),
                relics = ItemCount(ItemKind.Relic),
                herbs = ItemCount(ItemKind.Herb),
                powders = ItemCount(ItemKind.BlastPowder),
                frostShards = ItemCount(ItemKind.FrostShard),
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex
            };

            _stageIndex++;
            dungeonSeed = dungeonSeed * 31 + 7; // 결정론적 체인 — 같은 시작 seed 면 같은 여정
            previewStartDepth = 0;
            Debug.Log($"[Stage] {StageLabel} 진입 (seed {dungeonSeed})");

            BuildPrototype();
            ApplyCarriedState(carry, $"{StageLabel} 진입 — 모닥불에서 상처를 돌봤다");
            SaveCheckpoint();
        }

        /// <summary>문 상태 전환 공통 경로: 렌더러가 있으면 연출과 함께, 없으면 데이터만.</summary>
        private IEnumerator SetDoorState(GridPos door, TileKind nextKind)
        {
            if (_tileRenderers.TryGetValue(door, out SpriteRenderer renderer))
                yield return AnimateDoorTransition(renderer, door, nextKind);
            else
                _grid.Map.Set(door, nextKind);
        }

        private IEnumerator FlashColor(SpriteRenderer renderer, Color32 flash)
        {
            if (renderer == null) yield break;

            Color original = renderer.color;
            renderer.color = flash;
            yield return new WaitForSeconds(0.08f);
            if (renderer != null)
                renderer.color = original;
        }

        private List<GridPos> FindPathToAdjacent(GridPos target)
        {
            var candidates = new[] { target.North, target.East, target.South, target.West };
            List<GridPos> best = null;

            foreach (GridPos candidate in candidates)
            {
                if (candidate.elevation != target.elevation || !_grid.Map.IsWalkable(candidate))
                    continue;
                if (IsLivingEnemyAt(candidate))
                    continue;

                List<GridPos> path = GridPathfinder.FindPath(_grid.Map, _playerPos, candidate);
                if (path.Exists(step => IsLivingEnemyAt(step)))
                    continue;
                if (path.Count > 0 && (best == null || path.Count < best.Count))
                    best = path;
            }

            return best ?? new List<GridPos>();
        }

        private Transform CreateHealthBar(GameObject owner, string objectName)
        {
            var background = new GameObject($"{objectName} Background");
            background.transform.SetParent(owner.transform, false);
            background.transform.localPosition = new Vector3(-0.25f, 0.82f, 0f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetHealthBarSprite(false);
            backgroundRenderer.sortingOrder = 30000;

            var fill = new GameObject($"{objectName} Fill");
            fill.transform.SetParent(owner.transform, false);
            fill.transform.localPosition = new Vector3(-0.25f, 0.82f, 0f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = GetHealthBarSprite(true);
            fillRenderer.sortingOrder = 30001;
            return fill.transform;
        }

        private static void UpdateHealthBar(Transform fill, CombatantState state)
        {
            if (fill == null || state == null) return;
            float ratio = state.Hp / (float)state.MaxHp;
            fill.localScale = new Vector3(ratio, 1f, 1f);
        }

        private void UpdateInputFloorRange()
        {
            if (_input == null || _dungeon == null) return;
            // 키보드/편집기 폴백을 위한 범위다. 실제 포인터 입력은 TilePicker가
            // 현재 화면에 렌더된 다이아몬드를 먼저 판정해 겹친 아래층 오선택을 막는다.
            _input.minElevation = _dungeon.Height.Elevation(_dungeon.BottomFloorIndex);
            _input.maxElevation =
                _dungeon.Height.Elevation(_dungeon.TopFloorIndex) +
                _dungeon.Height.ElevationsPerFloor - 1;
            _input.targetElevation = _dungeon.Height.Elevation(_activeFloorIndex);
        }

        public static string FloorLabel(int floorIndex) =>
            floorIndex <= 0 ? $"B{1 - floorIndex}" : $"F{floorIndex + 1}";

        private void PositionSelection(GridPos pos)
        {
            _selectionPos = pos;
            _selection.transform.position = _grid.GridToWorld(pos);
            _selection.GetComponent<SpriteRenderer>().sortingOrder = _grid.iso.SortingOrder(pos, -1);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;

            _configuredCamera = camera;
            camera.orthographic = true;
            if (hubMode && TryGetHubCameraFrame(camera.aspect, out OrthographicCameraFrame hubFrame))
            {
                camera.orthographicSize = hubFrame.Size;
                camera.transform.position = new Vector3(hubFrame.Center.x, hubFrame.Center.y, -10f);
            }
            else
            {
                camera.orthographicSize = viewMode == DungeonViewMode.DebugAll
                    ? debugCameraSize
                    : playCameraSize;
                if (viewMode == DungeonViewMode.Play && _playerState != null)
                {
                    Vector3 playerWorld = _grid.GridToWorld(_playerState.Position);
                    camera.transform.position = new Vector3(playerWorld.x, playerWorld.y, -10f);
                }
                else
                {
                    camera.transform.position = new Vector3(0f, -1.65f, -10f);
                }
            }
            _lastCameraAspect = camera.aspect;
            camera.backgroundColor = new Color32(6, 9, 13, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private bool TryGetHubCameraFrame(float aspect, out OrthographicCameraFrame frame)
        {
            frame = default;
            if (_grid == null || _grid.Map.Count == 0 || aspect <= 0f) return false;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                Vector2 world = _grid.iso.GridToWorld(pair.Key);
                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxY = Mathf.Max(maxY, world.y);
            }

            frame = OrthographicCameraFraming.Fit(
                minX, maxX, minY, maxY,
                aspect,
                playCameraSize,
                hubCameraHorizontalPadding,
                hubCameraVerticalPadding);
            return true;
        }

        private void ClearVisuals()
        {
            Transform previous = transform.Find("Generated Visuals");
            if (previous == null) return;

            if (Application.isPlaying)
                Destroy(previous.gameObject);
            else
                DestroyImmediate(previous.gameObject);
        }

        private EnemyAgent FindLivingEnemyAt(GridPos pos)
        {
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State != null && enemy.State.IsAlive && enemy.State.Position == pos)
                    return enemy;
            }
            return null;
        }

        private bool IsLivingEnemyAt(GridPos pos) => FindLivingEnemyAt(pos) != null;

        /// <summary>접근 후 실행 직전 재검증: 같은 elevation의 상하좌우 인접인가.</summary>
        private bool IsPlayerAdjacentTo(GridPos pos) =>
            _playerPos.elevation == pos.elevation && _playerPos.ManhattanTo(pos) == 1;

        /// <summary>플레이어가 밟은 칸의 아이템을 줍는다.</summary>
        private void TryCollectItemAt(GridPos pos)
        {
            foreach (ItemAgent item in _items)
            {
                if (item.Collected || item.Spawn.Position != pos) continue;

                ItemFootprint footprint = BackpackRules.Footprint(item.Spawn.Kind);
                if (!_inventory.TryAdd(item.Spawn.Kind, out int count))
                {
                    InteractionFeedback?.Invoke(
                        $"백팩 공간 부족 · {ItemCatalog.DisplayName(item.Spawn.Kind)} " +
                        $"{footprint}칸 필요");
                    Debug.Log(
                        $"[Item] {item.Spawn.Kind} 획득 실패 {pos} " +
                        $"(백팩 {CurrentBackpackLayout.UsedCells}/{BackpackRules.Capacity}칸)");
                    return;
                }

                item.Collected = true;
                if (item.Root != null) item.Root.SetActive(false);
                InventoryChanged?.Invoke();

                InteractionFeedback?.Invoke(
                    $"{ItemLabel(item.Spawn.Kind)} 획득 · {footprint}칸 · 보유 ×{count}");
                Debug.Log($"[Item] {item.Spawn.Kind} 획득 {pos} (보유 {count})");
                return;
            }
        }

        /// <summary>적 하나의 로직 상태·AI·씬 오브젝트 묶음.</summary>
        private sealed class EnemyAgent
        {
            public MonsterArchetype Archetype;
            public CombatantState State;
            public MonsterBrain Brain;
            public GameObject Root;
            public SpriteRenderer Renderer;
            public Transform HpFill;
            public Transform HpBackground;
            public MonsterMood LastMood;
            public TextMesh MoodIcon;
            public int DeathTurn = -1;
        }

        /// <summary>바닥에 놓인 아이템 프롭. 밟으면 Collected 로 바뀌고 숨겨진다.</summary>
        private sealed class ItemAgent
        {
            public ItemSpawn Spawn;
            public GameObject Root;
            public SpriteRenderer Renderer;
            public bool Collected;
        }
    }
}
