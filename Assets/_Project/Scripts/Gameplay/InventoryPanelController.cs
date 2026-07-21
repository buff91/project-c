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

        private static string IconClass(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Bomb: return "bomb-icon";
                case ItemKind.FrostBomb: return "frost-icon";
                case ItemKind.OilFlask: return "oil-icon";
                case ItemKind.ThrowingKnife: return "knife-icon";
                case ItemKind.RecallScroll: return "scroll-icon";
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
                _useButton.SetEnabled(count > 0);
                _useButton.text = kind == ItemKind.Potion ? "마시기"
                    : kind == ItemKind.RecallScroll ? "사용하기"
                    : "조준하기";
            }
        }

        private void UseSelected()
        {
            if (demo == null || CountOf(_selected) <= 0) return;

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
            if (_modal != null && _modal.ClassListContains("is-open"))
                Select(_selected);
        }
    }
}
