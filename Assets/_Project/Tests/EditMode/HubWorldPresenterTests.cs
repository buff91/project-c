using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public sealed class HubWorldPresenterTests
    {
        [Test]
        public void Present_ClosedFacilitiesBuildsStableBaseWorldAndLightPatches()
        {
            var map = new GridMap();
            HubLayout.Build(map);
            var iso = new IsoGrid(1f, 0.5f, 0.25f);
            var rootObject = new GameObject("Hub Presenter Test Root");
            var lightRequests = new List<LightRequest>();
            var animationOwners = new List<string>();
            var registry = new HubWorldRegistry();

            try
            {
                HubWorldVisuals visuals = CreateNullVisuals(lightRequests);
                var context = new HubWorldPresentationContext(
                    map,
                    iso,
                    rootObject.transform,
                    pos => new Vector3(pos.x + 10f, pos.y + 20f, pos.elevation),
                    (owner, _, __) => animationOwners.Add(owner.name));

                new HubWorldPresenter().Present(
                    new HubFacilitySnapshot(forgeOpen: false, bountyBoardOpen: false),
                    context,
                    visuals,
                    registry);

                AssertProp(rootObject.transform, "Campfire", HubLayout.Campfire, iso);
                AssertProp(rootObject.transform, "Portal", HubLayout.Portal, iso);
                AssertProp(rootObject.transform, "Merchant", HubLayout.Merchant, iso);
                AssertProp(rootObject.transform, "Stash", HubLayout.Stash, iso);
                AssertProp(rootObject.transform, "Codex", HubLayout.Codex, iso);
                Assert.IsNull(rootObject.transform.Find("Smith"));
                Assert.IsNull(rootObject.transform.Find("BountyBoard"));

                AssertInteraction(registry, HubLayout.Merchant, "merchant", "상인");
                AssertInteraction(registry, HubLayout.Stash, "stash", "창고");
                AssertInteraction(registry, HubLayout.Codex, "codex", "기록실");
                Assert.IsFalse(registry.TryGetInteraction(HubLayout.Campfire, out _));
                Assert.IsFalse(registry.TryGetInteraction(HubLayout.Portal, out _));

                Assert.AreEqual(13, CountLights(lightRequests, "campfire"));
                Assert.AreEqual(1, CountLights(lightRequests, "campfire", 3));
                Assert.AreEqual(4, CountLights(lightRequests, "campfire", 2));
                Assert.AreEqual(8, CountLights(lightRequests, "campfire", 1));
                Assert.AreEqual(4, CountLights(lightRequests, "portal"));
                Assert.AreEqual(1, CountLights(lightRequests, "portal", 3));
                Assert.AreEqual(3, CountLights(lightRequests, "portal", 2));
                Assert.AreEqual(0, CountLights(lightRequests, "portal", 1));

                Transform campfireLight = rootObject.transform.Find("campfire Light 6,4");
                Assert.NotNull(campfireLight);
                Assert.AreEqual(
                    new Vector3(16f, 24f, 0f),
                    campfireLight.position);
                Assert.AreEqual(
                    iso.SortingOrder(HubLayout.Campfire, -1),
                    campfireLight.GetComponent<SpriteRenderer>().sortingOrder);
                CollectionAssert.AreEquivalent(
                    new[] { "Campfire", "Portal" },
                    animationOwners);
                Assert.AreEqual(22, rootObject.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Present_FacilitySnapshotAddsMatchingPropAndInteractionTogether(
            bool forgeOpen,
            bool bountyBoardOpen)
        {
            var map = new GridMap();
            HubLayout.Build(map);
            var rootObject = new GameObject("Hub Facility Test Root");
            var registry = new HubWorldRegistry();

            try
            {
                var context = new HubWorldPresentationContext(
                    map,
                    new IsoGrid(),
                    rootObject.transform,
                    _ => Vector3.zero,
                    (_, __, ___) => { });
                new HubWorldPresenter().Present(
                    new HubFacilitySnapshot(forgeOpen, bountyBoardOpen),
                    context,
                    CreateNullVisuals(new List<LightRequest>()),
                    registry);

                Assert.AreEqual(
                    forgeOpen,
                    rootObject.transform.Find("Smith") != null);
                Assert.AreEqual(
                    bountyBoardOpen,
                    rootObject.transform.Find("BountyBoard") != null);

                bool hasForgeInteraction = registry.TryGetInteraction(
                    HubLayout.Smith,
                    out HubInteractionTarget forgeTarget);
                Assert.AreEqual(forgeOpen, hasForgeInteraction);
                if (forgeOpen)
                {
                    Assert.AreEqual("smith", forgeTarget.Id);
                    Assert.AreEqual("대장간", forgeTarget.Label);
                }

                bool hasBountyInteraction = registry.TryGetInteraction(
                    HubLayout.BountyBoard,
                    out HubInteractionTarget bountyTarget);
                Assert.AreEqual(bountyBoardOpen, hasBountyInteraction);
                if (bountyBoardOpen)
                {
                    Assert.AreEqual("bounty", bountyTarget.Id);
                    Assert.AreEqual("의뢰 게시판", bountyTarget.Label);
                }
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Present_IgnoresBlockedAndOtherElevationTilesInLightPatches()
        {
            var map = new GridMap();
            map.Set(HubLayout.Campfire, TileKind.Floor);
            map.Set(HubLayout.Campfire.East, TileKind.Floor);
            map.Set(HubLayout.Campfire.Offset(2, 0), TileKind.WeakFloor);
            map.Set(HubLayout.Campfire.West, TileKind.Wall);
            map.Set(HubLayout.Campfire.WithElevation(1), TileKind.Floor);
            map.Set(HubLayout.Campfire.Offset(3, 0), TileKind.Floor);
            var rootObject = new GameObject("Hub Light Filter Test Root");
            var requests = new List<LightRequest>();

            try
            {
                var context = new HubWorldPresentationContext(
                    map,
                    new IsoGrid(),
                    rootObject.transform,
                    _ => Vector3.zero,
                    (_, __, ___) => { });
                new HubWorldPresenter().Present(
                    new HubFacilitySnapshot(false, false),
                    context,
                    CreateNullVisuals(requests),
                    new HubWorldRegistry());

                Assert.AreEqual(3, CountLights(requests, "campfire"));
                Assert.AreEqual(0, CountLights(requests, "portal"));
                Assert.NotNull(rootObject.transform.Find("campfire Light 6,4"));
                Assert.NotNull(rootObject.transform.Find("campfire Light 7,4"));
                Assert.NotNull(rootObject.transform.Find("campfire Light 8,4"));
                Assert.IsNull(rootObject.transform.Find("campfire Light 5,4"));
                Assert.AreEqual(8, rootObject.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Visuals_PrefersCatalogAndKeepsExactFallbackMappings()
        {
            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            var texture = new Texture2D(8, 8);
            var createdSprites = new List<Sprite>();
            var fallbackRequests = new List<string>();
            var characterRequests = new List<bool>();
            var lightRequests = new List<LightRequest>();

            try
            {
                catalog.hubCampfire = CreateSprite(texture, "Catalog Campfire", createdSprites);
                catalog.hubPortal = CreateSprite(texture, "Catalog Portal", createdSprites);
                catalog.merchant = CreateSprite(texture, "Catalog Merchant", createdSprites);
                catalog.hubStash = CreateSprite(texture, "Catalog Stash", createdSprites);
                Sprite fallback = CreateSprite(texture, "Fallback", createdSprites);
                Sprite character = CreateSprite(texture, "Character Fallback", createdSprites);
                Sprite light = CreateSprite(texture, "Light Fallback", createdSprites);
                var visuals = new HubWorldVisuals(
                    catalog,
                    key =>
                    {
                        fallbackRequests.Add(key);
                        return fallback;
                    },
                    enemy =>
                    {
                        characterRequests.Add(enemy);
                        return character;
                    },
                    (kind, strength) =>
                    {
                        lightRequests.Add(new LightRequest(kind, strength));
                        return light;
                    });

                Assert.AreSame(catalog.hubCampfire, visuals.GetPropSprite(HubPropKind.Campfire));
                Assert.AreSame(catalog.hubPortal, visuals.GetPropSprite(HubPropKind.Portal));
                Assert.AreSame(catalog.merchant, visuals.GetPropSprite(HubPropKind.Merchant));
                Assert.AreSame(catalog.hubStash, visuals.GetPropSprite(HubPropKind.Stash));
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.Smith));
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.BountyBoard));
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.Codex));
                CollectionAssert.AreEqual(
                    new[] { "smith", "bounty", "codex" },
                    fallbackRequests);
                Assert.IsEmpty(characterRequests);

                catalog.hubCampfire = null;
                catalog.hubPortal = null;
                catalog.hubStash = null;
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.Campfire));
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.Portal));
                Assert.AreSame(fallback, visuals.GetPropSprite(HubPropKind.Stash));
                CollectionAssert.AreEqual(
                    new[] { "smith", "bounty", "codex", "campfire", "portal", "stash" },
                    fallbackRequests);

                catalog.merchant = null;
                Assert.AreSame(character, visuals.GetPropSprite(HubPropKind.Merchant));
                CollectionAssert.AreEqual(new[] { true }, characterRequests);
                Assert.AreSame(light, visuals.GetLightSprite("portal", 2));
                Assert.AreEqual(1, lightRequests.Count);
                Assert.AreEqual("portal", lightRequests[0].Kind);
                Assert.AreEqual(2, lightRequests[0].Strength);
            }
            finally
            {
                foreach (Sprite sprite in createdSprites)
                    Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Present_PassesOnlyCampfireAndPortalAnimationSets()
        {
            var map = new GridMap();
            HubLayout.Build(map);
            var catalog = ScriptableObject.CreateInstance<IsoVisualCatalog>();
            var campfireSet = CreateAnimationSet("hubCampfire");
            var portalSet = CreateAnimationSet("hubPortal");
            catalog.environmentAnimations = new List<EnvironmentAnimationSet>
            {
                campfireSet,
                portalSet
            };
            var rootObject = new GameObject("Hub Animation Test Root");
            var attached = new Dictionary<string, EnvironmentAnimationSet>();

            try
            {
                var context = new HubWorldPresentationContext(
                    map,
                    new IsoGrid(),
                    rootObject.transform,
                    _ => Vector3.zero,
                    (owner, _, set) => attached.Add(owner.name, set));
                var visuals = new HubWorldVisuals(
                    catalog,
                    _ => null,
                    _ => null,
                    (_, __) => null);

                new HubWorldPresenter().Present(
                    new HubFacilitySnapshot(true, true),
                    context,
                    visuals,
                    new HubWorldRegistry());

                Assert.AreEqual(2, attached.Count);
                Assert.AreSame(campfireSet, attached["Campfire"]);
                Assert.AreSame(portalSet, attached["Portal"]);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(catalog);
            }
        }

        private static HubWorldVisuals CreateNullVisuals(List<LightRequest> requests) =>
            new HubWorldVisuals(
                null,
                _ => null,
                _ => null,
                (kind, strength) =>
                {
                    requests.Add(new LightRequest(kind, strength));
                    return null;
                });

        private static void AssertProp(
            Transform root,
            string name,
            GridPos position,
            IsoGrid iso)
        {
            Transform prop = root.Find(name);
            Assert.NotNull(prop);
            Assert.AreEqual(
                new Vector3(position.x + 10f, position.y + 20f, position.elevation),
                prop.position);
            Assert.AreEqual(
                iso.SortingOrder(position, 1),
                prop.GetComponent<SpriteRenderer>().sortingOrder);
        }

        private static void AssertInteraction(
            HubWorldRegistry registry,
            GridPos position,
            string expectedId,
            string expectedLabel)
        {
            Assert.IsTrue(registry.TryGetInteraction(
                position,
                out HubInteractionTarget target));
            Assert.AreEqual(expectedId, target.Id);
            Assert.AreEqual(expectedLabel, target.Label);
        }

        private static int CountLights(
            List<LightRequest> requests,
            string kind,
            int? strength = null)
        {
            int count = 0;
            foreach (LightRequest request in requests)
            {
                if (request.Kind != kind) continue;
                if (strength.HasValue && request.Strength != strength.Value) continue;
                count++;
            }

            return count;
        }

        private static Sprite CreateSprite(
            Texture2D texture,
            string name,
            List<Sprite> created)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            sprite.name = name;
            created.Add(sprite);
            return sprite;
        }

        private static EnvironmentAnimationSet CreateAnimationSet(string key) =>
            new EnvironmentAnimationSet
            {
                slotKey = key,
                clips = new List<SpriteClip> { new SpriteClip() }
            };

        private readonly struct LightRequest
        {
            public string Kind { get; }
            public int Strength { get; }

            public LightRequest(string kind, int strength)
            {
                Kind = kind;
                Strength = strength;
            }
        }
    }
}
