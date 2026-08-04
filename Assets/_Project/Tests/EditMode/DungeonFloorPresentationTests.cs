using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests
{
    public class DungeonFloorPresentationTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _created)
                Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void BandDetail_IsDeterministicSparseAndNeverTouchesOrthogonalNeighbor()
        {
            var height = new DungeonHeightModel(4);
            foreach (int progress in new[] { 3, 6, 9 })
            {
                DungeonVisualContext context = DungeonVisualContext.From(
                    height,
                    height.Elevation(0),
                    progress);
                var selected = new HashSet<GridPos>();
                const int side = 64;
                for (int y = -side / 2; y < side / 2; y++)
                for (int x = -side / 2; x < side / 2; x++)
                {
                    var pos = new GridPos(x, y, context.Elevation);
                    bool first = DungeonFloorPresentation.ShouldUseBandDetail(pos, context);
                    bool second = DungeonFloorPresentation.ShouldUseBandDetail(pos, context);
                    Assert.AreEqual(first, second, $"determinism at {pos}");
                    if (first) selected.Add(pos);
                }

                float density = selected.Count / (float)(side * side);
                Assert.That(density, Is.InRange(0.07f, 0.15f),
                    $"progress {progress} detail density");
                foreach (GridPos pos in selected)
                {
                    Assert.IsFalse(selected.Contains(
                        new GridPos(pos.x + 1, pos.y, pos.elevation)));
                    Assert.IsFalse(selected.Contains(
                        new GridPos(pos.x, pos.y + 1, pos.elevation)));
                }
            }
        }

        [Test]
        public void BandDetail_ShallowBandNeverUsesDamageSprite()
        {
            DungeonVisualContext context = DungeonVisualContext.Preview(progressIndex: 0);
            for (int y = -16; y <= 16; y++)
            for (int x = -16; x <= 16; x++)
            {
                Assert.IsFalse(DungeonFloorPresentation.ShouldUseBandDetail(
                    new GridPos(x, y, 0),
                    context));
            }
        }

        [Test]
        public void Catalog_UsesBandSpriteOnlyForExplicitDetailAndKeepsFallbackContract()
        {
            IsoVisualCatalog catalog = CreateCatalog();
            Sprite shared = MakeSprite();
            Sprite deep = MakeSprite();
            catalog.floor = shared;
            catalog.deepFloor = deep;
            DungeonVisualContext context = DungeonVisualContext.Preview(progressIndex: 6);

            Assert.AreSame(
                shared,
                catalog.TileFor(TileKind.Floor, context, useBandFloorDetail: false));
            Assert.AreSame(
                deep,
                catalog.TileFor(TileKind.Floor, context, useBandFloorDetail: true));
            Assert.IsFalse(catalog.BandFloorFallsBackToShared(context));

            catalog.deepFloor = null;
            Assert.AreSame(
                shared,
                catalog.TileFor(TileKind.Floor, context, useBandFloorDetail: true));
            Assert.IsTrue(catalog.BandFloorFallsBackToShared(context));
        }

        [Test]
        public void FacilityDressing_UsesThreeOfThirtyTwoStableSlots()
        {
            IsoVisualCatalog catalog = CreateCatalog();
            Sprite grate = MakeSprite();
            Sprite cracked = MakeSprite();
            Sprite service = MakeSprite();
            catalog.hospitalFloorGrate = grate;
            catalog.hospitalFloorCracked = cracked;
            catalog.hospitalFloorService = service;

            int decorated = 0;
            for (int variation = 0;
                 variation < DungeonFloorPresentation.SurfaceVariationCount;
                 variation++)
            {
                if (catalog.HospitalFloorFor(variation) != null) decorated++;
            }

            Assert.AreEqual(3, decorated);
            Assert.AreSame(grate, catalog.HospitalFloorFor(0));
            Assert.AreSame(cracked, catalog.HospitalFloorFor(11));
            Assert.AreSame(service, catalog.HospitalFloorFor(23));
            Assert.IsNull(catalog.HospitalFloorFor(3));
            Assert.AreSame(grate, catalog.HospitalFloorFor(32));
            Assert.AreSame(service, catalog.HospitalFloorFor(-9));

            DungeonVisualContext context = DungeonVisualContext.Preview(progressIndex: 6);
            int placed = 0;
            const int side = 64;
            for (int y = -side / 2; y < side / 2; y++)
            for (int x = -side / 2; x < side / 2; x++)
            {
                var pos = new GridPos(x, y, 0);
                int first = DungeonFloorPresentation.SurfaceVariation(pos, context);
                int second = DungeonFloorPresentation.SurfaceVariation(pos, context);
                Assert.AreEqual(first, second, $"determinism at {pos}");
                if (catalog.HospitalFloorFor(first) != null) placed++;
            }
            Assert.That(placed / (float)(side * side), Is.InRange(0.07f, 0.12f));
        }

        [Test]
        public void CheckedInBandFloorTextures_AreReadableCanonicalSprites()
        {
            string[] names =
            {
                "env-floor-mid",
                "env-floor-deep",
                "env-floor-boss",
                "env-floor-mid-raised",
                "env-floor-deep-raised",
                "env-floor-boss-raised",
            };

            foreach (string name in names)
            {
                string path = $"Assets/_Project/Art/Environment/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.IsTrue(importer.isReadable,
                    $"{path} must stay readable for runtime tone mapping");

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(128f, sprite.rect.width, $"{path} width");
                Assert.AreEqual(64f, sprite.rect.height, $"{path} height");
                Assert.AreEqual(128f, sprite.pixelsPerUnit, $"{path} PPU");
                Assert.That(sprite.pivot.x / sprite.rect.width,
                    Is.EqualTo(0.5f).Within(0.0001f), $"{path} pivot x");
                Assert.That(sprite.pivot.y / sprite.rect.height,
                    Is.EqualTo(0.5f).Within(0.0001f), $"{path} pivot y");
            }
        }

        private IsoVisualCatalog CreateCatalog()
        {
            IsoVisualCatalog catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            _created.Add(catalog);
            return catalog;
        }

        private Sprite MakeSprite()
        {
            var texture = new Texture2D(1, 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
            _created.Add(sprite);
            _created.Add(texture);
            return sprite;
        }
    }
}
