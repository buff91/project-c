using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 인벤토리 화면 (UI Toolkit 모달 — docs/UI_ARCHITECTURE.md).
    /// 슬롯은 ItemCatalog.AllKinds 에서 생성하므로 아이템 종류 추가 시 UXML 수정이 없다.
    /// HUD 와 UIDocument 를 공유하되 코드는 분리한다 (DebugPanelController 패턴).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryPanelController : MonoBehaviour
    {
        public const int BackpackColumns = BackpackRules.Columns;
        public const int BackpackRows = BackpackRules.Rows;
        public const int BackpackSlotCount = BackpackRules.Capacity;
        public const int StashSlotCount = 48;
        /// <summary>
        /// 백팩 한 칸의 논리 피치. 640×360 캔버스 기준이다 — 옛 960×540 시절의 56 을 그대로
        /// 두면 6×4 격자가 336×224 로 카드 밖으로 나간다. 40 이면 240×160 이라 상세 페인까지
        /// 한 화면에 든다. 화면에서 차지하는 비율은 오히려 커졌다(56/540 = 10.4% → 40/360 = 11.1%).
        /// </summary>
        public const int BackpackCellPitch = 40;
        public const int BackpackCellInset = 2;

        public IsoPrototypeDemo demo;

        private VisualElement _root;
        private VisualElement _modal;
        private VisualElement _grid;
        private VisualElement _craftList;
        private VisualElement _detailIcon;
        private Label _capacityLabel;
        private Label _detailName;
        private Label _detailDesc;
        private VisualElement _detailStats;
        private VisualElement _capacityBar;
        private VisualElement _capacityFill;
        private Button _useButton;
        private Button _bagButton;
        private Button _closeButton;
        private PrototypeHudController _hudController;
        private readonly Dictionary<ItemKind, List<Button>> _slots =
            new Dictionary<ItemKind, List<Button>>();
        private ItemKind? _selected;
        private int _selectedInstanceIndex;

        private void OnEnable()
        {
            _hudController = GetComponent<PrototypeHudController>();
            if (_hudController != null)
                _hudController.DocumentChanged += BindDocument;
            BindDocument();
            if (demo != null) demo.InventoryChanged += RefreshCounts;
        }

        private void BindDocument()
        {
            UnbindDocument();
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _root = root;
            _modal = root.Q<VisualElement>("inventory-modal");
            _grid = root.Q<VisualElement>("inventory-grid");
            _craftList = root.Q<VisualElement>("craft-list");
            _detailIcon = root.Q<VisualElement>("inventory-detail-icon");
            _capacityLabel = root.Q<Label>("inventory-capacity");
            _detailName = root.Q<Label>("inventory-detail-name");
            _detailDesc = root.Q<Label>("inventory-detail-desc");
            _detailStats = root.Q<VisualElement>("inventory-detail-stats");
            _capacityBar = root.Q<VisualElement>("inventory-capacity-bar");
            _capacityFill = root.Q<VisualElement>("inventory-capacity-fill");
            _useButton = root.Q<Button>("inventory-use");
            _bagButton = root.Q<Button>("bag-button");
            _closeButton = root.Q<Button>("inventory-close");

            if (_bagButton != null) _bagButton.clicked += Open;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_useButton != null) _useButton.clicked += UseSelected;

            RefreshInventory();
        }

        private void OnDisable()
        {
            if (_hudController != null)
                _hudController.DocumentChanged -= BindDocument;
            _hudController = null;
            UnbindDocument();
            if (demo != null) demo.InventoryChanged -= RefreshCounts;
        }

        private void Update()
        {
            if (!Application.isPlaying || !InventoryPressed()) return;
            if (_modal != null && _modal.ClassListContains("is-open"))
                Close();
            else if (!AnyOtherModalOpen())
                Open();
        }

        private void UnbindDocument()
        {
            if (_bagButton != null) _bagButton.clicked -= Open;
            if (_closeButton != null) _closeButton.clicked -= Close;
            if (_useButton != null) _useButton.clicked -= UseSelected;
            _root = null;
            _modal = null;
            _grid = null;
            _craftList = null;
            _detailIcon = null;
            _capacityLabel = null;
            _detailName = null;
            _detailDesc = null;
            _detailStats = null;
            _capacityBar = null;
            _capacityFill = null;
            _useButton = null;
            _bagButton = null;
            _closeButton = null;
            _slots.Clear();
        }

        private static bool InventoryPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.iKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private bool AnyOtherModalOpen()
        {
            if (_root == null) return false;
            List<VisualElement> modals =
                _root.Query<VisualElement>(className: "settings-modal").ToList();
            foreach (VisualElement modal in modals)
            {
                if (modal != _modal && modal.ClassListContains("is-open"))
                    return true;
            }
            return false;
        }

        private void RebuildSlots()
        {
            if (_grid == null) return;
            _grid.Clear();
            _slots.Clear();

            BackpackLayout layout = demo != null
                ? demo.CurrentBackpackLayout
                : new Inventory(BackpackColumns, BackpackRows).CreateLayout();

            PopulateBackpackCells(_grid, "slot-cell");

            ItemKind? firstOwned = null;
            int firstOwnedInstanceIndex = 0;
            foreach (BackpackPlacement placement in layout.Placements)
            {
                ItemKind kind = placement.Kind;
                ItemKind captured = kind;
                int capturedInstanceIndex = placement.InstanceIndex;
                Button slot = CreateItemSlot(
                    kind,
                    1,
                    () => Select(captured, capturedInstanceIndex),
                    $"slot-{kind}-{placement.InstanceIndex}");
                slot.userData = placement.InstanceIndex;
                slot.AddToClassList("backpack-item");
                slot.AddToClassList(
                    $"backpack-size-{placement.Footprint.Width}x{placement.Footprint.Height}");
                slot.tooltip =
                    $"{ItemCatalog.DisplayName(kind)} · {placement.Footprint}칸";
                PlaceBackpackElement(
                    slot,
                    placement.X,
                    placement.Y,
                    placement.Footprint.Width,
                    placement.Footprint.Height);
                _grid.Add(slot);
                if (!_slots.TryGetValue(kind, out List<Button> kindSlots))
                {
                    kindSlots = new List<Button>();
                    _slots.Add(kind, kindSlots);
                }
                kindSlots.Add(slot);
                if (!firstOwned.HasValue)
                {
                    firstOwned = kind;
                    firstOwnedInstanceIndex = placement.InstanceIndex;
                }
            }

            if (_capacityLabel != null)
                _capacityLabel.text = $"{layout.UsedCells} / {layout.Capacity} 칸";
            UpdateCapacityBar(layout);

            if (_selected.HasValue && CountOf(_selected.Value) > 0)
                Select(
                    _selected.Value,
                    Mathf.Clamp(_selectedInstanceIndex, 0, CountOf(_selected.Value) - 1));
            else if (firstOwned.HasValue)
                Select(firstOwned.Value, firstOwnedInstanceIndex);
            else
                ClearSelection();
        }

        public static void PopulateBackpackCells(VisualElement grid, string namePrefix)
        {
            if (grid == null) return;
            for (int y = 0; y < BackpackRows; y++)
            for (int x = 0; x < BackpackColumns; x++)
            {
                VisualElement empty = CreateEmptySlot($"{namePrefix}-{x}-{y}");
                empty.AddToClassList("backpack-cell");
                empty.pickingMode = PickingMode.Ignore;
                PlaceBackpackElement(empty, x, y, 1, 1);
                grid.Add(empty);
            }
        }

        public static void PlaceBackpackElement(
            VisualElement element,
            int x,
            int y,
            int width,
            int height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x * BackpackCellPitch + BackpackCellInset;
            element.style.top = y * BackpackCellPitch + BackpackCellInset;
            element.style.width = width * BackpackCellPitch - BackpackCellInset * 2;
            element.style.height = height * BackpackCellPitch - BackpackCellInset * 2;
            element.style.minWidth = element.style.width;
            element.style.minHeight = element.style.height;
            element.style.marginLeft = 0;
            element.style.marginRight = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        }

        /// <summary>아이템 종류 → 아이콘 USS 클래스. 허브 상점/창고 슬롯도 공유한다.</summary>
        public static string IconClass(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Bomb: return "bomb-icon";
                case ItemKind.FrostBomb: return "frost-icon";
                case ItemKind.OilFlask: return "oil-icon";
                case ItemKind.ThrowingKnife: return "knife-icon";
                case ItemKind.RecallScroll: return "scroll-icon";
                case ItemKind.CoinPouch: return "coin-icon";
                case ItemKind.Gemstone: return "gem-icon";
                case ItemKind.Relic: return "relic-icon";
                case ItemKind.Herb: return "herb-icon";
                case ItemKind.BlastPowder: return "powder-icon";
                case ItemKind.FrostShard: return "shard-icon";
                default: return "potion-icon";
            }
        }

        /// <summary>백팩과 창고가 같은 슬롯 모양과 스택 표기를 사용하도록 공용 생성한다.</summary>
        public static Button CreateItemSlot(
            ItemKind kind,
            int count,
            Action onClick,
            string elementName)
        {
            var slot = new Button(onClick) { name = elementName };
            slot.AddToClassList("inventory-slot");
            slot.AddToClassList("item-grid-slot");

            var icon = new VisualElement();
            icon.AddToClassList("resource-icon");
            icon.AddToClassList(IconClass(kind));
            slot.Add(icon);

            var countLabel = new Label(count > 1 ? count.ToString() : "");
            countLabel.AddToClassList("inventory-slot-count");
            slot.Add(countLabel);
            return slot;
        }

        public static VisualElement CreateEmptySlot(string elementName)
        {
            var slot = new VisualElement { name = elementName };
            slot.AddToClassList("inventory-slot");
            slot.AddToClassList("item-grid-slot");
            slot.AddToClassList("inventory-empty-slot");
            return slot;
        }

        public static void ApplyDetailIcon(VisualElement icon, ItemKind? kind)
        {
            if (icon == null) return;
            foreach (ItemKind candidate in ItemCatalog.AllKinds)
                icon.RemoveFromClassList(IconClass(candidate));

            icon.EnableInClassList("is-empty", !kind.HasValue);
            if (kind.HasValue)
                icon.AddToClassList(IconClass(kind.Value));
        }

        private void Open()
        {
            _hudController?.CloseTransientOverlays();
            RefreshInventory();
            _modal?.BringToFront();
            _modal?.AddToClassList("is-open");
        }

        private void Close() => _modal?.RemoveFromClassList("is-open");

        private void Select(ItemKind kind, int instanceIndex)
        {
            _selected = kind;
            _selectedInstanceIndex = instanceIndex;
            foreach (KeyValuePair<ItemKind, List<Button>> pair in _slots)
            foreach (Button slot in pair.Value)
                slot.EnableInClassList(
                    "selected",
                    pair.Key == kind &&
                    slot.userData is int slotInstanceIndex &&
                    slotInstanceIndex == instanceIndex);

            int count = CountOf(kind);
            ItemFootprint footprint = BackpackRules.Footprint(kind);
            ApplyDetailIcon(_detailIcon, kind);
            if (_detailName != null)
                _detailName.text =
                    $"{ItemCatalog.DisplayName(kind)} ×{count} · {footprint}칸";
            if (_detailDesc != null)
                _detailDesc.text = ItemCatalog.Description(kind);
            FillDetailStats(kind, footprint);
            if (_useButton != null)
            {
                bool treasure = ItemCatalog.IsTreasure(kind);
                bool material = ItemCatalog.IsMaterial(kind);
                _useButton.SetEnabled(count > 0 && ItemCatalog.IsUsable(kind));
                _useButton.text = treasure ? "생환 시 환금"
                    : material ? "조합 재료"
                    : kind == ItemKind.Potion ? "마시기"
                    : kind == ItemKind.RecallScroll ? "사용하기"
                    : kind == ItemKind.CannedFood ? "먹기"
                    : kind == ItemKind.ExtractionBeacon ? "생환하기"
                    : EquipmentCatalog.IsEquipment(kind) ? "허브에서 장착"
                    : "조준하기";
            }
        }

        /// <summary>
        /// 상세 페인의 오른쪽이 비어 있던 자리를 채운다. 값은 전부
        /// <see cref="ItemDefinition"/>에 이미 있는 것들이다 — 분류·백팩 크기·경제.
        /// 내구도/충전 게이지는 넣지 않았다: 그런 시스템이 아직 없어서 눈금을 그리면
        /// 화면이 없는 규칙을 약속하게 된다(GDD/SYSTEMS 가 먼저 정할 일이다).
        /// </summary>
        private void FillDetailStats(ItemKind kind, ItemFootprint footprint)
        {
            if (_detailStats == null) return;
            _detailStats.Clear();

            AddDetailStat("분류", CategoryLabel(ItemCatalog.CategoryOf(kind)));
            AddDetailStat("크기", $"{footprint.Width}×{footprint.Height}칸");

            if (ItemCatalog.IsTreasure(kind))
                AddDetailStat("생환 가치", $"{ItemCatalog.GoldValue(kind)}G");

            int price = ItemCatalog.ShopPrice(kind);
            if (price > 0) AddDetailStat("상점가", $"{price}G");
        }

        private void AddDetailStat(string label, string value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("inventory-stat-row");

            var name = new Label(label) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("inventory-stat-name");
            var readout = new Label(value) { pickingMode = PickingMode.Ignore };
            readout.AddToClassList("inventory-stat-value");

            row.Add(name);
            row.Add(readout);
            _detailStats.Add(row);
        }

        private static string CategoryLabel(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Consumable: return "소모품";
                case ItemCategory.Treasure: return "전리품";
                case ItemCategory.Material: return "재료";
                case ItemCategory.Equipment: return "장비";
                default: return "--";
            }
        }

        /// <summary>
        /// 점유율 게이지. 비율과 경고 판정은 <see cref="BackpackPressure"/>가 소유한다 —
        /// UI가 자체 판정하면 숫자와 막대가 다른 말을 하는 상태가 조용히 생긴다.
        /// </summary>
        private void UpdateCapacityBar(BackpackLayout layout)
        {
            if (_capacityBar == null || _capacityFill == null) return;

            float ratio = BackpackPressure.Ratio(layout.UsedCells, layout.Capacity);
            _capacityFill.style.width = Length.Percent(ratio * 100f);

            bool warning = BackpackPressure.IsWarning(layout.UsedCells, layout.Capacity);
            _capacityBar.EnableInClassList("is-warning", warning);
            _capacityFill.EnableInClassList("is-warning", warning);
        }

        private void ClearSelection()
        {
            _selected = null;
            _selectedInstanceIndex = 0;
            foreach (List<Button> kindSlots in _slots.Values)
            foreach (Button slot in kindSlots)
                slot.RemoveFromClassList("selected");

            ApplyDetailIcon(_detailIcon, null);
            if (_detailName != null) _detailName.text = "빈 백팩";
            if (_detailDesc != null)
                _detailDesc.text = "던전에서 얻은 아이템이 이곳에 쌓입니다.";
            _detailStats?.Clear();
            if (_useButton != null)
            {
                _useButton.text = "사용";
                _useButton.SetEnabled(false);
            }
        }

        private void UseSelected()
        {
            if (demo == null || !_selected.HasValue || CountOf(_selected.Value) <= 0) return;

            ItemKind selected = _selected.Value;
            if (!ItemCatalog.IsUsable(selected)) return; // 전리품·재료·장비는 쓰지 않는다

            Close();
            switch (selected)
            {
                case ItemKind.Potion:
                    demo.UsePotion();
                    break;
                case ItemKind.RecallScroll:
                    demo.UseRecallScroll();
                    break;
                case ItemKind.CannedFood:
                    demo.EatFood();
                    break;
                case ItemKind.ExtractionBeacon:
                    demo.UseExtractionBeacon();
                    break;
                default:
                    demo.ToggleAim(selected);
                    break;
            }
        }

        private int CountOf(ItemKind kind) => demo != null ? demo.ItemCount(kind) : 0;

        private void RefreshInventory()
        {
            RebuildSlots();
            RefreshCraftList();
        }

        private void RefreshCounts() => RefreshInventory();

        /// <summary>레시피 3종을 항상 보여주고, 재료가 갖춰진 것만 조합 버튼을 활성화한다.</summary>
        private void RefreshCraftList()
        {
            if (_craftList == null || demo == null) return;
            _craftList.Clear();

            for (int i = 0; i < CraftingRules.Recipes.Length; i++)
            {
                Recipe recipe = CraftingRules.Recipes[i];
                var row = new VisualElement();
                row.AddToClassList("craft-row");

                string ingredients = recipe.IsPair
                    ? $"{ItemCatalog.DisplayName(recipe.IngredientA)} ×2"
                    : $"{ItemCatalog.DisplayName(recipe.IngredientA)} + {ItemCatalog.DisplayName(recipe.IngredientB)}";
                var label = new Label($"{ingredients} → {ItemCatalog.DisplayName(recipe.Output)}");
                label.AddToClassList("craft-row-label");
                row.Add(label);

                bool craftable = HasIngredients(recipe);
                row.EnableInClassList("uncraftable", !craftable);

                int recipeIndex = i;
                var button = new Button(() =>
                {
                    Close(); // 조합은 행동 1회 — 적 턴이 돌므로 닫는다
                    demo.CraftRecipe(recipeIndex);
                }) { text = "조합" };
                button.AddToClassList("craft-button");
                button.SetEnabled(craftable);
                row.Add(button);

                _craftList.Add(row);
            }
        }

        private bool HasIngredients(Recipe recipe)
        {
            return recipe.IsPair
                ? CountOf(recipe.IngredientA) >= 2
                : CountOf(recipe.IngredientA) >= 1 && CountOf(recipe.IngredientB) >= 1;
        }
    }
}
