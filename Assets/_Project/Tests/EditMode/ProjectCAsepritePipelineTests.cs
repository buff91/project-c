using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.EditorTools;
using ProjectC.Gameplay;
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
        public void TagFromClipName_AcceptsExactAndSuffixForms_RejectsUnknown()
        {
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("idle"));
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("Idle"));
            Assert.AreEqual("idle", ActorAnimationBake.TagFromClipName("actor-knight_idle"));
            Assert.AreEqual("death", ActorAnimationBake.TagFromClipName("actor-slime_Death"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName("sprint"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName("idle_extra"));
            Assert.IsNull(ActorAnimationBake.TagFromClipName(null));
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
        public void KnightSource_BakesSixTagsWithoutSwallowingLaterRanges()
        {
            const string source =
                "Assets/_Project/Art/Source/Aseprite/actor-knight.aseprite";
            ActorAnimationSet set = ActorAnimationBake.ExtractSet(source, "knight");

            // 기대값은 현재 원본의 스냅샷이다 — v0.3.3 치비 라이더 교체(2026-07-30) 기준.
            // idle 1프레임, walk 는 contact-a/pass/contact-b/pass 4셀(pass 는 픽셀 동일해도
            // 셀이 분리돼 개별 Sprite 로 구워진다). 각 수가 "그 태그의 프레임만" 세는 것이
            // 태그 삼킴 회귀의 방어선이다.
            Assert.AreEqual(6, set.clips.Count);
            AssertClip("idle", true, 1);
            AssertClip("walk", true, 4);
            AssertClip("attack", false, 3);
            AssertClip("hit", false, 1);
            AssertClip("fall", false, 1);
            AssertClip("death", false, 1);

            void AssertClip(string tag, bool loop, int distinctFrames)
            {
                SpriteClip clip = set.Find(tag);
                Assert.IsNotNull(clip, tag);
                Assert.AreEqual(loop, clip.loop, tag);
                Assert.AreEqual(
                    distinctFrames,
                    clip.frames.Where(frame => frame != null).Distinct().Count(),
                    tag);
            }
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
    }
}
