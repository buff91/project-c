using UnityEngine;
using ProjectC.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 탭/클릭 → 격자 좌표 역변환 확인용. (M0 "탭 → 격자 좌표 역변환")
    ///
    /// 입력 추상화(§12): 실제 게임 로직은 여기서 나온 GridPos 만 소비하면 되고,
    /// 어떤 입력장치(터치/마우스)인지는 이 레이어가 흡수한다.
    /// Input System 패키지가 있으면 그걸, 없으면 레거시 Input 을 자동 사용.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class IsoTapInput : MonoBehaviour
    {
        [Tooltip("역변환 기준 elevation 평면. (M0 데모: 0층 클릭 확인)")]
        public int targetElevation = 0;

        [Header("다중 높이 선택")]
        public bool pickHighestExisting = true;
        public int minElevation = -3;
        public int maxElevation = 1;

        [Tooltip("포인터가 올라간 칸을 매 프레임 추적할지. 조준처럼 필요한 순간에만 켠다.")]
        public bool trackHover;

        public event System.Action<GridPos, bool> TileTapped;
        public event System.Action<int> ViewRotationRequested;

        /// <summary>
        /// 포인터가 올라간 격자 칸이 바뀔 때만 발생한다(칸이 없으면 null).
        /// 마우스가 없는 기기에서는 아예 발생하지 않는다 — 호버가 없는 입력을 흉내내지 않는다.
        /// </summary>
        public event System.Action<GridPos?> TileHovered;

        /// <summary>방향키/WASD 한 칸 이동 요청 — 격자 델타 (화면 기준 → 회전 보정 완료).</summary>
        public event System.Action<int, int> StepRequested;

        /// <summary>스페이스바 — 인접 상호작용/근접공격 요청.</summary>
        public event System.Action InteractRequested;

        /// <summary>X 키 — 대기(턴 스킵) 요청.</summary>
        public event System.Action WaitRequested;

        /// <summary>PC 중클릭 드래그 — 화면 픽셀 기준 카메라 팬 요청.</summary>
        public event System.Action<Vector2> CameraPanRequested;

        /// <summary>Home 키 — 플레이어 추적 카메라로 복귀 요청.</summary>
        public event System.Action CameraRecenterRequested;

        /// <summary>
        /// PC 액션 휠 홀드 상태. 범용 Ctrl/Cmd 수정키는 OS 단축키와 충돌하므로
        /// 입력 레이어가 전용 Tab 액션으로 흡수해 HUD에 노출한다.
        /// </summary>
        public bool ActionWheelHeld => ActionWheelHoldKeyHeld();

        /// <summary>
        /// 화면 좌표에서 액터(몬스터 등)를 우선 집는 선택자. 게임 로직이 주입한다.
        /// 아이소 스프라이트는 발밑 타일보다 화면상 위에 그려져서, 평면 역변환만으로는
        /// 몸통 탭이 뒤쪽 타일로 새기 때문에 스프라이트 기준 보정이 필요하다.
        /// </summary>
        public System.Func<Vector2, GridPos?> ActorPicker;

        /// <summary>
        /// 화면에 실제 렌더된 타일을 고르는 선택자. 게임 로직이 주입하면 전체 elevation
        /// 역산 대신 현재 활성 층과 개구부 미리보기의 실제 표시 위치를 기준으로 선택한다.
        /// </summary>
        public System.Func<Vector2, GridPos?> TilePicker;

        /// <summary>
        /// 탭 지점이 화면 UI 위인지 판정하는 훅 (HUD가 주입).
        /// true 면 이번 탭을 무시한다 — 버튼 클릭이 월드 이동으로 관통하는 것을 막는다.
        /// </summary>
        public System.Func<Vector2, bool> UiBlocker;

        private GridManager _gm;
        private Camera _cam;
        private GridPos? _hovered;
        private Vector2 _lastHoverPoint = new Vector2(float.NaN, float.NaN);
        private bool _cameraPanGestureActive;
        private Vector2 _lastCameraPanPoint = new Vector2(float.NaN, float.NaN);

        private void Awake()
        {
            _gm = GetComponent<GridManager>();
            _cam = Camera.main;
        }

        private void Update()
        {
            UpdateCameraPan();
            if (CameraRecenterPressed()) CameraRecenterRequested?.Invoke();

            if (TryGetViewRotation(out int direction))
                ViewRotationRequested?.Invoke(direction);

            if (TryGetStep(out int viewDx, out int viewDy))
            {
                // 화면 기준 방향을 현재 회전에 맞는 격자 델타로 변환한다.
                Vector2 gridDelta = _gm.iso.RotateDeltaFromView(viewDx, viewDy);
                StepRequested?.Invoke(Mathf.RoundToInt(gridDelta.x), Mathf.RoundToInt(gridDelta.y));
            }

            if (SpacePressed()) InteractRequested?.Invoke();
            if (XPressed()) WaitRequested?.Invoke();

            UpdateHover();

            // 중클릭 드래그 중에는 월드가 읽기 전용이다. 왼쪽 버튼을 함께 눌러도
            // 카메라 이동과 타일 행동이 같은 프레임에 섞이지 않는다.
            if (!_cameraPanGestureActive && TryGetTap(out Vector2 screenPoint))
            {
                if (UiBlocker != null && UiBlocker(screenPoint))
                    return;

                GridPos? actor = ActorPicker?.Invoke(screenPoint);
                GridPos? tile = actor.HasValue ? null : TilePicker?.Invoke(screenPoint);
                if (!actor.HasValue && TilePicker != null && !tile.HasValue)
                    return;

                GridPos picked = actor ?? tile ?? PickGrid(screenPoint);
                bool exists = WorldInputRules.IsMapTile(_gm.Map, picked);
                Debug.Log($"[Tap] 화면 {screenPoint} → 격자 {picked} (타일 있음: {exists})");

                // 유한한 맵 바깥의 검은 여백도 역변환하면 GridPos는 만들어진다.
                // 여기서 걸러야 미탐색 자동 이동이나 조준 입력으로 관통하지 않는다.
                if (!exists) return;

                TileTapped?.Invoke(picked, true);
            }
        }

        private static bool TryGetViewRotation(out int direction)
        {
            direction = 0;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.qKey.wasPressedThisFrame)
            {
                direction = -1;
                return true;
            }
            if (keyboard.eKey.wasPressedThisFrame)
            {
                direction = 1;
                return true;
            }
            return false;
#else
            if (Input.GetKeyDown(KeyCode.Q))
            {
                direction = -1;
                return true;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                direction = 1;
                return true;
            }
            return false;
#endif
        }

        /// <summary>
        /// 화면 기준 한 칸 이동 입력 (PC): ↑/W=오른쪽 위, →/D=오른쪽 아래,
        /// ↓/S=왼쪽 아래, ←/A=왼쪽 위. 뷰 좌표 델타로 반환한다.
        /// </summary>
        private static bool TryGetStep(out int viewDx, out int viewDy)
        {
            viewDx = 0;
            viewDy = 0;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            { viewDy = -1; return true; }
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            { viewDx = 1; return true; }
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            { viewDy = 1; return true; }
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            { viewDx = -1; return true; }
            return false;
#else
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { viewDy = -1; return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { viewDx = 1; return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { viewDy = 1; return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { viewDx = -1; return true; }
            return false;
#endif
        }

        private static bool SpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool XPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.xKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.X);
#endif
        }

        private void UpdateCameraPan()
        {
            if (!TryGetCameraPanPointer(
                    out Vector2 screenPoint,
                    out bool pressedThisFrame,
                    out bool held))
            {
                CancelCameraPanGesture();
                return;
            }

            if (pressedThisFrame)
            {
                _cameraPanGestureActive = UiBlocker == null || !UiBlocker(screenPoint);
                _lastCameraPanPoint = screenPoint;
            }

            if (!_cameraPanGestureActive) return;
            if (!held)
            {
                CancelCameraPanGesture();
                return;
            }

            Vector2 delta = screenPoint - _lastCameraPanPoint;
            _lastCameraPanPoint = screenPoint;
            if (delta.sqrMagnitude > 0f)
                CameraPanRequested?.Invoke(delta);
        }

        /// <summary>
        /// 행동·회전·층 전환이 카메라 추적을 되찾을 때 현재 중클릭 제스처도 끝낸다.
        /// 버튼을 계속 누르고 있어도 다시 눌렀다 떼기 전에는 팬을 재개하지 않는다.
        /// </summary>
        public void CancelCameraPanGesture()
        {
            _cameraPanGestureActive = false;
            _lastCameraPanPoint = new Vector2(float.NaN, float.NaN);
        }

        private static bool TryGetCameraPanPointer(
            out Vector2 screenPoint,
            out bool pressedThisFrame,
            out bool held)
        {
            screenPoint = default;
            pressedThisFrame = false;
            held = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            screenPoint = mouse.position.ReadValue();
            pressedThisFrame = mouse.middleButton.wasPressedThisFrame;
            held = mouse.middleButton.isPressed;
            return true;
#else
            if (!Input.mousePresent) return false;
            screenPoint = Input.mousePosition;
            pressedThisFrame = Input.GetMouseButtonDown(2);
            held = Input.GetMouseButton(2);
            return true;
#endif
        }

        private static bool CameraRecenterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.homeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Home);
