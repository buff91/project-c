using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Project-C의 Aseprite 원본을 Unity 2D Aseprite Importer로 직접 가져오고,
    /// 첫 프레임 Sprite를 공용 IsoVisualCatalog 슬롯에 연결한다.
    /// </summary>
    public sealed class ProjectCAsepritePipeline : AssetPostprocessor
    {
        public const string SourceRoot = "Assets/_Project/Art/Source/Aseprite/";
        public const string CatalogPath =
            "Assets/_Project/Art/Environment/ProjectCEnvironmentCatalog.asset";

        private const float PixelsPerUnit = 64f;

        private static readonly Dictionary<string, string> CatalogSlots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "env-floor", "floor" },
                { "env-floor-raised", "raisedFloor" },
                { "env-floor-lower", "lowerFloor" },
                { "env-stairs", "stairs" },
                { "env-ladder", "ladder" },
                { "env-stairs-up", "stairsUp" },
                { "env-stairs-down", "stairsDown" },
                { "env-hole", "hole" },
                { "env-weak-floor", "weakFloor" },
                { "env-door-closed", "doorClosed" },
                { "env-door-open", "doorOpen" },
                { "env-stairs-rising-right", "stairsRisingRight" },
                { "env-stairs-rising-left", "stairsRisingLeft" },
                { "env-stairs-up-rising-right", "stairsUpRisingRight" },
                { "env-stairs-up-rising-left", "stairsUpRisingLeft" },
                { "env-stairs-down-rising-right", "stairsDownRisingRight" },
                { "env-stairs-down-rising-left", "stairsDownRisingLeft" },
                { "env-door-closed-rising-right", "doorClosedRisingRight" },
                { "env-door-closed-rising-left", "doorClosedRisingLeft" },
                { "env-door-open-rising-right", "doorOpenRisingRight" },
                { "env-door-open-rising-left", "doorOpenRisingLeft" },
                { "env-wall-rising-right", "rearWallRisingRight" },
                { "env-wall-rising-left", "rearWallRisingLeft" },
                { "env-wall-torch-rising-right", "rearWallTorchRisingRight" },
                { "env-wall-torch-rising-left", "rearWallTorchRisingLeft" },
                { "actor-player", "player" },
                { "actor-knight", "knight" },
                { "actor-ranger", "ranger" },
                { "actor-alchemist", "alchemist" },
                { "actor-goblin", "goblin" },
                { "actor-skeleton", "skeleton" },
                { "actor-slime", "slime" },
                { "actor-merchant", "merchant" },
                { "prop-explosive-barrel", "explosiveBarrel" },
                { "prop-campfire", "hubCampfire" },
                { "prop-stash", "hubStash" },
                { "prop-portal", "hubPortal" },
                { "marker-player", "playerFootprint" },
                { "marker-target", "selection" },
                { "item-potion", "potion" },
                { "item-bomb", "bomb" },
                { "item-frost-bomb", "frostBomb" },
                { "item-oil-flask", "oilFlask" },
                { "item-throwing-knife", "throwingKnife" },
                { "item-recall-scroll", "recallScroll" },
                { "item-coin-pouch", "coinPouch" },
                { "item-gemstone", "gemstone" },
                { "item-relic", "relic" },
                { "item-herb", "herb" },
                { "item-blast-powder", "blastPowder" },
                { "item-frost-shard", "frostShard" },
                { "fx-impact-physical", "fxImpactPhysical" },
                { "fx-impact-fire", "fxImpactFire" },
                { "fx-impact-frost", "fxImpactFrost" },
                { "fx-impact-heavy", "fxImpactHeavy" },
                { "fx-status-burn", "fxStatusBurn" },
                { "fx-status-freeze", "fxStatusFreeze" }
            };

        private static readonly Dictionary<string, Vector2> CustomPivots =
            new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
            {
                { "env-floor", new Vector2(0.5f, 0.5f) },
                { "env-stairs-rising-right", new Vector2(0.5f, 16f / 56f) },
                { "env-stairs-rising-left", new Vector2(0.5f, 16f / 56f) },
                { "env-stairs-up-rising-right", new Vector2(0.5f, 16f / 56f) },
                { "env-stairs-up-rising-left", new Vector2(0.5f, 16f / 56f) },
                { "env-stairs-down-rising-right", new Vector2(0.5f, 16f / 40f) },
                { "env-stairs-down-rising-left", new Vector2(0.5f, 16f / 40f) },
                { "env-door-closed-rising-right", new Vector2(0.5f, 16f / 80f) },
                { "env-door-closed-rising-left", new Vector2(0.5f, 16f / 80f) },
                { "env-door-open-rising-right", new Vector2(0.5f, 16f / 80f) },
                { "env-door-open-rising-left", new Vector2(0.5f, 16f / 80f) },
                { "env-wall-rising-right", new Vector2(0.5f, 8f / 56f) },
                { "env-wall-rising-left", new Vector2(0.5f, 8f / 56f) },
                { "env-wall-torch-rising-right", new Vector2(0.5f, 8f / 56f) },
                { "env-wall-torch-rising-left", new Vector2(0.5f, 8f / 56f) },
                { "actor-player", new Vector2(0.5f, 0.04f) },
                { "actor-knight", new Vector2(0.5f, 0.04f) },
                { "actor-ranger", new Vector2(0.5f, 0.04f) },
                { "actor-alchemist", new Vector2(0.5f, 0.04f) },
                { "actor-goblin", new Vector2(0.5f, 0.04f) },
                { "actor-skeleton", new Vector2(0.5f, 0.04f) },
                { "actor-slime", new Vector2(0.5f, 0.04f) },
                { "actor-merchant", new Vector2(0.5f, 0.04f) },
                { "prop-campfire", new Vector2(0.5f, 6f / 64f) },
                { "prop-explosive-barrel", new Vector2(0.5f, 5f / 64f) },
                { "prop-portal", new Vector2(0.5f, 6f / 80f) },
                { "prop-stash", new Vector2(0.5f, 11f / 64f) },
                { "marker-player", new Vector2(0.5f, 0.5f) },
                { "marker-target", new Vector2(0.5f, 0.5f) },
                { "item-blast-powder", new Vector2(0.5f, 5f / 32f) },
                { "item-bomb", new Vector2(0.5f, 4f / 32f) },
                { "item-coin-pouch", new Vector2(0.5f, 6f / 32f) },
                { "item-frost-bomb", new Vector2(0.5f, 4f / 32f) },
                { "item-frost-shard", new Vector2(0.5f, 4f / 32f) },
                { "item-gemstone", new Vector2(0.5f, 2f / 32f) },
                { "item-herb", new Vector2(0.5f, 5f / 32f) },
                { "item-oil-flask", new Vector2(0.5f, 4f / 32f) },
                { "item-potion", new Vector2(0.5f, 4f / 32f) },
                { "item-recall-scroll", new Vector2(0.5f, 3f / 32f) },
                { "item-relic", new Vector2(0.5f, 3f / 32f) },
                { "item-throwing-knife", new Vector2(0.5f, 2f / 32f) },
                { "fx-impact-physical", new Vector2(0.5f, 0.5f) },
                { "fx-impact-fire", new Vector2(0.5f, 0.5f) },
                { "fx-impact-frost", new Vector2(0.5f, 0.5f) },
                { "fx-impact-heavy", new Vector2(0.5f, 0.5f) },
                { "fx-status-burn", new Vector2(0.5f, 0.5f) },
                { "fx-status-freeze", new Vector2(0.5f, 0.5f) }
            };

        private static bool _catalogSyncQueued;

        private void OnPreprocessAsset()
        {
            if (!IsAsepriteSourcePath(assetPath) ||
                !(assetImporter is AsepriteImporter importer))
                return;

            ConfigureImporter(importer, assetPath);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!ContainsAsepriteSource(importedAssets) &&
                !ContainsAsepriteSource(deletedAssets) &&
                !ContainsAsepriteSource(movedAssets) &&
                !ContainsAsepriteSource(movedFromAssetPaths))
                return;

            QueueCatalogSync();
        }

        public static bool IsAsepriteSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith(SourceRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            string extension = Path.GetExtension(path);
            return extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ase", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetCatalogSlot(string sourcePath, out string slotName)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            return CatalogSlots.TryGetValue(assetName, out slotName);
        }

        public static Vector2 ResolvePivotNormalized(string sourcePath)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            return CustomPivots.TryGetValue(assetName, out Vector2 pivot)
                ? pivot
                : new Vector2(0.5f, 0f);
        }

        public static int FrameIndexFromSpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return int.MaxValue;

            int separator = spriteName.LastIndexOf('_');
            if (separator < 0 || separator == spriteName.Length - 1)
                return int.MaxValue;

            return int.TryParse(spriteName.Substring(separator + 1), out int frame)
                ? frame
                : int.MaxValue;
        }

        public static Sprite SelectFirstFrame(IEnumerable<Sprite> sprites)
        {
            return sprites?
                .Where(sprite => sprite != null)
                .OrderBy(sprite => FrameIndexFromSpriteName(sprite.name))
                .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        [MenuItem("Project-C/Art/Aseprite/Reimport and Sync Catalog")]
        public static void ReimportAndSyncCatalog()
        {
            string[] sources = FindAsepriteSources();
            foreach (string path in sources)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            SyncCatalog(logResult: true);
        }

        [MenuItem("Project-C/Art/Aseprite/Validate Sources")]
        public static void ValidateSources()
        {
            string[] sources = FindAsepriteSources();
            List<string> problems = CollectProblems(sources);
            if (problems.Count == 0)
            {
                Debug.Log($"[Project-C Aseprite] 검증 통과: {sources.Length}개 원본");
                return;
            }

            foreach (string problem in problems)
                Debug.LogWarning($"[Project-C Aseprite] {problem}");
        }

        private static void ConfigureImporter(AsepriteImporter importer, string path)
        {
            importer.importMode = FileImportModes.AnimatedSprite;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spriteMeshType = SpriteMeshType.FullRect;
            importer.generatePhysicsShape = false;
            importer.includeHiddenLayers = false;
            importer.layerImportMode = LayerImportModes.MergeFrame;
            importer.pivotSpace = PivotSpaces.Canvas;
            importer.pivotAlignment = SpriteAlignment.Custom;
            importer.customPivotPosition = ResolvePivotNormalized(path);
            importer.mosaicPadding = 4;
            importer.spritePadding = 0;
            importer.generateModelPrefab = false;
            importer.generateAnimationClips = true;
            importer.generateIndividualEvents = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.aniso = 1;

            SetUncompressed(importer, BuildTarget.StandaloneOSX);
            SetUncompressed(importer, BuildTarget.Android);
            SetUncompressed(importer, BuildTarget.iOS);
        }

        private static void SetUncompressed(AsepriteImporter importer, BuildTarget buildTarget)
        {
            TextureImporterPlatformSettings settings =
                importer.GetImporterPlatformSettings(buildTarget);
            settings.overridden = true;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetImporterPlatformSettings(settings);
        }

        private static void QueueCatalogSync()
        {
            if (_catalogSyncQueued)
                return;

            _catalogSyncQueued = true;
            EditorApplication.delayCall += () =>
            {
                _catalogSyncQueued = false;
                SyncCatalog(logResult: false);
            };
        }

        private static void SyncCatalog(bool logResult)
        {
            IsoVisualCatalog catalog =
                AssetDatabase.LoadAssetAtPath<IsoVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[Project-C Aseprite] 카탈로그를 찾을 수 없습니다: {CatalogPath}");
                return;
            }

            string[] sources = FindAsepriteSources();
            var duplicateNames = sources
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var serializedCatalog = new SerializedObject(catalog);
            int changed = 0;
            int bound = 0;

            foreach (string path in sources)
            {
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (duplicateNames.Contains(assetName) ||
                    !TryGetCatalogSlot(path, out string slotName))
                    continue;

                Sprite sprite = SelectFirstFrame(
                    AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
                if (sprite == null)
                    continue;

                SerializedProperty slot = serializedCatalog.FindProperty(slotName);
                if (slot == null)
                {
                    Debug.LogError(
                        $"[Project-C Aseprite] IsoVisualCatalog 슬롯이 없습니다: {slotName}");
                    continue;
                }

                bound++;
                if (slot.objectReferenceValue == sprite)
                    continue;

                slot.objectReferenceValue = sprite;
                changed++;
            }

            if (changed > 0)
            {
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            if (logResult)
            {
                Debug.Log(
                    $"[Project-C Aseprite] {sources.Length}개 원본 검사, " +
                    $"{bound}개 슬롯 연결, {changed}개 갱신");
            }
        }

        private static List<string> CollectProblems(IEnumerable<string> sources)
        {
            string[] paths = sources.ToArray();
            var problems = new List<string>();

            foreach (IGrouping<string, string> duplicate in paths
                         .GroupBy(Path.GetFileNameWithoutExtension,
                             StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                problems.Add(
                    $"중복 파일명 '{duplicate.Key}': {string.Join(", ", duplicate)}");
            }

            foreach (string path in paths)
            {
                if (!TryGetCatalogSlot(path, out _))
                    problems.Add($"카탈로그 규칙에 없는 파일명: {path}");

                if (!(AssetImporter.GetAtPath(path) is AsepriteImporter importer))
                {
                    problems.Add($"Unity Aseprite Importer가 적용되지 않음: {path}");
                    continue;
                }

                if (Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerUnit) > 0.01f ||
                    importer.filterMode != FilterMode.Point ||
                    importer.mipmapEnabled ||
                    importer.layerImportMode != LayerImportModes.MergeFrame)
                {
                    problems.Add($"임포트 규격 불일치(재임포트 필요): {path}");
                }

                if (SelectFirstFrame(
                        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) == null)
                    problems.Add($"Sprite 프레임이 없음: {path}");
            }

            return problems;
        }

        private static bool ContainsAsepriteSource(IEnumerable<string> paths)
        {
            return paths != null && paths.Any(IsAsepriteSourcePath);
        }

        private static string[] FindAsepriteSources()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot.TrimEnd('/')))
                return Array.Empty<string>();

            return AssetDatabase.FindAssets(string.Empty, new[] { SourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsAsepriteSourcePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
