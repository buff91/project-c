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
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridManager), typeof(IsoTapInput))]
    public class IsoPrototypeDemo : MonoBehaviour
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

        private void RefreshFloorVisibility()
        {
            if (_dungeon == null) return;

            RecomputeVisibility();

            foreach (var pair in _tileRenderers)
            {
                bool debugVisible = viewMode == DungeonViewMode.DebugAll;
                bool visible = _visibleTiles.Contains(pair.Key);
                bool explored = _exploredTiles.Contains(pair.Key);
                bool vertical = _verticalPreviewTiles.Contains(pair.Key);
                pair.Value.sprite = GetTileSprite(_grid.Map.Get(pair.Key).kind, pair.Key);
                pair.Value.enabled = debugVisible || visible || explored || vertical;
                float alpha = VisibilityAlpha(pair.Key);
                pair.Value.color = new Color(1f, 1f, 1f, alpha);
                pair.Value.transform.position = VisualPosition(pair.Key);
            }

            if (_goblin != null)
                SetSpriteHierarchyVisible(
                    _goblin,
                    _dungeon.Height.FloorIndex(_goblinState.Position.elevation) == _activeFloorIndex &&
                    (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(_goblinState.Position)));

            if (_barrelRenderer != null)
            {
                bool active = _dungeon.Height.FloorIndex(_barrelPos.elevation) == _activeFloorIndex;
                bool visible = _visibleTiles.Contains(_barrelPos) || _verticalPreviewTiles.Contains(_barrelPos);
                _barrelRenderer.enabled = viewMode == DungeonViewMode.DebugAll || visible;
                _barrelRenderer.color = new Color(
                    1f,
                    1f,
                    1f,
                    viewMode == DungeonViewMode.DebugAll
                        ? active ? 1f : debugAdjacentAlpha
                        : _visibleTiles.Contains(_barrelPos) ? 1f : verticalPreviewAlpha);
                _barrel.transform.position = VisualPosition(_barrelPos);
            }

            RebuildRearWalls();
            RebuildVerticalShafts();
            VerticalContextChanged?.Invoke();
        }

        private void RebuildVerticalShafts()
        {
            if (_shaftRoot != null)
            {
                if (Application.isPlaying) Destroy(_shaftRoot.gameObject);
                else DestroyImmediate(_shaftRoot.gameObject);
            }

            var root = new GameObject("Vertical Connections");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _shaftRoot = root.transform;

            if (viewMode == DungeonViewMode.DebugAll)
            {
                foreach (DungeonFloorInfo floor in _dungeon.Floors)
                {
                    CreateLinkedShaft(floor.DownStairs);
                    CreateHoleShaft(floor.Hole);
                }
                return;
            }

            if (!_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active)) return;
            if (active.UpStairs.HasValue && _visibleTiles.Contains(active.UpStairs.Value))
                CreateLinkedShaft(active.UpStairs);
            if (active.DownStairs.HasValue && _visibleTiles.Contains(active.DownStairs.Value))
                CreateLinkedShaft(active.DownStairs);
            if (active.Hole.HasValue && _visibleTiles.Contains(active.Hole.Value))
                CreateHoleShaft(active.Hole);
        }

        private void CreateLinkedShaft(GridPos? stair)
        {
            if (!stair.HasValue) return;
            foreach (GridPos linked in _grid.Map.LinksFrom(stair.Value))
                CreateVerticalShaft(stair.Value, linked, hole: false);
        }

        private void CreateHoleShaft(GridPos? hole)
        {
            if (!hole.HasValue) return;
            int minElevation = _dungeon.Height.Elevation(_dungeon.BottomFloorIndex);
            GridPos? landing = _grid.Map.FindLandingBelow(hole.Value, minElevation);
            if (landing.HasValue)
                CreateVerticalShaft(hole.Value, landing.Value, hole: true);
        }

        private void CreateVerticalShaft(GridPos from, GridPos to, bool hole)
        {
            Vector3 start = VisualPosition(from);
            Vector3 end = VisualPosition(to);
            float distance = Mathf.Max(0.35f, Mathf.Abs(end.y - start.y));

            var shaft = new GameObject(hole ? "Hole Drop Shaft" : "Stair Connection Shaft");
            shaft.transform.SetParent(_shaftRoot, false);
            shaft.transform.position = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.05f;
            var renderer = shaft.AddComponent<SpriteRenderer>();
            renderer.sprite = GetShaftSprite(hole);
            renderer.sortingOrder = 29980;
            renderer.color = new Color(1f, 1f, 1f, viewMode == DungeonViewMode.DebugAll ? 0.72f : 0.9f);
            shaft.transform.localScale = new Vector3(1.15f, distance, 1f);

            CreateShaftEndpoint(from, hole, arrival: false);
            CreateShaftEndpoint(to, hole, arrival: true);
        }

        private void CreateShaftEndpoint(GridPos pos, bool hole, bool arrival)
        {
            var endpoint = new GameObject(arrival ? "Shaft Arrival" : "Shaft Entrance");
            endpoint.transform.SetParent(_shaftRoot, false);
            endpoint.transform.position = VisualPosition(pos) + Vector3.up * 0.035f;
            var renderer = endpoint.AddComponent<SpriteRenderer>();
            renderer.sprite = GetShaftEndpointSprite(hole, arrival);
            renderer.sortingOrder = 29979;
            renderer.color = new Color(1f, 1f, 1f, arrival ? 0.72f : 0.95f);
        }

        private void RecomputeVisibility()
        {
            _visibleTiles.Clear();
            _verticalPreviewTiles.Clear();
            if (viewMode == DungeonViewMode.DebugAll) return;

            GridPos origin;
            if (_playerState != null)
                origin = _playerState.Position;
            else
            {
                _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
                origin = floor.Entry;
            }

            int minElevation = _dungeon.Height.Elevation(_activeFloorIndex);
            int maxElevation = minElevation + _dungeon.Height.ElevationsPerFloor - 1;
            foreach (GridPos pos in GridVisibility.Compute(
                         _grid.Map,
                         origin,
                         minElevation,
                         maxElevation,
                         fieldOfViewRadius))
            {
                _visibleTiles.Add(pos);
                _exploredTiles.Add(pos);
            }

            if (!_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo activeFloor)) return;

            if (activeFloor.Hole.HasValue &&
                _visibleTiles.Contains(activeFloor.Hole.Value) &&
                _dungeon.TryGetFloor(_activeFloorIndex - 1, out _))
            {
                AddVerticalWindow(activeFloor.Hole.Value, _activeFloorIndex - 1);
            }

            AddLinkedStairWindow(activeFloor.UpStairs);
            AddLinkedStairWindow(activeFloor.DownStairs);
        }

        private void AddLinkedStairWindow(GridPos? stair)
        {
            if (!stair.HasValue || !_visibleTiles.Contains(stair.Value)) return;
            foreach (GridPos linked in _grid.Map.LinksFrom(stair.Value))
                AddVerticalWindow(linked, _dungeon.Height.FloorIndex(linked.elevation));
        }

        private string BuildVerticalHintLabel()
        {
            if (_dungeon == null) return "EXPLORE TO FIND VERTICAL ROUTES";
            if (viewMode == DungeonViewMode.DebugAll) return "DEBUG: ALL FLOORS VISIBLE";
            if (!_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor))
                return "EXPLORE TO FIND VERTICAL ROUTES";

            var hints = new List<string>(3);
            if (floor.UpStairs.HasValue && _visibleTiles.Contains(floor.UpStairs.Value))
                hints.Add($"ORANGE ▲ TAP STAIR → {AboveFloorLabel}");
            if (floor.DownStairs.HasValue && _visibleTiles.Contains(floor.DownStairs.Value))
                hints.Add($"ORANGE ▼ TAP STAIR → {BelowFloorLabel}");
            if (floor.Hole.HasValue && _visibleTiles.Contains(floor.Hole.Value))
                hints.Add($"CYAN ▼ TAP HOLE TO DROP → {BelowFloorLabel}");

            return hints.Count > 0
                ? string.Join("\n", hints)
                : "EXPLORE TO FIND VERTICAL ROUTES";
        }

        private void AddVerticalWindow(GridPos center, int floorIndex)
        {
            foreach (var pair in _grid.Map.All())
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != floorIndex) continue;
                if (Mathf.Abs(pair.Key.x - center.x) <= verticalPreviewRadius &&
                    Mathf.Abs(pair.Key.y - center.y) <= verticalPreviewRadius)
                    _verticalPreviewTiles.Add(pair.Key);
            }
        }

        private GridPos FindPreviewPropPosition()
        {
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active);
            if (!active.Hole.HasValue || !_dungeon.TryGetFloor(active.FloorIndex - 1, out _))
                return active.Entry;

            GridPos hole = active.Hole.Value;
            int belowFloor = active.FloorIndex - 1;
            int baseElevation = _dungeon.Height.Elevation(belowFloor);
            for (int localHeight = _dungeon.Height.ElevationsPerFloor - 1; localHeight >= 0; localHeight--)
            {
                var candidate = new GridPos(hole.x, hole.y, baseElevation + localHeight);
                if (_grid.Map.IsSolidGround(candidate)) return candidate;
            }

            return _dungeon.TryGetFloor(belowFloor, out DungeonFloorInfo below)
                ? below.Entry
                : active.Entry;
        }

        private static void SetSpriteHierarchyVisible(GameObject root, bool visible)
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = visible;
        }

        private void RebuildRearWalls()
        {
            if (_visualRoot == null) return;

            _rearWallRenderers.Clear();

            if (_wallRoot != null)
            {
                if (Application.isPlaying) Destroy(_wallRoot.gameObject);
                else DestroyImmediate(_wallRoot.gameObject);
            }
            _wallRoot = null;
            if (!showRearWalls) return;

            var root = new GameObject("Rear View Walls");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _wallRoot = root.transform;

            GetViewDirections(out _, out _, out Vector2Int backA, out Vector2Int backB);
            foreach (var pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                int floor = _dungeon.Height.FloorIndex(pos.elevation);
                if (!pair.Value.IsWalkable ||
                    (viewMode == DungeonViewMode.Play &&
                     !_visibleTiles.Contains(pos) &&
                     !_exploredTiles.Contains(pos) &&
                     !_verticalPreviewTiles.Contains(pos)))
                    continue;

                if (!HasPlanarTile(pos.x + backA.x, pos.y + backA.y, floor))
                    CreateRearWall(pos, backA, flip: true);
                if (!HasPlanarTile(pos.x + backB.x, pos.y + backB.y, floor))
                    CreateRearWall(pos, backB, flip: false);
            }
        }

        private void CreateRearWall(GridPos pos, Vector2Int outward, bool flip)
        {
            var wall = new GameObject($"Rear Wall {pos} {outward}");
            wall.transform.SetParent(_wallRoot, false);
            Vector3 center = VisualPosition(pos);
            Vector3 outside = VisualPosition(new GridPos(
                pos.x + outward.x,
                pos.y + outward.y,
                pos.elevation));
            wall.transform.position = Vector3.Lerp(center, outside, 0.46f);

            var renderer = wall.AddComponent<SpriteRenderer>();
            bool torch = Mathf.Abs(pos.x * 3 + pos.y + _grid.iso.viewQuarterTurns) % 5 == 0;
            Sprite mapped = visualCatalog != null
                ? visualCatalog.RearWallFor(torch, risesRight: flip)
                : null;
            renderer.sprite = mapped != null ? mapped : GetWallSprite(torch);
            renderer.flipX = mapped == null && flip;
            renderer.sortingOrder = _grid.iso.SortingOrder(pos, -1);
            renderer.color = new Color(1f, 1f, 1f, VisibilityAlpha(pos));
            _rearWallRenderers.Add(renderer, pos);
        }

        private void UpdatePlayerOccluders(float deltaTime, bool instant = false)
        {
            if (_playerRenderer == null || _dungeon == null) return;

            Bounds playerBounds = _playerRenderer.bounds;
            playerBounds.Expand(new Vector3(
                playerOcclusionPadding * 2f,
                playerOcclusionPadding * 2f,
                0f));
            int playerSortingOrder = _playerRenderer.sortingOrder;

            foreach (var pair in _tileRenderers)
            {
                SpriteRenderer renderer = pair.Value;
                float baseAlpha = VisibilityAlpha(pair.Key);
                bool occludes = fadePlayerOccluders && renderer.enabled &&
                                SpriteOcclusion.ShouldFade(
                                    renderer.bounds,
                                    playerBounds,
                                    renderer.sortingOrder,
                                    playerSortingOrder);
                ApplyOcclusionAlpha(renderer, baseAlpha, occludes, deltaTime, instant);
            }

            foreach (var pair in _rearWallRenderers)
            {
                SpriteRenderer renderer = pair.Key;
                if (renderer == null) continue;
                float baseAlpha = VisibilityAlpha(pair.Value);
                bool occludes = fadePlayerOccluders && renderer.enabled &&
                                SpriteOcclusion.ShouldFade(
                                    renderer.bounds,
                                    playerBounds,
                                    renderer.sortingOrder,
                                    playerSortingOrder);
                ApplyOcclusionAlpha(renderer, baseAlpha, occludes, deltaTime, instant);
            }
        }

        private void ApplyOcclusionAlpha(
            SpriteRenderer renderer,
            float baseAlpha,
            bool occludes,
            float deltaTime,
            bool instant)
        {
            float targetAlpha = occludes
                ? Mathf.Min(baseAlpha, playerOccluderAlpha)
                : baseAlpha;
            Color color = renderer.color;
            color.a = instant
                ? targetAlpha
                : Mathf.MoveTowards(color.a, targetAlpha, playerOccluderFadeSpeed * deltaTime);
            renderer.color = color;
        }

        private bool IsFrontEdge(GridPos pos)
        {
            int floor = _dungeon.Height.FloorIndex(pos.elevation);
            GetViewDirections(out Vector2Int frontA, out Vector2Int frontB, out _, out _);
            return !HasPlanarTile(pos.x + frontA.x, pos.y + frontA.y, floor) ||
                   !HasPlanarTile(pos.x + frontB.x, pos.y + frontB.y, floor);
        }

        private bool HasPlanarTile(int x, int y, int floorIndex)
        {
            int baseElevation = _dungeon.Height.Elevation(floorIndex);
            for (int local = 0; local < _dungeon.Height.ElevationsPerFloor; local++)
            {
                if (_grid.Map.Has(new GridPos(x, y, baseElevation + local)))
                    return true;
            }
            return false;
        }

        private void GetViewDirections(
            out Vector2Int frontA,
            out Vector2Int frontB,
            out Vector2Int backA,
            out Vector2Int backB)
        {
            switch (_grid.iso.viewQuarterTurns)
            {
                case 1:
                    frontA = Vector2Int.up;
                    frontB = Vector2Int.left;
                    break;
                case 2:
                    frontA = Vector2Int.left;
                    frontB = Vector2Int.down;
                    break;
                case 3:
                    frontA = Vector2Int.down;
                    frontB = Vector2Int.right;
                    break;
                default:
                    frontA = Vector2Int.right;
                    frontB = Vector2Int.up;
                    break;
            }

            backA = -frontA;
            backB = -frontB;
        }

        private static string FloorLabel(int floorIndex) =>
            floorIndex <= 0 ? $"B{1 - floorIndex}" : $"F{floorIndex + 1}";

        private void PositionSelection(GridPos pos)
        {
            _selectionPos = pos;
            _selection.transform.position = _grid.GridToWorld(pos);
            _selection.GetComponent<SpriteRenderer>().sortingOrder = _grid.iso.SortingOrder(pos, -1);
        }

        private Vector3 VisualPosition(GridPos pos)
        {
            Vector3 world = _grid.GridToWorld(pos);
            if (_dungeon == null) return world;

            int floor = _dungeon.Height.FloorIndex(pos.elevation);
            if (viewMode == DungeonViewMode.DebugAll)
                world.y += (floor - _activeFloorIndex) * debugFloorSeparation;
            else if (floor != _activeFloorIndex && _verticalPreviewTiles.Contains(pos))
                world.y += (floor - _activeFloorIndex) * playAdjacentFloorSeparation;
            return world;
        }

        private float VisibilityAlpha(GridPos pos)
        {
            if (viewMode == DungeonViewMode.DebugAll)
                return _dungeon.Height.FloorIndex(pos.elevation) == _activeFloorIndex
                    ? 1f
                    : debugAdjacentAlpha;
            if (_visibleTiles.Contains(pos)) return 1f;
            if (_verticalPreviewTiles.Contains(pos)) return verticalPreviewAlpha;
            return exploredAlpha;
        }

        private GridPos TileVisualSortingPos(GridPos pos, TileKind kind)
        {
            return kind == TileKind.Stairs &&
                   StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing)
                ? landing
                : pos;
        }

        private void ApplyPlayerVisualSorting(GridPos pos)
        {
            if (_playerRenderer == null) return;
            GridPos sortingPos = StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing)
                ? landing
                : pos;
            _playerRenderer.sortingOrder = _grid.iso.SortingOrder(sortingPos, 1);
        }

        private static int TileSortOffset(TileKind kind)
        {
            if (kind == TileKind.DoorClosed || kind == TileKind.DoorOpen) return 0;
            return kind == TileKind.Stairs ? -1 : -2;
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

        private Sprite GetTileSprite(TileKind kind, GridPos pos)
        {
            if (kind == TileKind.DoorClosed || kind == TileKind.DoorOpen)
            {
                if (visualCatalog != null)
                {
                    Sprite mapped = visualCatalog.DoorFor(kind, DoorPlaneRisesRight(pos));
                    if (mapped != null) return mapped;
                }

                return GetDoorSprite(kind, pos);
            }

            if (kind == TileKind.Stairs ||
                kind == TileKind.StairsUp ||
                kind == TileKind.StairsDown)
            {
                if (visualCatalog != null)
                {
                    Sprite mapped = visualCatalog.StairsFor(kind, StairPlaneRisesRight(pos));
                    if (mapped != null) return mapped;
                }
            }

            int floorIndex = _dungeon != null ? _dungeon.Height.FloorIndex(pos.elevation) : 0;
            int localHeight = _dungeon != null ? _dungeon.Height.LocalHeight(pos.elevation) : pos.elevation;
            bool extruded = localHeight > 0 || IsFrontEdge(pos);
            int variant = Mathf.Abs(pos.x * 17 + pos.y * 31 + floorIndex * 13) % 4;
            Color32 baseColor = localHeight > 0
                ? raisedTop
                : floorIndex < 0 ? Shift(lowerTop, floorIndex * 5) : floorTop;

            if (visualCatalog != null)
            {
                Sprite mapped = visualCatalog.TileFor(kind, pos.elevation);
                if (mapped != null)
                    return extruded ? GetExtrudedMappedTileSprite(mapped, baseColor) : mapped;
            }

            string key = $"tile-{kind}-f{floorIndex}-h{localHeight}-v{variant}-x{extruded}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int textureHeight = extruded ? 48 : TilePixelHeight;
            int topOffset = extruded ? 16 : 0;
            var texture = NewTexture(TilePixelWidth, textureHeight);
            Color32 transparent = new Color32(0, 0, 0, 0);

            if (extruded)
                DrawExtrudedSides(texture, baseColor);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f)
                {
                    if (!extruded)
                        texture.SetPixel(px, py, transparent);
                    continue;
                }

                bool border = diamond > 0.88f;
                int noise = ((px / 7) + (py / 4) * 3 + variant) % 4;
                Color32 color = border ? tileSeam : Shift(baseColor, noise == 0 ? 5 : noise == 1 ? 0 : -4);

                bool stoneJoint = diamond < 0.72f &&
                                  ((px + py * 3 + variant * 11) % 29 == 0 ||
                                   (px * 2 - py + variant * 7) % 37 == 0);
                if (stoneJoint) color = Shift(baseColor, -14);

                bool moss = floorIndex < 0 && variant == 2 && py < 15 && px > 9 && px < 23;
                if (moss && (px + py) % 5 < 2)
                    color = new Color32(54, 78, 55, 255);

                if (kind == TileKind.DoorClosed)
                {
                    bool band = (px + py * 2) % 13 < 3;
                    bool iron = Mathf.Abs(px - 32) < 2 || Mathf.Abs(py - 16) < 2;
                    color = border || iron
                        ? outline
                        : band ? new Color32(164, 91, 43, 255) : new Color32(103, 57, 35, 255);
                }
                else if (kind == TileKind.DoorOpen)
                {
                    bool threshold = py > 11 && py < 20 && Mathf.Abs(px - 32) < 22;
                    color = border
                        ? outline
                        : threshold ? new Color32(177, 111, 52, 255) : color;
                }
                else if (kind == TileKind.Hole)
                    color = border ? accent : new Color32(4, 8, 11, 190);
                else if (kind == TileKind.WeakFloor && IsCrackPixel(px, py))
                    color = new Color32(24, 20, 19, 255);
                else if (kind == TileKind.Stairs && ((px + py * 2) % 12 < 3))
                    color = border ? outline : Shift(baseColor, 25);
                else if (kind == TileKind.StairsDown && ((px + py) % 10 < 3))
                    color = border ? outline : new Color32(220, 119, 47, 255);
                else if (kind == TileKind.StairsUp && ((px + py) % 10 < 3))
                    color = border ? outline : new Color32(74, 181, 219, 255);

                texture.SetPixel(px, py + topOffset, color);
            }

            texture.Apply(false, true);
            cached = CreateSprite(
                texture,
                extruded ? new Vector2(0.5f, 32f / 48f) : new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetExtrudedMappedTileSprite(Sprite topSprite, Color32 baseColor)
        {
            Texture2D source = topSprite.texture;
            Rect sourceRect = topSprite.rect;
            if (source == null || !source.isReadable ||
                Mathf.RoundToInt(sourceRect.width) != TilePixelWidth ||
                Mathf.RoundToInt(sourceRect.height) != TilePixelHeight)
                return topSprite;

            string key = $"mapped-extruded-{topSprite.name}-{baseColor.r}-{baseColor.g}-{baseColor.b}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, 48);
            DrawExtrudedSides(texture, baseColor);

            Color[] pixels = source.GetPixels(
                Mathf.RoundToInt(sourceRect.x),
                Mathf.RoundToInt(sourceRect.y),
                TilePixelWidth,
                TilePixelHeight);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                Color pixel = pixels[py * TilePixelWidth + px];
                if (pixel.a > 0f)
                    texture.SetPixel(px, py + 16, pixel);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 32f / 48f));
            _spriteCache[key] = cached;
            return cached;
        }

        private void DrawExtrudedSides(Texture2D texture, Color32 baseColor)
        {
            Color32 leftFace = Shift(baseColor, -24);
            Color32 rightFace = Shift(baseColor, -38);
            for (int py = 0; py < 32; py++)
            {
                int leftMin = py < 16 ? 32 - py * 2 : 0;
                int leftMax = py < 16 ? 32 : 64 - py * 2;
                int rightMin = py < 16 ? 32 : py * 2;
                int rightMax = py < 16 ? 32 + py * 2 : 63;

                for (int px = Mathf.Max(0, leftMin); px <= Mathf.Min(31, leftMax); px++)
                {
                    bool mortar = py % 7 == 0 || (px + (py / 7) * 8) % 19 == 0;
                    texture.SetPixel(px, py, mortar ? outline : leftFace);
                }
                for (int px = Mathf.Max(32, rightMin); px <= Mathf.Min(63, rightMax); px++)
                {
                    bool mortar = py % 7 == 0 || (px - (py / 7) * 7) % 21 == 0;
                    texture.SetPixel(px, py, mortar ? outline : rightFace);
                }
            }
        }

        private Sprite GetDoorSprite(TileKind kind, GridPos pos)
        {
            int floorIndex = _dungeon != null ? _dungeon.Height.FloorIndex(pos.elevation) : 0;
            bool closed = kind == TileKind.DoorClosed;
            bool risesRight = DoorPlaneRisesRight(pos);
            string key = $"door-iso-{kind}-f{floorIndex}-r{risesRight}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 64;
            const int height = 80;
            var texture = NewTexture(width, height);
            Color32 baseColor = floorIndex < 0 ? Shift(lowerTop, floorIndex * 5) : floorTop;

            // 문 아래에도 동일한 64×32 바닥 다이아몬드를 유지한다.
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                if (diamond > 1f) continue;
                texture.SetPixel(px, py, diamond > 0.88f ? tileSeam : baseColor);
            }

            Color32 stone = new Color32(70, 76, 77, 255);
            Color32 stoneLight = new Color32(102, 105, 99, 255);
            Color32 wood = new Color32(118, 66, 37, 255);
            Color32 woodLight = new Color32(170, 97, 48, 255);
            Color32 iron = new Color32(39, 43, 44, 255);

            // 통로 축에 수직인 아이소 평면을 사용한다. 회전해도 문짝이 벽의 사선과 맞는다.
            int leftBase = risesRight ? 9 : 25;
            int rightBase = risesRight ? 25 : 9;
            const int leftX = 15;
            const int rightX = 49;
            const int frameHeight = 40;

            FillSlantedPanel(
                texture,
                leftX,
                leftBase,
                rightX,
                rightBase,
                frameHeight,
                new Color32(6, 9, 11, 255),
                new Color32(10, 14, 16, 255),
                outline);

            if (closed)
            {
                int innerLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 5f / 34f)) + 3;
                int innerRightY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 29f / 34f)) + 3;
                FillSlantedPanel(texture, 20, innerLeftY, 44, innerRightY, 32, wood, woodLight, iron);
                DrawThickLine(texture, 20, innerLeftY + 11, 44, innerRightY + 11, 2, iron);
                DrawThickLine(texture, 20, innerLeftY + 24, 44, innerRightY + 24, 2, iron);
                FillRect(texture, risesRight ? 37 : 24, risesRight ? 31 : 27, 3, 3,
                    new Color32(227, 173, 70, 255));
            }
            else
            {
                // 열린 문짝은 오른쪽 기둥 쪽으로 접혀 중앙 통과 방향을 그대로 드러낸다.
                int foldedLeftY = Mathf.RoundToInt(Mathf.Lerp(leftBase, rightBase, 25f / 34f)) + 3;
                int foldedRightY = rightBase + 3;
                FillSlantedPanel(texture, 40, foldedLeftY, 47, foldedRightY, 31,
                    Shift(wood, -12), woodLight, iron);
            }

            DrawThickLine(texture, leftX, leftBase, leftX, leftBase + frameHeight, 5, stone);
            DrawThickLine(texture, rightX, rightBase, rightX, rightBase + frameHeight, 5, Shift(stone, -9));
            DrawThickLine(texture, leftX, leftBase + frameHeight, rightX, rightBase + frameHeight, 6, stone);
            DrawThickLine(texture, leftX + 2, leftBase + frameHeight + 1,
                rightX - 2, rightBase + frameHeight + 1, 2, stoneLight);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 16f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        private bool DoorPlaneRisesRight(GridPos pos)
        {
            bool passageNorthSouth = HasDoorSide(pos.North) && HasDoorSide(pos.South);
            Vector2Int planeAxis = passageNorthSouth ? Vector2Int.right : Vector2Int.up;
            return AxisRisesRight(pos, planeAxis);
        }

        private bool StairPlaneRisesRight(GridPos pos)
        {
            if (StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing))
                return _grid.iso.ProjectsToScreenRight(pos, landing);

            return AxisRisesRight(pos, Vector2Int.up);
        }

        private bool AxisRisesRight(GridPos pos, Vector2Int worldAxis)
        {
            Vector3 center = _grid.GridToWorld(pos);
            Vector3 alongPlane = _grid.GridToWorld(pos.Offset(worldAxis.x, worldAxis.y)) - center;
            return alongPlane.x * alongPlane.y >= 0f;
        }

        private bool HasDoorSide(GridPos pos)
        {
            TileData tile = _grid.Map.Get(pos);
            return tile != null && tile.IsSolidGround;
        }

        private Sprite GetWallSprite(bool torch)
        {
            string key = torch ? "rear-wall-torch" : "rear-wall";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 32;
            const int height = 56;
            const int wallHeight = 40;
            var texture = NewTexture(width, height);
            Color32 stone = new Color32(46, 52, 56, 255);
            Color32 stoneLight = new Color32(63, 68, 70, 255);
            Color32 stoneDark = new Color32(30, 36, 41, 255);

            // 바닥 모서리와 같은 2:1 경사를 가진 평행사변형 벽 패널.
            // 인접 타일의 패널 끝점이 이어져 회전해도 하나의 석벽처럼 보인다.
            for (int px = 0; px < width; px++)
            {
                int bottom = 16 - px / 2;
                for (int localY = 0; localY < wallHeight; localY++)
                {
                    int py = bottom + localY;
                    bool edge = px == 0 || px == width - 1 || localY <= 1 || localY >= wallHeight - 2;
                    bool mortar = localY == 13 || localY == 26 ||
                                  (localY < 13 && px == 16) ||
                                  (localY >= 13 && localY < 26 && (px == 8 || px == 24)) ||
                                  (localY >= 26 && px == 16);
                    bool topCap = localY >= wallHeight - 5;
                    Color32 color = edge || mortar
                        ? outline
                        : topCap ? stoneLight : ((px + localY) % 11 == 0 ? Shift(stone, 7) : stone);
                    texture.SetPixel(px, py, color);
                }
            }

            if (torch)
            {
                FillRect(texture, 13, 20, 6, 3, stoneDark);
                FillRect(texture, 15, 15, 3, 12, new Color32(79, 53, 34, 255));
                FillRect(texture, 11, 27, 11, 5, new Color32(235, 116, 35, 255));
                FillRect(texture, 13, 30, 7, 8, new Color32(255, 202, 72, 255));
                FillRect(texture, 15, 34, 3, 7, new Color32(255, 238, 143, 255));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 8f / height));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetSelectionSprite()
        {
            const string key = "selection";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool ring = diamond > 0.77f && diamond <= 0.94f;
                texture.SetPixel(px, py, ring ? new Color32(255, 177, 72, 230) : new Color32(0, 0, 0, 0));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetPlayerFootprintSprite()
        {
            const string key = "player-footprint";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 glow = new Color32(77, 232, 219, 235);
            Color32 core = new Color32(220, 255, 246, 255);
            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool outer = diamond > 0.82f && diamond <= 0.96f;
                bool tick = (px < 10 || px > 53) && diamond > 0.65f && diamond <= 0.98f;
                if (outer || tick)
                    texture.SetPixel(px, py, outer && (px + py) % 5 == 0 ? core : glow);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetPlayerLocatorSprite()
        {
            const string key = "player-locator";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(24, 24);
            Color32 glow = new Color32(94, 242, 219, 255);
            Color32 core = new Color32(224, 255, 239, 255);
            for (int y = 5; y < 18; y++)
            {
                int half = (17 - y) / 2;
                for (int x = 12 - half; x <= 12 + half; x++)
                    texture.SetPixel(x, y, y > 13 ? glow : core);
            }
            FillRect(texture, 10, 2, 5, 4, glow);
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetProjectileSprite()
        {
            const string key = "ranged-projectile";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(16, 16);
            FillRect(texture, 2, 6, 12, 4, new Color32(45, 94, 91, 220));
            FillRect(texture, 5, 7, 8, 3, new Color32(104, 244, 220, 255));
            FillRect(texture, 10, 6, 4, 5, new Color32(238, 255, 226, 255));
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetDoorInteractionSprite(bool opening)
        {
            string key = opening ? "door-open-burst" : "door-close-burst";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(48, 48);
            Color32 edge = opening
                ? new Color32(111, 245, 205, 255)
                : new Color32(255, 160, 72, 255);
            Color32 core = new Color32(255, 239, 166, 255);
            for (int i = 5; i < 43; i++)
            {
                if (i % 3 == 0)
                {
                    texture.SetPixel(i, 8, edge);
                    texture.SetPixel(i, 39, edge);
                    texture.SetPixel(8, i, edge);
                    texture.SetPixel(39, i, edge);
                }
            }
            FillRect(texture, 22, 3, 4, 9, core);
            FillRect(texture, 22, 36, 4, 9, core);
            FillRect(texture, 3, 22, 9, 4, core);
            FillRect(texture, 36, 22, 9, 4, core);

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetShaftSprite(bool hole)
        {
            string key = hole ? "shaft-hole" : "shaft-stairs";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(20, 64);
            Color32 edge = hole
                ? new Color32(67, 224, 211, 220)
                : new Color32(239, 139, 55, 220);
            Color32 core = hole
                ? new Color32(173, 255, 242, 255)
                : new Color32(255, 220, 126, 255);
            for (int y = 0; y < 64; y++)
            {
                if (y % 8 < 5)
                {
                    texture.SetPixel(3, y, edge);
                    texture.SetPixel(16, y, edge);
                }
                if (y % 16 >= 10 && y % 16 <= 12)
                {
                    for (int x = 7; x <= 12; x++) texture.SetPixel(x, y, core);
                    texture.SetPixel(6, y + 1, edge);
                    texture.SetPixel(13, y + 1, edge);
                }
            }
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetShaftEndpointSprite(bool hole, bool arrival)
        {
            string key = $"shaft-end-{hole}-{arrival}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(TilePixelWidth, TilePixelHeight);
            Color32 edge = hole
                ? new Color32(62, 226, 214, 245)
                : new Color32(246, 144, 57, 245);
            Color32 core = hole
                ? new Color32(203, 255, 244, 255)
                : new Color32(255, 230, 155, 255);

            for (int py = 0; py < TilePixelHeight; py++)
            for (int px = 0; px < TilePixelWidth; px++)
            {
                float diamond = Mathf.Abs((px - 31.5f) / 32f) + Mathf.Abs((py - 15.5f) / 16f);
                bool ring = diamond > 0.72f && diamond <= 0.94f;
                if (ring && (!arrival || (px + py) % 5 < 3))
                    texture.SetPixel(px, py, edge);
            }

            int arrowY = arrival ? 7 : 18;
            FillRect(texture, 29, arrowY, 6, 7, core);
            if (arrival)
            {
                FillRect(texture, 26, 7, 12, 3, core);
                FillRect(texture, 28, 10, 8, 3, core);
            }
            else
            {
                FillRect(texture, 26, 23, 12, 3, core);
                FillRect(texture, 28, 20, 8, 3, core);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetHealthBarSprite(bool filled)
        {
            string key = filled ? "health-filled" : "health-background";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 4);
            Color32 color = filled
                ? new Color32(87, 205, 96, 255)
                : new Color32(25, 29, 31, 230);

            FillRect(texture, 0, 0, texture.width, texture.height, color);
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetCharacterSprite(bool goblin)
        {
            string key = goblin ? "goblin" : "player";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 48);
            Color32 skin = goblin ? new Color32(113, 151, 62, 255) : new Color32(205, 177, 139, 255);
            Color32 body = goblin ? new Color32(94, 62, 39, 255) : new Color32(48, 90, 133, 255);
            Color32 metal = new Color32(172, 183, 183, 255);
            Color32 dark = new Color32(20, 25, 28, 255);

            // 짙은 외곽선을 먼저 그리고 내부 색을 덮어 픽셀 실루엣을 선명하게 만든다.
            FillRect(texture, 10, 2, 12, 7, dark);
            FillRect(texture, 7, 7, 18, 21, dark);
            FillRect(texture, 5, 12, 5, 15, dark);
            FillRect(texture, 22, 12, 5, 15, dark);
            FillRect(texture, 8, 25, 16, 15, dark);

            FillRect(texture, 12, 3, 4, 5, new Color32(37, 43, 47, 255));
            FillRect(texture, 17, 3, 4, 5, new Color32(31, 36, 40, 255));
            FillRect(texture, 9, 9, 14, 17, body);
            FillRect(texture, 10, 11, 3, 12, Shift(body, 22));
            FillRect(texture, 6, 14, 3, 11, skin);
            FillRect(texture, 23, 14, 3, 11, skin);
            FillRect(texture, 10, 27, 12, 11, skin);
            FillRect(texture, 12, 29, 3, 2, dark);
            FillRect(texture, 18, 29, 3, 2, dark);
            FillRect(texture, 14, 26, 5, 2, Shift(skin, 20));

            if (goblin)
            {
                FillRect(texture, 3, 31, 8, 3, dark);
                FillRect(texture, 21, 31, 8, 3, dark);
                FillRect(texture, 5, 32, 6, 2, skin);
                FillRect(texture, 21, 32, 6, 2, skin);
                FillRect(texture, 11, 35, 10, 3, Shift(skin, -12));
                FillRect(texture, 12, 17, 8, 3, new Color32(137, 78, 39, 255));
            }
            else
            {
                FillRect(texture, 8, 31, 16, 5, metal);
                FillRect(texture, 11, 35, 10, 5, new Color32(116, 129, 134, 255));
                FillRect(texture, 14, 35, 2, 4, Shift(metal, 30));
                FillRect(texture, 22, 10, 7, 15, dark);
                FillRect(texture, 23, 11, 5, 13, new Color32(47, 88, 126, 255));
                FillRect(texture, 24, 13, 2, 9, new Color32(74, 132, 177, 255));
                FillRect(texture, 4, 8, 2, 24, metal);
                FillRect(texture, 2, 28, 6, 2, new Color32(210, 160, 60, 255));
                FillRect(texture, 12, 16, 8, 3, new Color32(181, 142, 58, 255));
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetBarrelSprite()
        {
            const string key = "barrel";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(24, 32);
            Color32 wood = new Color32(140, 65, 41, 255);
            Color32 bright = new Color32(194, 92, 48, 255);
            Color32 band = new Color32(50, 43, 39, 255);
            FillRect(texture, 5, 3, 14, 24, wood);
            FillRect(texture, 7, 5, 4, 20, bright);
            FillRect(texture, 4, 6, 16, 3, band);
            FillRect(texture, 4, 21, 16, 3, band);
            FillRect(texture, 9, 13, 6, 6, new Color32(229, 177, 60, 255));
            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.08f));
            _spriteCache[key] = cached;
            return cached;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Prototype {width}x{height}"
            };

            var clear = new Color32[width * height];
            texture.SetPixels32(clear);
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture, Vector2 pivot)
        {
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                pivot,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private static void FillSlantedPanel(
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            int panelHeight,
            Color32 baseColor,
            Color32 lightColor,
            Color32 borderColor)
        {
            int minX = Mathf.Min(x0, x1);
            int maxX = Mathf.Max(x0, x1);
            int span = Mathf.Max(1, maxX - minX);
            for (int x = minX; x <= maxX; x++)
            {
                float t = (x - minX) / (float)span;
                int bottom = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int localY = 0; localY <= panelHeight; localY++)
                {
                    int y = bottom + localY;
                    bool border = x <= minX + 1 || x >= maxX - 1 || localY <= 1 || localY >= panelHeight - 1;
                    bool plankLight = !border && (x - minX) % 8 < 2;
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        texture.SetPixel(x, y, border ? borderColor : plankLight ? lightColor : baseColor);
                }
            }
        }

        private static void DrawThickLine(
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            int thickness,
            Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int radius = Mathf.Max(0, thickness / 2);

            while (true)
            {
                FillRect(texture, x0 - radius, y0 - radius, radius * 2 + 1, radius * 2 + 1, color);
                if (x0 == x1 && y0 == y1) break;
                int twiceError = error * 2;
                if (twiceError >= dy) { error += dy; x0 += sx; }
                if (twiceError <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
            {
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(px, py, color);
            }
        }

        private static bool IsCrackPixel(int x, int y)
        {
            return (x >= 28 && x <= 34 && y == 14 + (x % 3)) ||
                   (y >= 9 && y <= 15 && x == 29 - (y % 2) * 3) ||
                   (y >= 15 && y <= 20 && x == 35 + (y % 3));
        }

        private static Color32 Shift(Color32 color, int amount)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r + amount, 0, 255),
                (byte)Mathf.Clamp(color.g + amount, 0, 255),
                (byte)Mathf.Clamp(color.b + amount, 0, 255),
                color.a);
        }
    }
}
