using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 프로토타입용 화면 고정 HUD. 월드 표현과 분리하고 회전 요청만 Demo에 전달한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PrototypeHudController : MonoBehaviour
    {
        public IsoPrototypeDemo demo;

        [Header("HUD 프레젠테이션")]
        public HudPresentationMode presentationMode = HudPresentationMode.Auto;
        public VisualTreeAsset mobileHudAsset;
        public VisualTreeAsset desktopHudAsset;

        public HudPresentationMode ActivePresentation { get; private set; }
        public event Action DocumentChanged;

        private Button _rotateLeft;
        private Button _rotateRight;
        private Button _modeButton;
        private Button _combatButton;
        private Button _interactButton;
        private Button _potionButton;
        private Button _bombButton;
        private Button _frostButton;
        private Label _potionCountLabel;
        private Label _bombCountLabel;
        private Label _frostCountLabel;
        private DisplaySettingsPanelController _displaySettings;
        private Label _viewLabel;
        private Label _depthLabel;
        private Label _depthCaption;
        private Label _floorLabel;
        private Label _locationLabel;
        private Label _statusLabel;
        private Label _verticalHintLabel;
        private VisualElement _routeDiscovery;
        private Label _routeDiscoveryTitle;
        private Label _routeDiscoveryDetail;
        private Coroutine _routeDiscoveryRoutine;
        private Label _hpValueLabel;
        private VisualElement _hpHearts;
        private VisualElement _gameoverOverlay;
        private Label _gameoverTitle;
        private Label _gameoverCause;
        private Label _gameoverFloor;
        private Label _gameoverKills;
        private Button _restartButton;
        private Button _menuButton;
        private VisualElement _exitModal;
        private Label _exitDesc;
        private Button _exitAdvance;
        private Button _exitExtract;
        private VisualElement _minimapView;
        private Texture2D _minimapTexture;
        private Color32[] _minimapPixels;
        private Button _waitButton;
        private Button _gameMenuButton;
        private VisualElement _gameMenuModal;
        private VisualElement _inventoryModal;
        private Button _menuResume;
        private Button _menuLobby;
        private Button _menuAbandon;
        private VisualElement _actionWheel;
        private bool _wheelPinned; // 캐릭터 탭 토글로 열린 상태 (홀드와 구분)
        private ResponsiveUiLayout _responsiveLayout;
        private bool _developmentViewportRefreshRequested;
        private bool _reopenSettingsAfterViewportRefresh;

        private IsoTapInput _tapInput;

        private void OnEnable()
        {
            ApplyPresentation();
            BindDocument();
            BindResponsiveLayout();
            DevelopmentViewportService.Changed += HandleDevelopmentViewportChanged;
            if (demo != null)
            {
                _tapInput = demo.GetComponent<IsoTapInput>();
                if (_tapInput != null) _tapInput.UiBlocker = IsPointerOverHud;
                demo.ViewRotationChanged += HandleViewRotationChanged;
                demo.ActiveFloorChanged += HandleActiveFloorChanged;
                demo.ViewModeChanged += HandleViewModeChanged;
                demo.CombatModeChanged += HandleCombatModeChanged;
                demo.InteractionFeedback += HandleInteractionFeedback;
                demo.VerticalRouteDiscovered += HandleVerticalRouteDiscovered;
                demo.PlayerPositionChanged += HandlePlayerPositionChanged;
                demo.VerticalContextChanged += HandleVerticalContextChanged;
                demo.InventoryChanged += HandleInventoryChanged;
                demo.BombAimingChanged += HandleBombAimingChanged;
                demo.PlayerHpChanged += HandlePlayerHpChanged;
                demo.RunEnded += HandleRunEnded;
                demo.ExitChoiceRequested += HandleExitChoiceRequested;
                demo.PlayerTapped += HandlePlayerTapped;
            }
        }

        private void Start()
        {
            // UIDocument의 패널이 OnEnable 뒤에 준비되는 환경도 있어 한 번 더 안전하게 연결한다.
            BindDocument();
            BindResponsiveLayout();
            UpdateViewLabel();
        }

        private void OnDisable()
        {
            _responsiveLayout?.Dispose();
            _responsiveLayout = null;
            _displaySettings?.Dispose();
            _displaySettings = null;
            DevelopmentViewportService.Changed -= HandleDevelopmentViewportChanged;
            // null 로 재바인딩해 구독을 해제하고 필드를 비운다.
            // 필드를 남겨두면 재활성화 시 BindDocument가 같은 요소로 판단해 재구독을 건너뛴다.
            RebindButton(ref _rotateLeft, null, RotateLeft);
            RebindButton(ref _rotateRight, null, RotateRight);
            RebindButton(ref _modeButton, null, ToggleViewMode);
            RebindButton(ref _combatButton, null, ToggleCombatMode);
            RebindButton(ref _interactButton, null, PerformInteraction);
            RebindButton(ref _potionButton, null, UsePotion);
            RebindButton(ref _bombButton, null, ToggleBombAim);
            RebindButton(ref _frostButton, null, ToggleFrostBombAim);
            RebindButton(ref _restartButton, null, RestartRun);
            RebindButton(ref _menuButton, null, GoToMainMenu);
            if (demo != null)
            {
                demo.ViewRotationChanged -= HandleViewRotationChanged;
                demo.ActiveFloorChanged -= HandleActiveFloorChanged;
                demo.ViewModeChanged -= HandleViewModeChanged;
                demo.CombatModeChanged -= HandleCombatModeChanged;
                demo.InteractionFeedback -= HandleInteractionFeedback;
                demo.VerticalRouteDiscovered -= HandleVerticalRouteDiscovered;
                demo.PlayerPositionChanged -= HandlePlayerPositionChanged;
                demo.VerticalContextChanged -= HandleVerticalContextChanged;
                demo.InventoryChanged -= HandleInventoryChanged;
                demo.BombAimingChanged -= HandleBombAimingChanged;
                demo.PlayerHpChanged -= HandlePlayerHpChanged;
                demo.RunEnded -= HandleRunEnded;
                demo.ExitChoiceRequested -= HandleExitChoiceRequested;
                demo.PlayerTapped -= HandlePlayerTapped;
            }
            RebindButton(ref _waitButton, null, HandleWaitClicked);
            RebindButton(ref _gameMenuButton, null, OpenGameMenu);
            RebindButton(ref _menuResume, null, CloseGameMenu);
            RebindButton(ref _menuLobby, null, GoToLobbyKeepingSave);
            RebindButton(ref _menuAbandon, null, AbandonRun);
            RebindButton(ref _exitAdvance, null, HandleExitAdvance);
            RebindButton(ref _exitExtract, null, HandleExitExtract);
            if (_tapInput != null && _tapInput.UiBlocker == IsPointerOverHud)
                _tapInput.UiBlocker = null;
            _tapInput = null;
            if (_routeDiscoveryRoutine != null)
            {
                StopCoroutine(_routeDiscoveryRoutine);
                _routeDiscoveryRoutine = null;
            }
        }

        /// <summary>
        /// 탭 스크린 좌표가 HUD의 "실질" 요소 위인지. hud-root 는 풀스크린 컨테이너라
        /// 픽 결과의 조상 체인에서 컨트롤/패널류가 나올 때만 차단한다.
        /// </summary>
        private bool IsPointerOverHud(Vector2 screenPoint)
        {
            IPanel panel = GetComponent<UIDocument>().rootVisualElement?.panel;
            if (panel == null) return false;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            VisualElement picked = panel.Pick(panelPoint);

            for (VisualElement element = picked; element != null; element = element.parent)
            {
                if (element is Button || element is Slider || element is Toggle ||
                    element is ScrollView)
                    return true;
                if (element.ClassListContains("artifact-panel") ||
                    element.ClassListContains("settings-modal") ||
                    element.ClassListContains("gameover-overlay") ||
                    element.ClassListContains("status-chip") ||
                    element.ClassListContains("orb-row") ||
                    element.ClassListContains("debug-panel"))
                    return true;
            }

            return false;
        }

        private void BindDocument()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _displaySettings?.Dispose();
            _displaySettings = new DisplaySettingsPanelController(
                root, demo, "settings-button", CloseTransientOverlays);
            RebindButton(ref _rotateLeft, root.Q<Button>("rotate-left"), RotateLeft);
            RebindButton(ref _rotateRight, root.Q<Button>("rotate-right"), RotateRight);
            RebindButton(ref _modeButton, root.Q<Button>("mode-button"), ToggleViewMode);
            RebindButton(ref _combatButton, root.Q<Button>("combat-button"), ToggleCombatMode);
            RebindButton(ref _interactButton, root.Q<Button>("interact-button"), PerformInteraction);
            RebindButton(ref _potionButton, root.Q<Button>("potion-button"), UsePotion);
            RebindButton(ref _bombButton, root.Q<Button>("bomb-button"), ToggleBombAim);
            RebindButton(ref _frostButton, root.Q<Button>("frost-button"), ToggleFrostBombAim);
            RebindButton(ref _restartButton, root.Q<Button>("restart-button"), RestartRun);
            RebindButton(ref _menuButton, root.Q<Button>("menu-button"), GoToMainMenu);

            _viewLabel = root.Q<Label>("view-label");
            _depthLabel = root.Q<Label>("depth-label");
            _depthCaption = root.Q<Label>("depth-caption");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            _routeDiscovery = root.Q<VisualElement>("vertical-route-discovery");
            _routeDiscoveryTitle = root.Q<Label>("route-discovery-title");
            _routeDiscoveryDetail = root.Q<Label>("route-discovery-detail");
            _potionCountLabel = root.Q<Label>("potion-count");
            _bombCountLabel = root.Q<Label>("bomb-count");
            _frostCountLabel = root.Q<Label>("frost-count");
            _hpValueLabel = root.Q<Label>("hp-value");
            _hpHearts = root.Q<VisualElement>("hp-hearts");
            _gameoverOverlay = root.Q<VisualElement>("gameover-overlay");
            _gameoverTitle = root.Q<Label>("gameover-title");
            _gameoverCause = root.Q<Label>("gameover-cause");
            _gameoverFloor = root.Q<Label>("gameover-floor");
            _gameoverKills = root.Q<Label>("gameover-kills");
            _minimapView = root.Q<VisualElement>("minimap-view");
            RebindButton(ref _waitButton, root.Q<Button>("wait-button"), HandleWaitClicked);
            RebindButton(ref _gameMenuButton, root.Q<Button>("game-menu-button"), OpenGameMenu);
            RebindButton(ref _menuResume, root.Q<Button>("menu-resume"), CloseGameMenu);
            RebindButton(ref _menuLobby, root.Q<Button>("menu-lobby"), GoToLobbyKeepingSave);
            RebindButton(ref _menuAbandon, root.Q<Button>("menu-abandon"), AbandonRun);
            _gameMenuModal = root.Q<VisualElement>("game-menu-modal");
            _inventoryModal = root.Q<VisualElement>("inventory-modal");
            _actionWheel = root.Q<VisualElement>("action-wheel");
            BuildActionWheel();
            _exitModal = root.Q<VisualElement>("exit-modal");
            _exitDesc = root.Q<Label>("exit-desc");
            RebindButton(ref _exitAdvance, root.Q<Button>("exit-advance"), HandleExitAdvance);
            RebindButton(ref _exitExtract, root.Q<Button>("exit-extract"), HandleExitExtract);
            UpdateMinimap();
            UpdateHpDisplay();
            UpdateViewLabel();
            UpdateFloorLabel();
            UpdateModeLabel();
            UpdateCombatLabel();
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            UpdateItemLabels();
        }

        private void ApplyPresentation()
        {
            HudPresentationMode requested = DevelopmentViewportService.ResolvePresentation(
                presentationMode);
            ActivePresentation = HudPresentation.Resolve(requested, Application.isMobilePlatform);

            UIDocument document = GetComponent<UIDocument>();
            VisualTreeAsset target = ActivePresentation == HudPresentationMode.Mobile
                ? mobileHudAsset
                : desktopHudAsset;
            if (target != null && document.visualTreeAsset != target)
                document.visualTreeAsset = target;

            VisualElement contentRoot = document.rootVisualElement.Q<VisualElement>("hud-root");
            if (contentRoot == null) return;
            contentRoot.EnableInClassList(
                "hud-mobile", ActivePresentation == HudPresentationMode.Mobile);
            contentRoot.EnableInClassList(
                "hud-desktop", ActivePresentation == HudPresentationMode.Desktop);
            DocumentChanged?.Invoke();
        }

        private void BindResponsiveLayout()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement documentRoot = document.rootVisualElement;
            VisualElement contentRoot = documentRoot.Q<VisualElement>("hud-root");
            if (contentRoot == null) return;

            contentRoot.EnableInClassList(
                "hud-mobile", ActivePresentation == HudPresentationMode.Mobile);
            contentRoot.EnableInClassList(
                "hud-desktop", ActivePresentation == HudPresentationMode.Desktop);
            _responsiveLayout?.Dispose();
            _responsiveLayout = new ResponsiveUiLayout(documentRoot, contentRoot);
        }

        /// <summary>요소가 바뀐 경우에만 이전 구독을 풀고 새 요소에 다시 구독한다.</summary>
        private static void RebindButton(ref Button field, Button next, Action onClick)
        {
            if (field == next) return;
            if (field != null) field.clicked -= onClick;
            field = next;
            if (field != null) field.clicked += onClick;
        }

        private void RotateLeft()
        {
            if (demo != null) demo.RotateView(-1);
        }

        private void RotateRight()
        {
            if (demo != null) demo.RotateView(1);
        }

        private void ToggleViewMode()
        {
            if (demo != null) demo.ToggleViewMode();
        }

        private void ToggleCombatMode()
        {
            if (demo != null) demo.ToggleCombatMode();
        }

        private void UsePotion()
        {
            if (demo != null) demo.UsePotion();
        }

        private void ToggleBombAim()
        {
            if (demo != null) demo.ToggleBombAim();
        }

        private void ToggleFrostBombAim()
        {
            if (demo != null) demo.ToggleFrostBombAim();
        }

        private void HandleViewRotationChanged(int _)
        {
            UpdateViewLabel();
        }

        private void HandleActiveFloorChanged(int _)
        {
            UpdateFloorLabel();
            UpdateMinimap();
        }

        private void HandleViewModeChanged(DungeonViewMode _)
        {
            UpdateModeLabel();
        }

        private void HandleCombatModeChanged(CombatActionMode _)
        {
            UpdateCombatLabel();
        }

        private void HandleInteractionFeedback(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        private void HandleVerticalRouteDiscovered(VerticalRouteCue cue)
        {
            if (_routeDiscovery == null) return;
            if (_routeDiscoveryRoutine != null)
                StopCoroutine(_routeDiscoveryRoutine);

            if (_routeDiscoveryTitle != null) _routeDiscoveryTitle.text = cue.Title;
            if (_routeDiscoveryDetail != null) _routeDiscoveryDetail.text = cue.Detail;
            _routeDiscovery.RemoveFromClassList("route-ladder");
            _routeDiscovery.RemoveFromClassList("route-floor");
            _routeDiscovery.RemoveFromClassList("route-opening");
            switch (cue.Role)
            {
                case VerticalRouteRole.Ladder:
                    _routeDiscovery.AddToClassList("route-ladder");
                    break;
                case VerticalRouteRole.FloorUp:
                case VerticalRouteRole.FloorDown:
                    _routeDiscovery.AddToClassList("route-floor");
                    break;
                case VerticalRouteRole.OpeningUp:
                case VerticalRouteRole.OpeningDown:
                    _routeDiscovery.AddToClassList("route-opening");
                    break;
            }

            _routeDiscovery.BringToFront();
            _routeDiscovery.AddToClassList("is-open");
            _routeDiscoveryRoutine = StartCoroutine(HideVerticalRouteDiscovery());
        }

        private System.Collections.IEnumerator HideVerticalRouteDiscovery()
        {
            // 최초 발견 안내는 전투를 막지 않으므로, 월드 오브젝트와 문장을 연결해 읽을 시간을 준다.
            yield return new WaitForSecondsRealtime(7f);
            _routeDiscovery?.RemoveFromClassList("is-open");
            _routeDiscoveryRoutine = null;
        }

        private void HandlePlayerPositionChanged()
        {
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            UpdateMinimap();
        }

        private void HandleVerticalContextChanged()
        {
            UpdateVerticalHintLabel();
            // 시야 갱신마다 호출된다 — 미니맵 안개 상태의 단일 갱신 지점.
            UpdateMinimap();
        }

        private void UpdateMinimap()
        {
            if (_minimapView == null || demo == null) return;

            int size = demo.MinimapSize;
            if (size <= 0) return;
            if (_minimapTexture == null || _minimapTexture.width != size)
            {
                _minimapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                _minimapPixels = new Color32[size * size];
                _minimapView.style.backgroundImage = new StyleBackground(_minimapTexture);
            }

            if (!demo.FillMinimap(_minimapPixels, size, size)) return;
            _minimapTexture.SetPixels32(_minimapPixels);
            _minimapTexture.Apply(false);
        }

        private void HandleInventoryChanged()
        {
            UpdateItemLabels();
        }

        private void HandleBombAimingChanged(bool _)
        {
            UpdateAimHighlights();
        }

        private void HandlePlayerHpChanged()
        {
            UpdateHpDisplay();
        }

        private void UpdateHpDisplay()
        {
            CombatantState state = demo != null ? demo.PlayerState : null;
            if (_hpValueLabel != null)
                _hpValueLabel.text = state != null ? $"{state.Hp}/{state.MaxHp}" : "--/--";
            if (_hpHearts == null || state == null) return;

            int heartCount = _hpHearts.childCount;
            for (int i = 0; i < heartCount; i++)
            {
                bool filled = state.Hp * heartCount > i * state.MaxHp;
                _hpHearts[i].EnableInClassList("is-empty", !filled);
            }
        }

        // ── 게임 메뉴 / 대기 / 액션 휠 ──────────────────────

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_developmentViewportRefreshRequested)
            {
                _developmentViewportRefreshRequested = false;
                ApplyPresentation();
                BindDocument();
                BindResponsiveLayout();
                if (_reopenSettingsAfterViewportRefresh) _displaySettings?.Open();
                _reopenSettingsAfterViewportRefresh = false;
            }

            // 상호작용 대상은 이동/턴 어디서든 바뀔 수 있어 이벤트 대신 프레임 폴링한다.
            UpdateInteractButton();

            if (EscapePressed())
            {
                if (_displaySettings != null && _displaySettings.IsOpen)
                {
                    _displaySettings.Close();
                    return;
                }
                if (_gameMenuModal != null && _gameMenuModal.ClassListContains("is-open"))
                    CloseGameMenu();
                else
                    OpenGameMenu();
            }

            // Ctrl/Cmd 홀드 동안 액션 휠 표시 (캐릭터 탭 토글과 병행).
            bool hold = ModifierHeld();
            bool shouldShow = !AnyModalOpen() && (hold || _wheelPinned);
            if (_actionWheel != null)
            {
                bool isOpen = _actionWheel.ClassListContains("is-open");
                if (shouldShow && !isOpen)
                {
                    RefreshActionWheel();
                    _actionWheel.AddToClassList("is-open");
                }
                else if (!shouldShow && isOpen)
                {
                    _actionWheel.RemoveFromClassList("is-open");
                }
                if (shouldShow) PositionActionWheel();
            }
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool ModifierHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                    keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
#endif
        }

        private void HandleWaitClicked() => demo?.WaitTurn();

        private void HandlePlayerTapped()
        {
            _wheelPinned = !_wheelPinned;
        }

        private void OpenGameMenu()
        {
            CloseTransientOverlays();
            _gameMenuModal?.BringToFront();
            _gameMenuModal?.AddToClassList("is-open");
        }

        private void CloseGameMenu() => _gameMenuModal?.RemoveFromClassList("is-open");

        private void CloseTransientOverlays()
        {
            _wheelPinned = false;
            _actionWheel?.RemoveFromClassList("is-open");
            CloseGameMenu();
        }

        private bool AnyModalOpen()
        {
            return (_displaySettings != null && _displaySettings.IsOpen) ||
                   IsOpen(_gameMenuModal) || IsOpen(_inventoryModal) ||
                   IsOpen(_exitModal) || IsOpen(_gameoverOverlay);
        }

        private static bool IsOpen(VisualElement element) =>
            element != null && element.ClassListContains("is-open");

        private void HandleDevelopmentViewportChanged()
        {
            _reopenSettingsAfterViewportRefresh = _displaySettings != null && _displaySettings.IsOpen;
            _developmentViewportRefreshRequested = true;
        }

        private void GoToLobbyKeepingSave()
        {
            // 체크포인트는 층 도착마다 저장돼 있다 — 허브의 "이어하기"로 재개.
            SceneManager.LoadScene(FrontEndFlow.HubScene);
        }

        private void AbandonRun() => demo?.AbandonRun();

        // ── 액션 휠 ─────────────────────────────────────────

        private struct WheelSlot
        {
            public string Label;
            public Action Action;
            public bool Enabled;
        }

        private void BuildActionWheel()
        {
            if (_actionWheel == null) return;
            _actionWheel.Clear();
            for (int i = 0; i < 6; i++)
            {
                var button = new Button { name = $"wheel-{i}" };
                button.AddToClassList("wheel-button");
                var label = new Label { name = $"wheel-label-{i}" };
                label.AddToClassList("wheel-button-label");
                button.Add(label);
                _actionWheel.Add(button);
            }
        }

        /// <summary>지금 할 수 있는 것들로 휠 내용을 구성한다.</summary>
        private void RefreshActionWheel()
        {
            if (_actionWheel == null || demo == null) return;

            bool hasInteraction = demo.TryFindAdjacentInteraction(out _, out string interactLabel);
            var slots = new[]
            {
                new WheelSlot { Label = "대기", Action = () => demo.WaitTurn(), Enabled = true },
                new WheelSlot
                {
                    Label = hasInteraction ? interactLabel : "상호작용 없음",
                    Action = () => demo.InteractAdjacent(),
                    Enabled = hasInteraction
                },
                new WheelSlot
                {
                    Label = $"물약 {demo.PotionCount}",
                    Action = () => demo.UsePotion(),
                    Enabled = demo.PotionCount > 0
                },
                new WheelSlot
                {
                    Label = $"폭탄 조준 {demo.BombCount}",
                    Action = () => demo.ToggleBombAim(),
                    Enabled = demo.BombCount > 0
                },
                new WheelSlot
                {
                    Label = demo.CombatMode == CombatActionMode.Melee ? "원거리로" : "근접으로",
                    Action = () => demo.ToggleCombatMode(),
                    Enabled = true
                },
                new WheelSlot { Label = "메뉴", Action = OpenGameMenu, Enabled = true }
            };

            for (int i = 0; i < 6 && i < _actionWheel.childCount; i++)
            {
                var button = (Button)_actionWheel[i];
                WheelSlot slot = slots[i];
                Label label = button.Q<Label>($"wheel-label-{i}");
                if (label != null) label.text = slot.Label;
                button.SetEnabled(slot.Enabled);
                button.clickable = new Clickable(() =>
                {
                    _wheelPinned = false;
                    slot.Action();
                });
            }
        }

        /// <summary>플레이어 머리 위를 중심으로 6방향 원형 배치.</summary>
        private void PositionActionWheel()
        {
            if (_actionWheel == null || demo == null || Camera.main == null) return;
            IPanel panel = _actionWheel.panel;
            if (panel == null) return;

            // 플레이어 스크린 좌표 → 패널 좌표
            Vector3 world = Camera.main.WorldToScreenPoint(
                new Vector3(0f, 0.4f, 0f) + (Vector3)CameraFollowTarget());
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(world.x, Screen.height - world.y));

            const float radius = 96f;
            for (int i = 0; i < _actionWheel.childCount; i++)
            {
                float angle = Mathf.Deg2Rad * (90f - i * 60f); // 위에서 시계 방향 6등분
                var button = _actionWheel[i];
                button.style.left = panelPoint.x + Mathf.Cos(angle) * radius;
                button.style.top = panelPoint.y - Mathf.Sin(angle) * radius;
            }
        }

        private Vector2 CameraFollowTarget()
        {
            // 플레이어 월드 위치 (데모의 그리드 변환 재사용)
            var grid = demo.GetComponent<GridManager>();
            return grid != null ? (Vector2)grid.GridToWorld(demo.PlayerPos) : Vector2.zero;
        }

        private void HandleExitChoiceRequested()
        {
            if (_exitModal == null || demo == null) return;
            CloseTransientOverlays();
            if (_exitDesc != null)
            {
                int gold = demo.CarriedTreasureGold();
                _exitDesc.text =
                    $"들고 있는 전리품 가치: {ItemCatalog.FormatGold(gold)} · " +
                    $"다음은 던전 {demo.StageIndex + 1}";
            }
            _exitModal.BringToFront();
            _exitModal.AddToClassList("is-open");
        }

        private void HandleExitAdvance()
        {
            _exitModal?.RemoveFromClassList("is-open");
            demo?.ConfirmAdvanceStage();
        }

        private void HandleExitExtract()
        {
            _exitModal?.RemoveFromClassList("is-open");
            demo?.ExtractRun();
        }

        private void HandleRunEnded(RunSummary summary)
        {
            if (_gameoverOverlay == null) return;

            CloseTransientOverlays();

            bool survived = summary.Victory || summary.Extracted;
            _gameoverOverlay.EnableInClassList("is-victory", survived);
            if (_gameoverTitle != null)
                _gameoverTitle.text = summary.Victory ? "최심층 정복!"
                    : summary.Extracted ? "생환 성공!"
                    : "당신은 죽었습니다";
            if (_gameoverCause != null)
            {
                _gameoverCause.text = survived
                    ? (summary.GoldBanked > 0
                        ? $"+{ItemCatalog.FormatGold(summary.GoldBanked)} 창고 적립 · 소지품 보관 완료"
                        : "소지품을 창고에 보관했다")
                    : $"사인: {RunSummary.FormatCause(summary.CauseOfDeath)} — 소지품을 모두 잃었다";
                _gameoverCause.style.display = DisplayStyle.Flex;
            }
            if (_gameoverFloor != null)
                _gameoverFloor.text = $"도달 층: {IsoPrototypeDemo.FloorLabel(summary.DeepestFloorIndex)}";
            if (_gameoverKills != null)
                _gameoverKills.text = $"처치: {summary.Kills}";
            _gameoverOverlay.BringToFront();
            _gameoverOverlay.AddToClassList("is-open");
        }

        private void RestartRun()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoToMainMenu()
        {
            SceneManager.LoadScene(FrontEndFlow.MainMenuScene);
        }

        private void UpdateInteractButton()
        {
            if (_interactButton == null) return;
            string label = demo != null ? demo.ContextInteractionLabel : null;
            _interactButton.EnableInClassList("is-available", label != null);
            if (label != null) _interactButton.text = label;
        }

        private void PerformInteraction()
        {
            if (demo != null) demo.PerformContextInteraction();
        }

        private void UpdateItemLabels()
        {
            if (_potionCountLabel != null)
                _potionCountLabel.text = $"POTION ×{(demo != null ? demo.PotionCount : 0)}";
            if (_bombCountLabel != null)
                _bombCountLabel.text = $"BOMB ×{(demo != null ? demo.BombCount : 0)}";
            if (_frostCountLabel != null)
                _frostCountLabel.text = $"FROST ×{(demo != null ? demo.FrostBombCount : 0)}";
            UpdateAimHighlights();
        }

        private void UpdateAimHighlights()
        {
            bool aiming = demo != null && demo.BombAiming;
            _bombButton?.EnableInClassList("aiming", aiming && demo.AimedBombKind == ItemKind.Bomb);
            _frostButton?.EnableInClassList("aiming", aiming && demo.AimedBombKind == ItemKind.FrostBomb);
        }

        private void UpdateViewLabel()
        {
            if (_viewLabel != null)
                _viewLabel.text = $"VIEW {(demo != null ? demo.ViewQuarterTurns + 1 : 1)}/4";
        }

        private void UpdateFloorLabel()
        {
            if (_depthLabel != null)
                _depthLabel.text = demo != null ? demo.ActiveFloorLabel : "B1";
            if (_depthCaption != null)
                _depthCaption.text = demo != null ? demo.StageLabel : "던전 1/3";
            if (_floorLabel != null)
                _floorLabel.text = demo == null
                    ? "▲ --  ·  ▼ B2"
                    : $"▲ {demo.AboveFloorLabel}  ·  ▼ {demo.BelowFloorLabel}";
        }

        private void UpdateModeLabel()
        {
            if (_modeButton != null)
                _modeButton.text = demo != null && demo.ViewMode == DungeonViewMode.DebugAll
                    ? "MODE: DEBUG ALL"
                    : "MODE: PLAY FOV";
        }

        private void UpdateCombatLabel()
        {
            if (_combatButton != null)
            {
                bool ranged = demo != null && demo.CombatMode == CombatActionMode.Ranged;
                _combatButton.text = ranged
                    ? $"ATTACK: RANGED · RANGE {demo.RangedAttackRange}"
                    : "ATTACK: MELEE";
                _combatButton.EnableInClassList("ranged", ranged);
            }
        }

        private void UpdateLocationLabel()
        {
            if (_locationLabel != null)
                _locationLabel.text = demo != null ? demo.LocationLabel : "--";
        }

        private void UpdateVerticalHintLabel()
        {
            if (_verticalHintLabel != null)
                _verticalHintLabel.text = demo != null
                    ? demo.VerticalHintLabel
                    : "EXPLORE TO FIND VERTICAL ROUTES";
        }
    }
}
