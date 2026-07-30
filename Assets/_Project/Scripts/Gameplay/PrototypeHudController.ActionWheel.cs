using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    public partial class PrototypeHudController : MonoBehaviour
    {

        private void BuildActionWheel()
        {
            if (_actionWheel == null) return;
            _actionWheel.Clear();
            for (int i = 0; i < 6; i++)
            {
                var button = new Button { name = $"wheel-{i}" };
                button.AddToClassList("wheel-button");
                var icon = new VisualElement { name = $"wheel-icon-{i}" };
                icon.AddToClassList("wheel-button-icon");
                icon.pickingMode = PickingMode.Ignore;
                button.Add(icon);
                var label = new Label { name = $"wheel-label-{i}" };
                label.AddToClassList("wheel-button-label");
                label.pickingMode = PickingMode.Ignore;
                button.Add(label);
                _actionWheel.Add(button);
            }
        }

        /// <summary>지금 할 수 있는 것들로 휠 내용을 구성한다.</summary>
        private void RefreshActionWheel()
        {
            if (_actionWheel == null || demo == null) return;

            bool hasInteraction = demo.TryFindAdjacentInteraction(out _, out string interactLabel);
            var slots = new[]
            {
                new WheelSlot
                {
                    Label = "대기",
                    Tooltip = "한 턴 대기",
                    IconClass = "ui-wait-icon",
                    Action = () => demo.WaitTurn(),
                    Enabled = true
                },
                new WheelSlot
                {
                    Label = hasInteraction ? interactLabel : "주변 행동 없음",
                    Tooltip = hasInteraction ? interactLabel : "현재 가능한 주변 행동 없음",
                    IconClass = "ui-interact-icon",
                    Action = () => demo.InteractAdjacent(),
                    Enabled = hasInteraction
                },
                new WheelSlot
                {
                    Label = $"키트 ×{demo.PotionCount}",
                    Tooltip = $"응급 키트 사용 · 보유 {demo.PotionCount}",
                    IconClass = "potion-icon",
                    Action = () => demo.UsePotion(),
                    Enabled = demo.PotionCount > 0
                },
                new WheelSlot
                {
                    Label = $"폭발물 ×{demo.BombCount}",
                    Tooltip = $"급조 폭발물 조준 · 보유 {demo.BombCount}",
                    IconClass = "bomb-icon",
                    Action = () => demo.ToggleBombAim(),
                    Enabled = demo.BombCount > 0
                },
                new WheelSlot
                {
                    Label = $"냉기 ×{demo.FrostBombCount}",
                    Tooltip = $"냉각재 수류탄 조준 · 보유 {demo.FrostBombCount}",
                    IconClass = "frost-icon",
                    Action = () => demo.ToggleFrostBombAim(),
                    Enabled = demo.FrostBombCount > 0
                },
                new WheelSlot
                {
                    Label = demo.CombatMode == CombatActionMode.Melee ? "원거리" : "근접",
                    Tooltip = demo.CombatMode == CombatActionMode.Melee
                        ? "원거리 전투로 전환"
                        : "근접 전투로 전환",
                    IconClass = demo.CombatMode == CombatActionMode.Melee
                        ? "ui-ranged-icon"
                        : "ui-melee-icon",
                    Action = () => demo.ToggleCombatMode(),
                    Enabled = true
                }
            };

            for (int i = 0; i < 6 && i < _actionWheel.childCount; i++)
            {
                var button = (Button)_actionWheel[i];
                WheelSlot slot = slots[i];
                Label label = button.Q<Label>($"wheel-label-{i}");
                if (label != null) label.text = slot.Label;
                VisualElement icon = button.Q<VisualElement>($"wheel-icon-{i}");
                ApplyWheelIcon(icon, slot.IconClass);
                button.tooltip = slot.Tooltip;
                button.SetEnabled(slot.Enabled);
                button.EnableInClassList(
                    "is-context-missing",
                    i == 1 && !hasInteraction);
                button.clickable = new Clickable(() =>
                {
                    _wheelPinned = false;
                    _actionWheel?.RemoveFromClassList("is-open");
                    slot.Action();
                });
            }
        }

        private static void ApplyWheelIcon(VisualElement icon, string iconClass)
        {
            if (icon == null) return;
            icon.RemoveFromClassList("ui-wait-icon");
            icon.RemoveFromClassList("ui-interact-icon");
            icon.RemoveFromClassList("potion-icon");
            icon.RemoveFromClassList("bomb-icon");
            icon.RemoveFromClassList("frost-icon");
            icon.RemoveFromClassList("ui-melee-icon");
            icon.RemoveFromClassList("ui-ranged-icon");
            if (!string.IsNullOrEmpty(iconClass))
                icon.AddToClassList(iconClass);
        }

        /// <summary>플레이어를 중심으로 여섯 문맥 행동 셀을 방사형 배치한다.</summary>
        private void PositionActionWheel()
        {
            if (_actionWheel == null || demo == null || Camera.main == null) return;
            IPanel panel = _actionWheel.panel;
            if (panel == null) return;

            // 플레이어 스크린 좌표 → 패널 좌표
            Vector3 world = Camera.main.WorldToScreenPoint(
                new Vector3(0f, 0.4f, 0f) + (Vector3)CameraFollowTarget());
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(world.x, Screen.height - world.y));

            // 셀 크기는 Desktop/Touch USS가 다르게 결정한다. 실제 resolved size에서
            // 반지름과 화면 clamp를 파생해 스타일 교체 뒤에도 배치가 어긋나지 않게 한다.
            const float fallbackButtonWidth = 72f;
            const float fallbackButtonHeight = 64f;
            VisualElement firstButton = _actionWheel.childCount > 0 ? _actionWheel[0] : null;
            float resolvedWidth = firstButton != null
                ? firstButton.resolvedStyle.width
                : fallbackButtonWidth;
            float resolvedHeight = firstButton != null
                ? firstButton.resolvedStyle.height
                : fallbackButtonHeight;
            float buttonWidth = float.IsNaN(resolvedWidth) || resolvedWidth <= 0f
                ? fallbackButtonWidth
                : resolvedWidth;
            float buttonHeight = float.IsNaN(resolvedHeight) || resolvedHeight <= 0f
                ? fallbackButtonHeight
                : resolvedHeight;
            float radius = Mathf.Max(buttonWidth, buttonHeight) +
                           (ActivePresentation == HudPresentationMode.Mobile ? 12f : 8f);
            float buttonHalfWidth = buttonWidth * 0.5f;
            float buttonHalfHeight = buttonHeight * 0.5f;
            const float screenMargin = 12f;
            float panelWidth = panel.visualTree.layout.width;
            float panelHeight = panel.visualTree.layout.height;
            float horizontalInset = radius + buttonHalfWidth + screenMargin;
            float verticalInset = radius + buttonHalfHeight + screenMargin;
            float centerX = Mathf.Clamp(
                panelPoint.x,
                horizontalInset,
                Mathf.Max(horizontalInset, panelWidth - horizontalInset));
            float centerY = Mathf.Clamp(
                panelPoint.y,
                verticalInset,
                Mathf.Max(verticalInset, panelHeight - verticalInset));
            _actionWheel.style.left = centerX;
            _actionWheel.style.top = centerY;

            for (int i = 0; i < _actionWheel.childCount; i++)
            {
                float angle = Mathf.Deg2Rad * (90f - i * 60f);
                VisualElement button = _actionWheel[i];
                button.style.left = Mathf.Cos(angle) * radius;
                button.style.top = -Mathf.Sin(angle) * radius;
            }
        }

        private Vector2 CameraFollowTarget()
        {
            // 플레이어 월드 위치 (데모의 그리드 변환 재사용)
            var grid = demo.GetComponent<GridManager>();
            return grid != null ? (Vector2)grid.GridToWorld(demo.PlayerPos) : Vector2.zero;
        }
    }
}
