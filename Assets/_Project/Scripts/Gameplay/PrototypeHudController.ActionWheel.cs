using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace ProjectC.Gameplay
{
    public partial class PrototypeHudController : MonoBehaviour
    {
        private readonly List<Rect> _wheelReservedBounds = new List<Rect>(7);
        private int _wheelBlockedThroughFrame = -1;

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

            bool observing = demo.IsVerticalLookActive;
            bool hasInteraction = demo.TryFindAdjacentInteraction(out _, out string interactLabel);
            var slots = new[]
            {
                new WheelSlot
                {
                    Label = "대기",
                    Tooltip = "한 턴 대기",
                    IconClass = "ui-wait-icon",
                    Action = () => demo.WaitTurn(),
                    Enabled = !observing
                },
                new WheelSlot
                {
                    Label = hasInteraction ? interactLabel : "주변 행동 없음",
                    Tooltip = hasInteraction ? interactLabel : "현재 가능한 주변 행동 없음",
                    IconClass = "ui-interact-icon",
                    Action = () => demo.InteractAdjacent(),
                    Enabled = !observing && hasInteraction
                },
                new WheelSlot
                {
                    Label = $"키트 ×{demo.PotionCount}",
                    Tooltip = $"응급 키트 사용 · 보유 {demo.PotionCount}",
                    IconClass = "potion-icon",
                    Action = () => demo.UsePotion(),
                    Enabled = !observing && demo.PotionCount > 0
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
                // 원거리는 누구나 쓰지만 연사할 수 없다 — 라벨이 남은 충전을 이고 있어야
                // "지금 쏠까, 붙을까"를 휠에서 바로 판단한다.
                new WheelSlot
                {
                    Label = demo.CombatMode == CombatActionMode.Melee
                        ? $"사격 {demo.RangedCharges}/{demo.RangedChargeCapacity}"
                        : "근접",
                    Tooltip = demo.CombatMode == CombatActionMode.Melee
                        ? $"사격으로 전환 · 충전 {demo.RangedCharges}/{demo.RangedChargeCapacity}"
                        : "근접 전투로 전환",
                    IconClass = demo.CombatMode == CombatActionMode.Melee
                        ? "ui-ranged-icon"
                        : "ui-melee-icon",
                    Action = () => demo.ToggleCombatMode(),
                    Enabled = !observing && demo.HasRangedWeapon
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
            VisualElement wheelParent = _actionWheel.parent;
            if (wheelParent == null) return;
            // ScreenToPanel/worldBound는 패널 좌표지만 left/top은 hud-root 로컬 좌표다.
            // safe area가 hud-root를 이동·축소하는 모바일에서도 같은 좌표계를 쓰도록
            // 목적점과 예약 영역을 모두 휠 부모 로컬로 내린다.
            Vector2 localPoint = wheelParent.WorldToLocal(panelPoint);

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
            Rect parentBounds = wheelParent.localBound;
            float panelWidth = parentBounds.width;
            float panelHeight = parentBounds.height;
            float margin = ActivePresentation == HudPresentationMode.Mobile ? 12f : 8f;
            CollectWheelReservedBounds(wheelParent);
            Vector2 center = HudWheelPlacement.FindSafeCenter(
                localPoint,
                new Vector2(panelWidth, panelHeight),
                new Vector2(buttonWidth, buttonHeight),
                radius,
                margin,
                reserved: _wheelReservedBounds);
            _actionWheel.style.left = center.x;
            _actionWheel.style.top = center.y;

            for (int i = 0; i < _actionWheel.childCount; i++)
            {
                float angle = Mathf.Deg2Rad * (90f - i * 60f);
                VisualElement button = _actionWheel[i];
                button.style.left = Mathf.Cos(angle) * radius;
                button.style.top = -Mathf.Sin(angle) * radius;
            }
        }

        private void CollectWheelReservedBounds(VisualElement wheelParent)
        {
            _wheelReservedBounds.Clear();
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            AddWheelReservedBound(
                wheelParent, root.Q<VisualElement>(className: "vitals"));
            AddWheelReservedBound(
                wheelParent, root.Q<VisualElement>(className: "minimap-panel"));
            AddWheelReservedBound(
                wheelParent, root.Q<VisualElement>("message-log"));
            AddWheelReservedBound(
                wheelParent, root.Q<VisualElement>("vertical-hint-chip"));
            AddWheelReservedBound(
                wheelParent, root.Q<VisualElement>(className: "hud-bottom"));
            // transition 첫 프레임의 resolved opacity/display가 아직 이전 값이어도
            // 논리적으로 열린 과도 패널은 즉시 안전 영역으로 예약한다.
            AddWheelReservedBound(wheelParent, _bossPanel, IsOpen(_bossPanel));
            AddWheelReservedBound(
                wheelParent,
                _routeDiscovery,
                IsOpen(_routeDiscovery) &&
                !_routeDiscovery.ClassListContains("is-suppressed"));
        }

        /// <summary>
        /// display:none 패널은 열린 첫 프레임까지 worldBound가 0일 수 있다. 그 프레임에
        /// 휠을 그리지 않고 한 번의 UI 레이아웃 뒤 실제 footprint로 다시 배치한다.
        /// 핀/홀드 의도는 유지하므로 다음 프레임에 자동으로 돌아온다.
        /// </summary>
        private void WaitOneLayoutPassBeforeShowingWheel()
        {
            _wheelBlockedThroughFrame = Mathf.Max(
                _wheelBlockedThroughFrame,
                Time.frameCount);
            _actionWheel?.RemoveFromClassList("is-open");
        }

        private void AddWheelReservedBound(
            VisualElement wheelParent,
            VisualElement element,
            bool logicallyVisible = false)
        {
            if (element == null ||
                (!logicallyVisible &&
                 (element.resolvedStyle.display == DisplayStyle.None ||
                  element.resolvedStyle.opacity <= 0.01f)))
                return;

            Rect bounds = element.worldBound;
            if (bounds.width > 0f && bounds.height > 0f)
            {
                Vector2 min = wheelParent.WorldToLocal(
                    new Vector2(bounds.xMin, bounds.yMin));
                Vector2 max = wheelParent.WorldToLocal(
                    new Vector2(bounds.xMax, bounds.yMax));
                _wheelReservedBounds.Add(Rect.MinMaxRect(
                    Mathf.Min(min.x, max.x),
                    Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x),
                    Mathf.Max(min.y, max.y)));
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
