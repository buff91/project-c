using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public readonly struct DevelopmentViewportPreset
    {
        public readonly string Id;
        public readonly string Label;
        public readonly int Width;
        public readonly int Height;

        public bool IsFreeAspect => Width <= 0 || Height <= 0;

        public DevelopmentViewportPreset(string id, string label, int width, int height)
        {
            Id = id;
            Label = label;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// 에디터/개발 빌드에서 UI 프레젠테이션과 Game View 해상도를 즉시 바꾼다.
    /// 릴리스 빌드에서는 저장된 개발 오버라이드를 무시한다.
    /// </summary>
    public static class DevelopmentViewportService
    {
        private const string ModeKey = "project-c.development.viewport-mode";
        private const string PresetKey = "project-c.development.viewport-preset";
        private const string FreePresetId = "free";

        private static readonly DevelopmentViewportPreset[] PresetValues =
        {
            new DevelopmentViewportPreset(FreePresetId, "창 크기에 맞춤", 0, 0),
            new DevelopmentViewportPreset("phone-390x844", "모바일 세로 · 390×844", 390, 844),
            new DevelopmentViewportPreset("phone-430x932", "모바일 세로 · 430×932", 430, 932),
            new DevelopmentViewportPreset("phone-844x390", "모바일 가로 · 844×390", 844, 390),
            new DevelopmentViewportPreset("pc-1280x720", "PC · 1280×720", 1280, 720),
            new DevelopmentViewportPreset("pc-1920x1080", "PC · 1920×1080", 1920, 1080)
        };

        public static event Action Changed;

        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;
        public static IReadOnlyList<DevelopmentViewportPreset> Presets => PresetValues;

        public static HudPresentationMode SelectedMode
        {
            get
            {
                int value = PlayerPrefs.GetInt(ModeKey, (int)HudPresentationMode.Auto);
                return Enum.IsDefined(typeof(HudPresentationMode), value)
                    ? (HudPresentationMode)value
                    : HudPresentationMode.Auto;
            }
        }

        public static string SelectedPresetId =>
            FindPreset(PlayerPrefs.GetString(PresetKey, FreePresetId)).Id;

        public static HudPresentationMode ResolvePresentation(HudPresentationMode fallback)
        {
            return IsAvailable && PlayerPrefs.HasKey(ModeKey) ? SelectedMode : fallback;
        }

        public static string Apply(HudPresentationMode mode, string presetId)
        {
            if (!IsAvailable) return "개발 빌드에서만 사용할 수 있습니다";

            DevelopmentViewportPreset preset = FindPreset(presetId);
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetString(PresetKey, preset.Id);
            PlayerPrefs.Save();

            bool resolutionApplied = ApplyResolution(preset);
            Changed?.Invoke();
            string resolution = preset.IsFreeAspect
                ? "창 크기에 맞춤"
                : $"{preset.Width}×{preset.Height}";
            return resolutionApplied
                ? $"{ModeLabel(mode)} · {resolution}"
                : $"{ModeLabel(mode)} · 해상도 전환 실패";
        }

        public static DevelopmentViewportPreset FindPreset(string id)
        {
            foreach (DevelopmentViewportPreset preset in PresetValues)
            {
                if (preset.Id == id) return preset;
            }
            return PresetValues[0];
        }

        public static string ModeLabel(HudPresentationMode mode)
        {
            switch (mode)
            {
                case HudPresentationMode.Mobile: return "MOBILE UI";
                case HudPresentationMode.Desktop: return "PC UI";
                default: return "AUTO UI";
            }
        }

        private static bool ApplyResolution(DevelopmentViewportPreset preset)
        {
#if UNITY_EDITOR
            if (Application.isEditor && TryApplyEditorGameViewSize(preset))
                return true;
#endif
            if (preset.IsFreeAspect) return true;
            Screen.SetResolution(preset.Width, preset.Height, false);
            return true;
        }

#if UNITY_EDITOR
        private static bool TryApplyEditorGameViewSize(DevelopmentViewportPreset preset)
        {
            try
            {
                Type sizesType = FindEditorType("UnityEditor.GameViewSizes");
                Type singletonOpenType = FindEditorType("UnityEditor.ScriptableSingleton`1");
                Type groupEnumType = FindEditorType("UnityEditor.GameViewSizeGroupType");
                Type sizeEnumType = FindEditorType("UnityEditor.GameViewSizeType");
                Type sizeType = FindEditorType("UnityEditor.GameViewSize");
                Type gameViewType = FindEditorType("UnityEditor.GameView");
                Type editorWindowType = FindEditorType("UnityEditor.EditorWindow");
                if (sizesType == null || singletonOpenType == null || groupEnumType == null ||
                    sizeEnumType == null || sizeType == null || gameViewType == null ||
                    editorWindowType == null)
                    return false;

                Type singletonType = singletonOpenType.MakeGenericType(sizesType);
                object sizes = singletonType.GetProperty(
                    "instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(null, null);
                object standalone = Enum.Parse(groupEnumType, "Standalone");
                object group = sizesType.GetMethod("GetGroup")?.Invoke(sizes, new[] { standalone });
                if (group == null) return false;

                int selectedIndex = FindSizeIndex(group, preset);
                if (selectedIndex < 0 && !preset.IsFreeAspect)
                {
                    object fixedResolution = Enum.Parse(sizeEnumType, "FixedResolution");
                    ConstructorInfo constructor = sizeType.GetConstructor(new[]
                    {
                        sizeEnumType, typeof(int), typeof(int), typeof(string)
                    });
                    object size = constructor?.Invoke(new object[]
                    {
                        fixedResolution, preset.Width, preset.Height, $"Project-C · {preset.Label}"
                    });
                    group.GetType().GetMethod("AddCustomSize")?.Invoke(group, new[] { size });
                    selectedIndex = Convert.ToInt32(
                        group.GetType().GetMethod("GetTotalCount")?.Invoke(group, null)) - 1;
                }
                if (selectedIndex < 0) return false;

                MethodInfo getWindow = null;
                foreach (MethodInfo method in editorWindowType.GetMethods(
                             BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name == "GetWindow" && parameters.Length == 1 &&
                        parameters[0].ParameterType == typeof(Type))
                    {
                        getWindow = method;
                        break;
                    }
                }
                object gameView = getWindow?.Invoke(null, new object[] { gameViewType });
                PropertyInfo selected = gameViewType.GetProperty(
                    "selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selected?.SetValue(gameView, selectedIndex, null);
                gameViewType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(gameView, null);
                return gameView != null && selected != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Game View 해상도 전환 실패: {exception.Message}");
                return false;
            }
        }

        private static int FindSizeIndex(object group, DevelopmentViewportPreset preset)
        {
            Type groupType = group.GetType();
            MethodInfo totalMethod = groupType.GetMethod("GetTotalCount");
            MethodInfo getSizeMethod = groupType.GetMethod("GetGameViewSize");
            int total = Convert.ToInt32(totalMethod?.Invoke(group, null));
            for (int i = 0; i < total; i++)
            {
                object size = getSizeMethod?.Invoke(group, new object[] { i });
                if (size == null) continue;
                Type currentType = size.GetType();
                if (preset.IsFreeAspect)
                {
                    bool free = Convert.ToBoolean(currentType.GetProperty("isFreeAspectRatio")
                        ?.GetValue(size, null));
                    if (free) return i;
                    continue;
                }

                int width = Convert.ToInt32(currentType.GetProperty("width")?.GetValue(size, null));
                int height = Convert.ToInt32(currentType.GetProperty("height")?.GetValue(size, null));
                string kind = currentType.GetProperty("sizeType")?.GetValue(size, null)?.ToString();
                if (width == preset.Width && height == preset.Height && kind == "FixedResolution")
                    return i;
            }
            return -1;
        }

        private static Type FindEditorType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
#endif
    }
}
