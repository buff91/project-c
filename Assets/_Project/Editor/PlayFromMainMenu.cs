using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// 에디터 Play 시 어떤 씬이 열려 있어도 메인 메뉴부터 시작하는 토글. 기본 OFF —
    /// 평소엔 열려 있는 씬을 바로 도는 반복 개발용, 전체 흐름 확인 때만 켠다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        private const string MenuPath = "ProjectC/Play From Main Menu";
        private const string PrefKey = "ProjectC.PlayFromMainMenu";
        private const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        static PlayFromMainMenu()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, false));
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }

        private static void Apply()
        {
            if (!EditorPrefs.GetBool(PrefKey, false))
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
            if (scene == null)
            {
                Debug.LogWarning($"[PlayFromMainMenu] 씬을 찾을 수 없음: {MenuScenePath}");
                return;
            }
            EditorSceneManager.playModeStartScene = scene;
        }
    }
}
