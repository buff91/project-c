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

            if (TryGetTap(out Vector2 screenPoint))
            {
                GridPos picked = PickGrid(screenPoint);
                bool exists = _gm.Map.Has(picked);
                Debug.Log($"[Tap] 화면 {screenPoint} → 격자 {picked} (타일 있음: {exists})");
                OnTileTapped(picked, exists);
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

        /// <summary>
        /// 실제 게임 반응은 여기에 연결. (M1: 이동/공격 라우팅)
        /// 지금은 M0 확인용으로 비워둠 — Debug.Log 로 검증.
        /// </summary>
        private void OnTileTapped(GridPos pos, bool tileExists)
        {
            TileTapped?.Invoke(pos, tileExists);
        }
    }
}
