using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.EditorTools;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests
{
    public class ProjectCAsepritePipelineTests
    {
        private readonly List<Sprite> _sprites = new List<Sprite>();

        [TearDown]
        public void TearDown()
        {
            foreach (Sprite sprite in _sprites)
                Object.DestroyImmediate(sprite);
            _sprites.Clear();
        }

        [Test]
        public void SourcePath_AcceptsAsepriteExtensionsOnlyInsideSourceRoot()
        {
            Assert.IsTrue(ProjectCAsepritePipeline.IsAsepriteSourcePath(
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite"));
            Assert.IsTrue(ProjectCAsepritePipeline.IsAsepriteSourcePath(
                "Assets/_Project/Art/Source/Aseprite/Actors/actor-knight.ase"));
            Assert.IsFalse(ProjectCAsepritePipeline.IsAsepriteSourcePath(
                "Assets/_Project/Art/Runtime/actor-knight.aseprite"));
            Assert.IsFalse(ProjectCAsepritePipeline.IsAsepriteSourcePath(
                "Assets/_Project/Art/Source/Aseprite/actor-knight.png"));
        }

        [Test]
        public void CanvasContracts_RequireFloorAndActorSourceSizes()
        {
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite",
                out Vector2Int floor));
            Assert.AreEqual(new Vector2Int(128, 64), floor);
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                "Assets/_Project/Art/Source/Aseprite/env-floor-b2-parking-stop.aseprite",
                out Vector2Int floorDressing));
            Assert.AreEqual(new Vector2Int(128, 64), floorDressing);
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite",
                out Vector2Int actor));
            Assert.AreEqual(new Vector2Int(96, 128), actor);
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                "Assets/_Project/Art/Source/Aseprite/env-wall-rising-left.aseprite",
                out Vector2Int wall));
            Assert.AreEqual(new Vector2Int(64, 112), wall);

            Assert.IsTrue(ProjectCAsepritePipeline.RequiresReadableTexture(
                "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite"));
            Assert.IsTrue(ProjectCAsepritePipeline.RequiresReadableTexture(
                "Assets/_Project/Art/Source/Aseprite/env-floor-cracked.aseprite"));
            Assert.IsTrue(ProjectCAsepritePipeline.RequiresReadableTexture(
                "Assets/_Project/Art/Source/Aseprite/env-wall-rising-left.aseprite"));
            Assert.IsFalse(ProjectCAsepritePipeline.RequiresReadableTexture(
                "Assets/_Project/Art/Source/Aseprite/env-flooring.aseprite"));
            Assert.IsFalse(ProjectCAsepritePipeline.RequiresReadableTexture(
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite"));
            Assert.IsTrue(ProjectCAsepritePipeline.RequiresFreshSpritePacking(
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite"));
            Assert.IsFalse(ProjectCAsepritePipeline.RequiresFreshSpritePacking(
                "Assets/_Project/Art/Source/Aseprite/env-wall-rising-left.aseprite"));
        }

        [Test]
        public void ActorPackingRefresh_ClearsPreviousAtlasSizeBeforeImport()
        {
            const string source =
                "Assets/_Project/Art/Source/Aseprite/actor-skeleton.aseprite";
            AssetImporter importer = AssetImporter.GetAtPath(source);
            Assert.IsNotNull(importer);
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty previousTextureSize =
                serializedImporter.FindProperty("m_PreviousTextureSize");
            Assert.IsNotNull(previousTextureSize);
            Vector2 original = previousTextureSize.vector2Value;

            try
            {
                Assert.IsTrue(
                    ProjectCAsepritePipeline.TryInvalidateSpritePacking(importer));
                serializedImporter.UpdateIfRequiredOrScript();
                Assert.AreEqual(Vector2.zero, previousTextureSize.vector2Value);
            }
            finally
            {
                serializedImporter.UpdateIfRequiredOrScript();
                previousTextureSize.vector2Value = original;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [Test]
        public void ActorTags_RequireAllSixCanonicalClips()
        {
            CollectionAssert.IsEmpty(ProjectCAsepritePipeline.MissingRequiredActorTags(
                new[]
                {
                    "actor-knight_idle",
                    "walk",
                    "attack",
                    "hit",
                    "fall",
                    "actor-knight_death"
                }));

            CollectionAssert.AreEqual(
                new[] { "attack", "hit", "fall", "death" },
                ProjectCAsepritePipeline.MissingRequiredActorTags(
                    new[] { "idle", "actor-knight_walk", "sprint" }));
            CollectionAssert.AreEqual(
                new[] { "idle", "walk", "attack", "hit", "fall", "death" },
                ProjectCAsepritePipeline.MissingRequiredActorTags(null));
        }

        [Test]
        public void AsepritePackage_ExposesReadableSettingUsedByFloorPipeline()
        {
            const string source =
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite";
            AssetImporter importer = AssetImporter.GetAtPath(source);
            Assert.IsNotNull(importer);

            var serializedImporter = new SerializedObject(importer);
            SerializedProperty textureSettings =
                serializedImporter.FindProperty("m_TextureImporterSettings");
            Assert.IsNotNull(textureSettings);
            Assert.IsNotNull(textureSettings.FindPropertyRelative("m_IsReadable"));
        }

        [Test]
        public void CatalogSlot_MapsCanonicalAssetNames()
        {
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite",
                out string floorSlot));
            Assert.AreEqual("floor", floorSlot);

            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/env-dungeon-backdrop.aseprite",
                out string backdropSlot));
            Assert.AreEqual("dungeonBackdrop", backdropSlot);

            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/Actors/actor-merchant.aseprite",
                out string merchantSlot));
            Assert.AreEqual("merchant", merchantSlot);

            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/actor-arc-drone.aseprite",
                out string arcDroneSlot));
            Assert.AreEqual("arcDrone", arcDroneSlot);

            Assert.IsFalse(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/unknown.aseprite", out _));
        }

        [Test]
        public void CatalogSlot_MapsEveryArcadeEnemySourceToItsDedicatedKey()
        {
            var expected = new (string assetName, string actorKey)[]
            {
                ("actor-goblin", "goblin"),
                ("actor-skeleton", "skeleton"),
                ("actor-slime", "slime"),
                ("actor-slinger", "slinger"),
                ("actor-arc-drone", "arcDrone"),
                ("actor-grave-warden", "graveWarden")
            };

            foreach ((string assetName, string actorKey) in expected)
            {
                string path = $"Assets/_Project/Art/Source/Aseprite/{assetName}.aseprite";
                Assert.IsTrue(
                    ProjectCAsepritePipeline.TryGetCatalogSlot(path, out string actual),
                    path);
                Assert.AreEqual(actorKey, actual, path);
                Assert.AreEqual(
                    new Vector2(0.5f, 0.04f),
                    ProjectCAsepritePipeline.ResolvePivotNormalized(path),
                    path);
            }
        }

        [Test]
        public void ArcadeEnemySources_PreserveCanonicalAssetGuids()
        {
            var expected = new (string assetName, string guid)[]
            {
                ("actor-goblin", "140d82dfde08e47159fe8e835bf46607"),
                ("actor-skeleton", "12447a58e71744e55adbdc4706d9be42"),
                ("actor-slime", "ac10defb2ea41478f88213647de474be"),
                ("actor-slinger", "6324e100e318d4b5490cedf66fe5431c"),
                ("actor-arc-drone", "3c599dde46a734139bd0088c0a0418eb"),
                ("actor-grave-warden", "802a7c2b689f8498eb27ac761a707117")
            };

            foreach ((string assetName, string guid) in expected)
            {
                string path =
                    $"Assets/_Project/Art/Source/Aseprite/{assetName}.aseprite";
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path), path);
            }
        }

        [Test]
        public void CatalogSlot_MapsDepthBandFloors_WithCenteredPivot()
        {
            // 배치 1 발주 계약 — 밴드 바닥 6종은 정식 파일명으로 저장만 하면 자동 연결돼야 한다.
            var expected = new (string fileName, string slot)[]
            {
                ("env-floor-mid", "midFloor"),
                ("env-floor-mid-raised", "midRaisedFloor"),
                ("env-floor-deep", "deepFloor"),
                ("env-floor-deep-raised", "deepRaisedFloor"),
                ("env-floor-boss", "bossFloor"),
                ("env-floor-boss-raised", "bossRaisedFloor"),
            };
            foreach ((string fileName, string slot) in expected)
            {
                string path = $"Assets/_Project/Art/Source/Aseprite/{fileName}.aseprite";
                Assert.IsTrue(
                    ProjectCAsepritePipeline.TryGetCatalogSlot(path, out string actual),
                    $"CatalogSlots에 {fileName} 계약이 없다");
                Assert.AreEqual(slot, actual);
                Assert.AreEqual(
                    new Vector2(0.5f, 0.5f),
                    ProjectCAsepritePipeline.ResolvePivotNormalized(path),
                    $"{fileName} 피벗은 바닥 다이아 중앙이어야 한다");
            }
        }

        [Test]
        public void CatalogSlot_MapsHospitalDressing_WithStablePivots()
        {
            var expected = new (string fileName, string slot, Vector2 pivot)[]
            {
                ("env-floor-grate", "hospitalFloorGrate", new Vector2(0.5f, 0.5f)),
                ("env-floor-cracked", "hospitalFloorCracked", new Vector2(0.5f, 0.5f)),
                ("env-floor-service", "hospitalFloorService", new Vector2(0.5f, 0.5f)),
                (
                    "env-wall-pipes-rising-right",
                    "hospitalWallPipesRisingRight",
                    new Vector2(0.5f, 16f / 112f)
                ),
                (
                    "env-wall-window-rising-left",
                    "hospitalWallWindowRisingLeft",
                    new Vector2(0.5f, 16f / 112f)
                ),
                (
                    "env-wall-cabinet-rising-right",
                    "hospitalWallCabinetRisingRight",
                    new Vector2(0.5f, 16f / 112f)
                ),
            };

            foreach ((string fileName, string slot, Vector2 pivot) in expected)
            {
                string path = $"Assets/_Project/Art/Source/Aseprite/{fileName}.aseprite";
                Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(path, out string actual));
                Assert.AreEqual(slot, actual);
                Assert.AreEqual(pivot, ProjectCAsepritePipeline.ResolvePivotNormalized(path));
            }
        }

        [Test]
        public void CatalogSlot_MapsCompleteB2ServiceWall_WithWallPivots()
        {
            for (int segment = 0; segment < 3; segment++)
            foreach (string direction in new[] { "right", "left" })
            {
                string fileName =
                    $"env-wall-b2-service-segment-{segment}-rising-{direction}";
                string slotName =
                    $"b2ServiceWallSegment{segment}Rising" +
                    (direction == "right" ? "Right" : "Left");
                string path =
                    $"Assets/_Project/Art/Source/Aseprite/{fileName}.aseprite";
                Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(path, out string actual));
                Assert.AreEqual(slotName, actual);
                Assert.AreEqual(
                    new Vector2(0.5f, 16f / 112f),
                    ProjectCAsepritePipeline.ResolvePivotNormalized(path));
                Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                    path,
                    out Vector2Int canvas));
                Assert.AreEqual(new Vector2Int(64, 112), canvas);
                Assert.IsTrue(ProjectCAsepritePipeline.RequiresReadableTexture(path));
            }
        }

        [Test]
        public void CatalogSlot_MapsB2FuelCell_WithPropCanvasAndGroundedPivot()
        {
            const string path =
                "Assets/_Project/Art/Source/Aseprite/prop-explosive-barrel.aseprite";

            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                path,
                out string slot));
            Assert.AreEqual("explosiveBarrel", slot);
            Assert.AreEqual(
                new Vector2(0.5f, 10f / 128f),
                ProjectCAsepritePipeline.ResolvePivotNormalized(path));
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                path,
                out Vector2Int canvas));
            Assert.AreEqual(new Vector2Int(128, 128), canvas);
        }

        [Test]
        public void CatalogSlot_MapsB2FloorDressing_WithCenteredPivots()
        {
            var expected = new (string fileName, string slot)[]
            {
                ("env-floor-b2-parking-stop", "b2ParkingWheelStopFloor"),
                ("env-floor-b2-fallen-sign", "b2FallenWayfindingFloor"),
                ("env-floor-b2-cracked", "b2CrackedFloor"),
                ("env-floor-b2-parking-stop-view-0", "b2ParkingWheelStopFloorView0"),
                ("env-floor-b2-parking-stop-view-1", "b2ParkingWheelStopFloorView1"),
                ("env-floor-b2-parking-stop-view-2", "b2ParkingWheelStopFloorView2"),
                ("env-floor-b2-parking-stop-view-3", "b2ParkingWheelStopFloorView3"),
                ("env-floor-b2-fallen-sign-view-0", "b2FallenWayfindingFloorView0"),
                ("env-floor-b2-fallen-sign-view-1", "b2FallenWayfindingFloorView1"),
                ("env-floor-b2-fallen-sign-view-2", "b2FallenWayfindingFloorView2"),
                ("env-floor-b2-fallen-sign-view-3", "b2FallenWayfindingFloorView3"),
                ("env-floor-b2-barrel-bay-service-view-0", "b2BarrelBayServiceFloorView0"),
                ("env-floor-b2-barrel-bay-service-view-1", "b2BarrelBayServiceFloorView1"),
                ("env-floor-b2-barrel-bay-service-view-2", "b2BarrelBayServiceFloorView2"),
                ("env-floor-b2-barrel-bay-service-view-3", "b2BarrelBayServiceFloorView3"),
                ("env-floor-b2-barrel-bay-drain-view-0", "b2BarrelBayDrainFloorView0"),
                ("env-floor-b2-barrel-bay-drain-view-1", "b2BarrelBayDrainFloorView1"),
                ("env-floor-b2-barrel-bay-drain-view-2", "b2BarrelBayDrainFloorView2"),
                ("env-floor-b2-barrel-bay-drain-view-3", "b2BarrelBayDrainFloorView3"),
                ("env-floor-b2-macro-role-0-view-0", "b2MacroFloorRole0View0"),
                ("env-floor-b2-macro-role-0-view-1", "b2MacroFloorRole0View1"),
                ("env-floor-b2-macro-role-0-view-2", "b2MacroFloorRole0View2"),
                ("env-floor-b2-macro-role-0-view-3", "b2MacroFloorRole0View3"),
                ("env-floor-b2-macro-role-1-view-0", "b2MacroFloorRole1View0"),
                ("env-floor-b2-macro-role-1-view-1", "b2MacroFloorRole1View1"),
                ("env-floor-b2-macro-role-1-view-2", "b2MacroFloorRole1View2"),
                ("env-floor-b2-macro-role-1-view-3", "b2MacroFloorRole1View3"),
                ("env-floor-b2-macro-role-2-view-0", "b2MacroFloorRole2View0"),
                ("env-floor-b2-macro-role-2-view-1", "b2MacroFloorRole2View1"),
                ("env-floor-b2-macro-role-2-view-2", "b2MacroFloorRole2View2"),
                ("env-floor-b2-macro-role-2-view-3", "b2MacroFloorRole2View3"),
                ("env-floor-b2-macro-role-3-view-0", "b2MacroFloorRole3View0"),
                ("env-floor-b2-macro-role-3-view-1", "b2MacroFloorRole3View1"),
                ("env-floor-b2-macro-role-3-view-2", "b2MacroFloorRole3View2"),
                ("env-floor-b2-macro-role-3-view-3", "b2MacroFloorRole3View3"),
            };

            foreach ((string fileName, string slot) in expected)
            {
                string path = $"Assets/_Project/Art/Source/Aseprite/{fileName}.aseprite";
                Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(path, out string actual));
                Assert.AreEqual(slot, actual);
                Assert.AreEqual(
                    new Vector2(0.5f, 0.5f),
                    ProjectCAsepritePipeline.ResolvePivotNormalized(path));
                Assert.IsTrue(ProjectCAsepritePipeline.TryGetExpectedCanvasSize(
                    path,
                    out Vector2Int canvas));
                Assert.AreEqual(new Vector2Int(128, 64), canvas);
                Assert.IsTrue(ProjectCAsepritePipeline.RequiresReadableTexture(path));
            }
        }

        [Test]
        public void B2DirectionalSources_RequireCompleteViewZeroThroughThreeSets()
        {
            string sourceRoot = ProjectCAsepritePipeline.SourceRoot.TrimEnd('/');
            string[] sources = AssetDatabase.FindAssets(string.Empty, new[] { sourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(ProjectCAsepritePipeline.IsAsepriteSourcePath)
                .ToArray();

            CollectionAssert.IsEmpty(
                ProjectCAsepritePipeline.MissingRequiredB2ViewSources(sources));

            string[] withoutParkingViewTwo = sources
                .Where(path => !path.EndsWith(
                    "env-floor-b2-parking-stop-view-2.aseprite",
                    System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "env-floor-b2-parking-stop-view-2" },
                ProjectCAsepritePipeline.MissingRequiredB2ViewSources(
                    withoutParkingViewTwo));

            string[] withoutBarrelBayDrainViewThree = sources
                .Where(path => !path.EndsWith(
                    "env-floor-b2-barrel-bay-drain-view-3.aseprite",
                    System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "env-floor-b2-barrel-bay-drain-view-3" },
                ProjectCAsepritePipeline.MissingRequiredB2ViewSources(
                    withoutBarrelBayDrainViewThree));

            string[] withoutMacroRoleTwoViewOne = sources
                .Where(path => !path.EndsWith(
                    "env-floor-b2-macro-role-2-view-1.aseprite",
                    System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "env-floor-b2-macro-role-2-view-1" },
                ProjectCAsepritePipeline.MissingRequiredB2ViewSources(
                    withoutMacroRoleTwoViewOne));

            CollectionAssert.IsEmpty(
                ProjectCAsepritePipeline.MissingRequiredB2ServiceWallSources(sources));
            string[] withoutServiceCenterLeft = sources
                .Where(path => !path.EndsWith(
                    "env-wall-b2-service-segment-1-rising-left.aseprite",
                    System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "env-wall-b2-service-segment-1-rising-left" },
                ProjectCAsepritePipeline.MissingRequiredB2ServiceWallSources(
                    withoutServiceCenterLeft));
        }

        [Test]
        public void ResolvePivot_UsesStableCanvasAnchors()
        {
            Assert.AreEqual(
                new Vector2(0.5f, 0.5f),
                ProjectCAsepritePipeline.ResolvePivotNormalized(
                    "env-dungeon-backdrop.aseprite"));
            Assert.AreEqual(
                new Vector2(0.5f, 0.5f),
                ProjectCAsepritePipeline.ResolvePivotNormalized("env-floor.aseprite"));
            Assert.AreEqual(
                new Vector2(0.5f, 0.04f),
                ProjectCAsepritePipeline.ResolvePivotNormalized("actor-knight.aseprite"));
            Assert.AreEqual(
                new Vector2(0.5f, 8f / 56f),
                ProjectCAsepritePipeline.ResolvePivotNormalized(
                    "env-wall-rising-right.aseprite"));
            // 세워진 사다리 랜드마크 — 절차 아트와 같은 발 기준 피벗.
            Assert.AreEqual(
                new Vector2(0.5f, 0.08f),
                ProjectCAsepritePipeline.ResolvePivotNormalized("env-ladder.aseprite"));
        }

        [Test]
        public void SelectFirstFrame_UsesNumericFrameIndex()
        {
            Sprite frameTen = MakeSprite("actor-knight_10");
            Sprite frameTwo = MakeSprite("actor-knight_2");
            Sprite frameZero = MakeSprite("actor-knight_0");

            Assert.AreSame(
                frameZero,
                ProjectCAsepritePipeline.SelectFirstFrame(
                    new[] { frameTen, frameTwo, frameZero }));
        }

        [Test]
        public void SynchronizeSpriteSlots_RemovedAsepriteRestoresPngFallback_AndPreservesManualReference()
        {
            const string removedSource =
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite";
            Sprite removedSprite = ProjectCAsepritePipeline.SelectFirstFrame(
                AssetDatabase.LoadAllAssetsAtPath(removedSource).OfType<Sprite>());
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Runtime/actor-knight.png");
            Assert.IsNotNull(removedSprite);
            Assert.IsNotNull(fallback);

            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            try
            {
                catalog.knight = removedSprite;
                catalog.ranger = removedSprite;
                var removed = new Dictionary<string, string[]>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    { "actor-knight", new[] { removedSource } },
                    {
                        "actor-ranger",
                        new[]
                        {
                            "Assets/_Project/Art/Source/Aseprite/actor-ranger.aseprite"
                        }
                    }
                };

                int changed = ProjectCAsepritePipeline.SynchronizeSpriteSlots(
                    catalog,
                    System.Array.Empty<string>(),
                    removed,
                    out int bound);

                Assert.AreEqual(0, bound);
                Assert.AreEqual(1, changed);
                Assert.AreSame(fallback, catalog.knight);
                Assert.AreSame(
                    removedSprite,
                    catalog.ranger,
                    "다른 SourceRoot Aseprite를 수동 참조한 슬롯은 지우면 안 된다");
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void SynchronizeSpriteSlots_DeletedMissingReferenceRestoresEnvironmentAndRuntimePngs()
        {
            const string floorSource =
                "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite";
            const string slingerSource =
                "Assets/_Project/Art/Source/Aseprite/actor-slinger.aseprite";
            Sprite floorFallback = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Environment/env-floor.png");
            Sprite slingerFallback = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Runtime/actor-slinger.png");
            Assert.IsNotNull(floorFallback);
            Assert.IsNotNull(slingerFallback);

            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            try
            {
                catalog.floor = null; // 삭제된 subasset은 SerializedProperty에서 null처럼 보인다.
                catalog.slinger = null;
                var removed = new Dictionary<string, string[]>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    { "env-floor", new[] { floorSource, string.Empty } },
                    { "actor-slinger", new[] { slingerSource, string.Empty } }
                };

                int changed = ProjectCAsepritePipeline.SynchronizeSpriteSlots(
                    catalog,
                    System.Array.Empty<string>(),
                    removed,
                    out int bound);

                Assert.AreEqual(0, bound);
                Assert.AreEqual(2, changed);
                Assert.AreSame(floorFallback, catalog.floor);
                Assert.AreSame(slingerFallback, catalog.slinger);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void SynchronizeSpriteSlots_ExistingSourceWinsOverRemovalFallback()
        {
            const string source =
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite";
            Sprite sourceSprite = ProjectCAsepritePipeline.SelectFirstFrame(
                AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>());
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Runtime/actor-knight.png");
            Assert.IsNotNull(sourceSprite);
            Assert.IsNotNull(fallback);

            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            try
            {
                catalog.knight = fallback;
                var removed = new Dictionary<string, string[]>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    { "actor-knight", new[] { source } }
                };

                int changed = ProjectCAsepritePipeline.SynchronizeSpriteSlots(
                    catalog,
                    new[] { source },
                    removed,
                    out int bound);

                Assert.AreEqual(1, bound);
                Assert.AreEqual(1, changed);
                Assert.AreSame(sourceSprite, catalog.knight);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TagFromClipName_AcceptsExactAndSuffixForms_RejectsUnknown()
        {
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("idle"));
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("Idle"));
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("actor-knight_idle"));
            Assert.AreEqual("death", ActorAnimationBake.TagFromClipName("actor-slime_Death"));
            Assert.AreEqual("idle-north", ActorAnimationBake.TagFromClipName("idle-north"));
            Assert.AreEqual(
                "attack-west",
                ActorAnimationBake.TagFromClipName("actor-knight_attack-west"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName("sprint"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName("idle_extra"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName("idle-up"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName(null));
        }

        [Test]
        public void DirectionalActorTags_RequireEveryStateAndFacingOnceOptedIn()
        {
            Assert.IsEmpty(ProjectCAsepritePipeline.MissingDirectionalActorTags(
                new[] { "idle", "walk", "attack", "hit", "fall", "death" }));

            string[] complete =
                new[] { "idle", "walk", "attack", "hit", "fall", "death" }
                    .SelectMany(baseTag =>
                        new[] { "north", "east", "south", "west" }
                            .Select(facing => $"actor-test_{baseTag}-{facing}"))
                    .ToArray();
            Assert.IsEmpty(ProjectCAsepritePipeline.MissingDirectionalActorTags(complete));

            string[] missing = ProjectCAsepritePipeline.MissingDirectionalActorTags(
                new[] { "idle-north" });
            Assert.AreEqual(23, missing.Length);
            CollectionAssert.Contains(missing, "idle-east");
            CollectionAssert.Contains(missing, "attack-north");
            CollectionAssert.Contains(missing, "death-west");
        }

        [Test]
        public void ExtractClip_BakesSpriteCurve_FramesTimesLoopLength()
        {
            Sprite first = MakeSprite("actor-test_0");
            Sprite second = MakeSprite("actor-test_1");
            var clip = new AnimationClip { name = "actor-test_idle" };
            var binding = UnityEditor.EditorCurveBinding.PPtrCurve(
                string.Empty, typeof(SpriteRenderer), "m_Sprite");
            UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
            {
                new UnityEditor.ObjectReferenceKeyframe { time = 0f, value = first },
                new UnityEditor.ObjectReferenceKeyframe { time = 0.1f, value = second }
            });
            var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, settings);

            SpriteClip baked = ActorAnimationBake.ExtractClip(clip);

            Assert.IsNotNull(baked);
            Assert.AreEqual("idle", baked.tag);
            Assert.IsTrue(baked.loop);
            CollectionAssert.AreEqual(new[] { first, second }, baked.frames);
            CollectionAssert.AreEqual(new[] { 0f, 0.1f }, baked.frameStartTimes);
            Assert.AreEqual(clip.length, baked.length);
            Assert.IsTrue(baked.IsPlayable);

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ExtractClip_AsepriteTerminalHoldKey_IsNotBakedAsAFrame()
        {
            Sprite first = MakeSprite("actor-loop_0");
            Sprite second = MakeSprite("actor-loop_1");
            var clip = new AnimationClip { name = "attack" };
            var binding = UnityEditor.EditorCurveBinding.PPtrCurve(
                string.Empty, typeof(SpriteRenderer), "m_Sprite");
            UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
            {
                new UnityEditor.ObjectReferenceKeyframe { time = 0f, value = first },
                new UnityEditor.ObjectReferenceKeyframe { time = 0.1f, value = second },
                new UnityEditor.ObjectReferenceKeyframe { time = 0.2f, value = second }
            });

            SpriteClip baked = ActorAnimationBake.ExtractClip(clip);

            CollectionAssert.AreEqual(new[] { first, second }, baked.frames);
            CollectionAssert.AreEqual(new[] { 0f, 0.1f }, baked.frameStartTimes);
            Assert.IsFalse(baked.loop);

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ExtractClip_NoSpriteCurveOrUnknownTag_ReturnsNull()
        {
            var untagged = new AnimationClip { name = "sprint" };
            Assert.IsNull(ActorAnimationBake.ExtractClip(untagged));

            var noCurve = new AnimationClip { name = "idle" };
            Assert.IsNull(ActorAnimationBake.ExtractClip(noCurve), "sprite 커브가 없으면 굽지 않는다");

            Object.DestroyImmediate(untagged);
            Object.DestroyImmediate(noCurve);
        }

        [Test]
        public void KnightSource_ApprovedDirectionalTimeline_IsComplete()
        {
            const string source =
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite";
            AssetImporter importer = AssetImporter.GetAtPath(source);
            Assert.IsNotNull(importer);
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty canvas = serializedImporter.FindProperty("m_CanvasSize");
            Assert.IsNotNull(canvas);
            Assert.AreEqual(new Vector2Int(96, 128), canvas.vector2IntValue);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(source);
            string[] clipNames = assets.OfType<AnimationClip>()
                .Select(clip => clip.name)
                .ToArray();
            CollectionAssert.IsEmpty(
                ProjectCAsepritePipeline.MissingRequiredActorTags(clipNames));
            CollectionAssert.IsEmpty(
                ProjectCAsepritePipeline.MissingDirectionalActorTags(clipNames));

            Sprite[] frames = assets.OfType<Sprite>().ToArray();
            Assert.AreEqual(81, frames.Length);
            Assert.IsTrue(frames.Any(frame => frame.name == "Frame_0"));

            ActorAnimationSet set = ActorAnimationBake.ExtractSet(source, "knight");
            Assert.AreEqual(24, set.clips.Count);
            Assert.IsTrue(set.HasDirectionalClips);
            foreach (string state in new[]
                     {
                         SpriteClipTags.Idle,
                         SpriteClipTags.Walk,
                         SpriteClipTags.Attack,
                         SpriteClipTags.Hit,
                         SpriteClipTags.Fall,
                         SpriteClipTags.Death
                     })
            {
                foreach (ActorFacing4 facing in new[]
                         {
                             ActorFacing4.North,
                             ActorFacing4.East,
                             ActorFacing4.South,
                             ActorFacing4.West
                         })
                {
                    SpriteClip clip = set.Find(state, facing);
                    Assert.IsNotNull(clip, $"{state}-{facing}");
                    Assert.IsTrue(clip.IsPlayable, $"{state}-{facing}");
                    int expectedFrames =
                        state == SpriteClipTags.Idle ? 4 :
                        state == SpriteClipTags.Walk ? 3 :
                        state == SpriteClipTags.Fall ? 2 :
                        state == SpriteClipTags.Death ? 5 : 3;
                    Assert.AreEqual(expectedFrames, clip.frames.Length, $"{state}-{facing}");
                    Assert.AreEqual(
                        state == SpriteClipTags.Idle || state == SpriteClipTags.Walk,
                        clip.loop,
                        $"{state}-{facing}");
                }
            }

            Assert.IsTrue(IsoPrototypeDemo.SurvivorAnimationApproved);
            Assert.IsTrue(IsoPrototypeDemo.ShouldAttachSurvivorAnimator(set));
        }

        [Test]
        public void ArcadeEnemySources_ApprovedDirectionalTimelines_AreComplete()
        {
            var expected = new (string assetName, string actorKey)[]
            {
                ("actor-goblin", "goblin"),
                ("actor-skeleton", "skeleton"),
                ("actor-slime", "slime"),
                ("actor-slinger", "slinger"),
                ("actor-arc-drone", "arcDrone"),
                ("actor-grave-warden", "graveWarden")
            };

            foreach ((string assetName, string actorKey) in expected)
            {
                string source =
                    $"Assets/_Project/Art/Source/Aseprite/{assetName}.aseprite";
                AssetImporter importer = AssetImporter.GetAtPath(source);
                Assert.IsNotNull(importer, source);
                var serializedImporter = new SerializedObject(importer);
                SerializedProperty canvas = serializedImporter.FindProperty("m_CanvasSize");
                Assert.IsNotNull(canvas, source);
                Assert.AreEqual(new Vector2Int(96, 128), canvas.vector2IntValue, source);

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(source);
                string[] clipNames = assets.OfType<AnimationClip>()
                    .Select(clip => clip.name)
                    .ToArray();
                CollectionAssert.IsEmpty(
                    ProjectCAsepritePipeline.MissingRequiredActorTags(clipNames),
                    source);
                CollectionAssert.IsEmpty(
                    ProjectCAsepritePipeline.MissingDirectionalActorTags(clipNames),
                    source);
                Assert.AreEqual(81, assets.OfType<Sprite>().Count(), source);
                AssertSpriteRectIsTight(
                    assets.OfType<Sprite>().Single(frame => frame.name == "Frame_22"),
                    source + " walk-east-02");

                ActorAnimationSet set = ActorAnimationBake.ExtractSet(source, actorKey);
                Assert.IsNotNull(set, source);
                Assert.AreEqual(24, set.clips.Count, source);
                Assert.IsTrue(set.HasDirectionalClips, source);
                foreach (string state in new[]
                         {
                             SpriteClipTags.Idle,
                             SpriteClipTags.Walk,
                             SpriteClipTags.Attack,
                             SpriteClipTags.Hit,
                             SpriteClipTags.Fall,
                             SpriteClipTags.Death
                         })
                {
                    foreach (ActorFacing4 facing in new[]
                             {
                                 ActorFacing4.North,
                                 ActorFacing4.East,
                                 ActorFacing4.South,
                                 ActorFacing4.West
                             })
                    {
                        SpriteClip clip = set.Find(state, facing);
                        Assert.IsNotNull(clip, $"{source} {state}-{facing}");
                        Assert.IsTrue(clip.IsPlayable, $"{source} {state}-{facing}");
                        int expectedFrames =
                            state == SpriteClipTags.Idle ? 4 :
                            state == SpriteClipTags.Walk ? 3 :
                            state == SpriteClipTags.Fall ? 2 :
                            state == SpriteClipTags.Death ? 5 : 3;
                        Assert.AreEqual(
                            expectedFrames,
                            clip.frames.Length,
                            $"{source} {state}-{facing}");
                        Assert.AreEqual(
                            state == SpriteClipTags.Idle || state == SpriteClipTags.Walk,
                            clip.loop,
                            $"{source} {state}-{facing}");
                        if (state == SpriteClipTags.Fall)
                            Assert.GreaterOrEqual(
                                IsoPrototypeDemo.EnemyFallPresentationDuration,
                                clip.length,
                                $"{source} {state}-{facing} must finish before world fall");
                    }
                }
            }
        }

        [Test]
        public void ArcadeEnemyCatalog_BindsEveryCanonicalFrameAndDirectionalSet()
        {
            const string catalogPath =
                "Assets/_Project/Art/Environment/ProjectCEnvironmentCatalog.asset";
            IsoVisualCatalog catalog =
                AssetDatabase.LoadAssetAtPath<IsoVisualCatalog>(catalogPath);
            Assert.IsNotNull(catalog, catalogPath);

            var expected = new (string id, string assetName, string actorKey)[]
            {
                ("Goblin", "actor-goblin", "goblin"),
                ("Skeleton", "actor-skeleton", "skeleton"),
                ("Slime", "actor-slime", "slime"),
                ("Slinger", "actor-slinger", "slinger"),
                ("ArcDrone", "actor-arc-drone", "arcDrone"),
                ("GraveWarden", "actor-grave-warden", "graveWarden")
            };

            foreach ((string id, string assetName, string actorKey) in expected)
            {
                string source =
                    $"Assets/_Project/Art/Source/Aseprite/{assetName}.aseprite";
                Sprite frame0 = AssetDatabase.LoadAllAssetsAtPath(source)
                    .OfType<Sprite>()
                    .Single(frame => frame.name == "Frame_0");
                Assert.AreSame(frame0, catalog.MonsterFor(id),
                    $"{id} 카탈로그 슬롯이 정식 {assetName} Frame_0에서 이탈했다");

                ActorAnimationSet set = catalog.MonsterAnimationsFor(id);
                Assert.IsNotNull(set, $"{id} 방향 애니메이션 세트가 카탈로그에 없다");
                Assert.AreEqual(actorKey, set.actorKey, id);
                Assert.AreEqual(24, set.clips.Count, id);
                Assert.IsTrue(set.HasDirectionalClips, id);
            }
        }

        [Test]
        public void SurvivorApprovedAnimation_AttachesPlayableSetsOnly()
        {
            var approved = new ActorAnimationSet();
            approved.clips.Add(new SpriteClip { tag = SpriteClipTags.Idle });

            Assert.IsTrue(IsoPrototypeDemo.SurvivorAnimationApproved);
            Assert.IsTrue(IsoPrototypeDemo.ShouldAttachSurvivorAnimator(approved));
            Assert.IsFalse(IsoPrototypeDemo.ShouldAttachSurvivorAnimator(null));
        }

        [Test]
        public void ActorAnimationSet_DirectionalLookup_PrefersFacingThenFallsBackToBase()
        {
            var idle = new SpriteClip { tag = SpriteClipTags.Idle };
            var north = new SpriteClip { tag = "idle-north" };
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip> { idle, north }
            };

            Assert.IsTrue(set.HasDirectionalClips);
            Assert.AreSame(north, set.Find(SpriteClipTags.Idle, ActorFacing4.North));
            Assert.AreSame(idle, set.Find(SpriteClipTags.Idle, ActorFacing4.East));
            Assert.IsNull(set.Find(SpriteClipTags.Attack, ActorFacing4.South));
        }

        [Test]
        public void SetsEqual_DetectsFrameAndTagChanges()
        {
            Sprite frame = MakeSprite("actor-eq_0");
            System.Collections.Generic.List<ActorAnimationSet> Make(string tag) =>
                new System.Collections.Generic.List<ActorAnimationSet>
                {
                    new ActorAnimationSet
                    {
                        actorKey = "goblin",
                        clips = new System.Collections.Generic.List<SpriteClip>
                        {
                            new SpriteClip
                            {
                                tag = tag,
                                loop = true,
                                frames = new[] { frame },
                                frameStartTimes = new[] { 0f },
                                length = 0.1f
                            }
                        }
                    }
                };

            Assert.IsTrue(ActorAnimationBake.SetsEqual(Make("idle"), Make("idle")));
            Assert.IsFalse(ActorAnimationBake.SetsEqual(Make("idle"), Make("walk")));
            Assert.IsFalse(ActorAnimationBake.SetsEqual(
                Make("idle"), new System.Collections.Generic.List<ActorAnimationSet>()));
        }

        [Test]
        public void EnvironmentSetsEqual_DetectsSlotAndFrameChanges()
        {
            Sprite frame = MakeSprite("prop-campfire_0");
            System.Collections.Generic.List<EnvironmentAnimationSet> Make(
                string slot,
                string tag) =>
                new System.Collections.Generic.List<EnvironmentAnimationSet>
                {
                    new EnvironmentAnimationSet
                    {
                        slotKey = slot,
                        clips = new System.Collections.Generic.List<SpriteClip>
                        {
                            new SpriteClip
                            {
                                tag = tag,
                                loop = true,
                                frames = new[] { frame },
                                frameStartTimes = new[] { 0f },
                                length = 0.1f
                            }
                        }
                    }
                };

            Assert.IsTrue(ActorAnimationBake.EnvironmentSetsEqual(
                Make("hubCampfire", "idle"),
                Make("hubCampfire", "idle")));
            Assert.IsFalse(ActorAnimationBake.EnvironmentSetsEqual(
                Make("hubCampfire", "idle"),
                Make("hubPortal", "idle")));
            Assert.IsFalse(ActorAnimationBake.EnvironmentSetsEqual(
                Make("hubCampfire", "idle"),
                Make("hubCampfire", "walk")));
        }

        private Sprite MakeSprite(string name)
        {
            Sprite sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = name;
            _sprites.Add(sprite);
            return sprite;
        }

        private static void AssertSpriteRectIsTight(Sprite sprite, string context)
        {
            Assert.IsNotNull(sprite, context);
            Rect rect = sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            Assert.Greater(width, 0, context);
            Assert.Greater(height, 0, context);

            RenderTexture previous = RenderTexture.active;
            RenderTexture atlasCopy = RenderTexture.GetTemporary(
                sprite.texture.width,
                sprite.texture.height,
                0,
                RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                atlasCopy.filterMode = FilterMode.Point;
                Graphics.Blit(sprite.texture, atlasCopy);
                RenderTexture.active = atlasCopy;
                pixels.ReadPixels(rect, 0, 0);
                pixels.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                Color32[] colors = pixels.GetPixels32();
                bool left = false;
                bool right = false;
                bool bottom = false;
                bool top = false;
                for (int y = 0; y < height; y++)
                {
                    left |= colors[y * width].a > 0;
                    right |= colors[y * width + width - 1].a > 0;
                }
                for (int x = 0; x < width; x++)
                {
                    bottom |= colors[x].a > 0;
                    top |= colors[(height - 1) * width + x].a > 0;
                }

                Assert.IsTrue(
                    left && right && bottom && top,
                    $"{context}: Sprite rect must tightly match the imported alpha " +
                    $"bounds (left={left}, right={right}, bottom={bottom}, top={top})");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(atlasCopy);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
