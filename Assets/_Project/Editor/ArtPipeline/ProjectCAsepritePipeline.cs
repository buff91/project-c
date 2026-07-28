using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectC.Core;
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

        // 128-레짐: 바닥 타일 128×64px = 월드 1.0×0.5 유닛. PrototypeSpriteCanvas의
        // 절차 생성 상수(64)와 다른 것이 정상이다 — 스프라이트는 각자 PPU를 갖는다.
        private const float PixelsPerUnit = 128f;

        private static readonly Dictionary<string, string> CatalogSlots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "env-floor", "floor" },
                { "env-floor-raised", "raisedFloor" },
                { "env-floor-lower", "lowerFloor" },
                { "env-floor-mid", "midFloor" },
                { "env-floor-mid-raised", "midRaisedFloor" },
                { "env-floor-deep", "deepFloor" },
                { "env-floor-deep-raised", "deepRaisedFloor" },
                { "env-floor-boss", "bossFloor" },
                { "env-floor-boss-raised", "bossRaisedFloor" },
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
                { "actor-slinger", "slinger" },
                { "actor-arc-drone", "arcDrone" },
                { "actor-grave-warden", "graveWarden" },
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

        private static readonly HashSet<string> EnvironmentAnimationSlots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "env-wall-torch-rising-right",
                "env-wall-torch-rising-left",
                "prop-campfire",
                "prop-portal"
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
            // 피벗 SSOT는 ProjectCArtPivots — PNG 폴백 임포터와 값을 공유한다.
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            return ProjectCArtPivots.ResolveOrDefault(assetName, new Vector2(0.5f, 0f));
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
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            // 액터 애니메이션 베이크 — 태그별 AnimationClip 서브에셋을 프레임 배열로 굽는다.
            // actorKey는 Sprite 슬롯 필드명 계약(CatalogSlots)을 그대로 재사용한다.
            var bakedAnimations = new List<ActorAnimationSet>();
            foreach (string path in sources)
            {
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (duplicateNames.Contains(assetName) ||
                    !assetName.StartsWith("actor-", StringComparison.OrdinalIgnoreCase) ||
                    !TryGetCatalogSlot(path, out string actorKey))
                    continue;

                ActorAnimationSet set = ActorAnimationBake.ExtractSet(path, actorKey);
                if (set != null && set.HasClips)
                    bakedAnimations.Add(set);
            }

            bakedAnimations.Sort((a, b) => string.CompareOrdinal(a.actorKey, b.actorKey));
            if (!ActorAnimationBake.SetsEqual(catalog.actorAnimations, bakedAnimations))
            {
                catalog.actorAnimations = bakedAnimations;
                changed++;
            }

            // 환경/소품은 idle 태그만 굽는다. 렌더러가 숨겨지면 같은 경량
            // SpriteClipAnimator가 멈추므로 FOV 밖/비활성 층 비용도 들지 않는다.
            var environmentAnimations = new List<EnvironmentAnimationSet>();
            foreach (string path in sources)
            {
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (duplicateNames.Contains(assetName) ||
                    assetName.StartsWith("actor-", StringComparison.OrdinalIgnoreCase) ||
                    !EnvironmentAnimationSlots.Contains(assetName) ||
                    !TryGetCatalogSlot(path, out string slotKey))
                    continue;

                EnvironmentAnimationSet set =
                    ActorAnimationBake.ExtractEnvironmentSet(path, slotKey);
                if (set != null && set.HasClips)
                    environmentAnimations.Add(set);
            }

            environmentAnimations.Sort(
                (a, b) => string.CompareOrdinal(a.slotKey, b.slotKey));
            if (!ActorAnimationBake.EnvironmentSetsEqual(
                    catalog.environmentAnimations,
                    environmentAnimations))
            {
                catalog.environmentAnimations = environmentAnimations;
                changed++;
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            if (logResult)
            {
                Debug.Log(
                    $"[Project-C Aseprite] {sources.Length}개 원본 검사, " +
                    $"{bound}개 슬롯 연결, {changed}개 갱신, " +
                    $"애니 세트 {bakedAnimations.Count}개");
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

                CollectClipProblems(path, problems);
            }

            return problems;
        }

        /// <summary>
        /// 액터 소스의 태그 클립 규약 검사 — 베이크에서 조용히 버려지거나 어긋나는 것을
        /// 에디터에서 미리 잡는다 (파일명 계약처럼 "조용한 실패"를 만들지 않는다).
        /// </summary>
        private static void CollectClipProblems(string path, List<string> problems)
        {
            string assetName = Path.GetFileNameWithoutExtension(path);
            bool actor = assetName.StartsWith(
                "actor-",
                StringComparison.OrdinalIgnoreCase);
            if (!actor && !EnvironmentAnimationSlots.Contains(assetName))
                return;

            AnimationClip[] clips =
                AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            if (clips.Length == 0) return;
            bool hasTaggedClip = false;
            bool hasIdle = false;
            foreach (AnimationClip clip in clips)
            {
                string tag = ActorAnimationBake.TagFromClipName(clip.name);
                if (tag == null || (!actor && tag != SpriteClipTags.Idle))
                {
                    string contract = actor
                        ? "idle/walk/attack/hit/fall/death"
                        : "idle";
                    problems.Add(
                        $"태그 규약({contract}) 밖 클립 '{clip.name}': {path}");
                    continue;
                }

                hasTaggedClip = true;
                hasIdle |= tag == SpriteClipTags.Idle;
                if (ActorAnimationBake.HasNonSpriteCurves(clip))
                    problems.Add(
                        $"클립 '{clip.name}'에 sprite 외 커브가 있음 — 베이크에서 버려진다" +
                        $"(transform/color는 게임 코드 소유): {path}");
                if (actor &&
                    ActorAnimationBake.IsOneShotTag(tag) &&
                    clip.isLooping)
                    problems.Add($"원샷 태그 '{clip.name}'가 루프로 임포트됨 — Aseprite Tag Repeat=1 확인: {path}");
            }

            if (hasTaggedClip && !hasIdle)
                problems.Add($"태그 클립이 있는데 idle이 없음 — 재생기의 기본 상태가 비게 된다: {path}");
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
