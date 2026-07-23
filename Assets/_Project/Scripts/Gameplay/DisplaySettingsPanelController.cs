using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>공용 설정 UXML의 바인딩과 저장을 소유한다.</summary>
    public sealed class DisplaySettingsPanelController : IDisposable
    {
        private readonly IsoPrototypeDemo _demo;
        private readonly Button _openButton;
        private readonly Button _closeButton;
        private readonly Button _doneButton;
        private readonly Button _resetButton;
        private readonly VisualElement _modal;
        private readonly Toggle _occlusionToggle;
        private readonly Toggle _rearWallsToggle;
        private readonly Slider _occlusionAlpha;
        private readonly Slider _verticalAlpha;
        private readonly Slider _exploredAlpha;
        private readonly VisualElement _developmentViewport;
        private readonly DropdownField _viewportMode;
        private readonly DropdownField _viewportResolution;
        private readonly Button _viewportApply;
        private readonly Label _viewportStatus;
        private readonly VisualElement _templateHost;
        private readonly Action _beforeOpen;
        private DisplaySettingsData _settings;
        private bool _disposed;

        public bool IsOpen => _modal != null && _modal.ClassListContains("is-open");

        public DisplaySettingsPanelController(
            VisualElement root,
            IsoPrototypeDemo demo,
            string openButtonName,
            Action beforeOpen = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _demo = demo;
            _beforeOpen = beforeOpen;
            _openButton = root.Q<Button>(openButtonName);
            _closeButton = root.Q<Button>("settings-close");
            _doneButton = root.Q<Button>("settings-done");
            _resetButton = root.Q<Button>("settings-reset");
            _modal = root.Q<VisualElement>("settings-modal");
            _occlusionToggle = root.Q<Toggle>("occlusion-toggle");
            _rearWallsToggle = root.Q<Toggle>("rear-walls-toggle");
            _occlusionAlpha = root.Q<Slider>("occlusion-alpha");
            _verticalAlpha = root.Q<Slider>("vertical-alpha");
            _exploredAlpha = root.Q<Slider>("explored-alpha");
            _developmentViewport = root.Q<VisualElement>("development-viewport");
            _viewportMode = root.Q<DropdownField>("viewport-mode");
            _viewportResolution = root.Q<DropdownField>("viewport-resolution");
            _viewportApply = root.Q<Button>("viewport-apply");
            _viewportStatus = root.Q<Label>("viewport-status");
            _templateHost = _modal?.parent;

            if (_openButton != null) _openButton.clicked += Open;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_doneButton != null) _doneButton.clicked += Close;
            if (_resetButton != null) _resetButton.clicked += Reset;
            if (_viewportApply != null) _viewportApply.clicked += ApplyDevelopmentViewport;
            _occlusionToggle?.RegisterValueChangedCallback(HandleOcclusionToggle);
            _rearWallsToggle?.RegisterValueChangedCallback(HandleRearWallsToggle);
            _occlusionAlpha?.RegisterValueChangedCallback(HandleOcclusionAlpha);
            _verticalAlpha?.RegisterValueChangedCallback(HandleVerticalAlpha);
            _exploredAlpha?.RegisterValueChangedCallback(HandleExploredAlpha);

            _settings = DisplaySettingsStore.Load();
            Apply();
            ConfigureDevelopmentViewport();
            SyncControls();
        }

        public void Open()
        {
            if (_disposed) return;
            _beforeOpen?.Invoke();
            _templateHost?.BringToFront();
            _modal?.BringToFront();
            SyncControls();
            _modal?.AddToClassList("is-open");
        }

        public void Close()
        {
            if (_disposed) return;
            _modal?.RemoveFromClassList("is-open");
            DisplaySettingsStore.Save(_settings);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Close();
            _disposed = true;
            if (_openButton != null) _openButton.clicked -= Open;
            if (_closeButton != null) _closeButton.clicked -= Close;
            if (_doneButton != null) _doneButton.clicked -= Close;
            if (_resetButton != null) _resetButton.clicked -= Reset;
            if (_viewportApply != null) _viewportApply.clicked -= ApplyDevelopmentViewport;
            _occlusionToggle?.UnregisterValueChangedCallback(HandleOcclusionToggle);
            _rearWallsToggle?.UnregisterValueChangedCallback(HandleRearWallsToggle);
            _occlusionAlpha?.UnregisterValueChangedCallback(HandleOcclusionAlpha);
            _verticalAlpha?.UnregisterValueChangedCallback(HandleVerticalAlpha);
            _exploredAlpha?.UnregisterValueChangedCallback(HandleExploredAlpha);
        }

        private void Reset()
        {
            _settings = DisplaySettingsData.Defaults;
            Apply();
            SyncControls();
            DisplaySettingsStore.Save(_settings);
        }

        private void HandleOcclusionToggle(ChangeEvent<bool> evt)
        {
            _settings.FadePlayerOccluders = evt.newValue;
            Apply();
            _occlusionAlpha?.SetEnabled(evt.newValue);
        }

        private void HandleRearWallsToggle(ChangeEvent<bool> evt)
        {
            _settings.ShowRearWalls = evt.newValue;
            Apply();
        }

        private void HandleOcclusionAlpha(ChangeEvent<float> evt)
        {
            _settings.PlayerOccluderAlpha = evt.newValue;
            Apply();
        }

        private void HandleVerticalAlpha(ChangeEvent<float> evt)
        {
            _settings.VerticalPreviewAlpha = evt.newValue;
            Apply();
        }

        private void HandleExploredAlpha(ChangeEvent<float> evt)
        {
            _settings.ExploredAlpha = evt.newValue;
            Apply();
        }

        private void Apply()
        {
            _settings = _settings.Clamped();
            DisplaySettingsStore.Apply(_settings, _demo);
        }

        private void ConfigureDevelopmentViewport()
        {
            if (_developmentViewport == null) return;
            _developmentViewport.style.display = DevelopmentViewportService.IsAvailable
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!DevelopmentViewportService.IsAvailable) return;

            if (_viewportMode != null)
            {
                _viewportMode.choices = new List<string> { "자동", "모바일", "PC" };
                _viewportMode.index = (int)DevelopmentViewportService.SelectedMode;
            }

            if (_viewportResolution != null)
            {
                var choices = new List<string>();
                foreach (DevelopmentViewportPreset preset in DevelopmentViewportService.Presets)
                    choices.Add(preset.Label);
                _viewportResolution.choices = choices;
                DevelopmentViewportPreset selected = DevelopmentViewportService.FindPreset(
                    DevelopmentViewportService.SelectedPresetId);
                _viewportResolution.SetValueWithoutNotify(selected.Label);
            }
            UpdateViewportStatus();
        }

        private void ApplyDevelopmentViewport()
        {
            HudPresentationMode mode = ModeFromChoice(_viewportMode?.value);
            string presetId = DevelopmentViewportService.SelectedPresetId;
            if (_viewportResolution != null)
            {
                foreach (DevelopmentViewportPreset preset in DevelopmentViewportService.Presets)
                {
                    if (preset.Label == _viewportResolution.value)
                    {
                        presetId = preset.Id;
                        break;
                    }
                }
            }
            string result = DevelopmentViewportService.Apply(mode, presetId);
            if (_viewportStatus != null) _viewportStatus.text = result;
        }

        private void UpdateViewportStatus()
        {
            if (_viewportStatus == null) return;
            DevelopmentViewportPreset preset = DevelopmentViewportService.FindPreset(
                DevelopmentViewportService.SelectedPresetId);
            _viewportStatus.text = $"현재: {DevelopmentViewportService.ModeLabel(DevelopmentViewportService.SelectedMode)} · {preset.Label}";
        }

        private static HudPresentationMode ModeFromChoice(string choice)
        {
            if (choice == "모바일") return HudPresentationMode.Mobile;
            if (choice == "PC") return HudPresentationMode.Desktop;
            return HudPresentationMode.Auto;
        }

        private void SyncControls()
        {
            _occlusionToggle?.SetValueWithoutNotify(_settings.FadePlayerOccluders);
            _rearWallsToggle?.SetValueWithoutNotify(_settings.ShowRearWalls);
            _occlusionAlpha?.SetValueWithoutNotify(_settings.PlayerOccluderAlpha);
            _verticalAlpha?.SetValueWithoutNotify(_settings.VerticalPreviewAlpha);
            _exploredAlpha?.SetValueWithoutNotify(_settings.ExploredAlpha);
            _occlusionAlpha?.SetEnabled(_settings.FadePlayerOccluders);
        }
    }
}
