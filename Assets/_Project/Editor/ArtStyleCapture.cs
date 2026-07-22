using System.IO;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ProjectC.EditorTools
{
    /// <summary>허브와 전투 월드 아트의 공통 카탈로그 연결을 540×960으로 검증한다.</summary>
    public static class ArtStyleCapture
    {
        private const string CaptureRoot = "Assets/_Project/Captures";

        [MenuItem("Project-C/Capture Art Style Pair")]
        public static void CapturePair()
        {
            ProjectCArtImporter.ReimportRuntimeArt();
            CaptureScene("Assets/_Project/Scenes/Hub.unity", "art-v2-hub-world.png");
            CaptureScene("Assets/_Project/Scenes/IsoPrototype.unity", "art-v2-combat-world.png");
            AssetDatabase.Refresh();
        }

        private static void CaptureScene(string scenePath, string fileName)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            if (demo == null)
                throw new System.InvalidOperationException($"IsoPrototypeDemo missing: {scenePath}");

            demo.BuildPrototype();
            ValidateCatalogBindings(demo.hubMode);
            Camera camera = Camera.main;
            if (camera == null)
                throw new System.InvalidOperationException($"MainCamera missing: {scenePath}");

            Directory.CreateDirectory(CaptureRoot);
            string output = Path.GetFullPath(Path.Combine(CaptureRoot, fileName));
            var target = new RenderTexture(
                540,
                960,
                GraphicsFormat.R8G8B8A8_SRGB,
                GraphicsFormat.D24_UNorm_S8_UInt);
            target.Create();

            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new System.InvalidOperationException("Camera render request is unsupported.");
            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(540, 960, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = previous;

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            Debug.Log($"[ArtStyle] Captured {output}");
        }

        private static void ValidateCatalogBindings(bool hubMode)
        {
            RequireSprite("Player", "actor-");
            if (!hubMode) return;

            RequireSprite("Merchant", "actor-merchant");
            RequireSprite("Hero knight", "actor-knight");
            RequireSprite("Hero ranger", "actor-ranger");
            RequireSprite("Hero alchemist", "actor-alchemist");
            RequireSprite("Campfire", "prop-campfire");
            RequireSprite("Stash", "prop-stash");
            RequireSprite("Portal", "prop-portal");
        }

        private static void RequireSprite(string objectName, string spritePrefix)
        {
            GameObject instance = GameObject.Find(objectName);
            Sprite sprite = instance != null ? instance.GetComponent<SpriteRenderer>()?.sprite : null;
            if (sprite == null || !sprite.name.StartsWith(spritePrefix, System.StringComparison.Ordinal))
                throw new System.InvalidOperationException(
                    $"{objectName} expected {spritePrefix}*, got {sprite?.name ?? "null"}");
        }
    }
}
