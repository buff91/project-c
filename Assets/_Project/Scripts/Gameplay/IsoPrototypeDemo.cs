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
        [Tooltip("서로 보이는 상태에서 이 거리(체비셰프) 안이면 추격을 시작한다.")]
        [Range(3, 10)] public int goblinAggroRange = 6;
        [Range(2, 8)] public int rangedAttackRange = 6;
        public CombatActionMode combatMode = CombatActionMode.Melee;

        [Header("M1 아이템")]
        [Min(1)] public int potionHealAmount = 4;
        [Min(1)] public int bombDamage = 3;
        [Range(2, 8)] public int bombThrowRange = 4;

        [Header("M3 다층 던전")]
        [Range(2, 5)] public int floorCount = 3;
        [Range(3, 6)] public int elevationsPerFloor = 4;
        [Tooltip("절차 생성 seed. 같은 값이면 같은 던전이 재현된다.")]
        public int dungeonSeed = 1977;
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
        public int PotionCount => _inventory.Count(ItemKind.Potion);
        public int BombCount => _inventory.Count(ItemKind.Bomb);
        public bool BombAiming => _bombAiming;
        public event System.Action<int> ViewRotationChanged;
        public event System.Action<int> ActiveFloorChanged;
        public event System.Action<DungeonViewMode> ViewModeChanged;
        public event System.Action<CombatActionMode> CombatModeChanged;
        public event System.Action<string> InteractionFeedback;
        public event System.Action PlayerPositionChanged;
        public event System.Action VerticalContextChanged;
        public event System.Action InventoryChanged;
        public event System.Action<bool> BombAimingChanged;

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
        private readonly Inventory _inventory = new Inventory();
        private bool _bombAiming;
        private GameObject _selection;
        private GridPos _selectionPos;
        private Transform _wallRoot;
        private Transform _shaftRoot;
        private Coroutine _moveRoutine;
        private Transform _playerHpFill;
        private CombatantState _playerState;
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
            _enemies.Clear();
            _items.Clear();
            _inventory.Clear();
            _bombAiming = false;
            _barrelExploded = false;

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
            InventoryChanged?.Invoke();
            BombAimingChanged?.Invoke(false);
        }

        private void BuildRoomData()
        {
            _dungeon = DungeonGenerator.Generate(
                _grid.Map,
                roomSize,
                roomSize,
                floorCount,
                elevationsPerFloor,
                dungeonSeed);
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

            // 생성기가 배치한 스폰대로 모든 층의 적과 아이템을 만든다.
            var goblinArchetype = new MonsterArchetype(
                "Goblin", goblinMaxHp, goblinAttack, goblinAggroRange, patrolRadius: 2);
            foreach (DungeonFloorInfo floor in _dungeon.Floors)
            {
                foreach (GridPos spawn in floor.EnemySpawns)
                {
                    var enemy = new EnemyAgent
                    {
                        State = new CombatantState(
                            $"Goblin {FloorLabel(floor.FloorIndex)}-{_enemies.Count + 1}",
                            spawn,
                            goblinMaxHp,
                            goblinAttack),
                        Brain = new MonsterBrain(
                            goblinArchetype, spawn, dungeonSeed * 31 + _enemies.Count)
                    };
                    enemy.Root = CreateStandingSprite(enemy.State.Id, goblinSprite, spawn, out SpriteRenderer renderer);
                    enemy.Renderer = renderer;
                    enemy.HpFill = CreateHealthBar(enemy.Root, $"{enemy.State.Id} HP");
                    UpdateHealthBar(enemy.HpFill, enemy.State);
                    _enemies.Add(enemy);
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
            _moveRoutine = StartCoroutine(DrinkPotion());
        }

        /// <summary>폭탄 조준 모드를 켜고 끈다. 켠 상태에서 타일을 탭하면 투척한다.</summary>
        public void ToggleBombAim()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (!_bombAiming && BombCount <= 0)
            {
                InteractionFeedback?.Invoke("NO BOMBS");
                return;
            }

            SetBombAiming(!_bombAiming);
            InteractionFeedback?.Invoke(_bombAiming
                ? $"BOMB: 목표 타일 탭 · 사거리 {bombThrowRange} · 3×3 폭발"
                : "BOMB AIM CANCELED");
        }

        private void SetBombAiming(bool aiming)
        {
            if (_bombAiming == aiming) return;
            _bombAiming = aiming;
            BombAimingChanged?.Invoke(aiming);
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

        private void HandleTileTapped(GridPos target, bool tileExists)
        {
            if (!Application.isPlaying || _resolvingAction || _playerState == null || !_playerState.IsAlive)
                return;

            if (viewMode == DungeonViewMode.Play &&
                !_visibleTiles.Contains(target) &&
                !_exploredTiles.Contains(target))
                return;

            if (_bombAiming)
            {
                if (!BombRules.CanThrow(_grid.Map, _playerPos, target, bombThrowRange))
                {
                    bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, target);
                    InteractionFeedback?.Invoke(blocked
                        ? "THROW BLOCKED"
                        : $"OUT OF THROW RANGE · MAX {bombThrowRange}");
                    return;
                }

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ThrowBomb(target));
                return;
            }

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

            // 폭발통 밀기 (오브젝트 상호작용). 인접까지 접근한 뒤 민다.
            if (!_barrelExploded && target == _barrelPos &&
                _dungeon.Height.FloorIndex(target.elevation) == _activeFloorIndex)
            {
                List<GridPos> pushPath = FindPathToAdjacent(_barrelPos);
                if (pushPath.Count == 0) return;

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ApproachAndPushBarrel(pushPath));
                return;
            }

            EnemyAgent tappedEnemy = FindLivingEnemyAt(target);
            if (tappedEnemy != null)
            {
                if (combatMode == CombatActionMode.Ranged)
                {
                    PositionSelection(target);
                    _moveRoutine = StartCoroutine(RangedAttack(tappedEnemy));
                    return;
                }

                List<GridPos> attackPath = FindPathToAdjacent(tappedEnemy.State.Position);
                if (attackPath.Count == 0) return;

                PositionSelection(target);
                _moveRoutine = StartCoroutine(ApproachAndAttack(attackPath, tappedEnemy));
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

        private IEnumerator ApproachAndAttack(IReadOnlyList<GridPos> path, EnemyAgent enemy)
        {
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && enemy.State.IsAlive &&
                CombatRules.TryMelee(_playerState, enemy.State, out int damage))
            {
                yield return ShowEnemyHit(enemy, damage, "Melee");
                yield return ResolveEnemyPhase();
            }

            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator RangedAttack(EnemyAgent enemy)
        {
            _resolvingAction = true;
            if (CombatRules.TryRanged(
                    _playerState,
                    enemy.State,
                    _grid.Map,
                    rangedAttackRange,
                    out int damage))
            {
                yield return AnimateProjectile(_playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke($"RANGED HIT · {damage} DAMAGE");
                yield return ShowEnemyHit(enemy, damage, "Ranged");
                if (!enemy.State.IsAlive)
                    InteractionFeedback?.Invoke("ENEMY DEFEATED");

                yield return ResolveEnemyPhase();
            }
            else
            {
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke(blocked ? "SHOT BLOCKED" : $"OUT OF RANGE · MAX {rangedAttackRange}");
            }

            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator DrinkPotion()
        {
            _resolvingAction = true;
            _inventory.TryUse(ItemKind.Potion);
            InventoryChanged?.Invoke();

            int healed = _playerState.Heal(potionHealAmount);
            UpdateHealthBar(_playerHpFill, _playerState);
            InteractionFeedback?.Invoke($"POTION +{healed} HP");
            Debug.Log($"[Item] 물약 사용: +{healed} HP → {_playerState.Hp}/{_playerState.MaxHp}");
            yield return FlashColor(_playerRenderer, new Color32(96, 224, 128, 255));

            yield return ResolveEnemyPhase();
            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator ThrowBomb(GridPos target)
        {
            _resolvingAction = true;
            SetBombAiming(false);
            _inventory.TryUse(ItemKind.Bomb);
            InventoryChanged?.Invoke();

            yield return AnimateProjectile(_playerPos, target);
            yield return ResolveExplosion(target, bombDamage);

            if (_playerState.IsAlive)
                yield return ResolveEnemyPhase();

            _resolvingAction = false;
            _moveRoutine = null;
        }

        private IEnumerator AnimateBlast(GridPos center)
        {
            var blast = new GameObject("Bomb Blast");
            blast.transform.SetParent(_visualRoot, false);
            blast.transform.position = _grid.GridToWorld(center) + Vector3.up * 0.18f;
            var renderer = blast.AddComponent<SpriteRenderer>();
            renderer.sprite = GetBlastSprite();
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
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            TileData tile = _grid.Map.Get(door);
            if (_playerPos.elevation == door.elevation && _playerPos.ManhattanTo(door) == 1 &&
                tile != null && (tile.CanOpen || tile.CanClose))
            {
                TileKind nextKind = tile.CanOpen ? TileKind.DoorOpen : TileKind.DoorClosed;
                yield return SetDoorState(door, nextKind);
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

            // 의도적 낙하도 TryFall 하나로 수렴 — 낙뎀을 감수하는 하강 수단이다. (GDD §5.3)
            GridPos? landing = _grid.Map.FindLandingBelow(hole, BottomElevation);
            if (_playerState.IsAlive && landing.HasValue &&
                _playerPos.elevation == hole.elevation && _playerPos.ManhattanTo(hole) == 1)
            {
                yield return FallPlayer(hole, "DROP");
                if (_playerState.IsAlive)
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
                if (IsLivingEnemyAt(next))
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
                TryCollectItemAt(next);

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

        /// <summary>적 피격 공통 연출: HP바 → 플래시 → 사망 시 회색 처리.</summary>
        private IEnumerator ShowEnemyHit(EnemyAgent enemy, int damage, string source)
        {
            UpdateHealthBar(enemy.HpFill, enemy.State);
            Debug.Log($"[{source}] {enemy.State.Id}에게 {damage} 피해. " +
                      $"HP {enemy.State.Hp}/{enemy.State.MaxHp}");
            yield return FlashDamage(enemy.Renderer);

            if (!enemy.State.IsAlive)
            {
                enemy.Renderer.color = new Color32(60, 64, 66, 180);
                Debug.Log($"[Combat] {enemy.State.Id} 처치");
            }
        }

        /// <summary>플레이어 피격 공통 연출. 사망 시 붉은 처리와 재시작 안내.</summary>
        private IEnumerator ShowPlayerHit(int damage, string source)
        {
            UpdateHealthBar(_playerHpFill, _playerState);
            Debug.Log($"[{source}] 플레이어가 {damage} 피해. " +
                      $"HP {_playerState.Hp}/{_playerState.MaxHp}");
            yield return FlashDamage(_playerRenderer);

            if (!_playerState.IsAlive)
            {
                _playerRenderer.color = new Color32(120, 42, 42, 220);
                Debug.Log("[Combat] 플레이어 사망 — 프로토타입을 다시 실행해 재시작");
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

        /// <summary>플레이어가 밟은 칸의 아이템을 줍는다.</summary>
        private void TryCollectItemAt(GridPos pos)
        {
            foreach (ItemAgent item in _items)
            {
                if (item.Collected || item.Spawn.Position != pos) continue;

                item.Collected = true;
                if (item.Root != null) item.Root.SetActive(false);
                int count = _inventory.Add(item.Spawn.Kind);
                InventoryChanged?.Invoke();

                string label = item.Spawn.Kind == ItemKind.Potion ? "POTION" : "BOMB";
                InteractionFeedback?.Invoke($"{label} 획득 ×{count}");
                Debug.Log($"[Item] {item.Spawn.Kind} 획득 {pos} (보유 {count})");
                return;
            }
        }

        /// <summary>적 하나의 로직 상태·AI·씬 오브젝트 묶음.</summary>
        private sealed class EnemyAgent
        {
            public CombatantState State;
            public MonsterBrain Brain;
            public GameObject Root;
            public SpriteRenderer Renderer;
            public Transform HpFill;
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
