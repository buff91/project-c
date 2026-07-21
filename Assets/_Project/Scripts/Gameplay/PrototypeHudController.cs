using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private Button _frostButton;
        private Label _potionCountLabel;
        private Label _bombCountLabel;
        private Label _frostCountLabel;
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
        private Label _depthCaption;
        private Label _floorLabel;
        private Label _locationLabel;
        private Label _statusLabel;
        private Label _verticalHintLabel;
        private Label _hpValueLabel;
        private VisualElement _hpLiquid;
        private VisualElement _gameoverOverlay;
        private Label _gameoverTitle;
        private Label _gameoverCause;
        private Label _gameoverFloor;
        private Label _gameoverKills;
        private Button _restartButton;
        private Button _menuButton;
        private VisualElement _minimapView;
        private Texture2D _minimapTexture;
        private Color32[] _minimapPixels;

        private IsoTapInput _tapInput;

        private void OnEnable()
        {
            BindDocument();
            if (demo != null)
            {
                _tapInput = demo.GetComponent<IsoTapInput>();
                if (_tapInput != null) _tapInput.UiBlocker = IsPointerOverHud;
                demo.ViewRotationChanged += HandleViewRotationChanged;
                demo.ActiveFloorChanged += HandleActiveFloorChanged;
                demo.ViewModeChanged += HandleViewModeChanged;
                demo.CombatModeChanged += HandleCombatModeChanged;
                demo.InteractionFeedback += HandleInteractionFeedback;
                demo.PlayerPositionChanged += HandlePlayerPositionChanged;
                demo.VerticalContextChanged += HandleVerticalContextChanged;
                demo.InventoryChanged += HandleInventoryChanged;
                demo.BombAimingChanged += HandleBombAimingChanged;
                demo.PlayerHpChanged += HandlePlayerHpChanged;
                demo.RunEnded += HandleRunEnded;
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
            RebindButton(ref _frostButton, null, ToggleFrostBombAim);
            RebindButton(ref _settingsButton, null, OpenSettings);
            RebindButton(ref _settingsClose, null, CloseSettings);
            RebindButton(ref _settingsDone, null, CloseSettings);
            RebindButton(ref _settingsReset, null, ResetSettings);
            RebindButton(ref _restartButton, null, RestartRun);
            RebindButton(ref _menuButton, null, GoToMainMenu);
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
                demo.PlayerHpChanged -= HandlePlayerHpChanged;
                demo.RunEnded -= HandleRunEnded;
            }
            if (_tapInput != null && _tapInput.UiBlocker == IsPointerOverHud)
                _tapInput.UiBlocker = null;
            _tapInput = null;
        }

        /// <summary>
        /// 탭 스크린 좌표가 HUD의 "실질" 요소 위인지. hud-root 는 풀스크린 컨테이너라
        /// 픽 결과의 조상 체인에서 컨트롤/패널류가 나올 때만 차단한다.
        /// </summary>
        private bool IsPointerOverHud(Vector2 screenPoint)
        {
            IPanel panel = GetComponent<UIDocument>().rootVisualElement?.panel;
            if (panel == null) return false;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            VisualElement picked = panel.Pick(panelPoint);

            for (VisualElement element = picked; element != null; element = element.parent)
            {
                if (element is Button || element is Slider || element is Toggle ||
                    element is ScrollView)
                    return true;
                if (element.ClassListContains("artifact-panel") ||
                    element.ClassListContains("settings-modal") ||
                    element.ClassListContains("gameover-overlay") ||
                    element.ClassListContains("status-chip") ||
                    element.ClassListContains("orb-row") ||
                    element.ClassListContains("debug-panel"))
                    return true;
            }

            return false;
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
            RebindButton(ref _frostButton, root.Q<Button>("frost-button"), ToggleFrostBombAim);
            RebindButton(ref _settingsButton, root.Q<Button>("settings-button"), OpenSettings);
            RebindButton(ref _settingsClose, root.Q<Button>("settings-close"), CloseSettings);
            RebindButton(ref _settingsDone, root.Q<Button>("settings-done"), CloseSettings);
            RebindButton(ref _settingsReset, root.Q<Button>("settings-reset"), ResetSettings);
            RebindButton(ref _restartButton, root.Q<Button>("restart-button"), RestartRun);
            RebindButton(ref _menuButton, root.Q<Button>("menu-button"), GoToMainMenu);
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
            _depthCaption = root.Q<Label>("depth-caption");
            _floorLabel = root.Q<Label>("floor-label");
            _locationLabel = root.Q<Label>("location-label");
            _statusLabel = root.Q<Label>("status-label");
            _verticalHintLabel = root.Q<Label>("vertical-hint-label");
            _potionCountLabel = root.Q<Label>("potion-count");
            _bombCountLabel = root.Q<Label>("bomb-count");
            _frostCountLabel = root.Q<Label>("frost-count");
            _settingsModal = root.Q<VisualElement>("settings-modal");
            _hpValueLabel = root.Q<Label>("hp-value");
            _hpLiquid = root.Q<VisualElement>("hp-liquid");
            _gameoverOverlay = root.Q<VisualElement>("gameover-overlay");
            _gameoverTitle = root.Q<Label>("gameover-title");
            _gameoverCause = root.Q<Label>("gameover-cause");
            _gameoverFloor = root.Q<Label>("gameover-floor");
            _gameoverKills = root.Q<Label>("gameover-kills");
            _minimapView = root.Q<VisualElement>("minimap-view");
            UpdateMinimap();
            UpdateHpDisplay();
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

        private void ToggleFrostBombAim()
        {
            if (demo != null) demo.ToggleFrostBombAim();
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
            UpdateMinimap();
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
            UpdateMinimap();
        }

        private void HandleVerticalContextChanged()
        {
            UpdateVerticalHintLabel();
            // 시야 갱신마다 호출된다 — 미니맵 안개 상태의 단일 갱신 지점.
            UpdateMinimap();
        }

        private void UpdateMinimap()
        {
            if (_minimapView == null || demo == null) return;

            int size = demo.MinimapSize;
            if (size <= 0) return;
            if (_minimapTexture == null || _minimapTexture.width != size)
            {
                _minimapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                _minimapPixels = new Color32[size * size];
                _minimapView.style.backgroundImage = new StyleBackground(_minimapTexture);
            }

            if (!demo.FillMinimap(_minimapPixels, size, size)) return;
            _minimapTexture.SetPixels32(_minimapPixels);
            _minimapTexture.Apply(false);
        }

        private void HandleInventoryChanged()
        {
            UpdateItemLabels();
        }

        private void HandleBombAimingChanged(bool _)
        {
            UpdateAimHighlights();
        }

        private void HandlePlayerHpChanged()
        {
            UpdateHpDisplay();
        }

        private void UpdateHpDisplay()
        {
            CombatantState state = demo != null ? demo.PlayerState : null;
            if (_hpValueLabel != null)
                _hpValueLabel.text = state != null ? $"{state.Hp}/{state.MaxHp}" : "--/--";
            if (_hpLiquid != null && state != null)
                _hpLiquid.style.height = Length.Percent(100f * state.Hp / state.MaxHp);
        }

        private void HandleRunEnded(RunSummary summary)
        {
            if (_gameoverOverlay == null) return;

            bool victory = summary.Victory;
            _gameoverOverlay.EnableInClassList("is-victory", victory);
            if (_gameoverTitle != null)
                _gameoverTitle.text = victory ? "최심층 정복!" : "당신은 죽었습니다";
            if (_gameoverCause != null)
            {
                _gameoverCause.text = victory
                    ? "던전의 끝에 도달했다"
                    : $"사인: {RunSummary.FormatCause(summary.CauseOfDeath)}";
                _gameoverCause.style.display = DisplayStyle.Flex;
            }
            if (_gameoverFloor != null)
                _gameoverFloor.text = $"도달 층: {IsoPrototypeDemo.FloorLabel(summary.DeepestFloorIndex)}";
            if (_gameoverKills != null)
                _gameoverKills.text = $"처치: {summary.Kills}";
            _gameoverOverlay.AddToClassList("is-open");
        }

        private void RestartRun()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoToMainMenu()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private void UpdateItemLabels()
        {
            if (_potionCountLabel != null)
                _potionCountLabel.text = $"POTION ×{(demo != null ? demo.PotionCount : 0)}";
            if (_bombCountLabel != null)
                _bombCountLabel.text = $"BOMB ×{(demo != null ? demo.BombCount : 0)}";
            if (_frostCountLabel != null)
                _frostCountLabel.text = $"FROST ×{(demo != null ? demo.FrostBombCount : 0)}";
            UpdateAimHighlights();
        }

        private void UpdateAimHighlights()
        {
            bool aiming = demo != null && demo.BombAiming;
            _bombButton?.EnableInClassList("aiming", aiming && demo.AimedBombKind == ItemKind.Bomb);
            _frostButton?.EnableInClassList("aiming", aiming && demo.AimedBombKind == ItemKind.FrostBomb);
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
            if (_depthCaption != null)
                _depthCaption.text = demo != null ? demo.StageLabel : "던전 1/3";
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
