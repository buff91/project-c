using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>플레이어 층은 그대로 둔 채 실제 개구부 너머 인접 층을 보는 방향.</summary>
    public enum VerticalLookMode
    {
        Current = 0,
        Up = 1,
        Down = -1
    }

    /// <summary>
    /// 실제 Hole을 통해 이미 계산된 수직 FOV를 명시적으로 확대해 보는 상태.
    /// active floor는 턴·AI·미니맵의 기준으로 계속 유지하고, 카메라·피커·투척만 viewed floor를 쓴다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private VerticalLookMode _verticalLookMode = VerticalLookMode.Current;
        private readonly HashSet<GridPos> _verticalLookTiles = new HashSet<GridPos>();
        private GridPos? _verticalReadOnlyTile;
        private GameObject _verticalReadOnlyMarker;
        private SpriteRenderer _verticalReadOnlyRenderer;

        public VerticalLookMode VerticalLook => _verticalLookMode;
        public bool IsVerticalLookActive => _verticalLookMode != VerticalLookMode.Current;
        public int ViewedFloorIndex => IsVerticalLookActive
            ? _activeFloorIndex + (int)_verticalLookMode
            : _activeFloorIndex;
        public string ViewedFloorLabel =>
            _dungeon != null ? FloorLabel(ViewedFloorIndex) : "--";
        public bool CanLookUp => HasVerticalLookTiles(VerticalLookMode.Up);
        public bool CanLookDown => HasVerticalLookTiles(VerticalLookMode.Down);

        public void LookUp() => SetVerticalLook(VerticalLookMode.Up);
        public void LookCurrent() => SetVerticalLook(VerticalLookMode.Current);
        public void LookDown() => SetVerticalLook(VerticalLookMode.Down);

        public void SetVerticalLook(VerticalLookMode mode)
        {
            if (mode != VerticalLookMode.Current &&
                mode != VerticalLookMode.Up &&
                mode != VerticalLookMode.Down)
                return;
            if (!Application.isPlaying || hubMode || _dungeon == null || _grid == null ||
                _resolvingAction || _playerState == null || !_playerState.IsAlive ||
                viewMode != DungeonViewMode.Play)
                return;

            if (mode == VerticalLookMode.Current)
            {
                ExitVerticalLook(announce: true);
                return;
            }

            if (!HasVerticalLookTiles(mode))
            {
                InteractionFeedback?.Invoke(mode == VerticalLookMode.Up
                    ? "위층으로 열린 시야가 없다"
                    : "아래층으로 열린 시야가 없다");
                return;
            }

            if (_bombAiming && !VerticalThrowRules.Supports(_bombAimKind))
                SetBombAiming(false);

            ExitCameraLook(announce: false, applyCamera: false);
            ClearDropFocus(restoreSelection: true);
            _verticalLookMode = mode;
            _aimHoverCell = null;
            ClearVerticalReadOnlyMarker();
            RefreshVerticalLookAfterVisibility();
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            _input?.InvalidateHover();
            RefreshThrowRangePreview();
            VerticalContextChanged?.Invoke();
            string direction = mode == VerticalLookMode.Up ? "▲" : "▼";
            InteractionFeedback?.Invoke(
                $"{ViewedFloorLabel} {direction} 관찰 전용 · 직접 이동 불가 · 광역 투척만 가능");
        }

        /// <summary>Escape 처리용. 조준 취소 뒤에 호출되어 한 단계씩 현재 층으로 돌아온다.</summary>
        public bool CancelVerticalLook()
        {
            // 투사체가 개구부를 통과하는 동안 카메라만 먼저 현재 층으로 돌아가면
            // 판정은 원격인데 연출은 화면 밖에서 끝난다. 행동 완료가 복귀를 소유한다.
            if (!IsVerticalLookActive || _resolvingAction) return false;
            ExitVerticalLook(announce: true);
            return true;
        }

        /// <summary>
        /// 월드 행동은 현재 층에서만 시작한다. 층 보기 중 직접 조작을 눌렀을 때 턴을 쓰지 않고 막는다.
        /// </summary>
        private bool RejectWorldActionWhileVerticalLooking(GridPos? target = null)
        {
            if (!IsVerticalLookActive) return false;
            if (target.HasValue && IsVerticalLookTarget(target.Value))
                ShowVerticalReadOnlyRejection(target.Value);
            InteractionFeedback?.Invoke(
                "관찰 층에는 이동할 수 없다 · 광역 투척 또는 ◆ 현재 층 복귀");
            return true;
        }

        private void ExitVerticalLook(bool announce, bool refreshPresentation = true)
        {
            if (!IsVerticalLookActive && _verticalLookTiles.Count == 0) return;

            _verticalLookMode = VerticalLookMode.Current;
            _verticalLookTiles.Clear();
            _aimHoverCell = null;
            ClearVerticalReadOnlyMarker();
            if (_selection != null && _playerState != null)
                PositionSelection(_playerPos);

            if (refreshPresentation && _dungeon != null && _visualRoot != null)
            {
                ApplyViewToVisuals();
                ConfigureCamera(Camera.main);
                RefreshThrowRangePreview();
                _input?.InvalidateHover();
            }
            else
            {
                UpdateWorldHoverTracking();
            }

            VerticalContextChanged?.Invoke();
            if (announce) InteractionFeedback?.Invoke("현재 층으로 돌아왔다");
        }

        private void ResetVerticalLookForBuild()
        {
            _verticalLookMode = VerticalLookMode.Current;
            _verticalLookTiles.Clear();
            _verticalReadOnlyTile = null;
            _verticalReadOnlyMarker = null;
            _verticalReadOnlyRenderer = null;
        }

        private void SuspendVerticalLook()
        {
            _verticalLookMode = VerticalLookMode.Current;
            _verticalLookTiles.Clear();
            ClearVerticalReadOnlyMarker();
        }

        /// <summary>
        /// passive preview의 방향별 부분집합만 밝힌다. 새 FOV를 만들지 않으므로 층 보기 자체가
        /// 벽 뒤 정보나 아직 보지 못한 방을 드러내지 않는다.
        /// </summary>
        private void RefreshVerticalLookAfterVisibility()
        {
            _verticalLookTiles.Clear();
            if (!IsVerticalLookActive || _dungeon == null)
            {
                if (_verticalReadOnlyTile.HasValue &&
                    !IsReadOnlyVerticalTile(_verticalReadOnlyTile.Value))
                    ClearVerticalReadOnlyMarker();
                return;
            }

            int viewedFloor = ViewedFloorIndex;
            foreach (GridPos tile in _verticalPreviewTiles)
            {
                if (_dungeon.Height.FloorIndex(tile.elevation) == viewedFloor)
                    _verticalLookTiles.Add(tile);
            }

            if (_verticalReadOnlyTile.HasValue &&
                !IsReadOnlyVerticalTile(_verticalReadOnlyTile.Value))
                ClearVerticalReadOnlyMarker();

            // 폭발로 Hole이 막히거나 층이 바뀌어 창이 사라졌다면 조용히 현재 층으로 복귀한다.
            if (_verticalLookTiles.Count == 0)
            {
                _verticalLookMode = VerticalLookMode.Current;
                _aimHoverCell = null;
                ClearVerticalReadOnlyMarker();
                if (_selection != null && _playerState != null)
                    PositionSelection(_playerPos);
                ConfigureCamera(Camera.main);
                _input?.InvalidateHover();
            }
        }

        private bool HasVerticalLookTiles(VerticalLookMode mode)
        {
            if (mode == VerticalLookMode.Current || _dungeon == null) return false;
            int targetFloor = _activeFloorIndex + (int)mode;
            foreach (GridPos tile in _verticalPreviewTiles)
            {
                if (_dungeon.Height.FloorIndex(tile.elevation) == targetFloor)
                    return true;
            }
            return false;
        }

        private bool IsVerticalLookTarget(GridPos target) =>
            IsVerticalLookActive &&
            _dungeon != null &&
            _dungeon.Height.FloorIndex(target.elevation) == ViewedFloorIndex &&
            _verticalLookTiles.Contains(target);

        private bool CanUseVerticalThrowNearEndpoint(GridPos endpoint) =>
            IsVerticalLookActive &&
            _dungeon != null &&
            _dungeon.Height.FloorIndex(endpoint.elevation) == _activeFloorIndex &&
            _visibleTiles.Contains(endpoint);

        private bool TryResolveVerticalThrow(GridPos target, out VerticalThrowPath path)
        {
            path = default;
            return IsVerticalLookTarget(target) &&
                   VerticalThrowRules.TryResolve(
                       _grid.Map,
                       _dungeon.Height,
                       _playerPos,
                       target,
                       _bombAimKind,
                       bombThrowRange,
                       CanUseVerticalThrowNearEndpoint,
                       out path);
        }

        private string BuildVerticalLookHintLabel()
        {
            if (!IsVerticalLookActive) return null;
            string direction = _verticalLookMode == VerticalLookMode.Up ? "▲" : "▼";
            return $"{ViewedFloorLabel} {direction} 관찰 전용 · 직접 이동 불가 · " +
                   "폭발물/냉각재/기름 투척 · ◆/ESC 복귀";
        }

        /// <summary>
        /// 비활성 층은 밝게 보이더라도 읽기 전용이다. 포인터가 가리킨 타일에 열린 X 마커를
        /// 붙여 클릭 전에 규칙을 알리고, 클릭하면 같은 자리에서 마젠타로 응답한다.
        /// </summary>
        private void HandleVerticalReadOnlyHover(GridPos? cell)
        {
            if (_bombAiming)
            {
                ClearVerticalReadOnlyMarker();
                return;
            }

            GridPos? target = cell.HasValue && IsReadOnlyVerticalTile(cell.Value)
                ? cell
                : null;
            if (System.Nullable.Equals(target, _verticalReadOnlyTile)) return;
            if (!target.HasValue)
            {
                ClearVerticalReadOnlyMarker();
                return;
            }

            ShowVerticalReadOnlyMarker(target.Value, rejected: false);
        }

        private bool IsReadOnlyVerticalTile(GridPos pos) =>
            _dungeon != null &&
            _verticalPreviewTiles.Contains(pos) &&
            _dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex &&
            (!IsVerticalLookActive || _verticalLookTiles.Contains(pos));

        private void ShowVerticalReadOnlyRejection(GridPos target)
        {
            ShowVerticalReadOnlyMarker(target, rejected: true);
            FloatingText?.Show(
                VisualPosition(target) + Vector3.up * 0.08f,
                "VIEW ONLY",
                FloatingTextKind.ReadOnly);
        }

        private void ShowVerticalReadOnlyMarker(GridPos target, bool rejected)
        {
            EnsureVerticalReadOnlyMarker();
            if (_verticalReadOnlyRenderer == null) return;

            _verticalReadOnlyTile = target;
            _verticalReadOnlyMarker.transform.position =
                VisualPosition(target) + Vector3.up * 0.02f;
            _verticalReadOnlyMarker.transform.localScale =
                Vector3.one * (rejected ? 1.12f : 1f);
            _verticalReadOnlyRenderer.sortingOrder =
                VerticalReadOnlyMarkerSortingOrder(target);
            Color32 color = rejected
                ? Palette.NeonMagenta
                : new Color32(126, 151, 164, 196);
            _verticalReadOnlyRenderer.color = color;
            _verticalReadOnlyRenderer.enabled = true;
        }

        private void EnsureVerticalReadOnlyMarker()
        {
            if (_verticalReadOnlyRenderer != null || _visualRoot == null) return;

            _verticalReadOnlyMarker = new GameObject("Vertical Preview Read Only");
            _verticalReadOnlyMarker.hideFlags = HideFlags.DontSaveInEditor;
            _verticalReadOnlyMarker.transform.SetParent(_visualRoot, false);
            _verticalReadOnlyRenderer =
                _verticalReadOnlyMarker.AddComponent<SpriteRenderer>();
            _verticalReadOnlyRenderer.sprite = ActorSprites.GetReadOnlyPreviewSprite();
        }

        private void ClearVerticalReadOnlyMarker()
        {
            _verticalReadOnlyTile = null;
            if (_verticalReadOnlyRenderer != null)
                _verticalReadOnlyRenderer.enabled = false;
            if (_verticalReadOnlyMarker != null)
                _verticalReadOnlyMarker.transform.localScale = Vector3.one;
        }

        private void ApplyVerticalReadOnlyMarkerView()
        {
            if (!_verticalReadOnlyTile.HasValue || _verticalReadOnlyRenderer == null ||
                !_verticalReadOnlyRenderer.enabled)
                return;
            GridPos target = _verticalReadOnlyTile.Value;
            _verticalReadOnlyMarker.transform.position =
                VisualPosition(target) + Vector3.up * 0.02f;
            _verticalReadOnlyRenderer.sortingOrder =
                VerticalReadOnlyMarkerSortingOrder(target);
        }

        private int VerticalReadOnlyMarkerSortingOrder(GridPos target)
        {
            TileKind kind = _grid.Map.Get(target).kind;
            // 거절 표식은 그 칸의 바닥/계단/문과 원격 실루엣보다 앞에서 읽혀야 한다.
            // 열린 다이아라 몸체를 완전히 가리지 않으며, +2는 IsoGrid의 공식 최상위 micro 슬롯이다.
            return _grid.iso.SortingOrder(TileVisualSortingPos(target, kind), 2);
        }

        /// <summary>
        /// 관찰 패치만 중앙에 놓으면 현재 층의 플레이어가 화면 밖으로 사라져 수직 경로의
        /// 출발점을 잃는다. 플레이어와 관찰 패치를 한 프레임에 넣되 과도한 줌아웃은 막는다.
        /// </summary>
        private bool TryGetVerticalLookCameraFrame(
            float aspect,
            out Vector2 center,
            out float size)
        {
            center = default;
            size = playCameraSize;
            if (!IsVerticalLookActive || _verticalLookTiles.Count == 0) return false;

            int currentView = _grid.iso.viewQuarterTurns;
            float stableSize = playCameraSize;
            for (int view = 0; view < 4; view++)
            {
                if (!TryGetVerticalLookCameraFrameForView(
                        aspect,
                        view,
                        out Vector2 candidateCenter,
                        out float candidateSize))
                    return false;
                if (view == currentView)
                    center = candidateCenter;
                stableSize = Mathf.Max(stableSize, candidateSize);
            }

            size = stableSize;
            return true;
        }

        private bool TryGetVerticalLookCameraFrameForView(
            float aspect,
            int viewQuarterTurns,
            out Vector2 center,
            out float size)
        {
            center = default;
            size = playCameraSize;

            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;
            foreach (GridPos tile in _verticalLookTiles)
            {
                Vector3 world = VisualPositionForView(tile, viewQuarterTurns);
                if (!found)
                {
                    minX = maxX = world.x;
                    minY = maxY = world.y;
                    found = true;
                    continue;
                }

                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxY = Mathf.Max(maxY, world.y);
            }

            if (!found) return false;

            Vector2 playerCenter = default;
            bool hasPlayer = _playerState != null;
            if (hasPlayer)
            {
                Vector3 player = VisualPositionForView(
                    _playerState.Position,
                    viewQuarterTurns);
                playerCenter = new Vector2(player.x, player.y);
                minX = Mathf.Min(minX, player.x);
                maxX = Mathf.Max(maxX, player.x);
                minY = Mathf.Min(minY, player.y);
                maxY = Mathf.Max(maxY, player.y);
            }

            Vector2 boundsCenter = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            // 화면 가장자리의 작은 표식이 아니라 실제 플레이어 몸체까지 남도록 약간
            // 출발점 쪽으로 기울인다. 위/아래 모두 playerCenter 방향이라 대칭이다.
            center = hasPlayer
                ? Vector2.Lerp(boundsCenter, playerCenter, 0.35f)
                : boundsCenter;
            float safeAspect = Mathf.Max(0.1f, aspect);
            float requiredHalfHeight = Mathf.Max(maxY - center.y, center.y - minY) + 1.00f;
            float requiredHalfWidth = Mathf.Max(maxX - center.x, center.x - minX) + 0.90f;
            float requiredSize = Mathf.Max(requiredHalfHeight, requiredHalfWidth / safeAspect);
            size = Mathf.Clamp(requiredSize, playCameraSize, playCameraSize * 1.35f);
            return true;
        }
    }
}
