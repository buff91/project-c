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

            ClearDropFocus(restoreSelection: true);
            _verticalLookMode = mode;
            _aimHoverCell = null;
            RefreshVerticalLookAfterVisibility();
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            _input?.InvalidateHover();
            RefreshThrowRangePreview();
            VerticalContextChanged?.Invoke();
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
        private bool RejectWorldActionWhileVerticalLooking()
        {
            if (!IsVerticalLookActive) return false;
            InteractionFeedback?.Invoke("층 보기 중에는 이동할 수 없다 · ◆ 현재 층으로 돌아오기");
            return true;
        }

        private void ExitVerticalLook(bool announce, bool refreshPresentation = true)
        {
            if (!IsVerticalLookActive && _verticalLookTiles.Count == 0) return;

            _verticalLookMode = VerticalLookMode.Current;
            _verticalLookTiles.Clear();
            _aimHoverCell = null;
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
        }

        private void SuspendVerticalLook()
        {
            _verticalLookMode = VerticalLookMode.Current;
            _verticalLookTiles.Clear();
        }

        /// <summary>
        /// passive preview의 방향별 부분집합만 밝힌다. 새 FOV를 만들지 않으므로 층 보기 자체가
        /// 벽 뒤 정보나 아직 보지 못한 방을 드러내지 않는다.
        /// </summary>
        private void RefreshVerticalLookAfterVisibility()
        {
            _verticalLookTiles.Clear();
            if (!IsVerticalLookActive || _dungeon == null) return;

            int viewedFloor = ViewedFloorIndex;
            foreach (GridPos tile in _verticalPreviewTiles)
            {
                if (_dungeon.Height.FloorIndex(tile.elevation) == viewedFloor)
                    _verticalLookTiles.Add(tile);
            }

            // 폭발로 Hole이 막히거나 층이 바뀌어 창이 사라졌다면 조용히 현재 층으로 복귀한다.
            if (_verticalLookTiles.Count == 0)
            {
                _verticalLookMode = VerticalLookMode.Current;
                _aimHoverCell = null;
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
            string direction = _verticalLookMode == VerticalLookMode.Up ? "윗층" : "아랫층";
            return $"{direction} {ViewedFloorLabel} 보기 · 이동 잠금 · ◆ 현재 층";
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

            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;
            foreach (GridPos tile in _verticalLookTiles)
            {
                Vector3 world = VisualPosition(tile);
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
                Vector3 player = VisualPosition(_playerState.Position);
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
