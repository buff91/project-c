using System.Collections.Generic;
using System.IO;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.EditorTools
{
    public static class MainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string HudPath = "Assets/_Project/UI/MainMenuHUD.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/PrototypePanelSettings.asset";

        [MenuItem("Project-C/Build Main Menu Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color32(5, 7, 12, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.4f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var hudObject = new GameObject("Main Menu HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudPath);
            hudObject.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureFirstBuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[MainMenu] Scene: {ScenePath}");
        }

        private static void EnsureFirstBuildScene()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath) continue;
                scenes.Add(scene);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
