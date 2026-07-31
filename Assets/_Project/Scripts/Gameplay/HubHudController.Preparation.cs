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
            bool metaWritable = MetaStore.CanWrite;
            int returned = metaWritable ? ExpeditionLoadoutRules.Reconcile(_meta) : 0;
            if (returned > 0 && !SaveMetaOrReload()) returned = 0;
            RefreshPreparation(!metaWritable
                ? MetaReadOnlyMessage
                : returned > 0
                    ? $"영웅 기본 지급품 공간 확보 · {returned}개 창고 복귀"
                    : "");
            _stashModal?.BringToFront();
            _stashModal?.AddToClassList("is-open");
        }

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
                // 전리품은 환금 전용이고, 장비는 대장간에서 장착으로 관리한다(백팩 공간을 쓰지 않는다).
                ItemCategory category = ItemCatalog.CategoryOf(kind);
                if (category == ItemCategory.Treasure || category == ItemCategory.Equipment) continue;
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

            BackpackLayout layout = ExpeditionLoadoutRules.CreateLayout(_meta);
            foreach (BackpackPlacement placement in layout.Placements)
            {
                ItemKind kind = placement.Kind;
                // InstanceIndex 는 **칸** 인덱스이고 StarterCount 는 **충전**을 반환한다.
                // 둘을 직접 비교하면 지급품과 플레이어 물건이 한 칸에 섞였을 때 그 칸이
                // 통째로 잠겨 플레이어 소유분을 창고로 되돌릴 수 없게 된다.
                bool starter = placement.InstanceIndex < ChargeUnits.UnitsFor(
                    kind, ExpeditionLoadoutRules.StarterCount(kind));
                ItemKind captured = kind;
                PreparationSelectionSource source = starter
                    ? PreparationSelectionSource.Starter
                    : PreparationSelectionSource.Loadout;

                // 이 칸의 잔여 충전은 레이아웃이 실어 보낸 값을 그대로 쓴다 — UI가 다시
                // 계산하면 숫자와 되돌리는 양이 갈린다. 되돌리기·드래그가 이 값을 옮긴다.
                int cellCharges = placement.Charges;
                Button slot = InventoryPanelController.CreateItemSlot(
                    kind, cellCharges, null, $"loadout-{kind}-{placement.InstanceIndex}",
                    ItemCatalog.ChargesPerItem(kind));
                slot.AddToClassList("backpack-item");
                slot.AddToClassList(
                    $"backpack-size-{placement.Footprint.Width}x{placement.Footprint.Height}");
                slot.tooltip = starter
                    ? $"{ItemCatalog.DisplayName(kind)} · 영웅 기본 지급품"
                    : $"{ItemCatalog.DisplayName(kind)} · 원정 반입{ChargeSuffix(kind, cellCharges)}";
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
                    slot.userData = cellCharges;
                    RegisterPreparationDrag(slot, DragSource.Loadout, kind, cellCharges);
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
                _loadoutHero.text = $"{SurvivorProfile.DisplayName} 기본 지급 포함";
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
                    : ExpeditionLoadoutRules.StarterCount(kind);
            string sourceLabel = _preparationSource == PreparationSelectionSource.Stash
                ? "창고"
                : _preparationSource == PreparationSelectionSource.Loadout
                    ? "출정 백팩"
                    : "영웅 기본 지급";
            // 충전이 있는 종류에서 `×N`은 개수가 아니라 회분이다 — 같은 기호로 두면
            // "물약 ×6"이 여섯 칸으로 읽힌다. 칸수를 함께 내서 둘을 붙여 놓는다.
            string amountLabel = ItemCatalog.IsCharged(kind)
                ? $"{count}회분 · {ChargeUnits.UnitsFor(kind, count)}칸({footprint})"
                : $"×{count} · {footprint}칸";
            if (_stashName != null)
                _stashName.text =
                    $"{ItemCatalog.DisplayName(kind)} {amountLabel} · {sourceLabel}";
            if (_stashDesc != null) _stashDesc.text = ItemCatalog.Description(kind);

            bool fromStash = _preparationSource == PreparationSelectionSource.Stash;
            bool fromLoadout = _preparationSource == PreparationSelectionSource.Loadout;
            bool metaWritable = MetaStore.CanWrite;
            if (_toLoadout != null)
            {
                _toLoadout.SetEnabled(metaWritable && fromStash);
                // 버튼이 옮길 양을 스스로 말한다 — 이동 단위가 칸이라는 사실은
                // 격자만 봐서는 안 읽히고, 안 읽히면 여섯 번 누르던 습관이 남는다.
                int unit = fromStash
                    ? ExpeditionLoadoutRules.UnitChargesInStash(_meta, kind)
                    : 0;
                _toLoadout.text = fromStash && ItemCatalog.IsCharged(kind)
                    ? $"백팩에 넣기 ({unit}회분) →"
                    : "백팩에 넣기 →";
            }
            if (_toStash != null)
            {
                _toStash.SetEnabled(metaWritable && fromLoadout);
                _toStash.text = fromLoadout && ItemCatalog.IsCharged(kind)
                    ? $"← 창고로 ({SelectedCellCharges()}회분)"
                    : "← 창고로";
            }
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
            // 선택이 풀리면 수량 꼬리표도 함께 지운다 — 안 지우면 지난 선택의 회분이
            // 비활성 버튼에 남아 다음 아이템의 값처럼 읽힌다.
            if (_toLoadout != null) _toLoadout.text = "백팩에 넣기 →";
            if (_toStash != null) _toStash.text = "← 창고로";
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
            MoveKindToStash(_stashSelected.Value, SelectedCellCharges());
        }

        /// <summary>
        /// 선택된 출정 백팩 <b>칸</b>의 잔여 충전. 슬롯을 만들 때 실어 둔 값이라
        /// 화면에 보이는 숫자와 되돌아가는 양이 같다. 값이 없으면 1회분으로 본다.
        /// </summary>
        private int SelectedCellCharges() =>
            _selectedPreparationSlot?.userData is int charges && charges > 0 ? charges : 1;

        /// <summary>
        /// 창고 → 백팩은 <b>한 칸 분량</b>을 옮긴다. 덜 찬 칸도 셀은 만충과 똑같이 먹으므로
        /// 1회분씩 옮기면 클릭만 늘고 얻는 것이 없다(물약 6회분에 여섯 번).
        /// </summary>
        private void MoveKindToLoadout(ItemKind kind)
        {
            if (!EnsureMetaWritable()) return;
            int charges = ExpeditionLoadoutRules.UnitChargesInStash(_meta, kind);
            if (charges <= 0)
            {
                ShowTransferFailure(LoadoutTransferResult.MissingFromStash, kind, _loadoutPane);
                return;
            }

            LoadoutTransferResult result =
                ExpeditionLoadoutRules.TryMoveToLoadout(_meta, kind, charges);
            if (result != LoadoutTransferResult.Success)
            {
                ShowTransferFailure(result, kind, _loadoutPane);
                return;
            }

            if (!SaveMetaOrReload()) return;
            _stashSelected = kind;
            _preparationSource = PreparationSelectionSource.Loadout;
            _selectedPreparationSlot = null;
            RefreshPreparation(
                $"{ItemCatalog.DisplayName(kind)}{ChargeSuffix(kind, charges)} → 출정 백팩");
        }

        private void MoveKindToStash(ItemKind kind, int charges)
        {
            if (!EnsureMetaWritable()) return;
            LoadoutTransferResult result =
                ExpeditionLoadoutRules.TryMoveToStash(_meta, kind, charges);
            if (result != LoadoutTransferResult.Success)
            {
                ShowTransferFailure(result, kind, _stashPane);
                return;
            }

            if (!SaveMetaOrReload()) return;
            _stashSelected = kind;
            _preparationSource = PreparationSelectionSource.Stash;
            _selectedPreparationSlot = null;
            RefreshPreparation(
                $"{ItemCatalog.DisplayName(kind)}{ChargeSuffix(kind, charges)} → 창고");
        }

        /// <summary>
        /// 충전이 있는 종류에만 " N회분"을 덧붙인다. 1회분짜리에 붙이면 모든 문구가
        /// "폭탄 1회분"이 되어 없는 규칙을 있는 것처럼 읽히게 한다.
        /// </summary>
        private static string ChargeSuffix(ItemKind kind, int charges) =>
            ItemCatalog.IsCharged(kind) ? $" {charges}회분" : "";

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
            MoveKindToStash(_stashSelected.Value, SelectedCellCharges());
            evt.StopPropagation();
        }

        private static bool HasButtonAncestor(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (current is Button) return true;
            return false;
        }

        private void RegisterPreparationDrag(
            Button slot, DragSource source, ItemKind kind, int charges = 1)
        {
            slot.RegisterCallback<PointerDownEvent>(
                evt => BeginPreparationDrag(evt, slot, source, kind, charges));
            slot.RegisterCallback<PointerMoveEvent>(UpdatePreparationDrag);
            slot.RegisterCallback<PointerUpEvent>(CompletePreparationDrag);
        }

        private void BeginPreparationDrag(
            PointerDownEvent evt,
            Button slot,
            DragSource source,
            ItemKind kind,
            int charges)
        {
            if (evt.button != 0 || _dragSource != DragSource.None) return;
            _dragSource = source;
            _dragKind = kind;
            _dragCharges = charges;
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
                // 드롭 미리보기는 실제로 옮길 양(한 칸 분량)으로 판정한다 —
                // 1회분으로 물어보면 "들어간다"고 초록을 켜 놓고 드롭에서 거절한다.
                int charges = ExpeditionLoadoutRules.UnitChargesInStash(_meta, _dragKind);
                bool valid = charges > 0 &&
                    ExpeditionLoadoutRules.CanMoveToLoadout(_meta, _dragKind, charges);
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
            int charges = _dragCharges;
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
                MoveKindToStash(kind, charges);
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
            _dragCharges = 1;
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
