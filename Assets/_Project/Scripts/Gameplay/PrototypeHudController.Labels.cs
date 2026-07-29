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

        private void UpdateInteractButton()
        {
            if (_interactButton == null) return;
            string label = demo != null ? demo.ContextInteractionLabel : null;
            _interactButton.EnableInClassList("is-available", label != null);
            if (_interactLabel != null)
                _interactLabel.text = label ?? "상호작용";
        }

        private void PerformInteraction()
        {
            if (demo != null) demo.PerformContextInteraction();
        }

        private void UpdateItemLabels()
        {
            if (_potionCountLabel != null)
                _potionCountLabel.text = $"×{(demo != null ? demo.PotionCount : 0)}";
            if (_bombCountLabel != null)
                _bombCountLabel.text = $"×{(demo != null ? demo.BombCount : 0)}";
            if (_frostCountLabel != null)
                _frostCountLabel.text = $"×{(demo != null ? demo.FrostBombCount : 0)}";
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

        /// <summary>
        /// 층 계기 갱신. 이름은 그대로지만 역할이 바뀌었다 — <c>#floor-label</c>은 이제
        /// "▲ B1 · ▼ B3" 한 줄이 아니라 스택 위쪽 끝의 <b>경로 캡</b>이다.
        /// 캡 텍스트와 눈금은 스택 전체를 알아야 정할 수 있어 <see cref="RebuildFloorStack"/>이
        /// 함께 소유한다 — 여기서 따로 쓰면 둘이 어긋난다.
        /// </summary>
        private void UpdateFloorLabel()
        {
            if (_depthLabel != null)
                _depthLabel.text = demo != null ? demo.ActiveFloorLabel : "B1";
            if (_depthCaption != null)
                _depthCaption.text = demo != null ? demo.StageLabel : "던전 1/3";

            RebuildFloorStack();
        }

        private void UpdateModeLabel()
        {
            bool debugAll = demo != null && demo.ViewMode == DungeonViewMode.DebugAll;
            if (_modeLabel != null)
                _modeLabel.text = debugAll ? "ALL" : "FOV";
            if (_modeButton != null)
                _modeButton.tooltip = debugAll
                    ? "전체 구조 표시 중 — 플레이 시야로 전환"
                    : "플레이 시야 표시 중 — 전체 구조 디버그로 전환";
        }

        private void UpdateCombatLabel()
        {
            if (_combatButton != null || _combatLabel != null || _combatIcon != null)
            {
                bool ranged = demo != null && demo.CombatMode == CombatActionMode.Ranged;
                if (_combatLabel != null)
                    _combatLabel.text = ranged
                        ? $"원거리 {demo.RangedAttackRange}"
                        : "근접";
                if (_combatButton != null)
                    _combatButton.EnableInClassList("ranged", ranged);
                if (_combatIcon != null)
                {
                    _combatIcon.EnableInClassList("ui-melee-icon", !ranged);
                    _combatIcon.EnableInClassList("ui-ranged-icon", ranged);
                }
            }
        }

        private void UpdateLocationLabel()
        {
            if (_locationLabel != null)
                _locationLabel.text = demo != null ? demo.HudHeightLabel : "--";

            // 배고픔은 위치가 아니라 활력이라 vitals 에 있지만, 갱신 시점은 같다
            // (둘 다 플레이어가 한 행동 할 때마다 바뀐다).
            if (_hungerLabel != null)
            {
                _hungerLabel.text = demo != null ? demo.HungerLabel : "포만";
                _hungerLabel.EnableInClassList("is-warning", demo != null && demo.HungerIsWarning);
            }
        }

        private void UpdateVerticalHintLabel()
        {
            if (_verticalHintLabel == null) return;

            string hint = demo != null ? demo.VerticalHintLabel : null;
            _verticalHintLabel.text = hint ?? "";
            _verticalHintLabel.parent?.EnableInClassList(
                "is-open", !string.IsNullOrEmpty(hint));
        }
    }
}
