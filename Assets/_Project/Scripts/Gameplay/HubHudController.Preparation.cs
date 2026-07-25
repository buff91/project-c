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
    public partial class HubHudController : MonoBehaviour
    {

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
    }
}
