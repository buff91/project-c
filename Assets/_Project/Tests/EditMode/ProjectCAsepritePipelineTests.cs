using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.EditorTools;
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
        public void CatalogSlot_MapsCanonicalAssetNames()
        {
            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/env-floor.aseprite",
                out string floorSlot));
            Assert.AreEqual("floor", floorSlot);

            Assert.IsTrue(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/Actors/actor-merchant.aseprite",
                out string merchantSlot));
            Assert.AreEqual("merchant", merchantSlot);

            Assert.IsFalse(ProjectCAsepritePipeline.TryGetCatalogSlot(
                "Assets/_Project/Art/Source/Aseprite/unknown.aseprite", out _));
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
        public void ResolvePivot_UsesStableCanvasAnchors()
        {
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
    }
}
