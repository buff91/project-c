using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectC.Tests.PlayMode
{
    public sealed class HubWorldPresenterPlayModeTests
    {
        private bool _previousDevelopmentProfile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousDevelopmentProfile = DevelopmentSaveProfile.IsEnabled;
            DevelopmentSaveProfile.SetEnabled(true);
            DevelopmentSaveProfile.ClearDevelopmentData();
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            RunSaveStore.ContinueRequested = false;
            yield return LoadScene(FrontEndFlow.HubScene);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DevelopmentSaveProfile.ClearDevelopmentData();
            DevelopmentSaveProfile.SetEnabled(_previousDevelopmentProfile);
            RunSaveStore.ContinueRequested = false;
            yield return LoadScene(FrontEndFlow.MainMenuScene);
        }

        [UnityTest]
        public IEnumerator SameHubInstance_RebuildUsesLatestFacilitySnapshotWithoutVisualLeaks()
        {
            IsoPrototypeDemo hub = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(hub);
            Assert.IsTrue(hub.hubMode);

            AssertCurrentHub(hub, forgeOpen: false, bountyBoardOpen: false);

            MetaSaveData unlocked = MetaStore.LoadOrNew();
            unlocked.rescuedNpcs = new[] { "smith", "quartermaster" };
            Assert.IsTrue(MetaStore.Save(unlocked));
            hub.BuildPrototype();
            yield return null;

            AssertCurrentHub(hub, forgeOpen: true, bountyBoardOpen: true);

            MetaSaveData locked = MetaStore.LoadOrNew();
            locked.rescuedNpcs = new string[0];
            Assert.IsTrue(MetaStore.Save(locked));
            hub.BuildPrototype();
            yield return null;

            AssertCurrentHub(hub, forgeOpen: false, bountyBoardOpen: false);
        }

        private static void AssertCurrentHub(
            IsoPrototypeDemo hub,
            bool forgeOpen,
            bool bountyBoardOpen)
        {
            Transform generated = FindOnlyGeneratedVisualsRoot(hub.transform);
            Assert.AreEqual(1, CountDirectChildren(generated, "Campfire"));
            Assert.AreEqual(1, CountDirectChildren(generated, "Portal"));
            Assert.AreEqual(1, CountDirectChildren(generated, "Merchant"));
            Assert.AreEqual(1, CountDirectChildren(generated, "Stash"));
            Assert.AreEqual(1, CountDirectChildren(generated, "Codex"));
            Assert.AreEqual(forgeOpen ? 1 : 0, CountDirectChildren(generated, "Smith"));
            Assert.AreEqual(
                bountyBoardOpen ? 1 : 0,
                CountDirectChildren(generated, "BountyBoard"));
            Assert.AreEqual(13, CountDirectChildrenWithPrefix(generated, "campfire Light "));
            Assert.AreEqual(4, CountDirectChildrenWithPrefix(generated, "portal Light "));

            AssertInteraction(hub, HubLayout.Merchant, true, "merchant", "상인");
            AssertInteraction(hub, HubLayout.Stash, true, "stash", "창고");
            AssertInteraction(hub, HubLayout.Codex, true, "codex", "기록실");
            AssertInteraction(hub, HubLayout.Smith, forgeOpen, "smith", "대장간");
            AssertInteraction(
                hub,
                HubLayout.BountyBoard,
                bountyBoardOpen,
                "bounty",
                "의뢰 게시판");
        }

        private static Transform FindOnlyGeneratedVisualsRoot(Transform hubRoot)
        {
            Transform found = null;
            int count = 0;
            for (int i = 0; i < hubRoot.childCount; i++)
            {
                Transform child = hubRoot.GetChild(i);
                if (child.name != "Generated Visuals") continue;
                found = child;
                count++;
            }

            Assert.AreEqual(1, count, "재빌드 뒤 Generated Visuals 루트가 중복됐다.");
            Assert.NotNull(found);
            return found;
        }

        private static int CountDirectChildren(Transform root, string name)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name) count++;
            return count;
        }

        private static int CountDirectChildrenWithPrefix(Transform root, string prefix)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name.StartsWith(prefix)) count++;
            return count;
        }

        private static void AssertInteraction(
            IsoPrototypeDemo hub,
            GridPos position,
            bool expected,
            string expectedId,
            string expectedLabel)
        {
            FieldInfo hubWorldField = typeof(IsoPrototypeDemo).GetField(
                "_hubWorld",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(hubWorldField);
            object registry = hubWorldField.GetValue(hub);
            Assert.NotNull(registry);

            MethodInfo tryGet = registry.GetType().GetMethod(
                "TryGetInteraction",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(tryGet);
            object[] arguments = { position, null };
            bool found = (bool)tryGet.Invoke(registry, arguments);
            Assert.AreEqual(expected, found);
            if (!expected) return;

            object target = arguments[1];
            Assert.NotNull(target);
            Assert.AreEqual(expectedId, target.GetType().GetProperty("Id")?.GetValue(target));
            Assert.AreEqual(
                expectedLabel,
                target.GetType().GetProperty("Label")?.GetValue(target));
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }
    }
}
