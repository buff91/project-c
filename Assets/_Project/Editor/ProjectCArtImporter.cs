using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Project-C 런타임 픽셀아트의 임포트 규격을 한 곳에서 강제한다.
    /// 씬별 수동 설정 차이로 Point/PPU/Pivot이 갈라지는 것을 막는다.
    /// 대상은 PNG 폴백 두 폴더(Runtime·Environment) — Aseprite 원본은
    /// ProjectCAsepritePipeline이 같은 규격으로 처리한다.
    /// </summary>
    public sealed class ProjectCArtImporter : AssetPostprocessor
    {
        private const string RuntimeArtRoot = "Assets/_Project/Art/Runtime/";
        private const string EnvironmentArtRoot = "Assets/_Project/Art/Environment/";

        // 128-레짐: 바닥 타일 128×64px = 월드 1.0×0.5 유닛.
        private const float WorldPixelsPerUnit = 128f;
        // ui-* 는 UI Toolkit이 픽셀 크기로 소비한다 — 64-레짐에 남는다(월드에 놓이지 않는다).
        private const float UiPixelsPerUnit = 64f;

        private void OnPreprocessTexture()
        {
            bool runtimeArt =
                assetPath.StartsWith(RuntimeArtRoot, System.StringComparison.Ordinal);
            bool environmentArt =
                assetPath.StartsWith(EnvironmentArtRoot, System.StringComparison.Ordinal);
            if (!runtimeArt && !environmentArt)
                return;

            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            bool uiSprite = baseName.StartsWith("ui-", System.StringComparison.Ordinal);

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = uiSprite ? UiPixelsPerUnit : WorldPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = ResolvePivot(baseName);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static Vector2 ResolvePivot(string baseName)
        {
            // 피벗 SSOT는 ProjectCArtPivots(Aseprite 파이프라인과 공유).
            // 표에 없는 월드 스프라이트는 액터 접지 기본값 — 캔버스 바닥이 아니라
            // 실제 불투명 픽셀의 접지선이 GridToWorld 위치에 닿아야 한다.
            return ProjectCArtPivots.ResolveOrDefault(baseName, new Vector2(0.5f, 0.04f));
        }

        [MenuItem("Project-C/Reimport Runtime Pixel Art")]
        public static void ReimportRuntimeArt()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D", new[] { RuntimeArtRoot, EnvironmentArtRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
