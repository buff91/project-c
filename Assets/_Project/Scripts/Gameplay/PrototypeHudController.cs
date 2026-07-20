using System;
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
        private Button _potionButton;
        private Button _bombButton;
        private Label _potionCountLabel;
        private Label _bombCountLabel;
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
                demo.InventoryChanged += HandleInventoryChanged;
                demo.BombAimingChanged += HandleBombAimingChanged;
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
            // null 로 재바인딩해 구독을 해제하고 필드를 비운다.
            // 필드를 남겨두면 재활성화 시 BindDocument가 같은 요소로 판단해 재구독을 건너뛴다.
            RebindButton(ref _rotateLeft, null, RotateLeft);
            RebindButton(ref _rotateRight, null, RotateRight);
            RebindButton(ref _modeButton, null, ToggleViewMode);
            RebindButton(ref _combatButton, null, ToggleCombatMode);
            RebindButton(ref _potionButton, null, UsePotion);
            RebindButton(ref _bombButton, null, ToggleBombAim);
            RebindButton(ref _settingsButton, null, OpenSettings);
            RebindButton(ref _settingsClose, null, CloseSettings);
            RebindButton(ref _settingsDone, null, CloseSettings);
            RebindButton(ref _settingsReset, null, ResetSettings);
            RebindField<Toggle, bool>(ref _occlusionToggle, null, HandleOcclusionToggle);
            RebindField<Toggle, bool>(ref _rearWallsToggle, null, HandleRearWallsToggle);
            RebindField<Slider, float>(ref _occlusionAlpha, null, HandleOcclusionAlpha);
            RebindField<Slider, float>(ref _verticalAlpha, null, HandleVerticalAlpha);
            RebindField<Slider, float>(ref _exploredAlpha, null, HandleExploredAlpha);
            if (demo != null)
            {
                demo.ViewRotationChanged -= HandleViewRotationChanged;
                demo.ActiveFloorChanged -= HandleActiveFloorChanged;
                demo.ViewModeChanged -= HandleViewModeChanged;
                demo.CombatModeChanged -= HandleCombatModeChanged;
                demo.InteractionFeedback -= HandleInteractionFeedback;
                demo.PlayerPositionChanged -= HandlePlayerPositionChanged;
                demo.VerticalContextChanged -= HandleVerticalContextChanged;
                demo.InventoryChanged -= HandleInventoryChanged;
                demo.BombAimingChanged -= HandleBombAimingChanged;
            }
        }

        private void BindDocument()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            RebindButton(ref _rotateLeft, root.Q<Button>("rotate-left"), RotateLeft);
            RebindButton(ref _rotateRight, root.Q<Button>("rotate-right"), RotateRight);
            RebindButton(ref _modeButton, root.Q<Button>("mode-button"), ToggleViewMode);
            RebindButton(ref _combatButton, root.Q<Button>("combat-button"), ToggleCombatMode);
            RebindButton(ref _potionButton, root.Q<Button>("potion-button"), UsePotion);
            RebindButton(ref _bombButton, root.Q<Button>("bomb-button"), ToggleBombAim);
            RebindButton(ref _settingsButton, root.Q<Button>("settings-button"), OpenSettings);
            RebindButton(ref _settingsClose, root.Q<Button>("settings-close"), CloseSettings);
            RebindButton(ref _settingsDone, root.Q<Button>("settings-done"), CloseSettings);
            RebindButton(ref _settingsReset, root.Q<Button>("settings-reset"), ResetSettings);
            RebindField<Toggle, bool>(
                ref _occlusionToggle, root.Q<Toggle>("occlusion-toggle"), HandleOcclusionToggle);
            RebindField<Toggle, bool>(
                ref _rearWallsToggle, root.Q<Toggle>("rear-walls-toggle"), HandleRearWallsToggle);
            RebindField<Slider, float>(
                ref _occlusionAlpha, root.Q<Slider>("occlusion-alpha"), HandleOcclusionAlpha);
            RebindField<Slider, float>(
                ref _verticalAlpha, root.Q<Slider>("vertical-alpha"), HandleVerticalAlpha);
            RebindField<Slider, float>(
                ref _exploredAlpha, root.Q<Slider>("explored-alpha"), HandleExploredAlpha);

            _viewLabel = root.Q<Label>("view-label");
            _depthLabel = root.Q<Label>("depth-label");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            _potionCountLabel = root.Q<Label>("potion-count");
            _bombCountLabel = root.Q<Label>("bomb-count");
            _settingsModal = root.Q<VisualElement>("settings-modal");
            UpdateViewLabel();
            UpdateFloorLabel();
            UpdateModeLabel();
            UpdateCombatLabel();
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            UpdateItemLabels();
            SyncSettingsControls();
        }

        /// <summary>요소가 바뀐 경우에만 이전 구독을 풀고 새 요소에 다시 구독한다.</summary>
        private static void RebindButton(ref Button field, Button next, Action onClick)
        {
            if (field == next) return;
            if (field != null) field.clicked -= onClick;
            field = next;
            if (field != null) field.clicked += onClick;
        }

        private static void RebindField<TField, TValue>(
            ref TField field,
            TField next,
            EventCallback<ChangeEvent<TValue>> callback)
            where TField : VisualElement, INotifyValueChanged<TValue>
        {
            if (field == next) return;
            field?.UnregisterValueChangedCallback(callback);
            field = next;
            field?.RegisterValueChangedCallback(callback);
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

        private void UsePotion()
        {
            if (demo != null) demo.UsePotion();
        }

        private void ToggleBombAim()
        {
            if (demo != null) demo.ToggleBombAim();
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

        private void HandleInventoryChanged()
        {
            UpdateItemLabels();
        }

        private void HandleBombAimingChanged(bool aiming)
        {
            _bombButton?.EnableInClassList("aiming", aiming);
        }

        private void UpdateItemLabels()
        {
            if (_potionCountLabel != null)
                _potionCountLabel.text = $"POTION ×{(demo != null ? demo.PotionCount : 0)}";
            if (_bombCountLabel != null)
                _bombCountLabel.text = $"BOMB ×{(demo != null ? demo.BombCount : 0)}";
            _bombButton?.EnableInClassList("aiming", demo != null && demo.BombAiming);
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
