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
    /// 허브 캠프 HUD: 골드/이어하기 + 상점·영웅·창고 모달.
    /// 열리는 계기는 데모의 HubInteractionRequested(NPC 옆까지 걸어간 뒤)다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HubHudController : MonoBehaviour
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
        private Button _continueButton;
        private VisualElement _dungeonModal;
        private Label _dungeonName;
        private Label _dungeonDesc;
        private Label _dungeonRoute;
        private Button _dungeonEnter;
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
        private VisualElement _heroModal;
        private Label _heroName;
        private Label _heroDesc;
        private Label _heroStats;
        private Button _heroAction;
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
        private string _heroModalId;
        private IsoTapInput _tapInput;
        private ResponsiveUiLayout _responsiveLayout;
        private DisplaySettingsPanelController _displaySettings;

        private void OnEnable()
        {
            _meta = MetaStore.LoadOrNew();

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _responsiveLayout = new ResponsiveUiLayout(
                root, root.Q<VisualElement>("hub-root"));
            _goldLabel = root.Q<Label>("hub-gold");
            _statusLabel = root.Q<Label>("hub-status");
            _continueButton = root.Q<Button>("hub-continue");
            _dungeonModal = root.Q<VisualElement>("hub-dungeon-modal");
            _dungeonName = root.Q<Label>("hub-dungeon-name");
            _dungeonDesc = root.Q<Label>("hub-dungeon-desc");
            _dungeonRoute = root.Q<Label>("hub-dungeon-route");
            _dungeonEnter = root.Q<Button>("hub-dungeon-enter");
            _menuModal = root.Q<VisualElement>("hub-menu-modal");
            _shopModal = root.Q<VisualElement>("hub-shop-modal");
            _shopGrid = root.Q<VisualElement>("hub-shop-grid");
            _shopFeedback = root.Q<Label>("hub-shop-feedback");
            _shopGold = root.Q<Label>("hub-shop-gold");
            _shopName = root.Q<Label>("hub-shop-name");
            _shopDesc = root.Q<Label>("hub-shop-desc");
            _shopBuy = root.Q<Button>("hub-shop-buy");
            _heroModal = root.Q<VisualElement>("hub-hero-modal");
            _heroName = root.Q<Label>("hub-hero-name");
            _heroDesc = root.Q<Label>("hub-hero-desc");
            _heroStats = root.Q<Label>("hub-hero-stats");
            _heroAction = root.Q<Button>("hub-hero-action");
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
            Bind(root.Q<Button>("hub-hero-close"), CloseModals);
            Bind(root.Q<Button>("hub-stash-close"), CloseModals);
            Bind(root.Q<Button>("hub-dungeon-close"), CloseModals);
            Bind(root.Q<Button>("hub-dungeon-loadout"), OpenStash);
            Bind(root.Q<Button>("hub-dungeon-catacombs"), () => SelectDungeon(DungeonCatalog.DefaultId));
            Bind(_dungeonEnter, EnterSelectedDungeon);
            Bind(_heroAction, HandleHeroAction);
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
            root.Q<Button>("hub-dungeon-flooded")?.SetEnabled(false);
            root.Q<Button>("hub-dungeon-ember")?.SetEnabled(false);
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
        }

        private void OnDisable()
        {
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
            else if (id.StartsWith("hero:")) OpenHero(id.Substring(5));
            else if (id == "dungeon-select") OpenDungeonSelect();
        }

        private void HandleFeedback(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        private void CloseModals()
        {
            CancelPreparationDrag();
            _displaySettings?.Close();
            _dungeonModal?.RemoveFromClassList("is-open");
            _menuModal?.RemoveFromClassList("is-open");
            _shopModal?.RemoveFromClassList("is-open");
            _heroModal?.RemoveFromClassList("is-open");
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
            if (_dungeonName != null) _dungeonName.text = dungeon.DisplayName;
            if (_dungeonDesc != null) _dungeonDesc.text = dungeon.Description;
            if (_dungeonRoute != null)
            {
                BackpackLayout loadout = ExpeditionLoadoutRules.CreateLayout(
                    _meta, SelectedHero);
                _dungeonRoute.text =
                    $"{dungeon.RouteLabel} · 백팩 {loadout.UsedCells}/{loadout.Capacity}칸";
            }
            if (_dungeonEnter != null)
            {
                _dungeonEnter.text = $"{dungeon.DisplayName} 진입";
                _dungeonEnter.SetEnabled(true);
            }
        }

        private void EnterSelectedDungeon()
        {
            DungeonDefinition dungeon = DungeonCatalog.ById(_selectedDungeonId);
            if (!dungeon.IsAvailable) return;
            int returned = ExpeditionLoadoutRules.Reconcile(_meta, SelectedHero);
            MetaStore.Save(_meta);
            if (returned > 0 && _statusLabel != null)
                _statusLabel.text = $"영웅 기본 지급품 공간 확보 · {returned}개 창고 복귀";
            DungeonSelection.SelectedId = dungeon.Id;
            demo?.BeginSelectedDungeon();
        }

        private static void QuitGame() => Application.Quit();

        // ── 상점 ─────────────────────────────────────────────

        private void BuildShop()
        {
            if (_shopGrid == null) return;
            _shopGrid.Clear();
            _shopSlots.Clear();
            _shopCounts.Clear();

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int price = ItemCatalog.ShopPrice(kind);
                if (price <= 0) continue;

                ItemKind captured = kind;
                var slot = new Button(() => SelectShopItem(captured)) { name = $"shop-{kind}" };
                slot.AddToClassList("inventory-slot");

                var icon = new VisualElement();
                icon.AddToClassList("resource-icon");
                icon.AddToClassList(InventoryPanelController.IconClass(kind));
                slot.Add(icon);

                var priceLabel = new Label(ItemCatalog.FormatGold(price));
                priceLabel.AddToClassList("slot-price");
                slot.Add(priceLabel);

                var owned = new Label("x0");
                owned.AddToClassList("inventory-slot-count");
                slot.Add(owned);

                _shopGrid.Add(slot);
                _shopSlots.Add(kind, slot);
                _shopCounts.Add(kind, owned);
            }
        }

        private void RefreshShop()
        {
            _meta = MetaStore.LoadOrNew();
            if (_shopGold != null) _shopGold.text = ItemCatalog.FormatGold(_meta.gold);
            foreach (var pair in _shopCounts)
                pair.Value.text = $"보유 {_meta.GetCount(pair.Key)}";
            UpdateGoldLabel();
        }

        private void SelectShopItem(ItemKind kind)
        {
            _shopSelected = kind;
            foreach (var pair in _shopSlots)
                pair.Value.EnableInClassList("selected", pair.Key == kind);

            int price = ItemCatalog.ShopPrice(kind);
            if (_shopName != null)
                _shopName.text =
                    $"{ItemCatalog.DisplayName(kind)} — {ItemCatalog.FormatGold(price)}";
            if (_shopDesc != null) _shopDesc.text = ItemCatalog.Description(kind);
            if (_shopBuy != null)
            {
                _shopBuy.SetEnabled(_meta.gold >= price);
                _shopBuy.text = $"구매 ({ItemCatalog.FormatGold(price)})";
            }
            if (_shopFeedback != null) _shopFeedback.text = "";
        }

        private void BuySelected()
        {
            int price = ItemCatalog.ShopPrice(_shopSelected);
            if (!_meta.TrySpend(price))
            {
                if (_shopFeedback != null)
                    _shopFeedback.text =
                        $"소지금이 부족하다 ({ItemCatalog.FormatGold(_meta.gold)} / " +
                        $"{ItemCatalog.FormatGold(price)})";
                return;
            }
            _meta.AddCount(_shopSelected, 1);
            MetaStore.Save(_meta);
            if (_shopFeedback != null)
                _shopFeedback.text = $"{ItemCatalog.DisplayName(_shopSelected)} 구매 완료";
            RefreshShop();
            SelectShopItem(_shopSelected);
            UpdateGoldLabel();
        }

        // ── 영웅 ─────────────────────────────────────────────

        private void OpenHero(string heroId)
        {
            _heroModalId = heroId;
            HeroArchetype hero = HeroRoster.ById(heroId);
            bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);
            bool selected = (HeroSelection.SelectedId ?? HeroRoster.All[0].Id) == hero.Id;

            if (_heroName != null) _heroName.text = hero.DisplayName;
            if (_heroDesc != null) _heroDesc.text = hero.Description;
            if (_heroStats != null)
                _heroStats.text = $"HP {hero.MaxHp} · 근접 {hero.Attack} · 원거리 {hero.RangedDamage}" +
                                  (unlocked
                                      ? ""
                                      : $" · 해금 {ItemCatalog.FormatGold(hero.UnlockCost)}");
            if (_heroAction != null)
            {
                _heroAction.text = !unlocked
                    ? $"해금 ({ItemCatalog.FormatGold(hero.UnlockCost)})"
                    : selected ? "선택됨"
                    : "선택";
                _heroAction.SetEnabled(!selected || !unlocked);
            }
            _heroModal?.AddToClassList("is-open");
        }

        private void HandleHeroAction()
        {
            if (string.IsNullOrEmpty(_heroModalId)) return;
            HeroArchetype hero = HeroRoster.ById(_heroModalId);
            bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);

            if (!unlocked)
            {
                if (!_meta.TrySpend(hero.UnlockCost))
                {
                    if (_statusLabel != null)
                        _statusLabel.text =
                            $"소지금이 부족하다 ({ItemCatalog.FormatGold(_meta.gold)} / " +
                            $"{ItemCatalog.FormatGold(hero.UnlockCost)}) — 생환해서 벌어오자";
                    CloseModals();
                    return;
                }
                _meta.UnlockHero(hero.Id);
                MetaStore.Save(_meta);
                if (_statusLabel != null) _statusLabel.text = $"{hero.DisplayName} 해금!";
            }

            HeroSelection.SelectedId = hero.Id;
            int returned = ExpeditionLoadoutRules.Reconcile(_meta, hero);
            MetaStore.Save(_meta);
            demo?.RefreshHubHeroLocks();
            if (_statusLabel != null)
                _statusLabel.text = returned > 0
                    ? $"{hero.DisplayName} 합류 · 공간 조정으로 {returned}개 창고 복귀"
                    : $"{hero.DisplayName} 합류 · 이전 영웅은 대기 위치로 복귀";
            UpdateGoldLabel();
            OpenHero(hero.Id); // 버튼 상태 갱신
        }

        // ── 창고 / 공통 ──────────────────────────────────────

        private void OpenStash()
        {
            CloseModals();
            _meta = MetaStore.LoadOrNew();
            int returned = ExpeditionLoadoutRules.Reconcile(_meta, SelectedHero);
            if (returned > 0) MetaStore.Save(_meta);
            RefreshPreparation(returned > 0
                ? $"영웅 기본 지급품 공간 확보 · {returned}개 창고 복귀"
                : "");
            _stashModal?.BringToFront();
            _stashModal?.AddToClassList("is-open");
        }

        private HeroArchetype SelectedHero =>
            HeroRoster.ById(HeroSelection.SelectedId ?? HeroRoster.All[0].Id);

        private void RefreshPreparation(string feedback = null)
        {
            if (_stashGold != null) _stashGold.text = ItemCatalog.FormatGold(_meta.gold);
            RebuildStashGrid();
            RebuildLoadoutGrid();
            ApplyPreparationSelection();
            if (_preparationFeedback != null && feedback != null)
                _preparationFeedback.text = feedback;
            UpdateGoldLabel();
        }

        private void RebuildStashGrid()
        {
            if (_stashGrid == null) return;
            _stashGrid.Clear();
            _stashSlots.Clear();
            int occupiedSlots = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                if (ItemCatalog.IsTreasure(kind)) continue;
                int count = _meta.GetCount(kind);
                if (count <= 0) continue;

                ItemKind captured = kind;
                Button slot = InventoryPanelController.CreateItemSlot(
                    kind, count, null, $"stash-{kind}");
                slot.clicked += () => HandlePreparationSlotClicked(
                    captured, PreparationSelectionSource.Stash, slot);
                RegisterPreparationDrag(slot, DragSource.Stash, kind);
                _stashGrid.Add(slot);
                _stashSlots.Add(kind, slot);
                occupiedSlots++;
            }

            for (int i = occupiedSlots; i < InventoryPanelController.StashSlotCount; i++)
                _stashGrid.Add(InventoryPanelController.CreateEmptySlot($"stash-empty-{i}"));

            if (_stashCapacity != null)
                _stashCapacity.text =
                    $"{occupiedSlots} / {InventoryPanelController.StashSlotCount} 칸";
        }

        private void RebuildLoadoutGrid()
        {
            if (_loadoutGrid == null) return;
            _loadoutGrid.Clear();
            _loadoutSlots.Clear();
            _starterSlots.Clear();
            InventoryPanelController.PopulateBackpackCells(_loadoutGrid, "loadout-cell");

            HeroArchetype hero = SelectedHero;
            BackpackLayout layout = ExpeditionLoadoutRules.CreateLayout(_meta, hero);
            foreach (BackpackPlacement placement in layout.Placements)
            {
                ItemKind kind = placement.Kind;
                bool starter =
                    placement.InstanceIndex < ExpeditionLoadoutRules.StarterCount(hero, kind);
                ItemKind captured = kind;
                PreparationSelectionSource source = starter
                    ? PreparationSelectionSource.Starter
                    : PreparationSelectionSource.Loadout;

                Button slot = InventoryPanelController.CreateItemSlot(
                    kind, 1, null, $"loadout-{kind}-{placement.InstanceIndex}");
                slot.AddToClassList("backpack-item");
                slot.AddToClassList(
                    $"backpack-size-{placement.Footprint.Width}x{placement.Footprint.Height}");
                slot.tooltip = starter
                    ? $"{ItemCatalog.DisplayName(kind)} · 영웅 기본 지급품"
                    : $"{ItemCatalog.DisplayName(kind)} · 원정 반입";
                slot.clicked += () => HandlePreparationSlotClicked(captured, source, slot);
                if (starter)
                {
                    slot.AddToClassList("loadout-starter");
                    var badge = new Label("기본");
                    badge.AddToClassList("loadout-starter-badge");
                    slot.Add(badge);
                    AddPreparationSlot(_starterSlots, kind, slot);
                }
                else
                {
                    RegisterPreparationDrag(slot, DragSource.Loadout, kind);
                    AddPreparationSlot(_loadoutSlots, kind, slot);
                }

                InventoryPanelController.PlaceBackpackElement(
                    slot,
                    placement.X,
                    placement.Y,
                    placement.Footprint.Width,
                    placement.Footprint.Height);
                _loadoutGrid.Add(slot);
            }

            if (_loadoutCapacity != null)
                _loadoutCapacity.text = $"{layout.UsedCells} / {layout.Capacity}칸";
            if (_loadoutHero != null)
                _loadoutHero.text = $"{hero.DisplayName} 기본 지급 포함";
        }

        private static void AddPreparationSlot(
            Dictionary<ItemKind, List<Button>> slots,
            ItemKind kind,
            Button slot)
        {
            if (!slots.TryGetValue(kind, out List<Button> kindSlots))
            {
                kindSlots = new List<Button>();
                slots.Add(kind, kindSlots);
            }
            kindSlots.Add(slot);
        }

        private void HandlePreparationSlotClicked(
            ItemKind kind,
            PreparationSelectionSource source,
            Button slot)
        {
            if (_ignoreNextPreparationClick)
            {
                _ignoreNextPreparationClick = false;
                return;
            }
            _stashSelected = kind;
            _preparationSource = source;
            _selectedPreparationSlot = slot;
            ApplyPreparationSelection();
        }

        private void ApplyPreparationSelection()
        {
            foreach (Button slot in _stashSlots.Values)
                slot.RemoveFromClassList("selected");
            RemoveSelectedClass(_loadoutSlots);
            RemoveSelectedClass(_starterSlots);

            if (!_stashSelected.HasValue)
            {
                ClearPreparationSelection();
                return;
            }

            ItemKind kind = _stashSelected.Value;
            if (_preparationSource == PreparationSelectionSource.Stash)
            {
                if (!_stashSlots.TryGetValue(kind, out Button stashSlot))
                {
                    ClearPreparationSelection();
                    return;
                }
                _selectedPreparationSlot = stashSlot;
            }
            else
            {
                Dictionary<ItemKind, List<Button>> source =
                    _preparationSource == PreparationSelectionSource.Starter
                        ? _starterSlots
                        : _loadoutSlots;
                if (!source.TryGetValue(kind, out List<Button> sourceSlots) ||
                    sourceSlots.Count <= 0)
                {
                    ClearPreparationSelection();
                    return;
                }
                if (_selectedPreparationSlot == null ||
                    !_selectedPreparationSlot.ClassListContains("item-grid-slot"))
                    _selectedPreparationSlot = sourceSlots[0];
                else if (!sourceSlots.Contains(_selectedPreparationSlot))
                    _selectedPreparationSlot = sourceSlots[0];
            }

            _selectedPreparationSlot?.AddToClassList("selected");
            InventoryPanelController.ApplyDetailIcon(_stashDetailIcon, kind);
            ItemFootprint footprint = BackpackRules.Footprint(kind);
            int count = _preparationSource == PreparationSelectionSource.Stash
                ? _meta.GetCount(kind)
                : _preparationSource == PreparationSelectionSource.Loadout
                    ? _meta.GetLoadoutCount(kind)
                    : ExpeditionLoadoutRules.StarterCount(SelectedHero, kind);
            string sourceLabel = _preparationSource == PreparationSelectionSource.Stash
                ? "창고"
                : _preparationSource == PreparationSelectionSource.Loadout
                    ? "출정 백팩"
                    : "영웅 기본 지급";
            if (_stashName != null)
                _stashName.text =
                    $"{ItemCatalog.DisplayName(kind)} ×{count} · {footprint}칸 · {sourceLabel}";
            if (_stashDesc != null) _stashDesc.text = ItemCatalog.Description(kind);
            if (_toLoadout != null)
                _toLoadout.SetEnabled(_preparationSource == PreparationSelectionSource.Stash);
            if (_toStash != null)
                _toStash.SetEnabled(_preparationSource == PreparationSelectionSource.Loadout);
        }

        private static void RemoveSelectedClass(Dictionary<ItemKind, List<Button>> slots)
        {
            foreach (List<Button> kindSlots in slots.Values)
            foreach (Button slot in kindSlots)
                slot.RemoveFromClassList("selected");
        }

        private void ClearPreparationSelection()
        {
            _stashSelected = null;
            _preparationSource = PreparationSelectionSource.None;
            _selectedPreparationSlot = null;
            InventoryPanelController.ApplyDetailIcon(_stashDetailIcon, null);
            if (_stashName != null) _stashName.text = "아이템을 선택하세요";
            if (_stashDesc != null)
                _stashDesc.text =
                    "모바일은 선택 후 반대쪽 빈 공간을 탭하고, PC는 드래그할 수 있습니다.";
            _toLoadout?.SetEnabled(false);
            _toStash?.SetEnabled(false);
        }

        private void MoveSelectedToLoadout()
        {
            if (!_stashSelected.HasValue ||
                _preparationSource != PreparationSelectionSource.Stash)
                return;
            MoveKindToLoadout(_stashSelected.Value);
        }

        private void MoveSelectedToStash()
        {
            if (!_stashSelected.HasValue ||
                _preparationSource != PreparationSelectionSource.Loadout)
                return;
            MoveKindToStash(_stashSelected.Value);
        }

        private void MoveKindToLoadout(ItemKind kind)
        {
            LoadoutTransferResult result =
                ExpeditionLoadoutRules.TryMoveToLoadout(_meta, SelectedHero, kind);
            if (result != LoadoutTransferResult.Success)
            {
                ShowTransferFailure(result, kind, _loadoutPane);
                return;
            }

            MetaStore.Save(_meta);
            _stashSelected = kind;
            _preparationSource = PreparationSelectionSource.Loadout;
            _selectedPreparationSlot = null;
            RefreshPreparation($"{ItemCatalog.DisplayName(kind)} → 출정 백팩");
        }

        private void MoveKindToStash(ItemKind kind)
        {
            LoadoutTransferResult result =
                ExpeditionLoadoutRules.TryMoveToStash(_meta, kind);
            if (result != LoadoutTransferResult.Success)
            {
                ShowTransferFailure(result, kind, _stashPane);
                return;
            }

            MetaStore.Save(_meta);
            _stashSelected = kind;
            _preparationSource = PreparationSelectionSource.Stash;
            _selectedPreparationSlot = null;
            RefreshPreparation($"{ItemCatalog.DisplayName(kind)} → 창고");
        }

        private void ShowTransferFailure(
            LoadoutTransferResult result,
            ItemKind kind,
            VisualElement destination)
        {
            if (_preparationFeedback != null)
            {
                _preparationFeedback.text = result == LoadoutTransferResult.NoBackpackSpace
                    ? $"공간 부족 · {ItemCatalog.DisplayName(kind)}은(는) " +
                      $"{BackpackRules.Footprint(kind)}칸 필요"
                    : "옮길 수 없는 아이템입니다";
            }
            destination?.AddToClassList("drop-invalid");
            destination?.schedule.Execute(
                () => destination.RemoveFromClassList("drop-invalid")).StartingIn(650);
        }

        private void HandleLoadoutGridPointerUp(PointerUpEvent evt)
        {
            if (_dragSource != DragSource.None || HasButtonAncestor(evt.target as VisualElement))
                return;
            if (_preparationSource != PreparationSelectionSource.Stash ||
                !_stashSelected.HasValue)
                return;
            MoveKindToLoadout(_stashSelected.Value);
            evt.StopPropagation();
        }

        private void HandleStashGridPointerUp(PointerUpEvent evt)
        {
            if (_dragSource != DragSource.None || HasButtonAncestor(evt.target as VisualElement))
                return;
            if (_preparationSource != PreparationSelectionSource.Loadout ||
                !_stashSelected.HasValue)
                return;
            MoveKindToStash(_stashSelected.Value);
            evt.StopPropagation();
        }

        private static bool HasButtonAncestor(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (current is Button) return true;
            return false;
        }

        private void RegisterPreparationDrag(Button slot, DragSource source, ItemKind kind)
        {
            slot.RegisterCallback<PointerDownEvent>(
                evt => BeginPreparationDrag(evt, slot, source, kind));
            slot.RegisterCallback<PointerMoveEvent>(UpdatePreparationDrag);
            slot.RegisterCallback<PointerUpEvent>(CompletePreparationDrag);
        }

        private void BeginPreparationDrag(
            PointerDownEvent evt,
            Button slot,
            DragSource source,
            ItemKind kind)
        {
            if (evt.button != 0 || _dragSource != DragSource.None) return;
            _dragSource = source;
            _dragKind = kind;
            _dragElement = slot;
            _dragPointerId = evt.pointerId;
            _dragStart = new Vector2(evt.position.x, evt.position.y);
            _dragMoved = false;
            slot.CapturePointer(evt.pointerId);
        }

        private void UpdatePreparationDrag(PointerMoveEvent evt)
        {
            if (_dragSource == DragSource.None || evt.pointerId != _dragPointerId) return;
            Vector2 current = new Vector2(evt.position.x, evt.position.y);
            if (!_dragMoved && (current - _dragStart).sqrMagnitude < 36f) return;
            _dragMoved = true;
            _dragElement?.AddToClassList("expedition-drag-source");

            VisualElement picked = _dragElement?.panel?.Pick(current);
            bool overLoadout = IsInside(picked, _loadoutPane);
            bool overStash = IsInside(picked, _stashPane);
            ClearDropCues();

            if (_dragSource == DragSource.Stash && overLoadout)
            {
                bool valid = ExpeditionLoadoutRules.CanMoveToLoadout(
                    _meta, SelectedHero, _dragKind);
                _loadoutPane?.AddToClassList(valid ? "drop-valid" : "drop-invalid");
            }
            else if (_dragSource == DragSource.Loadout && overStash)
            {
                _stashPane?.AddToClassList("drop-valid");
            }
        }

        private void CompletePreparationDrag(PointerUpEvent evt)
        {
            if (_dragSource == DragSource.None || evt.pointerId != _dragPointerId) return;
            DragSource source = _dragSource;
            ItemKind kind = _dragKind;
            bool moved = _dragMoved;
            Vector2 current = new Vector2(evt.position.x, evt.position.y);
            VisualElement picked = _dragElement?.panel?.Pick(current);
            bool droppedToLoadout = source == DragSource.Stash && IsInside(picked, _loadoutPane);
            bool droppedToStash = source == DragSource.Loadout && IsInside(picked, _stashPane);
            CancelPreparationDrag();

            if (!moved || !droppedToLoadout && !droppedToStash) return;
            _ignoreNextPreparationClick = true;
            _stashModal?.schedule.Execute(
                () => _ignoreNextPreparationClick = false).StartingIn(0);
            if (droppedToLoadout)
                MoveKindToLoadout(kind);
            else
                MoveKindToStash(kind);
            evt.StopPropagation();
        }

        private void CancelPreparationDrag()
        {
            if (_dragElement != null && _dragPointerId >= 0 &&
                _dragElement.HasPointerCapture(_dragPointerId))
                _dragElement.ReleasePointer(_dragPointerId);
            _dragElement?.RemoveFromClassList("expedition-drag-source");
            ClearDropCues();
            _dragSource = DragSource.None;
            _dragElement = null;
            _dragPointerId = -1;
            _dragMoved = false;
        }

        private void ClearDropCues()
        {
            _stashPane?.RemoveFromClassList("drop-valid");
            _stashPane?.RemoveFromClassList("drop-invalid");
            _loadoutPane?.RemoveFromClassList("drop-valid");
            _loadoutPane?.RemoveFromClassList("drop-invalid");
        }

        private static bool IsInside(VisualElement element, VisualElement container)
        {
            if (container == null) return false;
            for (VisualElement current = element; current != null; current = current.parent)
                if (current == container) return true;
            return false;
        }

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
