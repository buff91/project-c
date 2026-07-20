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
        private Button _settingsButton;
        private Button _settingsClose;
        private Button _settingsDone;
        private Button _settingsReset;
        private VisualElement _settingsModal;
        private Toggle _occlusionToggle;
        private Toggle _rearWallsToggle;
        private Slider _occlusionAlpha;
        private Slider _verticalAlpha;
        private Slider _exploredAlpha;
        private Label _viewLabel;
        private Label _depthLabel;
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
            if (_settingsButton != null) _settingsButton.clicked -= OpenSettings;
            if (_settingsClose != null) _settingsClose.clicked -= CloseSettings;
            if (_settingsDone != null) _settingsDone.clicked -= CloseSettings;
            if (_settingsReset != null) _settingsReset.clicked -= ResetSettings;
            if (_occlusionToggle != null) _occlusionToggle.UnregisterValueChangedCallback(HandleOcclusionToggle);
            if (_rearWallsToggle != null) _rearWallsToggle.UnregisterValueChangedCallback(HandleRearWallsToggle);
            if (_occlusionAlpha != null) _occlusionAlpha.UnregisterValueChangedCallback(HandleOcclusionAlpha);
            if (_verticalAlpha != null) _verticalAlpha.UnregisterValueChangedCallback(HandleVerticalAlpha);
            if (_exploredAlpha != null) _exploredAlpha.UnregisterValueChangedCallback(HandleExploredAlpha);
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
            Button nextSettings = root.Q<Button>("settings-button");
            Button nextSettingsClose = root.Q<Button>("settings-close");
            Button nextSettingsDone = root.Q<Button>("settings-done");
            Button nextSettingsReset = root.Q<Button>("settings-reset");

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
            if (_settingsButton != nextSettings)
            {
                if (_settingsButton != null) _settingsButton.clicked -= OpenSettings;
                _settingsButton = nextSettings;
                if (_settingsButton != null) _settingsButton.clicked += OpenSettings;
            }
            if (_settingsClose != nextSettingsClose)
            {
                if (_settingsClose != null) _settingsClose.clicked -= CloseSettings;
                _settingsClose = nextSettingsClose;
                if (_settingsClose != null) _settingsClose.clicked += CloseSettings;
            }
            if (_settingsDone != nextSettingsDone)
            {
                if (_settingsDone != null) _settingsDone.clicked -= CloseSettings;
                _settingsDone = nextSettingsDone;
                if (_settingsDone != null) _settingsDone.clicked += CloseSettings;
            }
            if (_settingsReset != nextSettingsReset)
            {
                if (_settingsReset != null) _settingsReset.clicked -= ResetSettings;
                _settingsReset = nextSettingsReset;
                if (_settingsReset != null) _settingsReset.clicked += ResetSettings;
            }

            BindSettingsControls(root);

            _viewLabel = root.Q<Label>("view-label");
            _depthLabel = root.Q<Label>("depth-label");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            _settingsModal = root.Q<VisualElement>("settings-modal");
            UpdateViewLabel();
            UpdateFloorLabel();
            UpdateModeLabel();
            UpdateCombatLabel();
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            SyncSettingsControls();
        }

        private void BindSettingsControls(VisualElement root)
        {
            Toggle nextOcclusionToggle = root.Q<Toggle>("occlusion-toggle");
            Toggle nextRearWallsToggle = root.Q<Toggle>("rear-walls-toggle");
            Slider nextOcclusionAlpha = root.Q<Slider>("occlusion-alpha");
            Slider nextVerticalAlpha = root.Q<Slider>("vertical-alpha");
            Slider nextExploredAlpha = root.Q<Slider>("explored-alpha");

            if (_occlusionToggle != nextOcclusionToggle)
            {
                if (_occlusionToggle != null)
                    _occlusionToggle.UnregisterValueChangedCallback(HandleOcclusionToggle);
                _occlusionToggle = nextOcclusionToggle;
                if (_occlusionToggle != null)
                    _occlusionToggle.RegisterValueChangedCallback(HandleOcclusionToggle);
            }
            if (_rearWallsToggle != nextRearWallsToggle)
            {
                if (_rearWallsToggle != null)
                    _rearWallsToggle.UnregisterValueChangedCallback(HandleRearWallsToggle);
                _rearWallsToggle = nextRearWallsToggle;
                if (_rearWallsToggle != null)
                    _rearWallsToggle.RegisterValueChangedCallback(HandleRearWallsToggle);
            }
            if (_occlusionAlpha != nextOcclusionAlpha)
            {
                if (_occlusionAlpha != null)
                    _occlusionAlpha.UnregisterValueChangedCallback(HandleOcclusionAlpha);
                _occlusionAlpha = nextOcclusionAlpha;
                if (_occlusionAlpha != null)
                    _occlusionAlpha.RegisterValueChangedCallback(HandleOcclusionAlpha);
            }
            if (_verticalAlpha != nextVerticalAlpha)
            {
                if (_verticalAlpha != null)
                    _verticalAlpha.UnregisterValueChangedCallback(HandleVerticalAlpha);
                _verticalAlpha = nextVerticalAlpha;
                if (_verticalAlpha != null)
                    _verticalAlpha.RegisterValueChangedCallback(HandleVerticalAlpha);
            }
            if (_exploredAlpha != nextExploredAlpha)
            {
                if (_exploredAlpha != null)
                    _exploredAlpha.UnregisterValueChangedCallback(HandleExploredAlpha);
                _exploredAlpha = nextExploredAlpha;
                if (_exploredAlpha != null)
                    _exploredAlpha.RegisterValueChangedCallback(HandleExploredAlpha);
            }
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

        private void OpenSettings()
        {
            SyncSettingsControls();
            _settingsModal?.AddToClassList("is-open");
        }

        private void CloseSettings()
        {
            _settingsModal?.RemoveFromClassList("is-open");
        }

        private void ResetSettings()
        {
            if (demo == null) return;
            demo.fadePlayerOccluders = true;
            demo.playerOccluderAlpha = 0.3f;
            demo.verticalPreviewAlpha = 0.54f;
            demo.exploredAlpha = 0.16f;
            demo.showRearWalls = true;
            demo.ApplyVisualSettings();
            SyncSettingsControls();
        }

        private void HandleOcclusionToggle(ChangeEvent<bool> evt)
        {
            if (demo == null) return;
            demo.fadePlayerOccluders = evt.newValue;
            demo.ApplyVisualSettings();
            _occlusionAlpha?.SetEnabled(evt.newValue);
        }

        private void HandleRearWallsToggle(ChangeEvent<bool> evt)
        {
            if (demo == null) return;
            demo.showRearWalls = evt.newValue;
            demo.ApplyVisualSettings();
        }

        private void HandleOcclusionAlpha(ChangeEvent<float> evt)
        {
            if (demo == null) return;
            demo.playerOccluderAlpha = evt.newValue;
            demo.ApplyVisualSettings();
        }

        private void HandleVerticalAlpha(ChangeEvent<float> evt)
        {
            if (demo == null) return;
            demo.verticalPreviewAlpha = evt.newValue;
            demo.ApplyVisualSettings();
        }

        private void HandleExploredAlpha(ChangeEvent<float> evt)
        {
            if (demo == null) return;
            demo.exploredAlpha = evt.newValue;
            demo.ApplyVisualSettings();
        }

        private void SyncSettingsControls()
        {
            if (demo == null) return;
            _occlusionToggle?.SetValueWithoutNotify(demo.fadePlayerOccluders);
            _rearWallsToggle?.SetValueWithoutNotify(demo.showRearWalls);
            _occlusionAlpha?.SetValueWithoutNotify(demo.playerOccluderAlpha);
            _verticalAlpha?.SetValueWithoutNotify(demo.verticalPreviewAlpha);
            _exploredAlpha?.SetValueWithoutNotify(demo.exploredAlpha);
            _occlusionAlpha?.SetEnabled(demo.fadePlayerOccluders);
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
            if (_depthLabel != null)
                _depthLabel.text = demo != null ? demo.ActiveFloorLabel : "B1";
            if (_floorLabel != null)
                _floorLabel.text = demo == null
                    ? "▲ --  ·  ▼ B2"
                    : $"▲ {demo.AboveFloorLabel}  ·  ▼ {demo.BelowFloorLabel}";
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
