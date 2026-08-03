using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 현재 활성 층의 이미 본 공간을 무턴으로 둘러보는 PC 카메라 상태.
    /// FOV·턴·AI·활성 층은 플레이어를 계속 기준으로 삼고 카메라 중심만 임시 분리한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private static readonly Vector2 CameraLookBoundsPadding = new Vector2(0.75f, 0.55f);

        private readonly List<Vector2> _cameraLookKnownCenters = new List<Vector2>();
        private bool _cameraLookActive;
        private Vector2 _cameraLookCenter;
        private Vector2 _cameraLookOriginCenter;

        public bool IsCameraLookingAround => _cameraLookActive;

        private void HandleCameraPanRequested(Vector2 screenDelta) =>
            TryPanCamera(screenDelta);

        private void HandleCameraRecenterRequested() => RecenterCamera();

        /// <summary>
        /// 화면 드래그만큼 현재 구도의 중심을 이동한다. 배율은 건드리지 않으므로
        /// 일반 플레이와 B2 히어로룸 특수 프레임 모두 첫 드래그에서 줌이 튀지 않는다.
        /// </summary>
        public bool TryPanCamera(Vector2 screenDelta)
        {
            if (!CanPanCamera()) return false;

            Camera camera = _configuredCamera != null ? _configuredCamera : Camera.main;
            if (camera == null || camera.pixelHeight <= 0) return false;

            if (!_cameraLookActive)
            {
                _cameraLookOriginCenter = new Vector2(
                    camera.transform.position.x,
                    camera.transform.position.y);
                _cameraLookCenter = _cameraLookOriginCenter;
                _cameraLookActive = true;
                ClearDropFocus(restoreSelection: true);
                InteractionFeedback?.Invoke("둘러보기 · Home으로 캐릭터 복귀");
            }

            _cameraLookCenter += OrthographicCameraFraming.ScreenDragToWorldDelta(
                screenDelta,
                camera.orthographicSize,
                camera.pixelHeight);
            ConfigureCamera(camera);
            _input?.InvalidateHover();
            return true;
        }

        /// <summary>Home 및 HUD Escape 경로. 이미 추적 중이면 아무것도 소비하지 않는다.</summary>
        public bool RecenterCamera() =>
            ExitCameraLook(announce: true, applyCamera: true);

        /// <summary>HUD Escape가 메뉴를 열기 전에 자유 카메라 한 단계만 닫는다.</summary>
        public bool CancelCameraLook() => RecenterCamera();

        private bool CanPanCamera()
        {
            return Application.isPlaying &&
                   configureMainCamera &&
                   !hubMode &&
                   viewMode == DungeonViewMode.Play &&
                   !IsVerticalLookActive &&
                   !_bombAiming &&
                   !_resolvingAction &&
                   _grid != null &&
                   _dungeon != null &&
                   _playerState != null &&
                   _playerState.IsAlive &&
                   !_runSummary.Ended;
        }

        /// <summary>기존 vertical/B2/follow 계산의 크기를 보존하고 중심만 자유 보기 값으로 바꾼다.</summary>
        private OrthographicCameraFrame ApplyCameraLook(OrthographicCameraFrame frame)
        {
            if (!_cameraLookActive || hubMode || viewMode != DungeonViewMode.Play ||
                IsVerticalLookActive)
                return frame;

            _cameraLookCenter = ClampCameraLookCenter(_cameraLookCenter);
            return new OrthographicCameraFrame(_cameraLookCenter, frame.Size);
        }

        private Vector2 ClampCameraLookCenter(Vector2 requested)
        {
            _cameraLookKnownCenters.Clear();
            // HUD 안전영역 때문에 일반 타일 경계 밖으로 보정된 B2 시작 프레임도 첫 드래그에서
            // 갑자기 안쪽으로 튀지 않도록 자유 보기 진입 중심을 허용 경계에 포함한다.
            _cameraLookKnownCenters.Add(_cameraLookOriginCenter);

            foreach (GridPos pos in _exploredTiles)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex)
                    continue;

                Vector3 world = VisualPosition(pos);
                _cameraLookKnownCenters.Add(new Vector2(world.x, world.y));
            }

            return OrthographicCameraFraming.ClampCenterToProjectedBounds(
                requested,
                _cameraLookKnownCenters,
                CameraLookBoundsPadding);
        }

        private bool ExitCameraLook(bool announce, bool applyCamera)
        {
            _input?.CancelCameraPanGesture();
            if (!_cameraLookActive) return false;

            _cameraLookActive = false;
            _cameraLookCenter = default;
            _cameraLookOriginCenter = default;
            _cameraLookKnownCenters.Clear();

            if (applyCamera && configureMainCamera)
            {
                Camera camera = _configuredCamera != null ? _configuredCamera : Camera.main;
                ConfigureCamera(camera);
                _input?.InvalidateHover();
            }

            if (announce) InteractionFeedback?.Invoke("플레이어 추적");
            return true;
        }

        private void ResetCameraLookForBuild()
        {
            _cameraLookActive = false;
            _cameraLookCenter = default;
            _cameraLookOriginCenter = default;
            _cameraLookKnownCenters.Clear();
            _input?.CancelCameraPanGesture();
        }

        private void SuspendCameraLook()
        {
            bool wasActive = _cameraLookActive;
            _cameraLookActive = false;
            _cameraLookCenter = default;
            _cameraLookOriginCenter = default;
            _cameraLookKnownCenters.Clear();
            _input?.CancelCameraPanGesture();

            // 컴포넌트 토글은 씬 재빌드와 달리 카메라 Transform을 보존한다. 팬 중심을
            // 남긴 채 상태만 잊지 않도록 disable 경계에서 기본 추종 프레임을 복구한다.
            if (!wasActive || !configureMainCamera || _grid == null) return;
            Camera camera = _configuredCamera != null ? _configuredCamera : Camera.main;
            if (camera != null) ConfigureCamera(camera);
        }

        /// <summary>
        /// 분위기 배경은 카메라보다 8%만 크게 만들어진 한 장이므로, 팬할 때 중심도 같이
        /// 옮기지 않으면 금세 가장자리가 드러난다. 월드 안개/FOV 재계산은 하지 않는다.
        /// </summary>
        private void SyncDungeonAtmosphereBackdropCenter(Camera camera)
        {
            if (camera == null || _dungeonAtmosphereBackdrop == null) return;

            Vector3 current = _dungeonAtmosphereBackdrop.transform.position;
            _dungeonAtmosphereBackdrop.transform.position = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                current.z);
        }
    }
}
