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

        private void HandleViewRotationChanged(int _)
        {
            UpdateViewLabel();
        }

        private void HandleActiveFloorChanged(int _)
        {
            UpdateFloorLabel();
            UpdateMinimap();
            UpdateBossPanel();
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
            if (_statusLabel == null) return;

            _statusLabel.text = message;
            _statusLabel.parent?.AddToClassList("is-open");
            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(HideInteractionFeedback());
        }

        private System.Collections.IEnumerator HideInteractionFeedback()
        {
            yield return new WaitForSecondsRealtime(3f);
            _statusLabel?.parent?.RemoveFromClassList("is-open");
            _feedbackRoutine = null;
        }

        private void HandleVerticalRouteDiscovered(VerticalRouteCue cue)
        {
            string variant = null;
            switch (cue.Role)
            {
                case VerticalRouteRole.Ladder:
                    variant = "route-ladder";
                    break;
                case VerticalRouteRole.FloorUp:
                case VerticalRouteRole.FloorDown:
                    variant = "route-floor";
                    break;
                case VerticalRouteRole.OpeningUp:
                case VerticalRouteRole.OpeningDown:
                    variant = "route-opening";
                    break;
            }

            ShowDiscoveryCard(cue.Title, cue.Detail, variant);
        }

        /// <summary>
        /// 던전 입장 카드. 최초 발견 카드와 같은 자리·같은 수명을 쓴다 —
        /// 새 UI 를 만들지 않는 이유는 플레이어에게 둘 다 "지금 알아 둘 것" 한 장이기 때문이다.
        /// </summary>
        private void HandleDungeonEntryCue(string title, string detail) =>
            ShowDiscoveryCard(title, detail, variant: null);

        private void ShowDiscoveryCard(string title, string detail, string variant)
        {
            if (_routeDiscovery == null) return;
            if (_routeDiscoveryRoutine != null)
                StopCoroutine(_routeDiscoveryRoutine);

            if (_routeDiscoveryTitle != null) _routeDiscoveryTitle.text = title;
            if (_routeDiscoveryDetail != null) _routeDiscoveryDetail.text = detail;
            _routeDiscovery.RemoveFromClassList("route-ladder");
            _routeDiscovery.RemoveFromClassList("route-floor");
            _routeDiscovery.RemoveFromClassList("route-opening");
            if (!string.IsNullOrEmpty(variant))
                _routeDiscovery.AddToClassList(variant);

            _routeDiscovery.BringToFront();
            _routeDiscovery.AddToClassList("is-open");
            _routeDiscoveryRoutine = StartCoroutine(HideVerticalRouteDiscovery());
        }

        private System.Collections.IEnumerator HideVerticalRouteDiscovery()
        {
            // 최초 발견 안내는 전투를 막지 않으므로, 월드 오브젝트와 문장을 연결해 읽을 시간을 준다.
            yield return new WaitForSecondsRealtime(7f);
            _routeDiscovery?.RemoveFromClassList("is-open");
            _routeDiscoveryRoutine = null;
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
            if (_hpHearts == null || state == null) return;

            int heartCount = _hpHearts.childCount;
            for (int i = 0; i < heartCount; i++)
            {
                bool filled = state.Hp * heartCount > i * state.MaxHp;
                _hpHearts[i].EnableInClassList("is-empty", !filled);
            }
        }
    }
}
