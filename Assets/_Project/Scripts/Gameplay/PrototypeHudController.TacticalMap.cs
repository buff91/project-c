using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// PC 전용 전술 지도 모달. 월드 카메라와 시뮬레이션은 그대로 두고 UI Toolkit 안의
    /// 기록 텍스처만 팬/줌한다. 층 선택도 활성 층을 바꾸지 않는 읽기 전용 동작이다.
    /// </summary>
    public partial class PrototypeHudController : MonoBehaviour
    {
        private static readonly float[] TacticalMapZoomSteps = { 1f, 1.5f, 2f, 3f };

        private Button _tacticalMapOpen;
        private VisualElement _tacticalMapModal;
        private Button _tacticalMapClose;
        private Label _tacticalMapFloorState;
        private VisualElement _tacticalMapFloorList;
        private VisualElement _tacticalMapViewport;
        private VisualElement _tacticalMapCanvas;
        private Label _tacticalMapFloorBadge;
        private Label _tacticalMapNorthLabel;
        private VisualElement _tacticalMapPlayerMarker;
        private Button _tacticalMapZoomOut;
        private Label _tacticalMapZoomValue;
        private Button _tacticalMapZoomIn;
        private Button _tacticalMapFit;
        private Button _tacticalMapPlayer;
        private Label _tacticalMapHint;

        private Texture2D _tacticalMapTexture;
        private Color32[] _tacticalMapPixels;
        private int _tacticalMapFloorIndex;
        private int _tacticalMapZoomStep;
        private Vector2 _tacticalMapPan;
        private int _tacticalMapDragPointerId = -1;
        private Vector2 _tacticalMapLastPointer;

        private bool IsTacticalMapOpen => IsOpen(_tacticalMapModal);

        private void BindTacticalMap(VisualElement root)
        {
            UnbindTacticalMap();

            RebindButton(
                ref _tacticalMapOpen,
                root.Q<Button>("tactical-map-open"),
                ToggleTacticalMap);
            _tacticalMapModal = root.Q<VisualElement>("tactical-map-modal");
            RebindButton(
                ref _tacticalMapClose,
                root.Q<Button>("tactical-map-close"),
                CloseTacticalMap);
            _tacticalMapFloorState = root.Q<Label>("tactical-map-floor-state");
            _tacticalMapFloorList = root.Q<VisualElement>("tactical-map-floor-list");
            _tacticalMapViewport = root.Q<VisualElement>("tactical-map-viewport");
            _tacticalMapCanvas = root.Q<VisualElement>("tactical-map-canvas");
            _tacticalMapFloorBadge = root.Q<Label>("tactical-map-floor-badge");
            _tacticalMapNorthLabel = root.Q<Label>("tactical-map-north-label");
            _tacticalMapPlayerMarker = root.Q<VisualElement>("tactical-map-player-marker");
            RebindButton(
                ref _tacticalMapZoomOut,
                root.Q<Button>("tactical-map-zoom-out"),
                ZoomTacticalMapOut);
            _tacticalMapZoomValue = root.Q<Label>("tactical-map-zoom-value");
            RebindButton(
                ref _tacticalMapZoomIn,
                root.Q<Button>("tactical-map-zoom-in"),
                ZoomTacticalMapIn);
            RebindButton(
                ref _tacticalMapFit,
                root.Q<Button>("tactical-map-fit"),
                FitTacticalMap);
            RebindButton(
                ref _tacticalMapPlayer,
                root.Q<Button>("tactical-map-player"),
                CenterTacticalMapOnPlayer);
            _tacticalMapHint = root.Q<Label>("tactical-map-hint");

            if (_tacticalMapViewport != null)
            {
                _tacticalMapViewport.RegisterCallback<PointerDownEvent>(
                    HandleTacticalMapPointerDown);
                _tacticalMapViewport.RegisterCallback<PointerMoveEvent>(
                    HandleTacticalMapPointerMove);
                _tacticalMapViewport.RegisterCallback<PointerUpEvent>(
                    HandleTacticalMapPointerUp);
                _tacticalMapViewport.RegisterCallback<PointerCaptureOutEvent>(
                    HandleTacticalMapPointerCaptureOut);
                _tacticalMapViewport.RegisterCallback<WheelEvent>(
                    HandleTacticalMapWheel);
                _tacticalMapViewport.RegisterCallback<GeometryChangedEvent>(
                    HandleTacticalMapGeometryChanged);
            }

            if (_tacticalMapCanvas != null && _tacticalMapTexture != null)
                _tacticalMapCanvas.style.backgroundImage =
                    new StyleBackground(_tacticalMapTexture);

            _tacticalMapFloorIndex = demo != null ? demo.ActiveFloorIndex : 0;
            _tacticalMapZoomStep = 0;
            _tacticalMapPan = Vector2.zero;
            UpdateTacticalMapAvailability();
            UpdateTacticalMapZoomControls();
        }

        private void UnbindTacticalMap()
        {
            if (_tacticalMapViewport != null)
            {
                _tacticalMapViewport.UnregisterCallback<PointerDownEvent>(
                    HandleTacticalMapPointerDown);
                _tacticalMapViewport.UnregisterCallback<PointerMoveEvent>(
                    HandleTacticalMapPointerMove);
                _tacticalMapViewport.UnregisterCallback<PointerUpEvent>(
                    HandleTacticalMapPointerUp);
                _tacticalMapViewport.UnregisterCallback<PointerCaptureOutEvent>(
                    HandleTacticalMapPointerCaptureOut);
                _tacticalMapViewport.UnregisterCallback<WheelEvent>(
                    HandleTacticalMapWheel);
                _tacticalMapViewport.UnregisterCallback<GeometryChangedEvent>(
                    HandleTacticalMapGeometryChanged);
                if (_tacticalMapDragPointerId >= 0 &&
                    _tacticalMapViewport.HasPointerCapture(_tacticalMapDragPointerId))
                    _tacticalMapViewport.ReleasePointer(_tacticalMapDragPointerId);
            }

            RebindButton(ref _tacticalMapOpen, null, ToggleTacticalMap);
            RebindButton(ref _tacticalMapClose, null, CloseTacticalMap);
            RebindButton(ref _tacticalMapZoomOut, null, ZoomTacticalMapOut);
            RebindButton(ref _tacticalMapZoomIn, null, ZoomTacticalMapIn);
            RebindButton(ref _tacticalMapFit, null, FitTacticalMap);
            RebindButton(ref _tacticalMapPlayer, null, CenterTacticalMapOnPlayer);
            _tacticalMapModal = null;
            _tacticalMapFloorState = null;
            _tacticalMapFloorList = null;
            _tacticalMapViewport = null;
            _tacticalMapCanvas = null;
            _tacticalMapFloorBadge = null;
            _tacticalMapNorthLabel = null;
            _tacticalMapPlayerMarker = null;
            _tacticalMapZoomValue = null;
            _tacticalMapHint = null;
            _tacticalMapDragPointerId = -1;
        }

        private void DisposeTacticalMapTexture()
        {
            if (_tacticalMapTexture == null) return;
            if (Application.isPlaying) Destroy(_tacticalMapTexture);
            else DestroyImmediate(_tacticalMapTexture);
            _tacticalMapTexture = null;
            _tacticalMapPixels = null;
        }

        private void ToggleTacticalMap()
        {
            if (IsTacticalMapOpen) CloseTacticalMap();
            else OpenTacticalMap();
        }

        private void OpenTacticalMap()
        {
            if (ActivePresentation != HudPresentationMode.Desktop ||
                demo == null ||
                (_displaySettings != null && _displaySettings.IsOpen) ||
                IsOpen(_exitModal) || IsOpen(_gameoverOverlay))
                return;

            // 지도는 읽기 전용 최상위 모달이다. 기존 임시 상태를 한 번에 닫아
            // 조준·낙하 확인·수직 관찰의 의미가 지도 위에 겹치지 않게 한다.
            demo.CancelThrowAim();
            demo.CancelDropConfirmation();
            demo.CancelVerticalLook();
            // 취소 가능한 조준/확인 상태를 먼저 걷은 뒤 다시 판정한다. 행동 해결 중처럼
            // 취소할 수 없는 상태라면 MAP 버튼과 M 모두 여기서 같은 방식으로 거절한다.
            if (!demo.CanOpenMapInspection) return;

            demo.RecenterCamera();
            CloseTransientOverlays();

            _tacticalMapFloorIndex = demo.ActiveFloorIndex;
            _tacticalMapZoomStep = 0;
            _tacticalMapPan = Vector2.zero;
            RebuildTacticalMapFloors();
            RefreshTacticalMapTexture();
            _tacticalMapModal?.BringToFront();
            _tacticalMapModal?.AddToClassList("is-open");
            _tacticalMapViewport?.Focus();
        }

        private void CloseTacticalMap()
        {
            EndTacticalMapDrag();
            _tacticalMapModal?.RemoveFromClassList("is-open");
        }

        private void UpdateTacticalMapAvailability()
        {
            bool hasDesktopMap = ActivePresentation == HudPresentationMode.Desktop &&
                                 demo != null;
            // resolving 동안도 버튼을 회색으로 깜빡이지 않는다. 클릭/M은 OpenTacticalMap의
            // 취소→재판정 한 경로를 거쳐 안전하게 거절된다.
            _tacticalMapOpen?.SetEnabled(hasDesktopMap);
            if (IsTacticalMapOpen &&
                (!hasDesktopMap || !demo.CanOpenMapInspection))
                CloseTacticalMap();
        }

        private void RebuildTacticalMapFloors()
        {
            if (_tacticalMapFloorList == null) return;
            _tacticalMapFloorList.Clear();
            if (demo == null) return;

            List<DungeonFloorInfo> floors = SortedFloorsTopFirst();
            if (floors == null) return;
            if (!demo.CanInspectFloor(_tacticalMapFloorIndex))
                _tacticalMapFloorIndex = demo.ActiveFloorIndex;

            foreach (DungeonFloorInfo floor in floors)
            {
                int floorIndex = floor.FloorIndex;
                bool inspectable = demo.CanInspectFloor(floorIndex);
                var button = new Button(() => SelectTacticalMapFloor(floorIndex))
                {
                    text = demo.FloorLabel(floorIndex),
                    tooltip = inspectable
                        ? $"{demo.FloorLabel(floorIndex)} 지도 보기"
                        : "아직 지도 데이터가 없다"
                };
                button.AddToClassList("tactical-map-floor-button");
                button.EnableInClassList("is-explored", inspectable);
                button.EnableInClassList(
                    "is-current", floorIndex == demo.ActiveFloorIndex);
                button.EnableInClassList(
                    "is-selected", floorIndex == _tacticalMapFloorIndex);
                button.SetEnabled(inspectable);
                _tacticalMapFloorList.Add(button);
            }

            UpdateTacticalMapLabels();
        }

        private void SelectTacticalMapFloor(int floorIndex)
        {
            if (demo == null || !demo.CanInspectFloor(floorIndex)) return;
            _tacticalMapFloorIndex = floorIndex;
            _tacticalMapZoomStep = 0;
            _tacticalMapPan = Vector2.zero;
            RebuildTacticalMapFloors();
            RefreshTacticalMapTexture();
        }

        private void RefreshTacticalMapTexture()
        {
            if (_tacticalMapCanvas == null || demo == null ||
                !demo.CanInspectFloor(_tacticalMapFloorIndex))
                return;

            int size = demo.MinimapSize;
            if (size <= 0) return;
            if (_tacticalMapTexture == null || _tacticalMapTexture.width != size ||
                _tacticalMapTexture.height != size)
            {
                DisposeTacticalMapTexture();
                _tacticalMapTexture = new Texture2D(
                    size, size, TextureFormat.RGBA32, false)
                {
                    name = "Tactical Map Runtime",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _tacticalMapPixels = new Color32[size * size];
                _tacticalMapCanvas.style.backgroundImage =
                    new StyleBackground(_tacticalMapTexture);
            }

            if (!demo.FillInspectionMap(
                    _tacticalMapPixels,
                    size,
                    size,
                    _tacticalMapFloorIndex))
                return;

            _tacticalMapTexture.SetPixels32(_tacticalMapPixels);
            _tacticalMapTexture.Apply(false);
            UpdateTacticalMapLabels();
            UpdateTacticalMapPlayerMarker();
            ApplyTacticalMapTransform();
        }

        private void UpdateTacticalMapLabels()
        {
            if (demo == null) return;
            string play = demo.FloorLabel(demo.ActiveFloorIndex);
            string viewed = demo.FloorLabel(_tacticalMapFloorIndex);
            bool isCurrentFloor = _tacticalMapFloorIndex == demo.ActiveFloorIndex;
            if (_tacticalMapFloorState != null)
                _tacticalMapFloorState.text = isCurrentFloor
                    ? $"플레이 {play} · 현재 층"
                    : $"플레이 {play} · 기록 {viewed} · 이동/전투 불가";
            if (_tacticalMapFloorBadge != null)
                _tacticalMapFloorBadge.text = viewed;
            if (_tacticalMapNorthLabel != null)
                _tacticalMapNorthLabel.text = "N";
            if (_tacticalMapHint != null)
                _tacticalMapHint.text = isCurrentFloor
                    ? "현재 층 · 드래그 이동 · 휠 확대/축소 · M/ESC 닫기"
                    : "기록 지도 · 적/아이템 비표시 · 이동/전투 불가";
        }

        private void UpdateTacticalMapPlayerMarker()
        {
            if (_tacticalMapPlayerMarker == null || demo == null) return;
            bool show = _tacticalMapFloorIndex == demo.ActiveFloorIndex;
            _tacticalMapPlayerMarker.style.display =
                show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            int size = demo.MinimapSize;
            GridPos player = demo.PlayerPos;
            _tacticalMapPlayerMarker.style.left =
                Length.Percent(MinimapMarkerPercent(player.x, size));
            _tacticalMapPlayerMarker.style.bottom =
                Length.Percent(MinimapMarkerPercent(player.y, size));
        }

        private void ZoomTacticalMapOut() => ChangeTacticalMapZoom(-1, Vector2.zero);

        private void ZoomTacticalMapIn() => ChangeTacticalMapZoom(1, Vector2.zero);

        private void ChangeTacticalMapZoom(int direction, Vector2 focalOffset)
        {
            int next = Mathf.Clamp(
                _tacticalMapZoomStep + direction,
                0,
                TacticalMapZoomSteps.Length - 1);
            if (next == _tacticalMapZoomStep) return;

            float ratio = TacticalMapZoomSteps[next] /
                          TacticalMapZoomSteps[_tacticalMapZoomStep];
            // 포인터 아래의 지도 좌표를 유지한다. 버튼 줌은 focalOffset=0이라 중앙 기준이다.
            _tacticalMapPan = _tacticalMapPan * ratio + focalOffset * (1f - ratio);
            _tacticalMapZoomStep = next;
            ApplyTacticalMapTransform();
        }

        private void FitTacticalMap()
        {
            _tacticalMapZoomStep = 0;
            _tacticalMapPan = Vector2.zero;
            ApplyTacticalMapTransform();
        }

        private void CenterTacticalMapOnPlayer()
        {
            if (demo == null || !demo.CanInspectFloor(demo.ActiveFloorIndex)) return;
            bool floorChanged = _tacticalMapFloorIndex != demo.ActiveFloorIndex;
            _tacticalMapFloorIndex = demo.ActiveFloorIndex;
            if (floorChanged)
            {
                RebuildTacticalMapFloors();
                RefreshTacticalMapTexture();
            }

            Rect viewport = _tacticalMapViewport != null
                ? _tacticalMapViewport.contentRect
                : default;
            float baseSide = Mathf.Min(viewport.width, viewport.height);
            if (baseSide <= 0f) return;
            float canvasSide = baseSide * TacticalMapZoomSteps[_tacticalMapZoomStep];
            int size = Mathf.Max(1, demo.MinimapSize);
            float x = (Mathf.Clamp(demo.PlayerPos.x, 0, size - 1) + 0.5f) / size;
            float y = (Mathf.Clamp(demo.PlayerPos.y, 0, size - 1) + 0.5f) / size;
            _tacticalMapPan = new Vector2(
                (0.5f - x) * canvasSide,
                (y - 0.5f) * canvasSide);
            ApplyTacticalMapTransform();
        }

        private void ApplyTacticalMapTransform()
        {
            // 레이아웃 전 첫 입력도 배율 상태와 버튼에는 즉시 반영한다. 캔버스 치수는
            // GeometryChanged에서 뒤따라 적용되지만 readout이 한 프레임 낡아 있으면 안 된다.
            UpdateTacticalMapZoomControls();
            if (_tacticalMapViewport == null || _tacticalMapCanvas == null) return;
            Rect viewport = _tacticalMapViewport.contentRect;
            if (viewport.width <= 0f || viewport.height <= 0f ||
                float.IsNaN(viewport.width) || float.IsNaN(viewport.height))
                return;

            float canvasSide = Mathf.Min(viewport.width, viewport.height) *
                               TacticalMapZoomSteps[_tacticalMapZoomStep];
            float horizontalLimit = Mathf.Max(0f, (canvasSide - viewport.width) * 0.5f);
            float verticalLimit = Mathf.Max(0f, (canvasSide - viewport.height) * 0.5f);
            _tacticalMapPan.x = Mathf.Clamp(
                _tacticalMapPan.x, -horizontalLimit, horizontalLimit);
            _tacticalMapPan.y = Mathf.Clamp(
                _tacticalMapPan.y, -verticalLimit, verticalLimit);

            _tacticalMapCanvas.style.width = canvasSide;
            _tacticalMapCanvas.style.height = canvasSide;
            _tacticalMapCanvas.style.left =
                (viewport.width - canvasSide) * 0.5f + _tacticalMapPan.x;
            _tacticalMapCanvas.style.top =
                (viewport.height - canvasSide) * 0.5f + _tacticalMapPan.y;
        }

        private void UpdateTacticalMapZoomControls()
        {
            int percent = Mathf.RoundToInt(
                TacticalMapZoomSteps[Mathf.Clamp(
                    _tacticalMapZoomStep,
                    0,
                    TacticalMapZoomSteps.Length - 1)] * 100f);
            if (_tacticalMapZoomValue != null)
                _tacticalMapZoomValue.text = $"{percent}%";
            _tacticalMapZoomOut?.SetEnabled(_tacticalMapZoomStep > 0);
            _tacticalMapZoomIn?.SetEnabled(
                _tacticalMapZoomStep < TacticalMapZoomSteps.Length - 1);
        }

        private void HandleTacticalMapPointerDown(PointerDownEvent evt)
        {
            if (!IsTacticalMapOpen || (evt.button != 0 && evt.button != 2)) return;
            _tacticalMapDragPointerId = evt.pointerId;
            _tacticalMapLastPointer = evt.position;
            _tacticalMapViewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void HandleTacticalMapPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _tacticalMapDragPointerId ||
                !_tacticalMapViewport.HasPointerCapture(evt.pointerId))
                return;

            Vector2 current = evt.position;
            _tacticalMapPan += current - _tacticalMapLastPointer;
            _tacticalMapLastPointer = current;
            ApplyTacticalMapTransform();
            evt.StopPropagation();
        }

        private void HandleTacticalMapPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _tacticalMapDragPointerId) return;
            EndTacticalMapDrag();
            evt.StopPropagation();
        }

        private void HandleTacticalMapPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _tacticalMapDragPointerId)
                _tacticalMapDragPointerId = -1;
        }

        private void EndTacticalMapDrag()
        {
            if (_tacticalMapViewport != null && _tacticalMapDragPointerId >= 0 &&
                _tacticalMapViewport.HasPointerCapture(_tacticalMapDragPointerId))
                _tacticalMapViewport.ReleasePointer(_tacticalMapDragPointerId);
            _tacticalMapDragPointerId = -1;
        }

        private void HandleTacticalMapWheel(WheelEvent evt)
        {
            if (!IsTacticalMapOpen || Mathf.Approximately(evt.delta.y, 0f)) return;
            Vector2 focal = _tacticalMapViewport.WorldToLocal(evt.mousePosition) -
                            _tacticalMapViewport.contentRect.center;
            ChangeTacticalMapZoom(evt.delta.y < 0f ? 1 : -1, focal);
            evt.StopPropagation();
        }

        private void HandleTacticalMapGeometryChanged(GeometryChangedEvent _) =>
            ApplyTacticalMapTransform();

        private bool IsWorldCommandBlocked() => AnyModalOpen();
    }
}
