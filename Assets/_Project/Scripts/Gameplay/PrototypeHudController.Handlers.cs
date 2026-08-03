using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace ProjectC.Gameplay
{
    public partial class PrototypeHudController : MonoBehaviour
    {
        private const float DiscoveryNoticeDurationSeconds = 7f;
        private const float DiscoveryNoticeTransitionSeconds = 0.16f;

        private void HandleViewRotationChanged(int _)
        {
            UpdateViewLabel();
        }

        private void HandleActiveFloorChanged(int _)
        {
            UpdateFloorLabel();
            UpdateMinimap();
            UpdateBossPanel();
            UpdateVerticalViewControls();
        }

        private void HandleViewModeChanged(DungeonViewMode _)
        {
            UpdateModeLabel();
            UpdateVerticalViewControls();
        }

        private void HandleCombatModeChanged(CombatActionMode _)
        {
            UpdateCombatLabel();
        }

        /// <summary>
        /// 턴 피드백은 이제 버려지지 않고 로그에 쌓인다. 3초 뒤 사라지는 것은 줄이 아니라
        /// <b>강조</b>다 — 최신 줄은 밝게, 지난 줄은 어둡게 남는다. 예전엔 이 이벤트가
        /// 유일한 기록이라 3초를 놓치면 직전 턴에 무슨 일이 있었는지 알 방법이 없었다.
        /// </summary>
        private void HandleInteractionFeedback(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            _messages.Add(message);
            RebuildMessageLog();
            _statusLabel?.parent?.AddToClassList("is-open");
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
            var notice = new HudTransientNotice(title, detail, variant);
            if (_transientNotices.Enqueue(notice)) TryShowNextDiscoveryNotice();
        }

        private void TryShowNextDiscoveryNotice()
        {
            if (_routeDiscovery == null || _routeDiscoveryRoutine != null ||
                IsOpen(_bossPanel))
                return;
            bool wasActive = _transientNotices.HasActive;
            if (!_transientNotices.TryGetOrActivate(out HudTransientNotice notice))
                return;
            if (!wasActive || _routeDiscoveryRemainingSeconds <= 0f)
                _routeDiscoveryRemainingSeconds = DiscoveryNoticeDurationSeconds;

            _routeDiscovery.RemoveFromClassList("is-suppressed");
            _routeDiscoveryRoutine = StartCoroutine(PresentDiscoveryNotice(notice));
        }

        private System.Collections.IEnumerator PresentDiscoveryNotice(
            HudTransientNotice notice)
        {
            _routeDiscoveryIsClosing = false;
            if (_routeDiscoveryTitle != null) _routeDiscoveryTitle.text = notice.Title;
            if (_routeDiscoveryDetail != null) _routeDiscoveryDetail.text = notice.Detail;
            _routeDiscovery.RemoveFromClassList("route-ladder");
            _routeDiscovery.RemoveFromClassList("route-floor");
            _routeDiscovery.RemoveFromClassList("route-opening");
            if (!string.IsNullOrEmpty(notice.Variant))
                _routeDiscovery.AddToClassList(notice.Variant);

            // 이미 열려 있던 카드와 다음 카드 사이에도 닫힘→열림 상태 경계를 만든다.
            // 한 프레임 뒤 여는 이유는 UI Toolkit이 두 클래스 변경을 한 스타일 패스로
            // 합쳐 slide/opacity transition을 건너뛰지 않게 하기 위해서다.
            _routeDiscovery.RemoveFromClassList("is-open");
            yield return null;
            if (IsOpen(_bossPanel))
            {
                _routeDiscoveryRoutine = null;
                yield break;
            }

            _routeDiscovery.BringToFront();
            WaitOneLayoutPassBeforeShowingWheel();
            SetDiscoveryCloseInteractive(true);
            _routeDiscovery.AddToClassList("is-open");
            // 최초 발견 안내는 전투를 막지 않으므로, 월드 오브젝트와 문장을 연결해 읽을 시간을 준다.
            _routeDiscoveryVisibleSince = Time.realtimeSinceStartup;
            _routeDiscoveryIsTiming = true;
            yield return new WaitForSecondsRealtime(_routeDiscoveryRemainingSeconds);
            _routeDiscoveryIsTiming = false;
            _routeDiscoveryRemainingSeconds = 0f;
            yield return CloseAndAdvanceDiscoveryNotice();
        }

        /// <summary>
        /// 자동 수명과 PC 닫기 버튼이 같은 종료 경로를 쓴다. 현재 카드만 완료하고
        /// 대기 중인 서로 다른 카드는 FIFO 순서 그대로 다음에 연다.
        /// </summary>
        private void DismissDiscoveryNotice()
        {
            if (!_transientNotices.HasActive || _routeDiscoveryIsClosing) return;

            SetDiscoveryCloseInteractive(false);
            if (_routeDiscoveryRoutine != null)
            {
                StopCoroutine(_routeDiscoveryRoutine);
                _routeDiscoveryRoutine = null;
            }
            _routeDiscoveryIsTiming = false;
            _routeDiscoveryRemainingSeconds = 0f;
            _routeDiscoveryRoutine = StartCoroutine(CloseAndAdvanceDiscoveryNotice());
        }

        private System.Collections.IEnumerator CloseAndAdvanceDiscoveryNotice()
        {
            SetDiscoveryCloseInteractive(false);
            _routeDiscovery?.RemoveFromClassList("is-open");
            _routeDiscoveryIsClosing = true;
            // 닫힘 모션이 끝난 뒤 다음 카드를 연다. 즉시 내용을 갈면 이전 카드가
            // 사라지는 동안 다음 문장이 비쳐 보인다.
            yield return new WaitForSecondsRealtime(DiscoveryNoticeTransitionSeconds);
            _routeDiscoveryIsClosing = false;
            _transientNotices.CompleteActive();
            _routeDiscoveryRoutine = null;
            TryShowNextDiscoveryNotice();
        }

        private void PauseDiscoveryNoticeVisual(bool suppress = false)
        {
            if (_routeDiscoveryIsTiming)
            {
                float elapsed = Mathf.Max(
                    0f,
                    Time.realtimeSinceStartup - _routeDiscoveryVisibleSince);
                _routeDiscoveryRemainingSeconds = Mathf.Max(
                    0.05f,
                    _routeDiscoveryRemainingSeconds - elapsed);
                _routeDiscoveryIsTiming = false;
            }
            if (_routeDiscoveryRoutine != null)
            {
                StopCoroutine(_routeDiscoveryRoutine);
                _routeDiscoveryRoutine = null;
            }
            // 닫힘 중 보스/문서 교체가 끼면 이미 수명을 다한 카드를 다시 7초간
            // 재생하지 않는다. active는 fade 동안 유지해 같은 이벤트 재삽입만 막는다.
            if (_routeDiscoveryIsClosing)
            {
                _transientNotices.CompleteActive();
                _routeDiscoveryRemainingSeconds = 0f;
                _routeDiscoveryIsClosing = false;
            }
            _routeDiscovery?.RemoveFromClassList("is-open");
            _routeDiscovery?.EnableInClassList("is-suppressed", suppress);
            SetDiscoveryCloseInteractive(false);
        }

        private void SetDiscoveryCloseInteractive(bool interactive)
        {
            if (_routeDiscoveryCloseButton != null)
                _routeDiscoveryCloseButton.pickingMode = interactive
                    ? PickingMode.Position
                    : PickingMode.Ignore;
        }

        private void HandlePlayerPositionChanged()
        {
            _actionWheel?.RemoveFromClassList("is-open");
            UpdateLocationLabel();
            UpdateVerticalHintLabel();
            UpdateMinimap();
        }

        private void HandleVerticalContextChanged()
        {
            UpdateVerticalHintLabel();
            UpdateVerticalViewControls();
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

            if (_minimapFloorBadge != null)
                _minimapFloorBadge.text = demo.ActiveFloorLabel;
            if (_minimapNorthLabel != null)
                _minimapNorthLabel.text = "N";
            if (_minimapPlayerMarker != null)
            {
                GridPos player = demo.PlayerPos;
                float width = _minimapView.resolvedStyle.width;
                float height = _minimapView.resolvedStyle.height;
                float left = MinimapMarkerAxisPixels(player.x, size, width, height);
                float bottom = MinimapMarkerAxisPixels(player.y, size, height, width);

                _minimapPlayerMarker.style.left = float.IsNaN(left)
                    ? Length.Percent(MinimapMarkerPercent(player.x, size))
                    : new Length(left, LengthUnit.Pixel);
                _minimapPlayerMarker.style.bottom = float.IsNaN(bottom)
                    ? Length.Percent(MinimapMarkerPercent(player.y, size))
                    : new Length(bottom, LengthUnit.Pixel);
            }
        }

        private void HandleMinimapGeometryChanged(GeometryChangedEvent _) => UpdateMinimap();

        internal static float MinimapMarkerPercent(int coordinate, int size)
        {
            if (size <= 0) return 50f;
            float center = (Mathf.Clamp(coordinate, 0, size - 1) + 0.5f) / size;
            // The PC marker is 7 px inside a 52 px map viewport. Keep its full
            // outline visible even when the player stands on an edge tile.
            return Mathf.Clamp(center * 100f, 7f, 93f);
        }

        internal static float MinimapMarkerAxisPixels(
            int coordinate,
            int size,
            float axisLength,
            float crossLength)
        {
            if (axisLength <= 0f || crossLength <= 0f ||
                float.IsNaN(axisLength) || float.IsNaN(crossLength) ||
                float.IsInfinity(axisLength) || float.IsInfinity(crossLength))
                return float.NaN;

            float mapSide = Mathf.Min(axisLength, crossLength);
            float letterbox = (axisLength - mapSide) * 0.5f;
            return letterbox + mapSide * MinimapMarkerPercent(coordinate, size) / 100f;
        }

        private void HandleInventoryChanged()
        {
            UpdateItemLabels();
        }

        private void HandleBombAimingChanged(bool _)
        {
            UpdateAimHighlights();
            UpdateVerticalViewControls();
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
