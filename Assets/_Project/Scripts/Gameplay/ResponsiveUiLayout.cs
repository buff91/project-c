using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// UI Toolkit에는 런타임 미디어 쿼리가 없으므로 패널의 논리 크기를 USS 클래스로 변환한다.
    /// 실제 기기의 Safe Area도 같은 논리 좌표로 환산해 HUD 전체에 적용한다.
    ///
    /// <para>
    /// 패널 배율도 여기가 소유한다. <see cref="PanelSettings"/>는 ConstantPixelSize 모드로
    /// 두고 배율은 <see cref="UiPanelScale"/>가 화면마다 정한다 — 에셋에 박힌 기준 해상도가
    /// 아니라 코드가 논리 캔버스를 정하는 쪽이 SSOT다. 이 클래스가 맡는 이유는 이미
    /// <c>GeometryChangedEvent</c>와 <c>DevelopmentViewportService.Changed</c>를 둘 다
    /// 구독하고 있어서, 배율을 다시 계산해야 하는 순간과 정확히 같은 순간에 깨어나기 때문이다.
    /// </para>
    /// </summary>
    public sealed class ResponsiveUiLayout : IDisposable
    {
        public readonly struct ViewportProfile
        {
            public readonly bool Narrow;
            public readonly bool Short;
            public readonly bool Landscape;
            public readonly bool Expanded;
            public readonly bool Tall;
            public readonly bool UltraWide;

            public ViewportProfile(
                bool narrow,
                bool shortViewport,
                bool landscape,
                bool expanded,
                bool tall,
                bool ultraWide)
            {
                Narrow = narrow;
                Short = shortViewport;
                Landscape = landscape;
                Expanded = expanded;
                Tall = tall;
                UltraWide = ultraWide;
            }
        }

        // 임계값은 전부 640×360 논리 캔버스 기준이다(UiPanelScale). 옛 960×540 시절 숫자를
        // 그대로 두면 PC가 통째로 is-short 밖으로 나가거나 통째로 is-expanded 안으로 들어온다.
        public const float NarrowWidth = 520f;

        /// <summary>
        /// PC 논리 높이는 이제 360이라 세로 공간이 실제로 빠듯하다 — 여기 걸려야 맞다.
        /// 세로폰(640·844)은 안 걸린다.
        /// </summary>
        public const float ShortHeight = 420f;

        /// <summary>
        /// 여유 있는 창(5:4·4:3)만 골라낸다. 1280×1024 → 논리 640×512 → expanded,
        /// 16:9는 짧은 축이 항상 360이라 아니다.
        /// </summary>
        public const float ExpandedMinAxis = 480f;
        public const float ExtremeAspectRatio = 2f;

        private readonly VisualElement _panelRoot;
        private readonly VisualElement _contentRoot;
        /// <summary>
        /// <c>PrototypePanelSettings.asset</c>에 직렬화된 <c>m_Scale</c>. 에디터에서 나갈 때
        /// 이 값으로 되돌려 에셋에 유령 diff 가 남지 않게 한다.
        ///
        /// <para>
        /// 들어올 때의 값을 캐시하지 <b>않는</b> 이유: PanelSettings 는 씬 셋이 공유하는
        /// 에셋이라, 앞 씬이 남긴 런타임 값(예: 4)을 "원본"으로 캐시하면 그 값이 영구히
        /// 눌러앉는다. 실제로 그렇게 되면 다음 씬에서 배율이 이미 목표값이라 아무도 다시
        /// 쓰지 않고, 패널은 1배로 그린 채 남는다(메인 메뉴에서 실측).
        /// </para>
        /// </summary>
        public const float SerializedScale = 1f;

        private readonly PanelSettings _panelSettings;
        private bool _disposed;

        public ResponsiveUiLayout(
            VisualElement panelRoot,
            VisualElement contentRoot,
            PanelSettings panelSettings = null)
        {
            _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
            _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
            _panelSettings = panelSettings;
            _panelRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            DevelopmentViewportService.Changed += HandlePresentationChanged;
            _panelRoot.schedule.Execute(Refresh);
        }

        public static ViewportProfile Classify(float width, float height)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));

            bool landscape = width > height;
            float aspectRatio = landscape ? width / height : height / width;
            return new ViewportProfile(
                width < NarrowWidth,
                height < ShortHeight,
                landscape,
                Mathf.Min(width, height) >= ExpandedMinAxis,
                !landscape && aspectRatio >= ExtremeAspectRatio,
                landscape && aspectRatio >= ExtremeAspectRatio);
        }

        public void Refresh()
        {
            if (_disposed) return;
            ApplyInteractionProfile();

            Rect rect = _panelRoot.contentRect;
            if (rect.width <= 0f || rect.height <= 0f ||
                float.IsNaN(rect.width) || float.IsNaN(rect.height))
                return;

            rect = ApplyPanelScale(rect);

            ViewportProfile profile = Classify(rect.width, rect.height);
            _contentRoot.EnableInClassList("is-narrow", profile.Narrow);
            _contentRoot.EnableInClassList("is-short", profile.Short);
            _contentRoot.EnableInClassList("is-landscape", profile.Landscape);
            _contentRoot.EnableInClassList("is-expanded", profile.Expanded);
            _contentRoot.EnableInClassList("is-tall", profile.Tall);
            _contentRoot.EnableInClassList("is-ultrawide", profile.UltraWide);
            ApplySafeArea(rect.width, rect.height);
        }

        /// <summary>
        /// 실제 렌더 표면에서 배율을 정해 패널에 적용하고, 그 배율이 적용된 뒤의 논리 크기를
        /// 돌려준다.
        ///
        /// <para>
        /// <c>Screen.width/height</c>를 쓰지 않는 이유: 에디터 Game View 에서 그 값은 창에
        /// 맞춰 축소된 크기라 패널이 실제로 해석하는 표면과 다르다(실측 1859×1160 vs
        /// 2560×1440). 패널이 쓰는 표면은 <c>contentRect × scaledPixelsPerPoint</c>다.
        /// </para>
        /// <para>
        /// 반환값을 <c>contentRect</c> 재조회로 갈음하지 않는 이유: 방금 쓴 배율은 다음 레이아웃
        /// 패스에서야 반영되므로, 지금 다시 읽으면 한 프레임 낡은 크기로 분기가 정해진다.
        /// </para>
        /// </summary>
        private Rect ApplyPanelScale(Rect rect)
        {
            if (_panelSettings == null) return rect;

            float pixelsPerPoint = _panelRoot.panel?.scaledPixelsPerPoint ?? 1f;
            if (pixelsPerPoint <= 0f || float.IsNaN(pixelsPerPoint)) pixelsPerPoint = 1f;

            int surfaceWidth = Mathf.RoundToInt(rect.width * pixelsPerPoint);
            int surfaceHeight = Mathf.RoundToInt(rect.height * pixelsPerPoint);
            if (surfaceWidth <= 0 || surfaceHeight <= 0) return rect;

            int scale = UiPanelScale.Scale(surfaceWidth, surfaceHeight);

            // 기준은 에셋에 적힌 값이 아니라 **패널이 실제로 그리고 있는 배율**이다.
            // 에셋 값으로 비교하면, 앞 씬이 남긴 값이 우연히 목표와 같을 때 아무도 쓰지
            // 않아 패널이 1배로 남는다 — 메인 메뉴가 정확히 그 상태였다.
            if (!Mathf.Approximately(pixelsPerPoint, scale))
            {
                // PanelSettings.scale 의 setter 는 값이 같으면 조기 반환해 패널 갱신을
                // 건너뛴다. 패널이 아직 그 배율이 아니라면 값을 한 번 흔들어 적용을 강제한다.
                if (Mathf.Approximately(_panelSettings.scale, scale))
                    _panelSettings.scale = scale + 1f;
                _panelSettings.scale = scale;
            }

            return new Rect(0f, 0f, surfaceWidth / (float)scale, surfaceHeight / (float)scale);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if UNITY_EDITOR
            if (_panelSettings != null) _panelSettings.scale = SerializedScale;
#endif
            _panelRoot.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            DevelopmentViewportService.Changed -= HandlePresentationChanged;
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt) => Refresh();
        private void HandlePresentationChanged() => Refresh();

        private void ApplyInteractionProfile()
        {
            HudPresentationMode requested = DevelopmentViewportService.ResolvePresentation(
                HudPresentationMode.Auto);
            HudPresentationMode active = HudPresentation.Resolve(
                requested,
                Application.isMobilePlatform);
            bool touch = active == HudPresentationMode.Mobile;
            _contentRoot.EnableInClassList("ui-touch", touch);
            _contentRoot.EnableInClassList("ui-pointer", !touch);
        }

        private void ApplySafeArea(float panelWidth, float panelHeight)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                ClearSafeArea();
                return;
            }

            Rect safe = Screen.safeArea;
            float scaleX = panelWidth / Screen.width;
            float scaleY = panelHeight / Screen.height;
            _contentRoot.style.left = Mathf.Max(0f, safe.xMin * scaleX);
            _contentRoot.style.right = Mathf.Max(0f, (Screen.width - safe.xMax) * scaleX);
            _contentRoot.style.top = Mathf.Max(0f, (Screen.height - safe.yMax) * scaleY);
            _contentRoot.style.bottom = Mathf.Max(0f, safe.yMin * scaleY);
        }

        private void ClearSafeArea()
        {
            _contentRoot.style.left = 0f;
            _contentRoot.style.right = 0f;
            _contentRoot.style.top = 0f;
            _contentRoot.style.bottom = 0f;
        }
    }
}
