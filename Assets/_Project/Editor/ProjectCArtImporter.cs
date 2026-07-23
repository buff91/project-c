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

            // 월드 스프라이트는 캔버스 바닥이 아니라 실제 불투명 픽셀의
            // 접지선이 GridToWorld 위치에 닿아야 한다. 아트별 투명 여백을 반영한다.
            if (path.EndsWith("/prop-campfire.png")) return Grounded(6, 64);
            if (path.EndsWith("/prop-explosive-barrel.png")) return Grounded(5, 64);
            if (path.EndsWith("/prop-portal.png")) return Grounded(6, 80);
            if (path.EndsWith("/prop-stash.png")) return Grounded(11, 64);

            if (path.EndsWith("/item-blast-powder.png")) return Grounded(5, 32);
            if (path.EndsWith("/item-bomb.png")) return Grounded(4, 32);
            if (path.EndsWith("/item-coin-pouch.png")) return Grounded(6, 32);
            if (path.EndsWith("/item-frost-bomb.png")) return Grounded(4, 32);
            if (path.EndsWith("/item-frost-shard.png")) return Grounded(4, 32);
            if (path.EndsWith("/item-gemstone.png")) return Grounded(2, 32);
            if (path.EndsWith("/item-herb.png")) return Grounded(5, 32);
            if (path.EndsWith("/item-oil-flask.png")) return Grounded(4, 32);
            if (path.EndsWith("/item-potion.png")) return Grounded(4, 32);
            if (path.EndsWith("/item-recall-scroll.png")) return Grounded(3, 32);
            if (path.EndsWith("/item-relic.png")) return Grounded(3, 32);
            if (path.EndsWith("/item-throwing-knife.png")) return Grounded(2, 32);

            return new Vector2(0.5f, 0.04f);
        }

        private static Vector2 Grounded(int transparentBottomPixels, int textureHeight) =>
            new Vector2(0.5f, transparentBottomPixels / (float)textureHeight);

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
