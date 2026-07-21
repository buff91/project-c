using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 개발용 디버그 창. 에디터/개발빌드에서만 존재하며 Cmd+D(맥)/Ctrl+D(그 외) 또는 F1로 토글.
    /// 치트는 Register 목록으로 등록해 패널이 버튼을 생성한다 — 추가가 한 줄로 끝난다.
    /// HUD 와 같은 UIDocument 를 공유하되 코드는 분리한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DebugPanelController : MonoBehaviour
    {
        private const int LogCapacity = 30;
        private const float StatusRefreshInterval = 0.5f;

        public IsoPrototypeDemo demo;

        private VisualElement _panel;
        private VisualElement _commands;
        private Label _status;
        private ScrollView _log;
        private readonly List<(string Label, Action Action)> _entries = new List<(string, Action)>();
        private readonly Queue<string> _logLines = new Queue<string>(LogCapacity);
        private float _statusTimer;
        private bool _logDirty;

        private static bool DebugAllowed => Application.isEditor || Debug.isDebugBuild;

        private void OnEnable()
        {
            if (!DebugAllowed) return;

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _panel = root.Q<VisualElement>("debug-panel");
            _commands = root.Q<VisualElement>("debug-commands");
            _status = root.Q<Label>("debug-status");
            _log = root.Q<ScrollView>("debug-log");

            RegisterDefaultCommands();
            BuildCommandButtons();

            Application.logMessageReceived += HandleLogMessage;
            if (demo != null) demo.InteractionFeedback += HandleFeedback;
        }

        private void OnDisable()
        {
            if (!DebugAllowed) return;
            Application.logMessageReceived -= HandleLogMessage;
            if (demo != null) demo.InteractionFeedback -= HandleFeedback;
            _entries.Clear();
        }

        /// <summary>치트/커맨드 추가 지점. 라벨과 실행만 주면 버튼이 생긴다.</summary>
        public void Register(string label, Action action)
        {
            _entries.Add((label, action));
        }

        private void RegisterDefaultCommands()
        {
            _entries.Clear();
            if (demo == null) return;
            Register("GOD 토글", demo.DebugToggleGodMode);
            Register("HP 풀회복", demo.DebugHealFull);
            Register("피해 1", () => demo.DebugDamageSelf(1));
            Register("전체 시야", demo.ToggleViewMode);
            Register("물약 +1", () => demo.DebugGiveItem(ItemKind.Potion));
            Register("폭탄 +1", () => demo.DebugGiveItem(ItemKind.Bomb));
            Register("냉기 +1", () => demo.DebugGiveItem(ItemKind.FrostBomb));
            Register("층 몬스터 전멸", demo.DebugKillAllOnFloor);
            Register("한 층 위로", () => demo.DebugJumpFloor(1));
            Register("한 층 아래로", () => demo.DebugJumpFloor(-1));
            Register("세이브 삭제", demo.DebugClearSave);
        }

        private void BuildCommandButtons()
        {
            if (_commands == null) return;
            _commands.Clear();
            foreach ((string label, Action action) in _entries)
            {
                var button = new Button(action) { text = label };
                button.AddToClassList("debug-cmd");
                _commands.Add(button);
            }
        }

        private void Update()
        {
            if (!DebugAllowed || _panel == null) return;

            if (ToggleRequested())
                _panel.ToggleInClassList("is-open");

            if (!_panel.ClassListContains("is-open")) return;

            _statusTimer += Time.unscaledDeltaTime;
            if (_statusTimer >= StatusRefreshInterval)
            {
                _statusTimer = 0f;
                RefreshStatus();
            }

            if (_logDirty)
            {
                _logDirty = false;
                RefreshLog();
            }
        }

        private static bool ToggleRequested()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.f1Key.wasPressedThisFrame) return true;
            bool modifier = keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed ||
                            keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            return modifier && keyboard.dKey.wasPressedThisFrame;
#else
            if (Input.GetKeyDown(KeyCode.F1)) return true;
            bool modifier = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) ||
                            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            return modifier && Input.GetKeyDown(KeyCode.D);
#endif
        }

        private void RefreshStatus()
        {
            if (_status == null || demo == null) return;
            float fps = Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f;
            string hp = demo.PlayerState != null
                ? $"{demo.PlayerState.Hp}/{demo.PlayerState.MaxHp}"
                : "--";
            _status.text =
                $"seed {demo.DebugSeed} · 턴 {demo.DebugTurnNumber} · FPS {fps:0}\n" +
                $"{demo.LocationLabel}\n" +
                $"HP {hp} · GOD {(demo.DebugGodMode ? "ON" : "off")} · " +
                $"층 몬스터 {demo.DebugLivingEnemiesOnFloor()}";
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            PushLine(type == LogType.Log ? condition : $"[{type}] {condition}");
        }

        private void HandleFeedback(string message) => PushLine($"» {message}");

        private void PushLine(string line)
        {
            while (_logLines.Count >= LogCapacity)
                _logLines.Dequeue();
            _logLines.Enqueue(line);
            _logDirty = true;
        }

        private void RefreshLog()
        {
            if (_log == null) return;
            _log.contentContainer.Clear();
            foreach (string line in _logLines)
            {
                var label = new Label(line);
                label.AddToClassList("debug-log-line");
                _log.contentContainer.Add(label);
            }
            _log.scrollOffset = new Vector2(0f, float.MaxValue);
        }
    }
}