#endif
        }

        private static bool ActionWheelHoldKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.isPressed;
#else
            return Input.GetKey(KeyCode.Tab);
#endif
        }

        private GridPos PickGrid(Vector2 screenPoint)
        {
            if (!pickHighestExisting)
                return _gm.ScreenToGrid(screenPoint, _cam, targetElevation);

            for (int elevation = maxElevation; elevation >= minElevation; elevation--)
            {
                GridPos candidate = _gm.ScreenToGrid(screenPoint, _cam, elevation);
                if (_gm.Map.Has(candidate))
                    return candidate;
            }

            return _gm.ScreenToGrid(screenPoint, _cam, targetElevation);
        }

        /// <summary>
        /// 포인터가 올라간 칸을 갱신하고, <b>바뀐 프레임에만</b> 알린다.
        /// 화면 좌표가 그대로면 픽 자체를 건너뛴다 — TilePicker 는 렌더된 타일을 전부 훑으므로
        /// 조준 중 매 프레임 돌리면 공짜가 아니다. 조준은 턴 사이 정지 상태라 카메라도 멈춰 있다.
        /// </summary>
        private void UpdateHover()
        {
            if (!trackHover)
            {
                bool hadHover = _hovered.HasValue;
                _hovered = null;
                _lastHoverPoint = new Vector2(float.NaN, float.NaN);
                if (hadHover) TileHovered?.Invoke(null);
                return;
            }

            if (!TryGetPointerPosition(out Vector2 screenPoint))
            {
                if (_hovered == null) return;
                _hovered = null;
                TileHovered?.Invoke(null);
                return;
            }

            if (screenPoint == _lastHoverPoint) return;
            _lastHoverPoint = screenPoint;

            GridPos? hovered = ResolveHover(screenPoint);
            if (System.Nullable.Equals(hovered, _hovered)) return;

            _hovered = hovered;
            TileHovered?.Invoke(hovered);
        }

        /// <summary>
        /// 시점 회전이나 카메라 이동 뒤 포인터가 움직이지 않아도 다음 프레임에 다시 픽한다.
        /// 현재 hover를 먼저 지워 소비자가 오래된 칸을 강조하지 않게 한다.
        /// </summary>
        public void InvalidateHover()
        {
            bool hadHover = _hovered.HasValue;
            _hovered = null;
            _lastHoverPoint = new Vector2(float.NaN, float.NaN);
            if (hadHover) TileHovered?.Invoke(null);
        }

        /// <summary>탭과 같은 선택자를 쓴다 — 조준선이 가리키는 칸과 탭이 고르는 칸이 갈리면 안 된다.</summary>
        private GridPos? ResolveHover(Vector2 screenPoint)
        {
            if (UiBlocker != null && UiBlocker(screenPoint)) return null;

            GridPos? actor = ActorPicker?.Invoke(screenPoint);
            if (actor.HasValue) return actor;

            GridPos? tile = TilePicker?.Invoke(screenPoint);
            if (tile.HasValue) return tile;
            if (TilePicker != null) return null;

            GridPos picked = PickGrid(screenPoint);
            return WorldInputRules.IsMapTile(_gm.Map, picked) ? picked : (GridPos?)null;
        }

        /// <summary>마우스가 있을 때만 화면 좌표를 준다. 터치는 호버가 없다.</summary>
        private static bool TryGetPointerPosition(out Vector2 screenPoint)
        {
            screenPoint = default;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            screenPoint = mouse.position.ReadValue();
            return true;
#else
            if (!Input.mousePresent) return false;
            screenPoint = Input.mousePosition;
            return true;
#endif
        }

        /// <summary>이번 프레임에 '눌림'이 있었으면 스크린 좌표 반환.</summary>
        private bool TryGetTap(out Vector2 screenPoint)
        {
            screenPoint = default;
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPoint = pointer.position.ReadValue();
                return true;
            }
            return false;
#else
            if (Input.GetMouseButtonDown(0))
            {
                screenPoint = Input.mousePosition;
                return true;
            }
            return false;
#endif
        }

        private void OnDisable() => CancelCameraPanGesture();
    }
}
