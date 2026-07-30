using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>허브와 던전이 공유하는 월드 가독성 설정값.</summary>
    public struct DisplaySettingsData
    {
        public bool FadePlayerOccluders;
        public bool ShowRearWalls;
        public float PlayerOccluderAlpha;
        public float VerticalPreviewAlpha;
        public float ExploredAlpha;

        public static DisplaySettingsData Defaults => new DisplaySettingsData
        {
            FadePlayerOccluders = true,
            ShowRearWalls = true,
            PlayerOccluderAlpha = 0.3f,
            VerticalPreviewAlpha = 0.62f,
            ExploredAlpha = 0.4f
        };

        public DisplaySettingsData Clamped()
        {
            DisplaySettingsData value = this;
            value.PlayerOccluderAlpha = Mathf.Clamp(value.PlayerOccluderAlpha, 0.12f, 0.7f);
            value.VerticalPreviewAlpha = Mathf.Clamp(value.VerticalPreviewAlpha, 0.1f, 0.8f);
            value.ExploredAlpha = Mathf.Clamp(value.ExploredAlpha, 0.05f, 0.55f);
            return value;
        }
    }

    public static class DisplaySettingsStore
    {
        private const string Prefix = "project-c.display.";

        public static DisplaySettingsData Load()
        {
            DisplaySettingsData defaults = DisplaySettingsData.Defaults;
            return new DisplaySettingsData
            {
                FadePlayerOccluders = PlayerPrefs.GetInt(
                    Prefix + "fade-occluders", defaults.FadePlayerOccluders ? 1 : 0) != 0,
                ShowRearWalls = PlayerPrefs.GetInt(
                    Prefix + "rear-walls", defaults.ShowRearWalls ? 1 : 0) != 0,
                PlayerOccluderAlpha = PlayerPrefs.GetFloat(
                    Prefix + "occluder-alpha", defaults.PlayerOccluderAlpha),
                // 키에 -v2를 붙인 이유: 밝기 완화(v0.3.3)로 기본값이 바뀌었는데, 구 키에 저장된
                // 예전 값이 새 기본값을 영원히 덮어쓰는 것을 끊는다. 구 키는 마이그레이션 없이 방치한다.
                VerticalPreviewAlpha = PlayerPrefs.GetFloat(
                    Prefix + "vertical-alpha-v2", defaults.VerticalPreviewAlpha),
                ExploredAlpha = PlayerPrefs.GetFloat(
                    Prefix + "explored-alpha-v2", defaults.ExploredAlpha)
            }.Clamped();
        }

        public static void Save(DisplaySettingsData value)
        {
            value = value.Clamped();
            PlayerPrefs.SetInt(Prefix + "fade-occluders", value.FadePlayerOccluders ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "rear-walls", value.ShowRearWalls ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "occluder-alpha", value.PlayerOccluderAlpha);
            PlayerPrefs.SetFloat(Prefix + "vertical-alpha-v2", value.VerticalPreviewAlpha);
            PlayerPrefs.SetFloat(Prefix + "explored-alpha-v2", value.ExploredAlpha);
            PlayerPrefs.Save();
        }

        public static void Apply(DisplaySettingsData value, IsoPrototypeDemo demo)
        {
            if (demo == null) return;
            value = value.Clamped();
            demo.fadePlayerOccluders = value.FadePlayerOccluders;
            demo.showRearWalls = value.ShowRearWalls;
            demo.playerOccluderAlpha = value.PlayerOccluderAlpha;
            demo.verticalPreviewAlpha = value.VerticalPreviewAlpha;
            demo.exploredAlpha = value.ExploredAlpha;
            demo.ApplyVisualSettings();
        }
    }
}
