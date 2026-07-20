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
        [Range(9, 14)] public int roomSize = 11;
        [Range(0.03f, 0.3f)] public float secondsPerStep = 0.09f;
        public bool buildOnStart = true;
        public bool configureMainCamera = true;

        [Header("카메라 구도")]
        [Range(4f, 7f)] public float playCameraSize = 5.2f;
        [Range(7f, 11f)] public float debugCameraSize = 8.8f;

        [Header("M1 전투")]
        [Min(1)] public int playerMaxHp = 8;
        [Min(1)] public int playerAttack = 2;
        [Min(1)] public int goblinMaxHp = 5;
        [Min(1)] public int goblinAttack = 1;
        [Range(2, 8)] public int rangedAttackRange = 6;
        public CombatActionMode combatMode = CombatActionMode.Melee;

        [Header("M3 다층 던전")]
        [Range(2, 5)] public int floorCount = 3;
        [Range(3, 6)] public int elevationsPerFloor = 4;
        [Tooltip("검증 장면에서 시작할 깊이. 1이면 B2에서 시작한다.")]
        [Range(0, 2)] public int previewStartDepth = 1;
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
        public event System.Action<int> ViewRotationChanged;
        public event System.Action<int> ActiveFloorChanged;
        public event System.Action<DungeonViewMode> ViewModeChanged;
        public event System.Action<CombatActionMode> CombatModeChanged;
        public event System.Action<string> InteractionFeedback;
        public event System.Action PlayerPositionChanged;
        public event System.Action VerticalContextChanged;

        private GridManager _grid;
        private IsoTapInput _input;
        private Transform _visualRoot;
        private GameObject _player;
        private SpriteRenderer _playerRenderer;
        private GridSortingObject _playerSorting;
        private Transform _playerLocator;
        private Transform _playerFootprint;
        private GridPos _playerPos;
        private GameObject _goblin;
        private SpriteRenderer _goblinRenderer;
        private GameObject _barrel;
        private SpriteRenderer _barrelRenderer;
        private GridPos _barrelPos;
        private GameObject _selection;
        private GridPos _selectionPos;
        private Transform _wallRoot;
        private Transform _shaftRoot;
        private Coroutine _moveRoutine;
        private Transform _playerHpFill;
        private Transform _goblinHpFill;
        private CombatantState _playerState;
        private CombatantState _goblinState;
        private readonly TurnManager _turns = new TurnManager();
        private bool _resolvingAction;
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
            }
        }

        public void BuildPrototype()
        {
            if (_grid == null) _grid = GetComponent<GridManager>();
            if (_input == null) _input = GetComponent<IsoTapInput>();

            // 이전 8×8 프로토타입 씬을 열어도 세 방 레이아웃의 최소 규격으로 자동 이행한다.
            roomSize = Mathf.Max(9, roomSize);

            if (Application.isPlaying && _moveRoutine != null)
                StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _resolvingAction = false;
            _turns.Reset();
            _visibleTiles.Clear();
            _exploredTiles.Clear();
            _verticalPreviewTiles.Clear();

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

            if (configureMainCamera)
                ConfigureCamera(Camera.main);

            ViewRotationChanged?.Invoke(_grid.iso.viewQuarterTurns);
            ActiveFloorChanged?.Invoke(_activeFloorIndex);
            ViewModeChanged?.Invoke(viewMode);
            CombatModeChanged?.Invoke(combatMode);
            PlayerPositionChanged?.Invoke();
        }

        private void BuildRoomData()
        {
            _dungeon = DungeonGenerator.Generate(
                _grid.Map,
                roomSize,
                roomSize,
                floorCount,
                elevationsPerFloor);
            int startDepth = Mathf.Clamp(previewStartDepth, 0, _dungeon.Floors.Count - 1);
            _activeFloorIndex = _dungeon.Floors[startDepth].FloorIndex;
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

            RefreshFloorVisibility();
        }

        private void CreateActorsAndProps()
        {
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo activeFloor);
            _playerPos = activeFloor.Entry;
            _playerState = new CombatantState("Player", _playerPos, playerMaxHp, playerAttack);
            Sprite playerSprite = visualCatalog != null && visualCatalog.player != null
                ? visualCatalog.player
                : GetCharacterSprite(false);
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
            footprintRenderer.sprite = GetPlayerFootprintSprite();
            footprintRenderer.sortingOrder = 29990;
            _playerFootprint = footprint.transform;

            Sprite goblinSprite = visualCatalog != null && visualCatalog.goblin != null
                ? visualCatalog.goblin
                : GetCharacterSprite(true);
            Sprite barrelSprite = visualCatalog != null && visualCatalog.explosiveBarrel != null
                ? visualCatalog.explosiveBarrel
                : GetBarrelSprite();
            var goblinPos = activeFloor.EnemySpawn;
            _goblinState = new CombatantState("Goblin", goblinPos, goblinMaxHp, goblinAttack);
            _goblin = CreateStandingSprite("Goblin", goblinSprite, goblinPos, out _goblinRenderer);

            _barrelPos = FindPreviewPropPosition();
            _barrel = CreateStandingSprite("Explosive Barrel", barrelSprite, _barrelPos, out _barrelRenderer);

            _playerHpFill = CreateHealthBar(_player, "Player HP");
            _goblinHpFill = CreateHealthBar(_goblin, "Goblin HP");
            UpdateHealthBar(_playerHpFill, _playerState);
            UpdateHealthBar(_goblinHpFill, _goblinState);

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
            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.1f;
            _playerLocator.localScale = new Vector3(pulse, pulse, 1f);
            if (_playerFootprint != null)
            {
                float footprintPulse = 1f + Mathf.Sin(Time.time * 5f) * 0.04f;
                _playerFootprint.localScale = new Vector3(footprintPulse, footprintPulse, 1f);
            }

            UpdatePlayerOccluders(Time.deltaTime);
        }

        private GameObject CreateStandingSprite(string objectName, Sprite sprite, GridPos pos, out SpriteRenderer renderer)
        {
            var instance = new GameObject(objectName);
            instance.transform.SetParent(_visualRoot, false);
            instance.transform.position = _grid.GridToWorld(pos);
            renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = _grid.iso.SortingOrder(pos, 1);
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
            if (_goblinRenderer != null)
            {
                _goblin.transform.position = _grid.GridToWorld(_goblinState.Position);
                _goblinRenderer.sortingOrder = _grid.iso.SortingOrder(_goblinState.Position, 1);
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

        private void HandleTileTapped(GridPos target, bool tileExists)
        {
            if (!Application.isPlaying || _resolvingAction || _playerState == null || !_playerState.IsAlive)
                return;

            if (viewMode == DungeonViewMode.Play &&
                !_visibleTiles.Contains(target) &&
                !_exploredTiles.Contains(target))
                return;

            TileData targetTile = _grid.Map.Get(target);
            if (targetTile != null && targetTile.kind == TileKind.Hole)
            {
                List<GridPos> dropPath = FindPathToAdjacent(target);
                if (dropPath.Count == 0) return;

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ApproachAndDrop(dropPath, target));
                return;
            }

            if (targetTile != null && (targetTile.CanOpen || targetTile.CanClose))
            {
                List<GridPos> doorPath = FindPathToAdjacent(target);
                if (doorPath.Count == 0) return;

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ApproachAndToggleDoor(doorPath, target));
                return;
            }

            if (_goblinState != null && _goblinState.IsAlive && target == _goblinState.Position)
            {
                if (combatMode == CombatActionMode.Ranged)
                {
                    PositionSelection(target);
                    _moveRoutine = StartCoroutine(RangedAttack());
                    return;
                }

                List<GridPos> attackPath = FindPathToAdjacent(_goblinState.Position);
                if (attackPath.Count == 0) return;

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ApproachAndAttack(attackPath));
                return;
            }

            if (!tileExists || !_grid.Map.IsWalkable(target) ||
                _dungeon.Height.FloorIndex(target.elevation) != _activeFloorIndex)
                return;

            List<GridPos> path = GridPathfinder.FindPath(_grid.Map, _playerPos, target);
            if (path.Count == 0) return;

            TileKind targetKind = _grid.Map.Get(target).kind;
            if (targetKind == TileKind.StairsUp || targetKind == TileKind.StairsDown)
            {
                IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(target);
                if (links.Count > 0)
                    path.Add(links[0]);
            }

            PositionSelection(target);
            _moveRoutine = StartCoroutine(MoveAlong(path));
        }

        private IEnumerator MoveAlong(IReadOnlyList<GridPos> path)
        {
            _resolvingAction = true;
            yield return MovePlayerPath(path);
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

        private IEnumerator ApproachAndAttack(IReadOnlyList<GridPos> path)
        {
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && _goblinState.IsAlive &&
                CombatRules.TryMelee(_playerState, _goblinState, out int damage))
            {
                UpdateHealthBar(_goblinHpFill, _goblinState);
                Debug.Log($"[Turn {_turns.TurnNumber}] 플레이어가 고블린에게 {damage} 피해. " +
                          $"HP {_goblinState.Hp}/{_goblinState.MaxHp}");
                yield return FlashDamage(_goblinRenderer);

                if (!_goblinState.IsAlive)
                {
                    _goblinRenderer.color = new Color32(60, 64, 66, 180);
                    Debug.Log("[Combat] 고블린 처치");
                }

                yield return ResolveEnemyPhase();
            }

            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator RangedAttack()
        {
            _resolvingAction = true;
            if (CombatRules.TryRanged(
                    _playerState,
                    _goblinState,
                    _grid.Map,
                    rangedAttackRange,
                    out int damage))
            {
                yield return AnimateProjectile(_playerPos, _goblinState.Position);
                UpdateHealthBar(_goblinHpFill, _goblinState);
                InteractionFeedback?.Invoke($"RANGED HIT · {damage} DAMAGE");
                Debug.Log($"[Ranged] 고블린에게 {damage} 피해. HP {_goblinState.Hp}/{_goblinState.MaxHp}");
                yield return FlashDamage(_goblinRenderer);

                if (!_goblinState.IsAlive)
                {
                    _goblinRenderer.color = new Color32(60, 64, 66, 180);
                    InteractionFeedback?.Invoke("GOBLIN DEFEATED");
                }

                yield return ResolveEnemyPhase();
            }
            else
            {
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, _goblinState.Position);
                InteractionFeedback?.Invoke(blocked ? "SHOT BLOCKED" : $"OUT OF RANGE · MAX {rangedAttackRange}");
            }

            _resolvingAction = false;
            _moveRoutine = null;
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
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            TileData tile = _grid.Map.Get(door);
            if (_playerPos.elevation == door.elevation && _playerPos.ManhattanTo(door) == 1 &&
                tile != null && (tile.CanOpen || tile.CanClose))
            {
                TileKind nextKind = tile.CanOpen ? TileKind.DoorOpen : TileKind.DoorClosed;
                if (_tileRenderers.TryGetValue(door, out SpriteRenderer renderer))
                {
                    yield return AnimateDoorTransition(renderer, door, nextKind);
                }
                else
                    _grid.Map.Set(door, nextKind);
                RefreshFloorVisibility();
                string feedback = nextKind == TileKind.DoorOpen ? "DOOR OPENED" : "DOOR CLOSED";
                InteractionFeedback?.Invoke(feedback);
                Debug.Log($"[Door] {door} {feedback}");
                yield return ResolveEnemyPhase();
            }

            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator ApproachAndDrop(IReadOnlyList<GridPos> path, GridPos hole)
        {
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            int minElevation = _dungeon.Height.Elevation(_dungeon.BottomFloorIndex);
            GridPos? landing = _grid.Map.FindLandingBelow(hole, minElevation);
            if (_playerState.IsAlive && landing.HasValue &&
                _playerPos.elevation == hole.elevation && _playerPos.ManhattanTo(hole) == 1)
            {
                int destinationFloor = _dungeon.Height.FloorIndex(landing.Value.elevation);
                InteractionFeedback?.Invoke($"DROP → {FloorLabel(destinationFloor)}");
                yield return AnimateHoleDrop(hole, landing.Value);

                _playerPos = landing.Value;
                _playerState.MoveTo(landing.Value);
                _playerSorting.x = landing.Value.x;
                _playerSorting.y = landing.Value.y;
                _playerSorting.elevation = landing.Value.elevation;
                _playerSorting.Apply();
                _activeFloorIndex = destinationFloor;
                UpdateInputFloorRange();
                RefreshFloorVisibility();
                PositionSelection(landing.Value);
                ConfigureCamera(Camera.main);
                ActiveFloorChanged?.Invoke(_activeFloorIndex);
                PlayerPositionChanged?.Invoke();
                InteractionFeedback?.Invoke($"LANDED · {LocationLabel}");
                yield return ResolveEnemyPhase();
            }

            _resolvingAction = false;
            _moveRoutine = null;
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
                if (_goblinState.IsAlive && next == _goblinState.Position)
                    yield break;

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

                _playerPos = next;
                _playerState.MoveTo(next);
                PlayerPositionChanged?.Invoke();
                _playerSorting.x = next.x;
                _playerSorting.y = next.y;
                _playerSorting.elevation = next.elevation;
                _player.transform.position = end;
                ConfigureCamera(Camera.main);

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
                }
                else
                {
                    RefreshFloorVisibility();
                }

                yield return ResolveEnemyPhase();
                if (!_playerState.IsAlive)
                    yield break;
            }
        }

        private IEnumerator ResolveEnemyPhase()
        {
            if (!_turns.TryBeginEnemyPhase()) yield break;

            if (_goblinState.IsAlive && CombatRules.TryMelee(_goblinState, _playerState, out int damage))
            {
                UpdateHealthBar(_playerHpFill, _playerState);
                Debug.Log($"[Turn {_turns.TurnNumber}] 고블린이 플레이어에게 {damage} 피해. " +
                          $"HP {_playerState.Hp}/{_playerState.MaxHp}");
                yield return FlashDamage(_playerRenderer);

                if (!_playerState.IsAlive)
                {
                    _playerRenderer.color = new Color32(120, 42, 42, 220);
                    Debug.Log("[Combat] 플레이어 사망 — 프로토타입을 다시 실행해 재시작");
                }
            }

            _turns.TryCompleteEnemyPhase();
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

        private IEnumerator FlashDamage(SpriteRenderer renderer)
        {
            if (renderer == null) yield break;

            Color original = renderer.color;
            renderer.color = new Color32(255, 92, 72, 255);
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
                if (_goblinState != null && _goblinState.IsAlive && candidate == _goblinState.Position)
                    continue;

                List<GridPos> path = GridPathfinder.FindPath(_grid.Map, _playerPos, candidate);
                if (_goblinState != null && _goblinState.IsAlive &&
                    path.Exists(step => step == _goblinState.Position))
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
            _input.minElevation = _dungeon.Height.Elevation(_activeFloorIndex);
            _input.maxElevation = _input.minElevation + _dungeon.Height.ElevationsPerFloor - 1;
            _input.targetElevation = _input.minElevation;
        }

        private static string FloorLabel(int floorIndex) =>
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

            camera.orthographic = true;
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
            camera.backgroundColor = new Color32(6, 9, 13, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
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
    }
}
