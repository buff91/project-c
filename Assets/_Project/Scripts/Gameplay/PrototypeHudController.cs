using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 프로토타입용 화면 고정 HUD. 월드 표현과 분리하고 회전 요청만 Demo에 전달한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class PrototypeHudController : MonoBehaviour
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
        private Button _verticalViewUp;
        private Button _verticalViewCurrent;
        private Button _verticalViewDown;
        private Label _verticalViewState;
        private Button _modeButton;
        private Button _combatButton;
        private Button _interactButton;
        private Button _potionButton;
        private Button _bombButton;
        private Button _frostButton;
        private Label _potionCountLabel;
        private Label _bombCountLabel;
        private Label _frostCountLabel;
        private Label _modeLabel;
        private Label _combatLabel;
        private Label _interactLabel;
        private VisualElement _combatIcon;
        private DisplaySettingsPanelController _displaySettings;
        private Label _viewLabel;
        private Label _depthLabel;
        private Label _depthCaption;
        private Label _floorLabel;
        private Label _locationLabel;
        private Label _hungerLabel;
        private Label _statusLabel;
        private Label _verticalHintLabel;
        private VisualElement _routeDiscovery;
        private Label _routeDiscoveryTitle;
        private Label _routeDiscoveryDetail;
        private Button _routeDiscoveryCloseButton;
        private readonly HudTransientNoticeQueue _transientNotices =
            new HudTransientNoticeQueue();
        private Coroutine _routeDiscoveryRoutine;
        private float _routeDiscoveryRemainingSeconds;
        private float _routeDiscoveryVisibleSince;
        private bool _routeDiscoveryIsTiming;
        private bool _routeDiscoveryIsClosing;
        private Coroutine _feedbackRoutine;
        private Label _hpValueLabel;
        private VisualElement _hpHearts;
        private VisualElement _gameoverOverlay;
        private Label _gameoverTitle;
        private Label _gameoverCause;
        private Label _gameoverFloor;
        private Label _gameoverKills;
        private Button _menuButton;
        private VisualElement _exitModal;
        private Label _exitTitle;
        private Label _exitDesc;
        private Button _exitAdvance;
        private Button _exitExtract;
        private VisualElement _bossPanel;
        private Label _bossKicker;
        private Label _bossName;
        private VisualElement _bossHealthFill;
        private Label _bossHealthValue;
        private Label _bossObjective;
        private VisualElement _minimapView;
        private Label _minimapFloorBadge;
        private Label _minimapNorthLabel;
        private Button _minimapPlayerMarker;
        private VisualElement _floorInstrument;
        private Texture2D _minimapTexture;
        private Color32[] _minimapPixels;
        private Button _waitButton;
        private VisualElement _turnPill;
        private Label _turnLabel;
        private Button _gameMenuButton;
        private VisualElement _gameMenuModal;
        private VisualElement _inventoryModal;
        private Button _menuResume;
        private Button _menuLobby;
        private Button _menuAbandon;
        private VisualElement _actionWheel;
        private ResponsiveUiLayout _responsiveLayout;
        private bool _developmentViewportRefreshRequested;
        private bool _reopenSettingsAfterViewportRefresh;
        private VerticalLookMode _lastVerticalLookMode = (VerticalLookMode)(-1);

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
                if (_tapInput != null)
                {
                    _tapInput.UiBlocker = IsPointerOverHud;
                    _tapInput.WorldCommandBlocker = IsWorldCommandBlocked;
                }
                demo.ViewRotationChanged += HandleViewRotationChanged;
                demo.ActiveFloorChanged += HandleActiveFloorChanged;
                demo.ViewModeChanged += HandleViewModeChanged;
                demo.CombatModeChanged += HandleCombatModeChanged;
                demo.InteractionFeedback += HandleInteractionFeedback;
                demo.VerticalRouteDiscovered += HandleVerticalRouteDiscovered;
                demo.DungeonEntryCue += HandleDungeonEntryCue;
                demo.PlayerPositionChanged += HandlePlayerPositionChanged;
                demo.VerticalContextChanged += HandleVerticalContextChanged;
                demo.InventoryChanged += HandleInventoryChanged;
                demo.BombAimingChanged += HandleBombAimingChanged;
                demo.PlayerHpChanged += HandlePlayerHpChanged;
                demo.BossStateChanged += HandleBossStateChanged;
                demo.RunEnded += HandleRunEnded;
                demo.ExitChoiceRequested += HandleExitChoiceRequested;
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
            CloseTacticalMap();
            UnbindTacticalMap();
            DisposeTacticalMapTexture();
            _responsiveLayout?.Dispose();
            _responsiveLayout = null;
            _displaySettings?.Dispose();
            _displaySettings = null;
            DevelopmentViewportService.Changed -= HandleDevelopmentViewportChanged;
            // null 로 재바인딩해 구독을 해제하고 필드를 비운다.
            // 필드를 남겨두면 재활성화 시 BindDocument가 같은 요소로 판단해 재구독을 건너뛴다.
            RebindButton(ref _rotateLeft, null, RotateLeft);
            RebindButton(ref _rotateRight, null, RotateRight);
            RebindButton(ref _verticalViewUp, null, LookUp);
            RebindButton(ref _verticalViewCurrent, null, LookCurrent);
            RebindButton(ref _verticalViewDown, null, LookDown);
            RebindButton(ref _modeButton, null, ToggleViewMode);
            RebindButton(ref _combatButton, null, ToggleCombatMode);
            RebindButton(ref _interactButton, null, PerformInteraction);
            RebindButton(ref _potionButton, null, UsePotion);
            RebindButton(ref _bombButton, null, ToggleBombAim);
            RebindButton(ref _frostButton, null, ToggleFrostBombAim);
            RebindButton(ref _menuButton, null, ReturnToCamp);
            RebindButton(ref _minimapPlayerMarker, null, RecenterCameraFromMinimap);
            if (_minimapView != null)
                _minimapView.UnregisterCallback<GeometryChangedEvent>(HandleMinimapGeometryChanged);
            if (demo != null)
            {
                demo.ViewRotationChanged -= HandleViewRotationChanged;
                demo.ActiveFloorChanged -= HandleActiveFloorChanged;
                demo.ViewModeChanged -= HandleViewModeChanged;
                demo.CombatModeChanged -= HandleCombatModeChanged;
                demo.InteractionFeedback -= HandleInteractionFeedback;
                demo.VerticalRouteDiscovered -= HandleVerticalRouteDiscovered;
                demo.DungeonEntryCue -= HandleDungeonEntryCue;
                demo.PlayerPositionChanged -= HandlePlayerPositionChanged;
                demo.VerticalContextChanged -= HandleVerticalContextChanged;
                demo.InventoryChanged -= HandleInventoryChanged;
                demo.BombAimingChanged -= HandleBombAimingChanged;
                demo.PlayerHpChanged -= HandlePlayerHpChanged;
                demo.BossStateChanged -= HandleBossStateChanged;
                demo.RunEnded -= HandleRunEnded;
                demo.ExitChoiceRequested -= HandleExitChoiceRequested;
            }
            RebindButton(ref _waitButton, null, HandleWaitClicked);
            RebindButton(ref _gameMenuButton, null, OpenGameMenu);
            RebindButton(ref _menuResume, null, CloseGameMenu);
            RebindButton(ref _menuLobby, null, GoToLobbyKeepingSave);
            RebindButton(ref _menuAbandon, null, AbandonRun);
            RebindButton(ref _exitAdvance, null, HandleExitAdvance);
            RebindButton(ref _exitExtract, null, HandleExitExtract);
            RebindButton(
                ref _routeDiscoveryCloseButton,
                null,
                DismissDiscoveryNotice);
            if (_tapInput != null && _tapInput.UiBlocker == IsPointerOverHud)
                _tapInput.UiBlocker = null;
            if (_tapInput != null && _tapInput.WorldCommandBlocker == IsWorldCommandBlocked)
                _tapInput.WorldCommandBlocker = null;
            _tapInput = null;
            _actionWheel?.RemoveFromClassList("is-open");
            PauseDiscoveryNoticeVisual();
            _transientNotices.Clear();
            _routeDiscoveryRemainingSeconds = 0f;
            _routeDiscoveryIsTiming = false;
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }
            _statusLabel?.parent?.RemoveFromClassList("is-open");
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
            RebindButton(ref _verticalViewUp, root.Q<Button>("vertical-view-up"), LookUp);
            RebindButton(
                ref _verticalViewCurrent,
                root.Q<Button>("vertical-view-current"),
                LookCurrent);
            RebindButton(ref _verticalViewDown, root.Q<Button>("vertical-view-down"), LookDown);
            RebindButton(ref _modeButton, root.Q<Button>("mode-button"), ToggleViewMode);
            RebindButton(ref _combatButton, root.Q<Button>("combat-button"), ToggleCombatMode);
            RebindButton(ref _interactButton, root.Q<Button>("interact-button"), PerformInteraction);
            RebindButton(ref _potionButton, root.Q<Button>("potion-button"), UsePotion);
            RebindButton(ref _bombButton, root.Q<Button>("bomb-button"), ToggleBombAim);
            RebindButton(ref _frostButton, root.Q<Button>("frost-button"), ToggleFrostBombAim);
            RebindButton(ref _menuButton, root.Q<Button>("menu-button"), ReturnToCamp);

            _viewLabel = root.Q<Label>("view-label");
            _floorInstrument = root.Q<VisualElement>("floor-instrument");
            _verticalViewState = root.Q<Label>("vertical-view-state");
            _depthLabel = root.Q<Label>("depth-label");
            _depthCaption = root.Q<Label>("depth-caption");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _hungerLabel = root.Q<Label>("hunger-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            PauseDiscoveryNoticeVisual();
            _routeDiscovery = root.Q<VisualElement>("vertical-route-discovery");
            _routeDiscoveryTitle = root.Q<Label>("route-discovery-title");
            _routeDiscoveryDetail = root.Q<Label>("route-discovery-detail");
            RebindButton(
                ref _routeDiscoveryCloseButton,
                root.Q<Button>("route-discovery-close"),
                DismissDiscoveryNotice);
            SetDiscoveryCloseInteractive(false);
            _potionCountLabel = root.Q<Label>("potion-count");
            _bombCountLabel = root.Q<Label>("bomb-count");
            _frostCountLabel = root.Q<Label>("frost-count");
            _modeLabel = root.Q<Label>("mode-label");
            _combatLabel = root.Q<Label>("combat-label");
            _interactLabel = root.Q<Label>("interact-label");
            _combatIcon = root.Q<VisualElement>("combat-icon");
            _hpValueLabel = root.Q<Label>("hp-value");
            _hpHearts = root.Q<VisualElement>("hp-hearts");
            _gameoverOverlay = root.Q<VisualElement>("gameover-overlay");
            _gameoverTitle = root.Q<Label>("gameover-title");
            _gameoverCause = root.Q<Label>("gameover-cause");
            _gameoverFloor = root.Q<Label>("gameover-floor");
            _gameoverKills = root.Q<Label>("gameover-kills");
            if (_minimapView != null)
                _minimapView.UnregisterCallback<GeometryChangedEvent>(HandleMinimapGeometryChanged);
            _minimapView = root.Q<VisualElement>("minimap-view");
            if (_minimapView != null)
                _minimapView.RegisterCallback<GeometryChangedEvent>(HandleMinimapGeometryChanged);
            _minimapFloorBadge = root.Q<Label>("minimap-floor-badge");
            _minimapNorthLabel = root.Q<Label>("minimap-north-label");
            RebindButton(
                ref _minimapPlayerMarker,
                root.Q<Button>("minimap-player-marker"),
                RecenterCameraFromMinimap);
            RebindButton(ref _waitButton, root.Q<Button>("wait-button"), HandleWaitClicked);
            _turnPill = root.Q<VisualElement>(className: "turn-pill");
            _turnLabel = _turnPill?.Q<Label>(className: "turn-label");
            RebindButton(ref _gameMenuButton, root.Q<Button>("game-menu-button"), OpenGameMenu);
            RebindButton(ref _menuResume, root.Q<Button>("menu-resume"), CloseGameMenu);
            RebindButton(ref _menuLobby, root.Q<Button>("menu-lobby"), GoToLobbyKeepingSave);
            RebindButton(ref _menuAbandon, root.Q<Button>("menu-abandon"), AbandonRun);
            _gameMenuModal = root.Q<VisualElement>("game-menu-modal");
            _inventoryModal = root.Q<VisualElement>("inventory-modal");
            _actionWheel = root.Q<VisualElement>("action-wheel");
            BuildActionWheel();
            _exitModal = root.Q<VisualElement>("exit-modal");
            _exitTitle = root.Q<Label>("exit-title");
            _exitDesc = root.Q<Label>("exit-desc");
            RebindButton(ref _exitAdvance, root.Q<Button>("exit-advance"), HandleExitAdvance);
            RebindButton(ref _exitExtract, root.Q<Button>("exit-extract"), HandleExitExtract);
            _bossPanel = root.Q<VisualElement>("boss-panel");
            _bossKicker = root.Q<Label>("boss-kicker");
            _bossName = root.Q<Label>("boss-name");
            _bossHealthFill = root.Q<VisualElement>("boss-health-fill");
            _bossHealthValue = root.Q<Label>("boss-health-value");
            _bossObjective = root.Q<Label>("boss-objective");
            BindTacticalMap(root);
            BindReadouts(root);
            _lastVerticalLookMode = (VerticalLookMode)(-1);
            UpdateMinimap();
            UpdateHpDisplay();
            UpdateViewLabel();
            UpdateVerticalViewControls();
            UpdateFloorLabel();
            UpdateModeLabel();
            UpdateCombatLabel();
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            UpdateItemLabels();
            UpdateBossPanel();
            UpdateStatusChips();
            // 로그는 컨트롤러가 들고 있으므로 해상도 전환으로 문서를 다시 지어도 살아남는다.
            RebuildMessageLog();
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
            _responsiveLayout = new ResponsiveUiLayout(
                documentRoot, contentRoot, document.panelSettings);
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

        private void LookUp()
        {
            if (demo != null) demo.LookUp();
        }

        private void LookCurrent()
        {
            if (demo != null) demo.LookCurrent();
        }

        private void LookDown()
        {
            if (demo != null) demo.LookDown();
        }

        private void RecenterCameraFromMinimap()
        {
            if (demo != null) demo.RecenterCamera();
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
            // 상태이상도 마찬가지다(빙결은 피해가 없어 HP 이벤트로도 안 잡힌다).
            // 조합이 그대로면 UpdateStatusChips 가 즉시 빠진다.
            UpdateStatusChips();
            UpdateTacticalMapAvailability();

            if (HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleMap))
            {
                ToggleTacticalMap();
                return;
            }

            if (HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.Cancel))
            {
                if (IsTacticalMapOpen)
                {
                    CloseTacticalMap();
                    return;
                }
                if (IsOpen(_actionWheel))
                {
                    _actionWheel?.RemoveFromClassList("is-open");
                    return;
                }
                if (IsOpen(_inventoryModal))
                {
                    _inventoryModal.RemoveFromClassList("is-open");
                    return;
                }
                if (_displaySettings != null && _displaySettings.IsOpen)
                {
                    _displaySettings.Close();
                    return;
                }
                if (demo != null && demo.CancelThrowAim())
                    return;
                if (demo != null && demo.CancelDropConfirmation())
                    return;
                if (demo != null && demo.CancelVerticalLook())
                    return;
                if (demo != null && demo.CancelCameraLook())
                    return;
                if (_gameMenuModal != null && _gameMenuModal.ClassListContains("is-open"))
                    CloseGameMenu();
                else
                    OpenGameMenu();
            }

            // PC 액션 휠은 Tab을 누르는 동안만 표시한다. 캐릭터 클릭 고정 경로는
            // 포인터가 몸체를 스칠 때 휠이 상시 남는 원인이어서 제거했다.
            // Cmd/Ctrl은 스크린샷·복사 같은 OS 단축키와 충돌해 사용하지 않는다.
            bool hold = _tapInput != null && _tapInput.ActionWheelHeld;
            bool shouldShow = ShouldShowActionWheel(
                hold,
                AnyModalOpen(),
                Time.frameCount,
                _wheelBlockedThroughFrame);
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

        private void HandleWaitClicked() => demo?.WaitTurn();

        internal static bool ShouldShowActionWheel(
            bool tabHeld,
            bool anyModalOpen,
            int frame,
            int blockedThroughFrame)
        {
            return tabHeld && !anyModalOpen && frame > blockedThroughFrame;
        }

        private void OpenGameMenu()
        {
            CloseTransientOverlays();
            _gameMenuModal?.BringToFront();
            _gameMenuModal?.AddToClassList("is-open");
        }

        private void CloseGameMenu() => _gameMenuModal?.RemoveFromClassList("is-open");

        public void CloseTransientOverlays()
        {
            _actionWheel?.RemoveFromClassList("is-open");
            _inventoryModal?.RemoveFromClassList("is-open");
            CloseTacticalMap();
            CloseGameMenu();
        }

        private bool AnyModalOpen()
        {
            return (_displaySettings != null && _displaySettings.IsOpen) ||
                   IsOpen(_gameMenuModal) || IsOpen(_inventoryModal) ||
                   IsTacticalMapOpen ||
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
            public string Tooltip;
            public string IconClass;
            public Action Action;
            public bool Enabled;
        }
    }
}
