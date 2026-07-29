using System.Collections.Generic;
using System.Text;
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
    /// 허브 캠프 HUD: 골드/이어하기 + 상점·창고·대장간·의뢰·기록실 모달(영웅 라우팅 없음).
    /// 열리는 계기는 데모의 HubInteractionRequested(NPC 옆까지 걸어간 뒤)다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class HubHudController : MonoBehaviour
    {
        private enum PreparationSelectionSource
        {
            None = 0,
            Stash = 1,
            Loadout = 2,
            Starter = 3
        }

        private enum DragSource
        {
            None = 0,
            Stash = 1,
            Loadout = 2
        }

        public IsoPrototypeDemo demo;

        private MetaSaveData _meta;
        private Label _goldLabel;
        private Label _statusLabel;
        private VisualElement _statusChip;
        private Coroutine _statusRoutine;
        private Button _continueButton;
        private VisualElement _dungeonModal;
        private Label _dungeonName;
        private Label _dungeonDesc;
        private Label _dungeonRoute;
        private Button _dungeonEnter;
        private Button _catacombsDungeonOption;
        private Button _floodedDungeonOption;
        private Button _emberDungeonOption;
        private string _selectedDungeonId = DungeonCatalog.DefaultId;
        private VisualElement _menuModal;
        private VisualElement _shopModal;
        private VisualElement _shopGrid;
        private Label _shopFeedback;
        private Label _shopGold;
        private Label _shopName;
        private Label _shopDesc;
        private Button _shopBuy;
        private ItemKind _shopSelected = ItemKind.Potion;
        private readonly System.Collections.Generic.Dictionary<ItemKind, Button> _shopSlots =
            new System.Collections.Generic.Dictionary<ItemKind, Button>();
        private readonly System.Collections.Generic.Dictionary<ItemKind, Label> _shopCounts =
            new System.Collections.Generic.Dictionary<ItemKind, Label>();
        private VisualElement _smithModal;
        private VisualElement _smithList;
        private Label _smithGold;
        private Label _smithFeedback;
        private VisualElement _bountyModal;
        private VisualElement _codexModal;
        private VisualElement _codexList;
        private Label _codexCount;
        private VisualElement _bountyList;
        private Label _bountyGold;
        private VisualElement _stashModal;
        private VisualElement _stashGrid;
        private VisualElement _loadoutGrid;
        private VisualElement _stashPane;
        private VisualElement _loadoutPane;
        private Label _stashGold;
        private Label _stashCapacity;
        private Label _loadoutCapacity;
        private Label _loadoutHero;
        private Label _preparationFeedback;
        private VisualElement _stashDetailIcon;
        private Label _stashName;
        private Label _stashDesc;
        private Button _toLoadout;
        private Button _toStash;
        private ItemKind? _stashSelected;
        private PreparationSelectionSource _preparationSource;
        private Button _selectedPreparationSlot;
        private readonly Dictionary<ItemKind, Button> _stashSlots =
            new Dictionary<ItemKind, Button>();
        private readonly Dictionary<ItemKind, List<Button>> _loadoutSlots =
            new Dictionary<ItemKind, List<Button>>();
        private readonly Dictionary<ItemKind, List<Button>> _starterSlots =
            new Dictionary<ItemKind, List<Button>>();
        private DragSource _dragSource;
        private ItemKind _dragKind;
        private Button _dragElement;
        private int _dragPointerId = -1;
        private Vector2 _dragStart;
        private bool _dragMoved;
        private bool _ignoreNextPreparationClick;
        private IsoTapInput _tapInput;
        private ResponsiveUiLayout _responsiveLayout;
        private DisplaySettingsPanelController _displaySettings;

        private void OnEnable()
        {
            _meta = MetaStore.LoadOrNew();

            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            _responsiveLayout = new ResponsiveUiLayout(
                root, root.Q<VisualElement>("hub-root"), document.panelSettings);
            _goldLabel = root.Q<Label>("hub-gold");
            _statusLabel = root.Q<Label>("hub-status");
            _statusChip = root.Q<VisualElement>("hub-status-chip");
            _continueButton = root.Q<Button>("hub-continue");
            _dungeonModal = root.Q<VisualElement>("hub-dungeon-modal");
            _dungeonName = root.Q<Label>("hub-dungeon-name");
            _dungeonDesc = root.Q<Label>("hub-dungeon-desc");
            _dungeonRoute = root.Q<Label>("hub-dungeon-route");
            _dungeonEnter = root.Q<Button>("hub-dungeon-enter");
            _catacombsDungeonOption = root.Q<Button>("hub-dungeon-catacombs");
            _floodedDungeonOption = root.Q<Button>("hub-dungeon-flooded");
            _emberDungeonOption = root.Q<Button>("hub-dungeon-ember");
            _menuModal = root.Q<VisualElement>("hub-menu-modal");
            _shopModal = root.Q<VisualElement>("hub-shop-modal");
            _shopGrid = root.Q<VisualElement>("hub-shop-grid");
            _shopFeedback = root.Q<Label>("hub-shop-feedback");
            _shopGold = root.Q<Label>("hub-shop-gold");
            _shopName = root.Q<Label>("hub-shop-name");
            _shopDesc = root.Q<Label>("hub-shop-desc");
            _shopBuy = root.Q<Button>("hub-shop-buy");
            _smithModal = root.Q<VisualElement>("hub-smith-modal");
            _smithList = root.Q<VisualElement>("hub-smith-list");
            _smithGold = root.Q<Label>("hub-smith-gold");
            _smithFeedback = root.Q<Label>("hub-smith-feedback");
            _bountyModal = root.Q<VisualElement>("hub-bounty-modal");
            _bountyList = root.Q<VisualElement>("hub-bounty-list");
            _bountyGold = root.Q<Label>("hub-bounty-gold");
            _codexModal = root.Q<VisualElement>("hub-codex-modal");
            _codexList = root.Q<VisualElement>("hub-codex-list");
            _codexCount = root.Q<Label>("hub-codex-count");
            _stashModal = root.Q<VisualElement>("hub-stash-modal");
            _stashGrid = root.Q<VisualElement>("hub-stash-grid");
            _loadoutGrid = root.Q<VisualElement>("hub-loadout-grid");
            _stashPane = root.Q<VisualElement>(className: "expedition-stash-pane");
            _loadoutPane = root.Q<VisualElement>(className: "expedition-loadout-pane");
            _stashGold = root.Q<Label>("hub-stash-gold");
            _stashCapacity = root.Q<Label>("hub-stash-capacity");
            _loadoutCapacity = root.Q<Label>("hub-loadout-capacity");
            _loadoutHero = root.Q<Label>("hub-loadout-hero");
            _preparationFeedback = root.Q<Label>("hub-prep-feedback");
            _stashDetailIcon = root.Q<VisualElement>("hub-stash-detail-icon");
            _stashName = root.Q<Label>("hub-stash-name");
            _stashDesc = root.Q<Label>("hub-stash-desc");
            _toLoadout = root.Q<Button>("hub-to-loadout");
            _toStash = root.Q<Button>("hub-to-stash");

            Bind(root.Q<Button>("hub-shop-close"), CloseModals);
            Bind(root.Q<Button>("hub-smith-close"), CloseModals);
            Bind(root.Q<Button>("hub-bounty-close"), CloseModals);
            Bind(root.Q<Button>("hub-codex-close"), CloseModals);
            Bind(root.Q<Button>("hub-stash-close"), CloseModals);
            Bind(root.Q<Button>("hub-dungeon-close"), CloseModals);
            Bind(root.Q<Button>("hub-dungeon-loadout"), OpenStash);
            Bind(_catacombsDungeonOption, () => SelectDungeon(DungeonCatalog.DefaultId));
            Bind(_floodedDungeonOption, () => SelectDungeon("flooded-vault"));
            Bind(_dungeonEnter, EnterSelectedDungeon);
            Bind(_shopBuy, BuySelected);
            Bind(_toLoadout, MoveSelectedToLoadout);
            Bind(_toStash, MoveSelectedToStash);
            Bind(_continueButton, ContinueRun);
            Bind(root.Q<Button>("hub-menu-button"), OpenMenu);
            Bind(root.Q<Button>("hub-menu-resume"), CloseModals);
            Bind(root.Q<Button>("hub-menu-quit"), QuitGame);
            _displaySettings = new DisplaySettingsPanelController(
                root, demo, "hub-settings-button", CloseModals);

            if (_continueButton != null)
                _continueButton.EnableInClassList("is-available", RunSaveStore.HasSave);
            ConfigureDungeonOption(_catacombsDungeonOption, DungeonCatalog.DefaultId);
            ConfigureDungeonOption(_floodedDungeonOption, "flooded-vault");
            ConfigureDungeonOption(_emberDungeonOption, "ember-keep");
            _stashGrid?.RegisterCallback<PointerUpEvent>(HandleStashGridPointerUp);
            _loadoutGrid?.RegisterCallback<PointerUpEvent>(HandleLoadoutGridPointerUp);

            BuildShop();
            UpdateGoldLabel();

            if (demo != null)
            {
                demo.HubInteractionRequested += HandleHubInteraction;
                demo.InteractionFeedback += HandleFeedback;
                _tapInput = demo.GetComponent<IsoTapInput>();
                if (_tapInput != null) _tapInput.UiBlocker = IsPointerOverHud;
            }

            ShowStatus("상인·대장간·의뢰·기록실·창고를 탭하고, 포탈로 걸어가면 출발");
        }

        private void OnDisable()
        {
            if (_statusRoutine != null)
            {
                StopCoroutine(_statusRoutine);
                _statusRoutine = null;
            }
            _statusChip?.RemoveFromClassList("is-open");
            CancelPreparationDrag();
            _stashGrid?.UnregisterCallback<PointerUpEvent>(HandleStashGridPointerUp);
            _loadoutGrid?.UnregisterCallback<PointerUpEvent>(HandleLoadoutGridPointerUp);
            _responsiveLayout?.Dispose();
            _responsiveLayout = null;
            _displaySettings?.Dispose();
            _displaySettings = null;
            if (demo != null)
            {
                demo.HubInteractionRequested -= HandleHubInteraction;
                demo.InteractionFeedback -= HandleFeedback;
            }
            if (_tapInput != null && _tapInput.UiBlocker == IsPointerOverHud)
                _tapInput.UiBlocker = null;
            _tapInput = null;
        }

        private static void Bind(Button button, System.Action onClick)
        {
            if (button != null) button.clicked += () => onClick();
        }

        private bool IsPointerOverHud(Vector2 screenPoint)
        {
            IPanel panel = GetComponent<UIDocument>().rootVisualElement?.panel;
            if (panel == null) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            for (VisualElement element = panel.Pick(panelPoint); element != null; element = element.parent)
            {
                if (element is Button || element is ScrollView) return true;
                if (element.ClassListContains("artifact-panel") ||
                    element.ClassListContains("settings-modal") ||
                    element.ClassListContains("status-chip"))
                    return true;
            }
            return false;
        }

        private void Update()
        {
            if (!Application.isPlaying || !EscapePressed()) return;
            if (_displaySettings != null && _displaySettings.IsOpen)
            {
                _displaySettings.Close();
                return;
            }
            if (_dungeonModal != null && _dungeonModal.ClassListContains("is-open"))
            {
                CloseModals();
                return;
            }
            if (_menuModal != null && _menuModal.ClassListContains("is-open"))
                CloseModals();
            else
                OpenMenu();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ── 상호작용 라우팅 ──────────────────────────────────

        private void HandleHubInteraction(string id)
        {
            CloseModals();
            if (id == "merchant") { RefreshShop(); SelectShopItem(_shopSelected); _shopModal?.AddToClassList("is-open"); }
            else if (id == "stash") OpenStash();
            else if (id == "smith") OpenSmith();
            else if (id == "bounty") OpenBounty();
            else if (id == "codex") OpenCodex();
            else if (id == "dungeon-select") OpenDungeonSelect();
        }

        /// <summary>
        /// 캠프 피드백 한 줄. 상시 문장이던 시절엔 화면 아래 한 줄이 영구히 튜토리얼에
        /// 묶여 있었다 — 한 번 읽으면 끝인 문장이었다. 이제 켜고 7초 뒤 닫는다.
        /// </summary>
        private void ShowStatus(string message)
        {
            if (_statusLabel == null || string.IsNullOrEmpty(message)) return;

            _statusLabel.text = message;
            _statusChip?.AddToClassList("is-open");
            if (_statusRoutine != null) StopCoroutine(_statusRoutine);
            if (isActiveAndEnabled) _statusRoutine = StartCoroutine(HideStatus());
        }

        private System.Collections.IEnumerator HideStatus()
        {
            // 던전의 발견 카드와 같은 수명 — 읽을 시간은 주되 자리를 차지하지 않는다.
            yield return new WaitForSecondsRealtime(7f);
            _statusChip?.RemoveFromClassList("is-open");
            _statusRoutine = null;
        }

        private void HandleFeedback(string message) => ShowStatus(message);

        private void CloseModals()
        {
            CancelPreparationDrag();
            _displaySettings?.Close();
            _dungeonModal?.RemoveFromClassList("is-open");
            _menuModal?.RemoveFromClassList("is-open");
            _shopModal?.RemoveFromClassList("is-open");
            _smithModal?.RemoveFromClassList("is-open");
            _bountyModal?.RemoveFromClassList("is-open");
            _codexModal?.RemoveFromClassList("is-open");
            _stashModal?.RemoveFromClassList("is-open");
        }

        private void OpenMenu()
        {
            CloseModals();
            _menuModal?.AddToClassList("is-open");
        }

        private void OpenDungeonSelect()
        {
            SelectDungeon(DungeonSelection.SelectedId);
            _dungeonModal?.BringToFront();
            _dungeonModal?.AddToClassList("is-open");
        }

        private void SelectDungeon(string dungeonId)
        {
            DungeonDefinition dungeon = DungeonCatalog.ById(dungeonId);
            if (!dungeon.IsAvailable) return;

            _selectedDungeonId = dungeon.Id;
            UpdateDungeonOptionSelection();
            if (_dungeonName != null) _dungeonName.text = dungeon.DisplayName;
            if (_dungeonDesc != null) _dungeonDesc.text = dungeon.Description;
            if (_dungeonRoute != null)
            {
                BackpackLayout loadout = ExpeditionLoadoutRules.CreateLayout(
                    _meta);
                _dungeonRoute.text =
                    $"{dungeon.RouteLabel} · 백팩 {loadout.UsedCells}/{loadout.Capacity}칸";
            }
            if (_dungeonEnter != null)
            {
                _dungeonEnter.text = $"{dungeon.DisplayName} 진입";
                _dungeonEnter.SetEnabled(true);
            }
        }

        private static void ConfigureDungeonOption(Button button, string dungeonId)
        {
            if (button == null) return;

            bool available = DungeonCatalog.ById(dungeonId).IsAvailable;
            button.SetEnabled(available);
            button.EnableInClassList("locked", !available);
        }

        private void UpdateDungeonOptionSelection()
        {
            _catacombsDungeonOption?.EnableInClassList(
                "selected", _selectedDungeonId == DungeonCatalog.DefaultId);
            _floodedDungeonOption?.EnableInClassList(
                "selected", _selectedDungeonId == "flooded-vault");
            _emberDungeonOption?.EnableInClassList(
                "selected", _selectedDungeonId == "ember-keep");
        }

        private void EnterSelectedDungeon()
        {
            DungeonDefinition dungeon = DungeonCatalog.ById(_selectedDungeonId);
            if (!dungeon.IsAvailable) return;
            int returned = ExpeditionLoadoutRules.Reconcile(_meta);
            MetaStore.Save(_meta);
            if (returned > 0)
                ShowStatus($"기본 지급품 공간 확보 · {returned}개 창고 복귀");
            DungeonSelection.SelectedId = dungeon.Id;
            demo?.BeginSelectedDungeon();
        }

        private static void QuitGame() => Application.Quit();

        private void UpdateGoldLabel()
        {
            if (_goldLabel != null) _goldLabel.text = ItemCatalog.FormatGold(_meta.gold);
        }

        private void ContinueRun()
        {
            RunSaveStore.ContinueRequested = true;
            SceneManager.LoadScene(FrontEndFlow.DungeonScene);
        }
    }
}
