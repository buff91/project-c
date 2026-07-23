using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// UI Toolkit에는 런타임 미디어 쿼리가 없으므로 패널의 논리 크기를 USS 클래스로 변환한다.
    /// 실제 기기의 Safe Area도 같은 논리 좌표로 환산해 HUD 전체에 적용한다.
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

        public const float NarrowWidth = 520f;
        public const float ShortHeight = 700f;
        public const float ExpandedMinAxis = 590f;
        public const float ExtremeAspectRatio = 2f;

        private readonly VisualElement _panelRoot;
        private readonly VisualElement _contentRoot;
        private bool _disposed;

        public ResponsiveUiLayout(VisualElement panelRoot, VisualElement contentRoot)
        {
            _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
            _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
            _panelRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
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
            Rect rect = _panelRoot.contentRect;
            if (rect.width <= 0f || rect.height <= 0f ||
                float.IsNaN(rect.width) || float.IsNaN(rect.height))
                return;

            ViewportProfile profile = Classify(rect.width, rect.height);
            _contentRoot.EnableInClassList("is-narrow", profile.Narrow);
            _contentRoot.EnableInClassList("is-short", profile.Short);
            _contentRoot.EnableInClassList("is-landscape", profile.Landscape);
            _contentRoot.EnableInClassList("is-expanded", profile.Expanded);
            _contentRoot.EnableInClassList("is-tall", profile.Tall);
            _contentRoot.EnableInClassList("is-ultrawide", profile.UltraWide);
            ApplySafeArea(rect.width, rect.height);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _panelRoot.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt) => Refresh();

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
