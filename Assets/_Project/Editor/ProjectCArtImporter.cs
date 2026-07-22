using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Project-C 런타임 픽셀아트의 임포트 규격을 한 곳에서 강제한다.
    /// 씬별 수동 설정 차이로 Point/PPU/Pivot이 갈라지는 것을 막는다.
    /// </summary>
    public sealed class ProjectCArtImporter : AssetPostprocessor
    {
        private const string RuntimeArtRoot = "Assets/_Project/Art/Runtime/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(RuntimeArtRoot, System.StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = ResolvePivot(assetPath);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static Vector2 ResolvePivot(string path)
        {
            if (path.Contains("/marker-"))
                return new Vector2(0.5f, 0.5f);

            if (path.Contains("/item-"))
                return new Vector2(0.5f, 0.08f);

            return new Vector2(0.5f, 0.04f);
        }

        [MenuItem("Project-C/Reimport Runtime Pixel Art")]
        public static void ReimportRuntimeArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RuntimeArtRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
