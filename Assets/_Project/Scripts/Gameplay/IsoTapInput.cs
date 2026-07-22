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

        public event System.Action<GridPos, bool> TileTapped;
        public event System.Action<int> ViewRotationRequested;

        /// <summary>방향키/WASD 한 칸 이동 요청 — 격자 델타 (화면 기준 → 회전 보정 완료).</summary>
        public event System.Action<int, int> StepRequested;

        /// <summary>
        /// 화면 좌표에서 액터(몬스터 등)를 우선 집는 선택자. 게임 로직이 주입한다.
        /// 아이소 스프라이트는 발밑 타일보다 화면상 위에 그려져서, 평면 역변환만으로는
        /// 몸통 탭이 뒤쪽 타일로 새기 때문에 스프라이트 기준 보정이 필요하다.
        /// </summary>
        public System.Func<Vector2, GridPos?> ActorPicker;

        /// <summary>
        /// 탭 지점이 화면 UI 위인지 판정하는 훅 (HUD가 주입).
        /// true 면 이번 탭을 무시한다 — 버튼 클릭이 월드 이동으로 관통하는 것을 막는다.
        /// </summary>
        public System.Func<Vector2, bool> UiBlocker;

        private GridManager _gm;
        private Camera _cam;

        private void Awake()
        {
            _gm = GetComponent<GridManager>();
            _cam = Camera.main;
        }

        private void Update()
        {
            if (TryGetViewRotation(out int direction))
                ViewRotationRequested?.Invoke(direction);

            if (TryGetStep(out int viewDx, out int viewDy))
            {
                // 화면 기준 방향을 현재 회전에 맞는 격자 델타로 변환한다.
                Vector2 gridDelta = _gm.iso.RotateDeltaFromView(viewDx, viewDy);
                StepRequested?.Invoke(Mathf.RoundToInt(gridDelta.x), Mathf.RoundToInt(gridDelta.y));
            }

            if (TryGetTap(out Vector2 screenPoint))
            {
                if (UiBlocker != null && UiBlocker(screenPoint))
                    return;

                GridPos? actor = ActorPicker?.Invoke(screenPoint);
                GridPos picked = actor ?? PickGrid(screenPoint);
                bool exists = _gm.Map.Has(picked);
                Debug.Log($"[Tap] 화면 {screenPoint} → 격자 {picked} (타일 있음: {exists})");
                TileTapped?.Invoke(picked, exists);
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
    }
}
