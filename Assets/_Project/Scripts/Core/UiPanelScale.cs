using System;

namespace ProjectC.Core
{
    /// <summary>
    /// HUD 논리 캔버스를 정하는 규칙. 순수 C# — UnityEngine 비의존, EditMode/shim 테스트 가능.
    ///
    /// 왜 `ScaleWithScreenSize`를 버렸나: 기준 해상도 540×960(세로)에 match 0.5를 쓰면
    /// 배율이 화면마다 제각각이고(1280×720→1.333, 2560×1440→2.667) 논리 캔버스는 항상
    /// 960×540으로 고정된다. 화면이 커져도 HUD가 화면에서 차지하는 비율이 그대로라
    /// 넓은 모니터일수록 글자가 작아 보인다 — 이게 시인성 문제의 실체였다.
    ///
    /// 실측으로 확인한 것(2026-07-29, docs/captures/spike-*.png): **정수 배율은 픽셀 폰트를
    /// 선명하게 만들지 않는다.** UI Toolkit 텍스트 생성기가 글자마다 제 서브픽셀 위상에서
    /// 따로 래스터화하기 때문에, 배율이 3.12든 3.00이든 같은 글자가 다르게 그려진다.
    /// 정수 배율을 쓰는 이유는 선명도가 아니라 **논리 캔버스를 640×360으로 고정**하기
    /// 위해서다 — 그래야 같은 논리 px가 화면에서 커지고(2560×1440에서 +29%), PC 해상도
    /// 전부가 같은 배치를 공유해 분기가 사라진다.
    ///
    /// 짧은 축(minor axis) 기준인 이유: 높이 기준으로 잡으면 세로 화면에서 캔버스가 무너진다
    /// (1080×1920이 167×362가 된다). 짧은 축을 쓰면 가로/세로 양쪽에서 같은 규칙이 성립한다.
    /// </summary>
    public static class UiPanelScale
    {
        /// <summary>논리 캔버스의 짧은 축. 16:9에서 640×360이 나오는 값.</summary>
        public const int DesignMinorAxis = 360;

        /// <summary>
        /// 실제 렌더 표면 크기(픽셀)에서 패널 배율을 정한다. 항상 1 이상의 정수다.
        ///
        /// 주의: Unity 에디터에서 <c>Screen.width/height</c>는 Game View 창에 맞춰 축소된
        /// 값이라 패널이 실제로 해석하는 표면 크기와 다르다(실측: Screen 1859×1160 vs
        /// 패널 표면 2560×1440). 호출자는 <c>contentRect × scaledPixelsPerPoint</c>처럼
        /// 패널이 실제로 쓰는 표면 크기를 넘겨야 한다.
        /// </summary>
        public static int Scale(int surfaceWidth, int surfaceHeight)
        {
            int minor = Math.Min(surfaceWidth, surfaceHeight);
            if (minor <= 0) return 1;
            int scale = minor / DesignMinorAxis;
            return scale < 1 ? 1 : scale;
        }

        /// <summary>배율을 적용한 뒤의 논리 캔버스 크기. 검증·문서용.</summary>
        public static void LogicalSize(
            int surfaceWidth,
            int surfaceHeight,
            out float logicalWidth,
            out float logicalHeight)
        {
            int scale = Scale(surfaceWidth, surfaceHeight);
            logicalWidth = surfaceWidth / (float)scale;
            logicalHeight = surfaceHeight / (float)scale;
        }
    }
}
