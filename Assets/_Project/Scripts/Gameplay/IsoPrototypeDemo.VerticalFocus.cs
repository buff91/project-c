using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// Hole의 수직 미리보기와 의도적 낙하 확인 상태. 실제 FOV 계산은 Visibility partial이
    /// 계속 소유하고, 여기서는 그 결과 위에 hover/armed 강조만 얹는다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private GridPos? _hoveredDropHole;
        private GridPos? _armedDropHole;
        private readonly HashSet<GridPos> _focusedVerticalPreviewTiles = new HashSet<GridPos>();
        private GameObject _dropLandingMarker;
        private SpriteRenderer _dropLandingRenderer;

        private GridPos? FocusedDropHole => _armedDropHole ?? _hoveredDropHole;

        private void HandleVerticalFocusHover(GridPos? cell)
        {
            GridPos? next = IsFocusableDropHole(cell) ? cell : null;
            if (System.Nullable.Equals(next, _hoveredDropHole)) return;

            _hoveredDropHole = next;
            RefreshDropFocusPresentation();
            VerticalContextChanged?.Invoke();
        }

        private bool IsFocusableDropHole(GridPos? cell)
        {
            if (!cell.HasValue || hubMode || _bombAiming || IsVerticalLookActive || _resolvingAction ||
                viewMode != DungeonViewMode.Play || _grid == null || _dungeon == null)
                return false;

            GridPos hole = cell.Value;
            TileData tile = _grid.Map.Get(hole);
            return tile != null &&
                   tile.kind == TileKind.Hole &&
                   _dungeon.Height.FloorIndex(hole.elevation) == _activeFloorIndex &&
                   _visibleTiles.Contains(hole);
        }

        private bool TryHandleDropHoleTap(GridPos hole)
        {
            if (viewMode == DungeonViewMode.Play && !IsFocusableDropHoleForRefresh(hole))
            {
                ClearDropFocus(restoreSelection: true);
                InteractionFeedback?.Invoke("지금은 구멍 아래를 확인할 수 없다");
                return true;
            }

            if (!TryCreateDropPreview(hole, out HoleDropPreview preview))
            {
                InteractionFeedback?.Invoke("아래에 착지할 바닥이 없다");
                return true;
            }

            if (HoleInteractionRules.ResolveTap(_armedDropHole, hole) ==
                HoleDropTapDecision.Confirm)
                return TryConfirmArmedDrop();

            _armedDropHole = hole;
            _hoveredDropHole = hole;
            PositionSelection(hole);
            RefreshDropFocusPresentation();
            VerticalContextChanged?.Invoke();
            InteractionFeedback?.Invoke(
                $"{FloorLabel(preview.DestinationFloorIndex)} 충돌점 · 예상 피해 {preview.Damage} HP · " +
                DungeonDirectionRules.FallMeaningHint(_dungeon.Direction));
            return true;
        }

        private bool TryConfirmArmedDrop()
        {
            if (!_armedDropHole.HasValue) return false;

            GridPos hole = _armedDropHole.Value;
            if (!TryCreateDropPreview(hole, out _))
            {
                ClearDropFocus(restoreSelection: true);
                InteractionFeedback?.Invoke("낙하 경로가 더 이상 유효하지 않다");
                return true;
            }

            if (!TryFindApproach(hole, out ApproachPlan approach))
            {
                InteractionFeedback?.Invoke("구멍 가장자리까지 갈 수 없다");
                return true;
            }

            ClearDropFocus(restoreSelection: false);
            StartPlayerAction(hole, ApproachAndDrop(approach, hole));
            return true;
        }

        /// <summary>HUD의 Escape가 메뉴를 열기 전에 armed 낙하를 취소할 때 사용한다.</summary>
        public bool CancelDropConfirmation()
        {
            if (!_armedDropHole.HasValue) return false;
            ClearDropFocus(restoreSelection: true);
            InteractionFeedback?.Invoke("낙하 취소");
            return true;
        }

        private void ClearDropFocus(bool restoreSelection)
        {
            bool changed = _armedDropHole.HasValue || _hoveredDropHole.HasValue ||
                           _focusedVerticalPreviewTiles.Count > 0;
            _armedDropHole = null;
            _hoveredDropHole = null;
            _focusedVerticalPreviewTiles.Clear();
            if (_dropLandingRenderer != null) _dropLandingRenderer.enabled = false;
            if (restoreSelection && _selection != null && _playerState != null)
                PositionSelection(_playerPos);
            if (changed && _dungeon != null && _visualRoot != null)
                RefreshDropFocusPresentation();
            else
                UpdateWorldHoverTracking();
            if (changed) VerticalContextChanged?.Invoke();
        }

        private void ResetDropFocusForBuild()
        {
            if (_dropLandingRenderer != null) _dropLandingRenderer.enabled = false;
            _armedDropHole = null;
            _hoveredDropHole = null;
            _focusedVerticalPreviewTiles.Clear();
            _dropLandingMarker = null;
            _dropLandingRenderer = null;
        }

        private void SuspendDropFocus()
        {
            bool changed = _armedDropHole.HasValue || _hoveredDropHole.HasValue ||
                           _focusedVerticalPreviewTiles.Count > 0;
            _armedDropHole = null;
            _hoveredDropHole = null;
            _focusedVerticalPreviewTiles.Clear();
            if (_dropLandingRenderer != null) _dropLandingRenderer.enabled = false;
            if (changed) VerticalContextChanged?.Invoke();
        }

        private bool TryCreateDropPreview(GridPos hole, out HoleDropPreview preview)
        {
            preview = default;
            return _grid != null && _dungeon != null && HoleInteractionRules.TryCreatePreview(
                _grid.Map,
                _dungeon.Height,
                hole,
                BottomElevation,
                _dungeon.Direction,
                _playerLoadout.SafeFallHeight,
                out preview);
        }

        private string BuildDropFocusHintLabel()
        {
            if (!FocusedDropHole.HasValue ||
                !TryCreateDropPreview(FocusedDropHole.Value, out HoleDropPreview preview))
                return null;

            string meaning = preview.Meaning == FallMeaning.Shortcut
                ? "지름길"
                : preview.Meaning == FallMeaning.Retreat
                    ? "후퇴"
                    : "위험";
            string action = _armedDropHole.HasValue
                ? "재클릭/SPACE 확정"
                : "클릭해 낙하 고정";
            return $"{FloorLabel(preview.DestinationFloorIndex)} 충돌점 · {meaning} · " +
                   $"예상 피해 {preview.Damage} HP · {action}";
        }

        private string DropContextInteractionLabel
        {
            get
            {
                if (!_armedDropHole.HasValue ||
                    !TryCreateDropPreview(_armedDropHole.Value, out HoleDropPreview preview))
                    return null;
                return $"뛰어내리기 · 예상 피해 {preview.Damage} HP";
            }
        }

        /// <summary>
        /// passive 수직 FOV는 그대로 두고, 선택한 착지점과 같은 국소 창만 조금 더 밝힌다.
        /// 반드시 passive 집합과의 교집합만 사용해 벽 뒤 정보가 새지 않게 한다.
        /// </summary>
        private void RefreshDropFocusAfterVisibility()
        {
            _focusedVerticalPreviewTiles.Clear();

            GridPos? focused = FocusedDropHole;
            if (!focused.HasValue || !IsFocusableDropHoleForRefresh(focused.Value) ||
                !TryCreateDropPreview(focused.Value, out HoleDropPreview preview))
            {
                bool stateChanged = _armedDropHole.HasValue || _hoveredDropHole.HasValue;
                _armedDropHole = null;
                _hoveredDropHole = null;
                if (_dropLandingRenderer != null) _dropLandingRenderer.enabled = false;
                UpdateWorldHoverTracking();
                if (stateChanged)
                {
                    _input?.InvalidateHover();
                    VerticalContextChanged?.Invoke();
                }
                return;
            }

            int landingFloor = preview.DestinationFloorIndex;
            foreach (GridPos tile in _verticalPreviewTiles)
            {
                if (_dungeon.Height.FloorIndex(tile.elevation) == landingFloor &&
                    tile.ChebyshevTo(preview.Landing) <= verticalPreviewRadius)
                    _focusedVerticalPreviewTiles.Add(tile);
            }

            EnsureDropLandingMarker();
            if (_dropLandingRenderer != null)
            {
                _dropLandingMarker.transform.position =
                    VisualPosition(preview.Landing) + Vector3.up * 0.025f;
                _dropLandingRenderer.sortingOrder =
                    _grid.iso.SortingOrder(preview.Landing, -1);
                _dropLandingRenderer.color = _armedDropHole.HasValue
                    ? new Color(1f, 0.76f, 0.24f, 0.92f)
                    : new Color(0.18f, 0.86f, 0.94f, 0.72f);
                _dropLandingRenderer.enabled =
                    _verticalPreviewTiles.Contains(preview.Landing);
            }
            UpdateWorldHoverTracking();
        }

        /// <summary>
        /// hover/armed 변화는 기존 passive FOV의 알파와 오버레이만 바꾼다. 전체
        /// RefreshFloorVisibility를 부르면 포인터가 경계를 오갈 때 벽/샤프트까지 재생성된다.
        /// </summary>
        private void RefreshDropFocusPresentation()
        {
            if (_dungeon == null) return;

            RefreshDropFocusAfterVisibility();
            foreach (GridPos tile in _verticalPreviewTiles)
            {
                if (!_tileRenderers.TryGetValue(tile, out SpriteRenderer renderer)) continue;
                Color color = renderer.color;
                color.a = VisibilityAlpha(tile);
                renderer.color = color;
            }

            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy?.State == null ||
                    !_verticalPreviewTiles.Contains(enemy.State.Position))
                    continue;
                ApplyEnemyVisuals(enemy);
            }
        }

        private bool IsFocusableDropHoleForRefresh(GridPos hole)
        {
            if (hubMode || IsVerticalLookActive || viewMode != DungeonViewMode.Play ||
                _grid == null || _dungeon == null)
                return false;
            TileData tile = _grid.Map.Get(hole);
            return tile != null && tile.kind == TileKind.Hole &&
                   _dungeon.Height.FloorIndex(hole.elevation) == _activeFloorIndex &&
                   _visibleTiles.Contains(hole);
        }

        private void EnsureDropLandingMarker()
        {
            if (_dropLandingRenderer != null || _visualRoot == null) return;

            _dropLandingMarker = new GameObject("Drop Landing Preview");
            _dropLandingMarker.hideFlags = HideFlags.DontSaveInEditor;
            _dropLandingMarker.transform.SetParent(_visualRoot, false);
            _dropLandingRenderer = _dropLandingMarker.AddComponent<SpriteRenderer>();
            _dropLandingRenderer.sprite = visualCatalog != null && visualCatalog.selection != null
                ? visualCatalog.selection
                : ActorSprites.GetSelectionSprite();
        }

        private bool HasVisibleDropHole()
        {
            if (hubMode || viewMode != DungeonViewMode.Play || _grid == null || _dungeon == null)
                return false;
            foreach (var pair in _grid.Map.All())
            {
                if (pair.Value.kind == TileKind.Hole &&
                    _dungeon.Height.FloorIndex(pair.Key.elevation) == _activeFloorIndex &&
                    _visibleTiles.Contains(pair.Key))
                    return true;
            }
            return false;
        }

        private void UpdateWorldHoverTracking()
        {
            if (_input == null) return;
            bool shouldTrack = Application.isPlaying &&
                               (_bombAiming ||
                                !_resolvingAction &&
                                (IsVerticalLookActive ||
                                 _verticalPreviewTiles.Count > 0 ||
                                 HasVisibleDropHole()));
            if (_input.trackHover == shouldTrack) return;
            _input.trackHover = shouldTrack;
            _input.InvalidateHover();
        }
    }
}
