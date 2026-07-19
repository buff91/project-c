using System.IO;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.EditorTools
{
    public static class IsoPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/IsoPrototype.unity";
        private const string HudPath = "Assets/_Project/UI/PrototypeHUD.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/PrototypePanelSettings.asset";

        [MenuItem("Project-C/Build Isometric Prototype")]
        public static void CreateSceneAndCapture()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, -1.65f, -10f);

            var root = new GameObject("Iso Prototype");
            root.AddComponent<GridManager>();
            root.AddComponent<IsoTapInput>();
            var demo = root.AddComponent<IsoPrototypeDemo>();
            demo.buildOnStart = true;
            demo.configureMainCamera = true;

            CreateHud(demo);

            EditorSceneManager.SaveScene(scene, ScenePath);

            demo.BuildPrototype();
            Capture(camera);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IsoPrototype] Scene: {ScenePath}");
        }

        private static void CreateHud(IsoPrototypeDemo demo)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(540, 960);
                panelSettings.match = 0.5f;
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            var hudObject = new GameObject("Prototype HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudPath);

            var controller = hudObject.AddComponent<PrototypeHudController>();
            controller.demo = demo;
        }

        private static void Capture(Camera camera)
        {
            const int width = 540;
            const int height = 960;
            string output = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../docs/art-direction/iso-prototype-room.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var target = new RenderTexture(
                width,
                height,
                GraphicsFormat.R8G8B8A8_SRGB,
                GraphicsFormat.D24_UNorm_S8_UInt);
            target.Create();

            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new System.InvalidOperationException("현재 렌더 파이프라인이 카메라 캡처 요청을 지원하지 않습니다.");

            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = previous;

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            Debug.Log($"[IsoPrototype] Capture: {output}");
        }
    }
}
