using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;

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
        public IsoPrototypeDemo demo;

        private VisualElement _modal;
        private VisualElement _grid;
        private VisualElement _craftList;
        private Label _detailName;
        private Label _detailDesc;
        private Button _useButton;
        private Button _bagButton;
        private Button _closeButton;
        private readonly Dictionary<ItemKind, Button> _slots = new Dictionary<ItemKind, Button>();
        private readonly Dictionary<ItemKind, Label> _slotCounts = new Dictionary<ItemKind, Label>();
        private ItemKind _selected = ItemKind.Potion;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _modal = root.Q<VisualElement>("inventory-modal");
            _grid = root.Q<VisualElement>("inventory-grid");
            _craftList = root.Q<VisualElement>("craft-list");
            _detailName = root.Q<Label>("inventory-detail-name");
            _detailDesc = root.Q<Label>("inventory-detail-desc");
            _useButton = root.Q<Button>("inventory-use");
            _bagButton = root.Q<Button>("bag-button");
            _closeButton = root.Q<Button>("inventory-close");

            if (_bagButton != null) _bagButton.clicked += Open;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_useButton != null) _useButton.clicked += UseSelected;
            if (demo != null) demo.InventoryChanged += RefreshCounts;

            BuildSlots();
        }

        private void OnDisable()
        {
            if (_bagButton != null) _bagButton.clicked -= Open;
            if (_closeButton != null) _closeButton.clicked -= Close;
            if (_useButton != null) _useButton.clicked -= UseSelected;
            if (demo != null) demo.InventoryChanged -= RefreshCounts;
            _slots.Clear();
            _slotCounts.Clear();
        }

        private void BuildSlots()
        {
            if (_grid == null) return;
            _grid.Clear();
            _slots.Clear();
            _slotCounts.Clear();

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                ItemKind captured = kind;
                var slot = new Button(() => Select(captured)) { name = $"slot-{kind}" };
                slot.AddToClassList("inventory-slot");

                var icon = new VisualElement();
                icon.AddToClassList("resource-icon");
                icon.AddToClassList(IconClass(kind));
                slot.Add(icon);

                var count = new Label("×0");
                count.AddToClassList("inventory-slot-count");
                slot.Add(count);

                _grid.Add(slot);
                _slots.Add(kind, slot);
                _slotCounts.Add(kind, count);
            }

            RefreshCounts();
            Select(_selected);
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

        private void Open()
        {
            RefreshCounts();
            Select(_selected);
            _modal?.AddToClassList("is-open");
        }

        private void Close() => _modal?.RemoveFromClassList("is-open");

        private void Select(ItemKind kind)
        {
            _selected = kind;
            foreach (KeyValuePair<ItemKind, Button> pair in _slots)
                pair.Value.EnableInClassList("selected", pair.Key == kind);

            int count = CountOf(kind);
            if (_detailName != null)
                _detailName.text = $"{ItemCatalog.DisplayName(kind)} ×{count}";
            if (_detailDesc != null)
                _detailDesc.text = ItemCatalog.Description(kind);
            if (_useButton != null)
            {
                bool treasure = ItemCatalog.IsTreasure(kind);
                bool material = ItemCatalog.IsMaterial(kind);
                _useButton.SetEnabled(count > 0 && !treasure && !material);
                _useButton.text = treasure ? "생환 시 환금"
                    : material ? "조합 재료"
                    : kind == ItemKind.Potion ? "마시기"
                    : kind == ItemKind.RecallScroll ? "사용하기"
                    : "조준하기";
            }
        }

        private void UseSelected()
        {
            if (demo == null || CountOf(_selected) <= 0) return;

            if (ItemCatalog.IsTreasure(_selected) || ItemCatalog.IsMaterial(_selected)) return;

            Close();
            switch (_selected)
            {
                case ItemKind.Potion:
                    demo.UsePotion();
                    break;
                case ItemKind.RecallScroll:
                    demo.UseRecallScroll();
                    break;
                default:
                    demo.ToggleAim(_selected);
                    break;
            }
        }

        private int CountOf(ItemKind kind) => demo != null ? demo.ItemCount(kind) : 0;

        private void RefreshCounts()
        {
            foreach (KeyValuePair<ItemKind, Label> pair in _slotCounts)
            {
                int count = CountOf(pair.Key);
                pair.Value.text = $"×{count}";
                _slots[pair.Key].EnableInClassList("empty", count == 0);
            }
            RefreshCraftList();
            if (_modal != null && _modal.ClassListContains("is-open"))
                Select(_selected);
        }

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
