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
    /// partial 구성(관심사별 분할):
    ///  - IsoPrototypeDemo.cs           : 상태·필드·이벤트·수명주기(Awake/Start/Update)·방 빌드·카메라 헬퍼
    ///  - IsoPrototypeDemo.Debug.cs      : 디버그 창 전용 API(치트)
    ///  - IsoPrototypeDemo.View.cs       : 시점 회전/모드 토글·비주얼 적용·카메라 구도
    ///  - IsoPrototypeDemo.Interaction.cs: 탭/스텝/인접 상호작용·커넥터 판정
    ///  - IsoPrototypeDemo.Actions.cs    : 아이템/전투/조합/투척 행동 코루틴
    ///  - IsoPrototypeDemo.Movement.cs   : 경로 이동·문/비밀문/낙하 접근·여행(auto-travel)
    ///  - IsoPrototypeDemo.RunLifecycle.cs: 세이브/체크포인트/이어하기·던전 전환·정산/생환·텔레메트리
    ///  - IsoPrototypeDemo.Hub.cs        : 허브 프롭/포탈/영웅 잠금
    ///  - IsoPrototypeDemo.Enemies.cs    : 적 스폰·AI 턴·활성화
    ///  - IsoPrototypeDemo.Falls.cs      : 낙하/넉백 처리
    ///  - IsoPrototypeDemo.RestSites.cs  : 휴식 지점
    ///  - IsoPrototypeDemo.CombatFx.cs   : 전투/상태이상 연출
    ///  - IsoPrototypeDemo.Visibility.cs : FOV·수직 포털·후면 벽·가림
    ///  - IsoPrototypeDemo.Sprites*.cs   : 런타임 임시 스프라이트 생성(.Sprites/.Sprites.Actors/.Sprites.Primitives)
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridManager), typeof(IsoTapInput))]
    public partial class IsoPrototypeDemo : MonoBehaviour
    {
        public const int TilePixelWidth = 64;
        public const int TilePixelHeight = 32;
        public const int PixelsPerUnit = 64;

        /// <summary>
        /// IsoGrid.SortingOrder 로 배치할 수 없는 UI/오버레이 밴드의 정렬값 단일 출처.
        /// 월드 지오메트리는 IsoGrid 로 정렬하고, 아래 값들은 항상 그 위(또는 fog 는 최후면)에 겹친다.
        /// 절대값보다 상대 순서가 중요하다 — 값이 클수록 앞(카메라 쪽).
        /// </summary>
        private static class OverlaySorting
        {
            public const int FogBackdrop     = -100000; // 안개 배경: 항상 최후면
            public const int ShaftEndpoint   = 29979;   // 수직 샤프트 끝점
            public const int Shaft           = 29980;   // 수직 샤프트 본체
            public const int PlayerFootprint = 29990;   // 플레이어 발자국 데칼
            public const int HealthBarBack   = 30000;   // 체력바 배경
            public const int HealthBarFill   = 30001;   // 체력바 채움
            public const int PlayerLocator   = 30002;   // 플레이어 위치 표시(펄스)
            public const int Burst           = 30003;   // 감정/문 상호작용 버스트 연출
            public const int BossMarker      = 30004;   // 보스 표식
            public const int Projectile      = 31000;   // 투사체
            public const int Blast           = 31001;   // 폭발
            public const int CombatFx        = 31002;   // 전투/상태이상 FX
            public const int VerticalLabel   = 31990;   // 수직 경로 라벨
        }

        [Header("프로토타입")]
        [Tooltip("층 한 변 크기. 키우면 방·복도가 넓어지고 적/아이템 밀도가 면적 비례로 따라 오른다.")]
        [Range(9, 20)] public int roomSize = 13;
        [Range(0.03f, 0.3f)] public float secondsPerStep = 0.09f;
        public bool buildOnStart = true;
        public bool configureMainCamera = true;

        [Header("카메라 구도")]
        [Range(4f, 7f)] public float playCameraSize = 5.2f;
        [Range(7f, 16f)] public float debugCameraSize = 8.8f;
        [Tooltip("허브 가로 화면에서 방이 지나치게 작아지지 않도록 하는 전용 최소 크기.")]
        [Min(2.2f)] public float hubCameraMinimumSize = 2.55f;
        [Min(0f)] public float hubCameraHorizontalPadding = 0.6f;
        [Min(0f)] public float hubCameraVerticalPadding = 1.2f;

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
        [Tooltip("한 판에 완주해야 하는 던전 수. 첫 목적지는 B1~B10 단일 던전이며 이후 체인 확장용으로 남긴다.")]
        [Range(1, 5)] public int stageCount = 1;

        [Header("허브 모드")]
        [Tooltip("켜면 던전 대신 허브 캠프(상인/영웅/창고/포탈)를 만든다. Hub 씬 전용.")]
        public bool hubMode;

        [Header("M3 다층 던전")]
        [Range(2, 20)] public int floorCount = 10;
        [Range(3, 6)] public int elevationsPerFloor = 4;
        [Tooltip("절차 생성 seed. 같은 값이면 같은 던전이 재현된다.")]
        public int dungeonSeed = 1977;
        [Tooltip("검증 장면에서 시작할 깊이. 1이면 B2에서 시작한다.")]
        [Range(0, 19)] public int previewStartDepth = 0;
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
        public Color32 unknownFogColor = new Color32(7, 9, 14, 210);
        public Color32 unknownFogEdge = new Color32(10, 13, 19, 228);

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
        [Tooltip("카탈로그가 없는 편집 미리보기용 던전 석재 기준색")]
        public Color32 floorTop = new Color32(74, 64, 56, 255);
        [Tooltip("카탈로그가 없는 편집 미리보기용 단차 명도색")]
        public Color32 raisedTop = new Color32(152, 134, 111, 255);
        public Color32 lowerTop = new Color32(10, 13, 19, 255);
        public Color32 tileSeam = new Color32(10, 13, 19, 255);
        public Color32 outline = new Color32(5, 7, 12, 255);
        public Color32 accent = new Color32(79, 167, 160, 255);

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
        public int FloorCount => floorCount;
        public string DungeonId => DungeonSelection.Selected.Id;
        public string StageLabel => $"던전 {_stageIndex}/{stageCount}";
        public bool HasNextStage => _stageIndex < stageCount;
        public bool IsBossFloor =>
            !hubMode &&
            _dungeon != null &&
            _activeFloorIndex == _dungeon.BottomFloorIndex &&
            DungeonSelection.Selected.Boss != null;
        public bool BossDefeated => _bossDefeated;
        public bool BossExitUnlocked =>
            !hubMode &&
            DungeonBossRules.CanUseExit(DungeonSelection.Selected, _bossDefeated);
        public string BossName => DungeonSelection.Selected.Boss?.DisplayName ?? "--";
        public int BossHp => _boss != null && _boss.State != null ? _boss.State.Hp : 0;
        public int BossMaxHp =>
            _boss != null && _boss.State != null
                ? _boss.State.MaxHp
                : DungeonSelection.Selected.Boss?.Archetype.MaxHp ?? 0;
        public bool BombAiming => _bombAiming;
        public ItemKind AimedBombKind => _bombAimKind;
        public CombatantState PlayerState => _playerState;
        public RunSummary RunSummary => _runSummary;
        public RunTelemetry Telemetry => _runTelemetry;
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
        public event System.Action BossStateChanged;
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
        private RunTelemetry _runTelemetry;
        private FloatingTextSpawner _floatingText;
        private readonly HashSet<string> _travelVisibleEnemyIds = new HashSet<string>();
        private readonly HashSet<GridPos> _travelVisibleItemTiles = new HashSet<GridPos>();
        private readonly Dictionary<GridPos, string> _hubInteractables =
            new Dictionary<GridPos, string>();
        private readonly Dictionary<string, SpriteRenderer> _hubHeroProps =
            new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, GridPos> _hubHeroPositions =
            new Dictionary<string, GridPos>();
        private readonly Dictionary<SpriteRenderer, GridPos> _hubPropPositions =
            new Dictionary<SpriteRenderer, GridPos>();
        private readonly Dictionary<SpriteRenderer, GridPos> _hubLightPositions =
            new Dictionary<SpriteRenderer, GridPos>();
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
        private EnemyAgent _boss;
        private bool _bossDefeated;
        private GameObject _bossExitSeal;
        private SpriteRenderer _bossExitSealRenderer;
        private GridPos _bossExitPos;
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
            RunTelemetry previousTelemetry = _runTelemetry;

            // 이전 8×8 프로토타입 씬을 열어도 세 방 레이아웃의 최소 규격으로 자동 이행한다.
            roomSize = Mathf.Max(9, roomSize);

            // 이어하기: 저장된 seed/규격으로 같은 던전을 재생성하고 해당 층 입구에서 시작한다.
            RunSaveData continueData = null;
            if (Application.isPlaying && !hubMode && RunSaveStore.ContinueRequested)
            {
                RunSaveStore.ContinueRequested = false;
                if (RunSaveStore.TryLoad(out continueData))
                {
                    DungeonSelection.SelectedId = string.IsNullOrWhiteSpace(continueData.dungeonId)
                        ? DungeonCatalog.DefaultId
                        : continueData.dungeonId;
                    DungeonDefinition selected = DungeonSelection.Selected;
                    dungeonSeed = continueData.seed;
                    roomSize = Mathf.Max(9, continueData.roomSize);
                    floorCount = selected.FloorCount;
                    elevationsPerFloor = continueData.elevationsPerFloor;
                    stageCount = Mathf.Max(1, continueData.stageCount);
                    _stageIndex = Mathf.Max(1, continueData.stageIndex);
                }
            }
            if (Application.isPlaying && !hubMode)
                previewStartDepth = RunStartRules.ResolvePreviewDepth(continueData);
            if (Application.isPlaying && !hubMode && continueData == null && _stageIndex == 1)
            {
                DungeonDefinition selected = DungeonSelection.Selected;
                dungeonSeed = selected.Seed;
                floorCount = selected.FloorCount;
                stageCount = 1;
            }

            // 영웅 프리셋: 새 판은 메뉴 선택, 이어하기는 저장된 영웅. 편집 모드 미리보기는 인스펙터 값 유지.
            if (Application.isPlaying)
            {
                _hero = HeroRoster.ById(continueData != null ? continueData.heroId : HeroSelection.SelectedId);
                // 대장간 영구 강화를 영웅 기본 스탯 위에 얹는다 (티어 0 이면 기본값 그대로).
                MetaSaveData heroMeta = MetaStore.LoadOrNew();
                playerMaxHp = SmithyRules.EffectiveMaxHp(_hero, heroMeta);
                playerAttack = SmithyRules.EffectiveAttack(_hero, heroMeta);
                rangedAttackDamage = SmithyRules.EffectiveRangedDamage(_hero, heroMeta);
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
            ResetRestSitesForBuild();
            _inventory.Clear();
            _boss = null;
            _bossExitSeal = null;
            _bossExitSealRenderer = null;
            _bossDefeated = continueData != null && continueData.bossDefeated;
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
            if (Application.isPlaying && !hubMode)
            {
                int floorIndex = GlobalFloorIndex(_activeFloorIndex);
                if (continueData != null && continueData.telemetry != null)
                {
                    _runTelemetry = continueData.telemetry;
                }
                else if (_stageIndex > 1 && previousTelemetry != null && !previousTelemetry.Ended)
                {
                    _runTelemetry = previousTelemetry;
                }
                else
                {
                    _runTelemetry = RunTelemetry.Begin(
                        DungeonSelection.Selected.Id,
                        _hero != null ? _hero.Id : HeroSelection.SelectedId,
                        dungeonSeed,
                        floorIndex,
                        System.DateTime.UtcNow);
                }

                if (_runTelemetry.currentFloorIndex != floorIndex)
                    _runTelemetry.RecordFloorEntered(floorIndex);
            }
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
            BossStateChanged?.Invoke();
        }

        private void Update()
        {
            if (!Application.isPlaying || hubMode || _dungeon == null ||
                _runTelemetry == null || _runTelemetry.Ended)
                return;

            _runTelemetry.RecordElapsed(
                Time.unscaledDeltaTime,
                GlobalFloorIndex(_activeFloorIndex));
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
            locatorRenderer.sortingOrder = OverlaySorting.PlayerLocator;
            _playerLocator = locator.transform;

            var footprint = new GameObject("Player Footprint");
            footprint.transform.SetParent(_player.transform, false);
            footprint.transform.localPosition = Vector3.zero;
            var footprintRenderer = footprint.AddComponent<SpriteRenderer>();
            footprintRenderer.sprite = visualCatalog != null && visualCatalog.playerFootprint != null
                ? visualCatalog.playerFootprint
                : GetPlayerFootprintSprite();
            footprintRenderer.sortingOrder = OverlaySorting.PlayerFootprint;
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
                bool bossFloor =
                    floor.FloorIndex == _dungeon.BottomFloorIndex &&
                    DungeonSelection.Selected.Boss != null;
                GridPos bossSpawn = default;
                bool hasBossSpawn = bossFloor &&
                    DungeonBossRules.TrySelectSpawn(
                        floor.Entry,
                        floor.EnemySpawns,
                        out bossSpawn);

                foreach (GridPos spawn in floor.EnemySpawns)
                {
                    if (hasBossSpawn && spawn == bossSpawn)
                        continue;
                    SpawnEnemy(
                        MonsterRoster.PickForDepth(GlobalDepth(floor.FloorIndex), _spawnRng),
                        spawn,
                        floor.FloorIndex);
                }

                if (hasBossSpawn && !_bossDefeated)
                {
                    DungeonBossDefinition boss = DungeonSelection.Selected.Boss;
                    _boss = SpawnEnemy(
                        boss.Archetype,
                        bossSpawn,
                        floor.FloorIndex,
                        isBoss: true,
                        displayName: boss.DisplayName);
                }

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

                CreateRestSite(floor);
            }
            CreateBossExitSeal();

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
        /// 적 피격 공통 처리. 전투 결과와 로그는 항상 반영하되,
        /// 플로팅 피해·사망 안내·플래시는 현재 FOV 안에서만 공개한다.
        /// </summary>
        private IEnumerator ShowEnemyHit(EnemyAgent enemy, int damage, string source)
        {
            _runTelemetry?.RecordDamageDealt(
                source,
                damage,
                GlobalFloorIndex(_activeFloorIndex));
            UpdateHealthBar(enemy.HpFill, enemy.State);
            bool visibleToPlayer = IsEnemyVisibleToPlayer(enemy);
            CombatImpactKind impact = CombatPresentationRules.ImpactForSource(source);
            if (visibleToPlayer)
            {
                FloatingText?.ShowDamage(
                    enemy.Root != null
                        ? enemy.Root.transform.position
                        : _grid.GridToWorld(enemy.State.Position),
                    damage,
                    FloatingKindForImpact(impact));
            }
            Debug.Log($"[{source}] {enemy.State.Id}에게 {damage} 피해. " +
                      $"HP {enemy.State.Hp}/{enemy.State.MaxHp}");

            if (visibleToPlayer && enemy.Renderer != null)
                yield return PlayCombatImpact(
                    enemy.Root != null ? enemy.Root.transform : enemy.Renderer.transform,
                    enemy.Renderer,
                    impact);

            RecordEnemyDeath(enemy, visibleToPlayer);

            ApplyEnemyVisuals(enemy);
            if (enemy.IsBoss)
                BossStateChanged?.Invoke();
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
            _runTelemetry?.RecordDamageTaken(
                source,
                damage,
                !_playerState.IsAlive,
                GlobalFloorIndex(_activeFloorIndex));
            CombatImpactKind impact = CombatPresentationRules.ImpactForSource(source);
            FloatingText?.ShowDamage(
                _player.transform.position,
                damage,
                impact == CombatImpactKind.Physical
                    ? FloatingTextKind.PlayerDamage
                    : FloatingKindForImpact(impact));
            Debug.Log($"[{source}] 플레이어가 {damage} 피해. " +
                      $"HP {_playerState.Hp}/{_playerState.MaxHp}");
            yield return PlayCombatImpact(_player.transform, _playerRenderer, impact);

            if (!_playerState.IsAlive)
            {
                _playerRenderer.color = new Color32(120, 42, 42, 220);
                _runSummary.EndInDefeat(source);
                FinishRunTelemetry(RunTelemetryOutcome.Defeat, source);
                RunSaveStore.Clear();
                Debug.Log($"[Combat] 플레이어 사망 — 사인 {source}, " +
                          $"최심층 {FloorLabel(_runSummary.DeepestFloorIndex)}");
                RunEnded?.Invoke(_runSummary);
            }
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
            backgroundRenderer.sortingOrder = OverlaySorting.HealthBarBack;

            var fill = new GameObject($"{objectName} Fill");
            fill.transform.SetParent(owner.transform, false);
            fill.transform.localPosition = new Vector3(-0.25f, 0.82f, 0f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = GetHealthBarSprite(true);
            fillRenderer.sortingOrder = OverlaySorting.HealthBarFill;
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
                _runTelemetry?.RecordItemCollected(
                    item.Spawn.Kind,
                    GlobalFloorIndex(_activeFloorIndex));
                if (item.Root != null) item.Root.SetActive(false);
                InventoryChanged?.Invoke();

                InteractionFeedback?.Invoke(
                    $"{ItemCatalog.ShortLabel(item.Spawn.Kind)} 획득 · {footprint}칸 · 보유 ×{count}");
                Debug.Log($"[Item] {item.Spawn.Kind} 획득 {pos} (보유 {count})");
                return;
            }
        }

        /// <summary>적 하나의 로직 상태·AI·씬 오브젝트 묶음.</summary>
        private sealed class EnemyAgent
        {
            public MonsterArchetype Archetype;
            public bool IsBoss;
            public string DisplayName;
            public CombatantState State;
            public MonsterBrain Brain;
            public GameObject Root;
            public SpriteRenderer Renderer;
            public Transform HpFill;
            public Transform HpBackground;
            public MonsterMood LastMood;
            public TextMesh MoodIcon;
            public TextMesh BossMarker;
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
