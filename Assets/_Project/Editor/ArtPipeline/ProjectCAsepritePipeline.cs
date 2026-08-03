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
        private const string TextureImporterSettingsProperty =
            "m_TextureImporterSettings";
        private const string TextureReadableProperty = "m_IsReadable";
        private const string PreviousTextureSizeProperty =
            "m_PreviousTextureSize";

        private static readonly Vector2Int FloorCanvasSize = new Vector2Int(128, 64);
        private static readonly Vector2Int WallCanvasSize = new Vector2Int(64, 112);
        private static readonly Vector2Int ActorCanvasSize = new Vector2Int(96, 128);
        private static readonly Vector2Int ExplosiveBarrelCanvasSize =
            new Vector2Int(128, 128);
        private static readonly string[] PngFallbackRoots =
        {
            "Assets/_Project/Art/Environment/",
            "Assets/_Project/Art/Runtime/"
        };
        private static readonly string[] B2DirectionalFloorPrefixes =
        {
            "env-floor-b2-parking-stop-view-",
            "env-floor-b2-fallen-sign-view-",
            "env-floor-b2-barrel-bay-service-view-",
            "env-floor-b2-barrel-bay-drain-view-",
            "env-floor-b2-macro-role-0-view-",
            "env-floor-b2-macro-role-1-view-",
            "env-floor-b2-macro-role-2-view-",
            "env-floor-b2-macro-role-3-view-"
        };
        private static readonly string[] RequiredB2ServiceWallSources =
        {
            "env-wall-b2-service-segment-0-rising-right",
            "env-wall-b2-service-segment-0-rising-left",
            "env-wall-b2-service-segment-1-rising-right",
            "env-wall-b2-service-segment-1-rising-left",
            "env-wall-b2-service-segment-2-rising-right",
            "env-wall-b2-service-segment-2-rising-left"
        };
        private static readonly string[] RequiredActorTags =
        {
            SpriteClipTags.Idle,
            SpriteClipTags.Walk,
            SpriteClipTags.Attack,
            SpriteClipTags.Hit,
            SpriteClipTags.Fall,
            SpriteClipTags.Death
        };
        private static readonly ActorFacing4[] RequiredActorFacings =
        {
            ActorFacing4.North,
            ActorFacing4.East,
            ActorFacing4.South,
            ActorFacing4.West
        };

        private static readonly Dictionary<string, string> CatalogSlots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "env-dungeon-backdrop", "dungeonBackdrop" },
                { "env-floor", "floor" },
                { "env-floor-raised", "raisedFloor" },
                { "env-floor-lower", "lowerFloor" },
                { "env-floor-mid", "midFloor" },
                { "env-floor-mid-raised", "midRaisedFloor" },
                { "env-floor-deep", "deepFloor" },
                { "env-floor-deep-raised", "deepRaisedFloor" },
                { "env-floor-boss", "bossFloor" },
                { "env-floor-boss-raised", "bossRaisedFloor" },
                { "env-floor-grate", "hospitalFloorGrate" },
                { "env-floor-cracked", "hospitalFloorCracked" },
                { "env-floor-service", "hospitalFloorService" },
                { "env-floor-b2-parking-stop", "b2ParkingWheelStopFloor" },
                { "env-floor-b2-fallen-sign", "b2FallenWayfindingFloor" },
                { "env-floor-b2-cracked", "b2CrackedFloor" },
                { "env-floor-b2-parking-stop-view-0", "b2ParkingWheelStopFloorView0" },
                { "env-floor-b2-parking-stop-view-1", "b2ParkingWheelStopFloorView1" },
                { "env-floor-b2-parking-stop-view-2", "b2ParkingWheelStopFloorView2" },
                { "env-floor-b2-parking-stop-view-3", "b2ParkingWheelStopFloorView3" },
                { "env-floor-b2-fallen-sign-view-0", "b2FallenWayfindingFloorView0" },
                { "env-floor-b2-fallen-sign-view-1", "b2FallenWayfindingFloorView1" },
                { "env-floor-b2-fallen-sign-view-2", "b2FallenWayfindingFloorView2" },
                { "env-floor-b2-fallen-sign-view-3", "b2FallenWayfindingFloorView3" },
                { "env-floor-b2-barrel-bay-service-view-0", "b2BarrelBayServiceFloorView0" },
                { "env-floor-b2-barrel-bay-service-view-1", "b2BarrelBayServiceFloorView1" },
                { "env-floor-b2-barrel-bay-service-view-2", "b2BarrelBayServiceFloorView2" },
                { "env-floor-b2-barrel-bay-service-view-3", "b2BarrelBayServiceFloorView3" },
                { "env-floor-b2-barrel-bay-drain-view-0", "b2BarrelBayDrainFloorView0" },
                { "env-floor-b2-barrel-bay-drain-view-1", "b2BarrelBayDrainFloorView1" },
                { "env-floor-b2-barrel-bay-drain-view-2", "b2BarrelBayDrainFloorView2" },
                { "env-floor-b2-barrel-bay-drain-view-3", "b2BarrelBayDrainFloorView3" },
                { "env-floor-b2-macro-role-0-view-0", "b2MacroFloorRole0View0" },
                { "env-floor-b2-macro-role-0-view-1", "b2MacroFloorRole0View1" },
                { "env-floor-b2-macro-role-0-view-2", "b2MacroFloorRole0View2" },
                { "env-floor-b2-macro-role-0-view-3", "b2MacroFloorRole0View3" },
                { "env-floor-b2-macro-role-1-view-0", "b2MacroFloorRole1View0" },
                { "env-floor-b2-macro-role-1-view-1", "b2MacroFloorRole1View1" },
                { "env-floor-b2-macro-role-1-view-2", "b2MacroFloorRole1View2" },
                { "env-floor-b2-macro-role-1-view-3", "b2MacroFloorRole1View3" },
                { "env-floor-b2-macro-role-2-view-0", "b2MacroFloorRole2View0" },
                { "env-floor-b2-macro-role-2-view-1", "b2MacroFloorRole2View1" },
                { "env-floor-b2-macro-role-2-view-2", "b2MacroFloorRole2View2" },
                { "env-floor-b2-macro-role-2-view-3", "b2MacroFloorRole2View3" },
                { "env-floor-b2-macro-role-3-view-0", "b2MacroFloorRole3View0" },
                { "env-floor-b2-macro-role-3-view-1", "b2MacroFloorRole3View1" },
                { "env-floor-b2-macro-role-3-view-2", "b2MacroFloorRole3View2" },
                { "env-floor-b2-macro-role-3-view-3", "b2MacroFloorRole3View3" },
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
                { "env-wall-pipes-rising-right", "hospitalWallPipesRisingRight" },
                { "env-wall-pipes-rising-left", "hospitalWallPipesRisingLeft" },
                { "env-wall-window-rising-right", "hospitalWallWindowRisingRight" },
                { "env-wall-window-rising-left", "hospitalWallWindowRisingLeft" },
                { "env-wall-cabinet-rising-right", "hospitalWallCabinetRisingRight" },
                { "env-wall-cabinet-rising-left", "hospitalWallCabinetRisingLeft" },
                { "env-wall-b2-service-segment-0-rising-right", "b2ServiceWallSegment0RisingRight" },
                { "env-wall-b2-service-segment-0-rising-left", "b2ServiceWallSegment0RisingLeft" },
                { "env-wall-b2-service-segment-1-rising-right", "b2ServiceWallSegment1RisingRight" },
                { "env-wall-b2-service-segment-1-rising-left", "b2ServiceWallSegment1RisingLeft" },
                { "env-wall-b2-service-segment-2-rising-right", "b2ServiceWallSegment2RisingRight" },
                { "env-wall-b2-service-segment-2-rising-left", "b2ServiceWallSegment2RisingLeft" },
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
        private static bool _readablePropertyWarningLogged;
        private static bool _spritePackingPropertyWarningLogged;
        private static readonly Dictionary<string, HashSet<string>>
            PendingRemovedSourcePaths =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase);

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
            RecordRemovedSources(deletedAssets, movedAssets, movedFromAssetPaths);

            if (!ContainsAsepriteSource(importedAssets) &&
                !ContainsAsepriteSource(deletedAssets) &&
                !ContainsAsepriteSource(movedAssets) &&
                !ContainsAsepriteSource(movedFromAssetPaths))
                return;

            QueueCatalogSync();
        }

        private static void RecordRemovedSources(
            IEnumerable<string> deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string deletedPath in deletedAssets ?? Array.Empty<string>())
                RecordRemovedSource(deletedPath, movedToPath: null);

            int movedCount = Math.Min(
                movedAssets?.Length ?? 0,
                movedFromAssetPaths?.Length ?? 0);
            for (int index = 0; index < movedCount; index++)
                RecordRemovedSource(movedFromAssetPaths[index], movedAssets[index]);
        }

        private static void RecordRemovedSource(
            string oldPath,
            string movedToPath)
        {
            if (!IsAsepriteSourcePath(oldPath) ||
                !TryGetCatalogSlot(oldPath, out _))
                return;

            string assetName = Path.GetFileNameWithoutExtension(oldPath);
            if (!PendingRemovedSourcePaths.TryGetValue(
                    assetName,
                    out HashSet<string> paths))
            {
                paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                PendingRemovedSourcePaths.Add(assetName, paths);
            }

            paths.Add(oldPath);
            paths.Add(movedToPath ?? string.Empty);
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

        public static bool RequiresReadableTexture(string sourcePath)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            return IsFloorAssetName(assetName) ||
                   assetName.StartsWith("env-wall-", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetExpectedCanvasSize(
            string sourcePath,
            out Vector2Int canvasSize)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            if (IsFloorAssetName(assetName))
            {
                canvasSize = FloorCanvasSize;
                return true;
            }

            if (IsWallAssetName(assetName))
            {
                canvasSize = WallCanvasSize;
                return true;
            }

            if (assetName.StartsWith("actor-", StringComparison.OrdinalIgnoreCase))
            {
                canvasSize = ActorCanvasSize;
                return true;
            }

            if (assetName.Equals(
                    "prop-explosive-barrel",
                    StringComparison.OrdinalIgnoreCase))
            {
                canvasSize = ExplosiveBarrelCanvasSize;
                return true;
            }

            canvasSize = default;
            return false;
        }

        private static bool IsFloorAssetName(string assetName) =>
            assetName.Equals("env-floor", StringComparison.OrdinalIgnoreCase) ||
            assetName.StartsWith("env-floor-", StringComparison.OrdinalIgnoreCase);

        private static bool IsWallAssetName(string assetName) =>
            assetName.StartsWith("env-wall-", StringComparison.OrdinalIgnoreCase);

        public static string[] MissingRequiredActorTags(IEnumerable<string> clipNames)
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (clipNames != null)
            {
                foreach (string clipName in clipNames)
                {
                    string tag = ActorAnimationBake.TagFromClipName(clipName);
                    if (tag != null)
                        present.Add(ActorAnimationBake.BaseTag(tag));
                }
            }

            return RequiredActorTags.Where(tag => !present.Contains(tag)).ToArray();
        }

        /// <summary>
        /// 방향 태그를 하나라도 쓰기 시작한 액터는 상태마다 4방향을 모두 요구한다.
        /// 비방향 구형 세트는 빈 배열을 반환해 기존 폴백 계약을 보존한다.
        /// </summary>
        public static string[] MissingDirectionalActorTags(IEnumerable<string> clipNames)
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasDirectional = false;
            if (clipNames != null)
            {
                foreach (string clipName in clipNames)
                {
                    string tag = ActorAnimationBake.TagFromClipName(clipName);
                    if (tag == null) continue;
                    present.Add(tag);
                    hasDirectional |= DirectionalSpriteClipTags.TryParse(tag, out _, out _);
                }
            }

            if (!hasDirectional) return Array.Empty<string>();

            return RequiredActorTags
                .SelectMany(baseTag => RequiredActorFacings.Select(
                    facing => DirectionalSpriteClipTags.Compose(baseTag, facing)))
                .Where(tag => !present.Contains(tag))
                .ToArray();
        }

        public static string[] MissingRequiredB2ViewSources(
            IEnumerable<string> sourcePaths)
        {
            var sourceNames = new HashSet<string>(
                (sourcePaths ?? Array.Empty<string>())
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase);
            return B2DirectionalFloorPrefixes
                .SelectMany(prefix => Enumerable.Range(0, 4)
                    .Select(view => $"{prefix}{view}"))
                .Where(assetName => !sourceNames.Contains(assetName))
                .ToArray();
        }

        public static string[] MissingRequiredB2ServiceWallSources(
            IEnumerable<string> sourcePaths)
        {
            var sourceNames = new HashSet<string>(
                (sourcePaths ?? Array.Empty<string>())
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase);
            return RequiredB2ServiceWallSources
                .Where(assetName => !sourceNames.Contains(assetName))
                .ToArray();
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
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

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

            // Aseprite Importer 5.0.x keeps the previous Sprite rect/UV when
            // every cel still spans the same canvas and the packed atlas size
            // is unchanged. Actor frames intentionally use full-canvas cels,
            // so changing their alpha silhouette could otherwise pair a new
            // atlas with stale rects and display fragmented characters.
            if (RequiresFreshSpritePacking(path) &&
                !TryInvalidateSpritePacking(importer) &&
                !_spritePackingPropertyWarningLogged)
            {
                _spritePackingPropertyWarningLogged = true;
                Debug.LogWarning(
                    "[Project-C Aseprite] Unity 2D Aseprite Importer의 이전 " +
                    $"아틀라스 크기 속성을 찾지 못했습니다: {path}");
            }

            if (RequiresReadableTexture(path) &&
                !TrySetTextureReadable(importer, readable: true) &&
                !_readablePropertyWarningLogged)
            {
                _readablePropertyWarningLogged = true;
                Debug.LogWarning(
                    "[Project-C Aseprite] Unity 2D Aseprite Importer의 readable " +
                    $"직렬화 속성을 찾지 못했습니다: {path}");
            }

            SetUncompressed(importer, BuildTarget.StandaloneOSX);
            SetUncompressed(importer, BuildTarget.Android);
            SetUncompressed(importer, BuildTarget.iOS);
        }

        public static bool RequiresFreshSpritePacking(string sourcePath)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);
            return assetName.StartsWith("actor-", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryInvalidateSpritePacking(AssetImporter assetImporter)
        {
            var importer = assetImporter as AsepriteImporter;
            if (importer == null) return false;

            var serializedImporter = new SerializedObject(importer);
            serializedImporter.UpdateIfRequiredOrScript();
            SerializedProperty previousTextureSize =
                serializedImporter.FindProperty(PreviousTextureSizeProperty);
            if (previousTextureSize == null)
                return false;

            previousTextureSize.vector2Value = Vector2.zero;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TrySetTextureReadable(
            AsepriteImporter importer,
            bool readable)
        {
            var serializedImporter = new SerializedObject(importer);
            serializedImporter.UpdateIfRequiredOrScript();
            SerializedProperty textureSettings =
                serializedImporter.FindProperty(TextureImporterSettingsProperty);
            SerializedProperty readableProperty =
                textureSettings?.FindPropertyRelative(TextureReadableProperty);
            if (readableProperty == null)
                return false;

            if (readableProperty.boolValue != readable)
            {
                readableProperty.boolValue = readable;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            }

            return true;
        }

        private static bool TryGetTextureReadable(
            AsepriteImporter importer,
            out bool readable)
        {
            var serializedImporter = new SerializedObject(importer);
            serializedImporter.UpdateIfRequiredOrScript();
            SerializedProperty textureSettings =
                serializedImporter.FindProperty(TextureImporterSettingsProperty);
            SerializedProperty readableProperty =
                textureSettings?.FindPropertyRelative(TextureReadableProperty);
            if (readableProperty == null)
            {
                readable = false;
                return false;
            }

            readable = readableProperty.boolValue;
            return true;
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

        private static Dictionary<string, string[]> DrainRemovedSourcePaths()
        {
            var snapshot = PendingRemovedSourcePaths.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
            PendingRemovedSourcePaths.Clear();
            return snapshot;
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
            int changed = SynchronizeSpriteSlots(
                catalog,
                sources,
                DrainRemovedSourcePaths(),
                out int bound);

            var duplicateNames = sources
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        public static int SynchronizeSpriteSlots(
            IsoVisualCatalog catalog,
            IEnumerable<string> sourcePaths,
            IReadOnlyDictionary<string, string[]> removedSourcePaths,
            out int bound)
        {
            bound = 0;
            if (catalog == null)
                return 0;

            string[] sources = (sourcePaths ?? Array.Empty<string>()).ToArray();
            var duplicateNames = sources
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeSourceNames = sources
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedByName = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
            if (removedSourcePaths != null)
            {
                foreach (KeyValuePair<string, string[]> pair in removedSourcePaths)
                {
                    removedByName[pair.Key] = new HashSet<string>(
                        pair.Value ?? Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
                }
            }

            var serializedCatalog = new SerializedObject(catalog);
            int changed = 0;

            // 현재 존재하는 정식 원본이 항상 우선이다.
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

            // 삭제/이동 이벤트에서 실제로 빠져나간 Aseprite 참조만 복구한다.
            // 다른 Aseprite/PNG를 사용자가 수동으로 꽂은 슬롯은 경로가 일치하지 않아 보존된다.
            foreach (KeyValuePair<string, string> mapping in CatalogSlots)
            {
                if (activeSourceNames.Contains(mapping.Key) ||
                    !removedByName.TryGetValue(
                        mapping.Key,
                        out HashSet<string> removedPaths))
                    continue;

                SerializedProperty slot = serializedCatalog.FindProperty(mapping.Value);
                if (slot == null)
                    continue;

                Sprite current = slot.objectReferenceValue as Sprite;
                if (!WasRemovedSourceReference(current, removedPaths))
                    continue;

                Sprite fallback = FindPngFallback(mapping.Key);
                if (current != null && current == fallback)
                    continue;

                // current가 Missing으로 null처럼 보여도 대입을 수행해 직렬화된 stale GUID를 지운다.
                slot.objectReferenceValue = fallback;
                changed++;
            }

            if (changed > 0)
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool WasRemovedSourceReference(
            Sprite current,
            IReadOnlyCollection<string> removedPaths)
        {
            if (removedPaths == null || removedPaths.Count == 0)
                return false;
            if (current == null)
                return true;

            string currentPath = AssetDatabase.GetAssetPath(current);
            if (string.IsNullOrEmpty(currentPath))
                return removedPaths.Contains(string.Empty);

            return removedPaths.Any(path =>
                !string.IsNullOrEmpty(path) &&
                string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
        }

        private static Sprite FindPngFallback(string assetName)
        {
            foreach (string root in PngFallbackRoots)
            {
                string path = $"{root}{assetName}.png";
                Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                                  AssetDatabase.LoadAllAssetsAtPath(path)
                                      .OfType<Sprite>()
                                      .FirstOrDefault();
                if (fallback != null)
                    return fallback;
            }

            return null;
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

            string[] missingB2Views = MissingRequiredB2ViewSources(paths);
            if (missingB2Views.Length > 0)
            {
                problems.Add(
                    "B2 방향 원본 세트 불완전(view-0..3 필수) — 누락: " +
                    string.Join(", ", missingB2Views));
            }

            string[] missingB2ServiceWalls =
                MissingRequiredB2ServiceWallSources(paths);
            if (missingB2ServiceWalls.Length > 0)
            {
                problems.Add(
                    "B2 서비스 벽 원본 세트 불완전(3세그먼트×좌우 필수) — 누락: " +
                    string.Join(", ", missingB2ServiceWalls));
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

                Sprite firstSprite = SelectFirstFrame(
                    AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
                if (firstSprite == null)
                    problems.Add($"Sprite 프레임이 없음: {path}");
                else
                    CollectCanvasProblems(path, importer, firstSprite, problems);

                CollectClipProblems(path, problems);
            }

            return problems;
        }

        private static void CollectCanvasProblems(
            string path,
            AsepriteImporter importer,
            Sprite firstSprite,
            List<string> problems)
        {
            if (TryGetExpectedCanvasSize(path, out Vector2Int expectedCanvas) &&
                (Mathf.RoundToInt(importer.canvasSize.x) != expectedCanvas.x ||
                 Mathf.RoundToInt(importer.canvasSize.y) != expectedCanvas.y))
            {
                problems.Add(
                    $"캔버스 규격 불일치({expectedCanvas.x}×{expectedCanvas.y} 필수, " +
                    $"현재 {importer.canvasSize.x:0}×{importer.canvasSize.y:0}): {path}");
            }

            string assetName = Path.GetFileNameWithoutExtension(path);
            if (IsFloorAssetName(assetName))
            {
                Rect rect = firstSprite.rect;
                if (Mathf.RoundToInt(rect.width) != FloorCanvasSize.x ||
                    Mathf.RoundToInt(rect.height) != FloorCanvasSize.y)
                {
                    problems.Add(
                        $"바닥 첫 Sprite 규격 불일치(128×64 필수, " +
                        $"현재 {rect.width:0}×{rect.height:0}): {path}");
                }

                Vector2 expectedPivot = new Vector2(0.5f, 0.5f);
                bool importerPivotMismatch =
                    importer.pivotSpace != PivotSpaces.Canvas ||
                    importer.pivotAlignment != SpriteAlignment.Custom ||
                    Vector2.Distance(importer.customPivotPosition, expectedPivot) > 0.0001f;
                Vector2 spritePivot = new Vector2(
                    firstSprite.pivot.x / Mathf.Max(1f, rect.width),
                    firstSprite.pivot.y / Mathf.Max(1f, rect.height));
                if (importerPivotMismatch ||
                    Vector2.Distance(spritePivot, expectedPivot) > 0.0001f)
                {
                    problems.Add(
                        "바닥 피봇 규격 불일치(Canvas 중앙 0.5,0.5 필수): " + path);
                }
            }
            else if (IsWallAssetName(assetName))
            {
                // Aseprite Importer는 투명 여백을 Sprite rect에서 trim한다. 벽은
                // 64×112 canvas와 Canvas-space 피벗이 계약이고, trimmed rect 크기나
                // 그 rect 안의 정규화 피벗을 64×112 값과 직접 비교하면 정상 자산도
                // 외곽 세그먼트에서 실패한다.
                Vector2 expectedPivot = ResolvePivotNormalized(path);
                bool importerPivotMismatch =
                    importer.pivotSpace != PivotSpaces.Canvas ||
                    importer.pivotAlignment != SpriteAlignment.Custom ||
                    Vector2.Distance(importer.customPivotPosition, expectedPivot) > 0.0001f;
                if (importerPivotMismatch)
                {
                    problems.Add(
                        $"벽 피봇 규격 불일치(Canvas {expectedPivot.x:0.###}," +
                        $"{expectedPivot.y:0.###} 필수): {path}");
                }
            }

            if (!RequiresReadableTexture(path))
                return;

            if (!TryGetTextureReadable(importer, out bool readable))
            {
                problems.Add(
                    "Unity 2D Aseprite Importer의 readable 직렬화 속성을 " +
                    $"확인할 수 없음: {path}");
            }
            else if (!readable)
            {
                problems.Add(
                    "환경 텍스처 Read/Write 비활성 — 톤매핑/단차 생성이 " +
                    $"원본 폴백함(재임포트 필요): {path}");
            }
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
                        ? "idle/walk/attack/hit/fall/death + 선택적 -north/-east/-south/-west"
                        : "idle";
                    problems.Add(
                        $"태그 규약({contract}) 밖 클립 '{clip.name}': {path}");
                    continue;
                }

                hasTaggedClip = true;
                hasIdle |= ActorAnimationBake.BaseTag(tag) == SpriteClipTags.Idle;
                if (ActorAnimationBake.HasNonSpriteCurves(clip))
                    problems.Add(
                        $"클립 '{clip.name}'에 sprite 외 커브가 있음 — 베이크에서 버려진다" +
                        $"(transform/color는 게임 코드 소유): {path}");
                if (actor &&
                    ActorAnimationBake.IsOneShotTag(tag) &&
                    clip.isLooping)
                    problems.Add($"원샷 태그 '{clip.name}'가 루프로 임포트됨 — Aseprite Tag Repeat=1 확인: {path}");
            }

            if (actor && hasTaggedClip)
            {
                string[] missing = MissingRequiredActorTags(
                    clips.Select(clip => clip.name));
                if (missing.Length > 0)
                {
                    problems.Add(
                        $"액터 필수 태그 누락({string.Join("/", missing)}): {path}");
                }

                string[] missingDirectional = MissingDirectionalActorTags(
                    clips.Select(clip => clip.name));
                if (missingDirectional.Length > 0)
                {
                    problems.Add(
                        $"방향 액터 태그 불완전({string.Join("/", missingDirectional)}): {path}");
                }
            }
            else if (hasTaggedClip && !hasIdle)
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
