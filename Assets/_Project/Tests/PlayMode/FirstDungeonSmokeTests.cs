using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectC.Tests.PlayMode
{
    public sealed class FirstDungeonSmokeTests
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

            yield return LoadScene(FrontEndFlow.MainMenuScene);
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
        public IEnumerator MainMenuToHubToB10BossAndUnlockedExit_CompletesRun()
        {
            Assert.NotNull(Object.FindAnyObjectByType<MainMenuController>());
            InvokeMainMenuStart();
            yield return WaitForScene(FrontEndFlow.HubScene);

            IsoPrototypeDemo hub = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(hub);
            Assert.IsTrue(hub.hubMode);

            hub.BeginSelectedDungeon();
            yield return WaitForScene(FrontEndFlow.DungeonScene);
            yield return null;

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.IsFalse(dungeon.hubMode);
            Assert.AreEqual(DungeonCatalog.DefaultId, dungeon.DungeonId);
            Assert.AreEqual(10, dungeon.FloorCount);
            Assert.AreEqual("B1", dungeon.ActiveFloorLabel);

            dungeon.DebugJumpFloor(-9);
            yield return null;

            Assert.AreEqual("B10", dungeon.ActiveFloorLabel);
            Assert.IsTrue(dungeon.IsBossFloor);
            Assert.Greater(dungeon.BossHp, 0);
            Assert.IsFalse(dungeon.BossDefeated);
            Assert.IsFalse(dungeon.BossExitUnlocked);

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsTrue(root.Q<VisualElement>("boss-panel").ClassListContains("is-open"));

            int exitRequests = 0;
            dungeon.ExitChoiceRequested += () => exitRequests++;
            Assert.IsFalse(dungeon.DebugRequestBossExit());
            Assert.AreEqual(0, exitRequests);

            dungeon.DebugDefeatBoss();
            yield return null;

            Assert.IsTrue(dungeon.BossDefeated);
            Assert.IsTrue(dungeon.BossExitUnlocked);
            Assert.AreEqual(
                "EXIT UNSEALED",
                root.Q<Label>("boss-health-value").text);
            Assert.IsTrue(RunSaveStore.TryLoad(out RunSaveData checkpoint));
            Assert.IsTrue(checkpoint.bossDefeated);
            Assert.AreEqual(-9, checkpoint.currentFloorIndex);

            Assert.IsTrue(dungeon.DebugRequestBossExit());
            Assert.AreEqual(1, exitRequests);
            dungeon.ConfirmAdvanceStage();
            yield return null;

            Assert.IsTrue(dungeon.RunSummary.Victory);
            Assert.IsFalse(RunSaveStore.HasSave);
        }

        private static void InvokeMainMenuStart()
        {
            MethodInfo start = typeof(MainMenuController).GetMethod(
                "StartNewGame",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(start);
            start.Invoke(null, null);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return WaitForScene(sceneName);
            yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName);
        }
    }
}
