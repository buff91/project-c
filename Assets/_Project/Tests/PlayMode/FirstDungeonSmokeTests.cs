using System.Collections;
using System.IO;
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
        public IEnumerator MainMenuToHubToRooftopBossAndUnlockedExit_CompletesRun()
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
            // 첫 던전은 아케이드 타워(상승) — 지하 기계실 B2 에서 시작해 8F 옥상으로 올라간다.
            // 출처: DungeonCatalog(direction: Ascend, firstBuildingFloor: -2, "B2 → 8F + 옥상"),
            // docs/STATUS.md "첫 던전/보스", GDD §10.1.
            Assert.AreEqual("B2", dungeon.ActiveFloorLabel);
            Assert.NotNull(dungeon.Telemetry);
            Assert.AreEqual(RunTelemetryOutcome.InProgress, dungeon.Telemetry.outcome);

            // 상승 던전의 진행 최종 층은 공간 최상단(+9)이다 — 하강 던전의 -9 가 아니다.
            dungeon.DebugJumpFloor(9);
            yield return null;

            Assert.AreEqual("8F", dungeon.ActiveFloorLabel);
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
            Assert.AreEqual(9, checkpoint.currentFloorIndex);
            Assert.NotNull(checkpoint.telemetry);
            Assert.AreEqual(9, checkpoint.telemetry.currentFloorIndex);

            // 치트 훅으로 출구 칸까지 이동시키고 선택지가 뜨는지 본다.
            Assert.IsTrue(dungeon.DebugRequestBossExit());
            Assert.AreEqual(1, exitRequests);

            // 여기서부터가 **실제 플레이 경로**다. 위 훅은 TryRequestExitChoice 를 직접 부르지만,
            // 플레이어는 출구를 밟고 SPACE(=InteractAdjacent)를 누른다. 이 경로가 예전에
            // 타일 종류(StairsDown)로 분기해서, 진출 계단이 상행인 상승 던전에서는
            // 출구를 밟아도 아무 일이 없었다. 훅만 검증하면 그 결함을 놓친다.
            dungeon.InteractAdjacent();
            yield return null;
            Assert.AreEqual(
                2,
                exitRequests,
                "출구를 밟고 상호작용했는데 선택지가 뜨지 않는다 — " +
                "출구 판정이 타일 종류에 묶여 있으면 상승 던전에서 완주할 수 없다.");

            dungeon.ConfirmAdvanceStage();
            yield return null;

            Assert.IsTrue(dungeon.RunSummary.Victory);
            Assert.IsTrue(dungeon.Telemetry.Ended);
            Assert.AreEqual(RunTelemetryOutcome.Victory, dungeon.Telemetry.outcome);
            Assert.AreEqual(1, dungeon.Telemetry.bossKills);
            Assert.IsTrue(dungeon.Telemetry.cheatsUsed);
            Assert.AreEqual(
                1,
                Directory.GetFiles(RunTelemetryStore.ReportDirectoryPath, "*.json").Length);
            Assert.IsFalse(RunSaveStore.HasSave);
        }

        [UnityTest]
        public IEnumerator HubToFloodedVaultBosslessExit_CompletesRun()
        {
            InvokeMainMenuStart();
            yield return WaitForScene(FrontEndFlow.HubScene);

            HubHudController hubHud = Object.FindAnyObjectByType<HubHudController>();
            Assert.NotNull(hubHud);
            InvokeHubDungeonEntry(hubHud, "flooded-vault");
            yield return WaitForScene(FrontEndFlow.DungeonScene);
            yield return null;

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.AreEqual("flooded-vault", dungeon.DungeonId);
            Assert.AreEqual("1구역", dungeon.ActiveFloorLabel);
            Assert.IsFalse(dungeon.HasBoss);
            Assert.IsTrue(dungeon.BossExitUnlocked);

            dungeon.DebugJumpFloor(-9);
            yield return null;

            Assert.AreEqual("10구역", dungeon.ActiveFloorLabel);
            Assert.IsFalse(dungeon.IsBossFloor);
            Assert.IsTrue(dungeon.BossExitUnlocked);

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsFalse(root.Q<VisualElement>("boss-panel").ClassListContains("is-open"));

            int exitRequests = 0;
            dungeon.ExitChoiceRequested += () => exitRequests++;
            Assert.IsTrue(dungeon.DebugRequestBossExit());
            Assert.AreEqual(1, exitRequests);
            Assert.AreEqual("최심부 출구", root.Q<Label>("exit-title").text);
            StringAssert.Contains("최심부 도달", root.Q<Label>("exit-desc").text);

            dungeon.ConfirmAdvanceStage();
            yield return null;

            Assert.IsTrue(dungeon.RunSummary.Victory);
            Assert.AreEqual(RunTelemetryOutcome.Victory, dungeon.Telemetry.outcome);
            Assert.AreEqual(0, dungeon.Telemetry.bossKills);
            Assert.IsFalse(RunSaveStore.HasSave);
        }

        private static void InvokeMainMenuStart()
        {
            // 첫 실행이든 재접속이든 타이틀의 기본 버튼은 캠프로 간다(TitleEntryRouting).
            MethodInfo enterCamp = typeof(MainMenuController).GetMethod(
                "EnterCamp",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(enterCamp);
            enterCamp.Invoke(null, null);
        }

        private static void InvokeHubDungeonEntry(HubHudController hubHud, string dungeonId)
        {
            MethodInfo selectDungeon = typeof(HubHudController).GetMethod(
                "SelectDungeon",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo enterDungeon = typeof(HubHudController).GetMethod(
                "EnterSelectedDungeon",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(selectDungeon);
            Assert.NotNull(enterDungeon);

            selectDungeon.Invoke(hubHud, new object[] { dungeonId });
            enterDungeon.Invoke(hubHud, null);
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
