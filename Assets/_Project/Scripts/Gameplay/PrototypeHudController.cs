using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 프로토타입용 화면 고정 HUD. 월드 표현과 분리하고 회전 요청만 Demo에 전달한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PrototypeHudController : MonoBehaviour
    {
        public IsoPrototypeDemo demo;

        private Button _rotateLeft;
        private Button _rotateRight;
        private Button _modeButton;
        private Button _combatButton;
        private Label _viewLabel;
        private Label _floorLabel;
        private Label _locationLabel;
        private Label _statusLabel;
        private Label _verticalHintLabel;

        private void OnEnable()
        {
            BindDocument();
            if (demo != null)
            {
                demo.ViewRotationChanged += HandleViewRotationChanged;
                demo.ActiveFloorChanged += HandleActiveFloorChanged;
                demo.ViewModeChanged += HandleViewModeChanged;
                demo.CombatModeChanged += HandleCombatModeChanged;
                demo.InteractionFeedback += HandleInteractionFeedback;
                demo.PlayerPositionChanged += HandlePlayerPositionChanged;
                demo.VerticalContextChanged += HandleVerticalContextChanged;
            }
        }

        private void Start()
        {
            // UIDocument의 패널이 OnEnable 뒤에 준비되는 환경도 있어 한 번 더 안전하게 연결한다.
            BindDocument();
            UpdateViewLabel();
        }

        private void OnDisable()
        {
            if (_rotateLeft != null) _rotateLeft.clicked -= RotateLeft;
            if (_rotateRight != null) _rotateRight.clicked -= RotateRight;
            if (_modeButton != null) _modeButton.clicked -= ToggleViewMode;
            if (_combatButton != null) _combatButton.clicked -= ToggleCombatMode;
            if (demo != null)
            {
                demo.ViewRotationChanged -= HandleViewRotationChanged;
                demo.ActiveFloorChanged -= HandleActiveFloorChanged;
                demo.ViewModeChanged -= HandleViewModeChanged;
                demo.CombatModeChanged -= HandleCombatModeChanged;
                demo.InteractionFeedback -= HandleInteractionFeedback;
                demo.PlayerPositionChanged -= HandlePlayerPositionChanged;
                demo.VerticalContextChanged -= HandleVerticalContextChanged;
            }
        }

        private void BindDocument()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            Button nextLeft = root.Q<Button>("rotate-left");
            Button nextRight = root.Q<Button>("rotate-right");
            Button nextMode = root.Q<Button>("mode-button");
            Button nextCombat = root.Q<Button>("combat-button");

            if (_rotateLeft != nextLeft)
            {
                if (_rotateLeft != null) _rotateLeft.clicked -= RotateLeft;
                _rotateLeft = nextLeft;
                if (_rotateLeft != null) _rotateLeft.clicked += RotateLeft;
            }

            if (_rotateRight != nextRight)
            {
                if (_rotateRight != null) _rotateRight.clicked -= RotateRight;
                _rotateRight = nextRight;
                if (_rotateRight != null) _rotateRight.clicked += RotateRight;
            }

            if (_modeButton != nextMode)
            {
                if (_modeButton != null) _modeButton.clicked -= ToggleViewMode;
                _modeButton = nextMode;
                if (_modeButton != null) _modeButton.clicked += ToggleViewMode;
            }
            if (_combatButton != nextCombat)
            {
                if (_combatButton != null) _combatButton.clicked -= ToggleCombatMode;
                _combatButton = nextCombat;
                if (_combatButton != null) _combatButton.clicked += ToggleCombatMode;
            }

            _viewLabel = root.Q<Label>("view-label");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            UpdateViewLabel();
            UpdateFloorLabel();
            UpdateModeLabel();
            UpdateCombatLabel();
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
        }

        private void RotateLeft()
        {
            if (demo != null) demo.RotateView(-1);
        }

        private void RotateRight()
        {
            if (demo != null) demo.RotateView(1);
        }

        private void ToggleViewMode()
        {
            if (demo != null) demo.ToggleViewMode();
        }

        private void ToggleCombatMode()
        {
            if (demo != null) demo.ToggleCombatMode();
        }

        private void HandleViewRotationChanged(int _)
        {
            UpdateViewLabel();
        }

        private void HandleActiveFloorChanged(int _)
        {
            UpdateFloorLabel();
        }

        private void HandleViewModeChanged(DungeonViewMode _)
        {
            UpdateModeLabel();
        }

        private void HandleCombatModeChanged(CombatActionMode _)
        {
            UpdateCombatLabel();
        }

        private void HandleInteractionFeedback(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        private void HandlePlayerPositionChanged()
        {
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
        }

        private void HandleVerticalContextChanged()
        {
            UpdateVerticalHintLabel();
        }

        private void UpdateViewLabel()
        {
            if (_viewLabel != null)
                _viewLabel.text = $"VIEW {(demo != null ? demo.ViewQuarterTurns + 1 : 1)}/4";
        }

        private void UpdateFloorLabel()
        {
            if (_floorLabel == null) return;
            _floorLabel.text = demo == null
                ? "▲ --   [B1]   ▼ B2"
                : $"▲ {demo.AboveFloorLabel}   [{demo.ActiveFloorLabel}]   ▼ {demo.BelowFloorLabel}";
        }

        private void UpdateModeLabel()
        {
            if (_modeButton != null)
                _modeButton.text = demo != null && demo.ViewMode == DungeonViewMode.DebugAll
                    ? "MODE: DEBUG ALL"
                    : "MODE: PLAY FOV";
        }

        private void UpdateCombatLabel()
        {
            if (_combatButton != null)
            {
                bool ranged = demo != null && demo.CombatMode == CombatActionMode.Ranged;
                _combatButton.text = ranged
                    ? $"ATTACK: RANGED · RANGE {demo.RangedAttackRange}"
                    : "ATTACK: MELEE";
                _combatButton.EnableInClassList("ranged", ranged);
            }
        }

        private void UpdateLocationLabel()
        {
            if (_locationLabel != null)
                _locationLabel.text = demo != null ? demo.LocationLabel : "--";
        }

        private void UpdateVerticalHintLabel()
        {
            if (_verticalHintLabel != null)
                _verticalHintLabel.text = demo != null
                    ? demo.VerticalHintLabel
                    : "EXPLORE TO FIND VERTICAL ROUTES";
        }
    }
}
