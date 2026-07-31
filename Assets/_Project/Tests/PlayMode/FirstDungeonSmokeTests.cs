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
        private static readonly FieldInfo RangedChargesField =
            typeof(IsoPrototypeDemo).GetField(
                "_rangedCharges",
                BindingFlags.Instance | BindingFlags.NonPublic);

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
            Assert.AreEqual(
                "출구의 봉인이 풀렸다 — 출구로 향하라",
                root.Q<Label>("boss-objective").text);
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
            Assert.AreEqual("최종 구역 출구", root.Q<Label>("exit-title").text);
            StringAssert.Contains("최종 구역 도달", root.Q<Label>("exit-desc").text);

            dungeon.ConfirmAdvanceStage();
            yield return null;

            Assert.IsTrue(dungeon.RunSummary.Victory);
            Assert.AreEqual(RunTelemetryOutcome.Victory, dungeon.Telemetry.outcome);
            Assert.AreEqual(0, dungeon.Telemetry.bossKills);
            Assert.IsFalse(RunSaveStore.HasSave);
        }

        [UnityTest]
        public IEnumerator CheckpointAndDungeonTransition_PreserveRangedChargeRhythm()
        {
            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            dungeon.stageCount = 2;

            var live = new RangedChargeState(1) { turnsSinceGain = 3 };
            SetRangedCharges(dungeon, live);
            dungeon.DebugSaveCheckpoint();

            Assert.IsTrue(RunSaveStore.TryLoad(out RunSaveData checkpoint));
            Assert.IsNotNull(checkpoint.rangedCharges);
            Assert.AreEqual(1, checkpoint.rangedCharges.charges);
            Assert.AreEqual(3, checkpoint.rangedCharges.turnsSinceGain);
            Assert.AreNotSame(
                live,
                checkpoint.rangedCharges,
                "체크포인트는 런타임 상태와 분리된 JSON 스냅샷이어야 한다");

            dungeon.DebugJumpFloor(9);
            yield return null;
            dungeon.DebugDefeatBoss();
            yield return null;
            dungeon.ConfirmAdvanceStage();
            yield return null;

            RangedChargeState carried = GetRangedCharges(dungeon);
            Assert.AreEqual(2, dungeon.StageIndex);
            Assert.AreEqual(1, carried.charges);
            Assert.AreEqual(3, carried.turnsSinceGain);
            Assert.AreEqual("B2", dungeon.ActiveFloorLabel);
            Assert.AreEqual(
                dungeon.ActiveFloorLabel,
                dungeon.Telemetry.currentFloorLabel,
                "스테이지 누적 진행 지수로 라벨을 재계산하면 2단계 B2가 9F로 동결된다");
            Assert.AreEqual("B2", dungeon.ReachedFloorLabel,
                "게임오버/로그용 최고 도달 라벨도 전역 층 키(B11)가 아니라 동결 문자열을 써야 한다");

            Assert.IsTrue(RunSaveStore.TryLoad(out RunSaveData nextStageCheckpoint));
            Assert.AreEqual(2, nextStageCheckpoint.stageIndex);
            Assert.AreEqual(1, nextStageCheckpoint.rangedCharges.charges);
            Assert.AreEqual(3, nextStageCheckpoint.rangedCharges.turnsSinceGain);
        }

        [UnityTest]
        public IEnumerator MidExtractionTap_StopsAdjacentAndOpensDirectionNeutralModal()
        {
            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            dungeon.DebugJumpFloor(3);
            yield return null;

            int exitRequests = 0;
            dungeon.ExitChoiceRequested += () => exitRequests++;
            Assert.IsTrue(dungeon.DebugRequestExtractionPoint(),
                "첫 중간 탈출구가 있는 4번째 진행 층이어야 한다");
            yield return null;

            Assert.IsTrue(dungeon.AtExtractionPoint,
                "접근 경로는 탈출구 인접 칸에서 끝난다 — 대상 칸 동일성을 요구하면 항상 실패한다");
            Assert.AreEqual(1, exitRequests);

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.AreEqual("비상 탈출구", root.Q<Label>("exit-title").text);
            StringAssert.Contains("더 나아가면", root.Q<Label>("exit-desc").text);
            StringAssert.DoesNotContain("더 내려가면", root.Q<Label>("exit-desc").text);
        }

        [UnityTest]
        public IEnumerator CheckpointWithoutRangedState_RestoresLegacySaveAtFullCharge()
        {
            DungeonDefinition selected = DungeonSelection.Selected;
            string savePath = Path.Combine(
                DevelopmentSaveProfile.ActiveRootPath,
                DevelopmentSaveProfile.RunFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(
                savePath,
                $"{{\"schemaVersion\":1,\"dungeonId\":\"{selected.Id}\"," +
                $"\"seed\":{selected.Seed},\"roomSize\":13," +
                $"\"floorCount\":{selected.FloorCount},\"elevationsPerFloor\":4," +
                "\"stageCount\":1,\"stageIndex\":1,\"currentFloorIndex\":0," +
                $"\"currentProgressIndex\":0,\"hp\":{SurvivorProfile.MaxHp}}}");
            RunSaveStore.ContinueRequested = true;

            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.AreEqual(dungeon.RangedChargeCapacity, dungeon.RangedCharges);
            Assert.AreEqual(0, GetRangedCharges(dungeon).turnsSinceGain);
        }

        [UnityTest]
        public IEnumerator CheckpointWithSerializedZeroRangedState_PreservesDepletedCharge()
        {
            DungeonDefinition selected = DungeonSelection.Selected;
            string savePath = Path.Combine(
                DevelopmentSaveProfile.ActiveRootPath,
                DevelopmentSaveProfile.RunFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(
                savePath,
                $"{{\"schemaVersion\":1,\"dungeonId\":\"{selected.Id}\"," +
                $"\"seed\":{selected.Seed},\"roomSize\":13," +
                $"\"floorCount\":{selected.FloorCount},\"elevationsPerFloor\":4," +
                "\"stageCount\":1,\"stageIndex\":1,\"currentFloorIndex\":0," +
                $"\"currentProgressIndex\":0,\"hp\":{SurvivorProfile.MaxHp}," +
                "\"rangedCharges\":{\"charges\":0,\"turnsSinceGain\":0}}");
            RunSaveStore.ContinueRequested = true;

            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.Greater(dungeon.RangedChargeCapacity, 0);
            Assert.AreEqual(0, dungeon.RangedCharges,
                "원문에 저장된 실제 0/0은 필드 누락으로 오인해 만충시키면 안 된다");
            Assert.AreEqual(0, GetRangedCharges(dungeon).turnsSinceGain);
        }

        [UnityTest]
        public IEnumerator FutureMeta_BlocksNewExpeditionAndPreservesOriginal()
        {
            string metaPath = Path.Combine(
                DevelopmentSaveProfile.ActiveRootPath,
                DevelopmentSaveProfile.MetaFileName);
            string original =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"gold\":77,\"futureWallet\":{\"shards\":9}}";
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath));
            File.WriteAllText(metaPath, original);

            InvokeMainMenuStart();
            yield return WaitForScene(FrontEndFlow.HubScene);
            yield return null;

            IsoPrototypeDemo hub = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(hub);
            Assert.IsTrue(hub.hubMode);

            hub.BeginSelectedDungeon();
            yield return null;

            Assert.AreEqual(FrontEndFlow.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(original, File.ReadAllText(metaPath));
            Assert.IsFalse(RunSaveStore.HasSave,
                "차단된 출정이 미래 메타와 별개의 새 체크포인트를 만들면 안 된다");
        }

        [UnityTest]
        public IEnumerator FutureMetaAppearingMidRun_BlocksSettlementAndPreservesCheckpoint()
        {
            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            int treasureBefore = dungeon.ItemCount(ItemKind.CoinPouch);
            dungeon.DebugGiveItem(ItemKind.CoinPouch);
            dungeon.DebugSaveCheckpoint();
            Assert.IsTrue(RunSaveStore.HasSave);

            string metaPath = Path.Combine(
                DevelopmentSaveProfile.ActiveRootPath,
                DevelopmentSaveProfile.MetaFileName);
            string original =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"gold\":77,\"futureWallet\":{\"shards\":9}}";
            File.WriteAllText(metaPath, original);

            dungeon.ExtractRun();
            yield return null;

            Assert.IsFalse(dungeon.RunSummary.Ended,
                "메타 정산이 실패했는데 런을 끝내면 획득 보상이 사라진다");
            Assert.AreEqual(treasureBefore + 1, dungeon.ItemCount(ItemKind.CoinPouch));
            Assert.IsTrue(RunSaveStore.HasSave,
                "정산 실패 시 이어갈 체크포인트를 먼저 지우면 안 된다");
            Assert.AreEqual(original, File.ReadAllText(metaPath));
        }

        [UnityTest]
        public IEnumerator CompletedMetaSettlement_WithCheckpointStillPresent_IsNotAppliedTwice()
        {
            EquipmentDefinition carriedWeapon = EquipmentCatalog.ById("pipe-spear");
            Assert.NotNull(carriedWeapon);
            MetaSaveData prepared = MetaStore.LoadOrNew();
            prepared.AddCount(carriedWeapon.Item, 1);
            prepared.SetEquipped(EquipmentSlot.Weapon, carriedWeapon.Id);
            prepared.activeBountyIds = new[] { "cull" };
            Assert.IsTrue(MetaStore.Save(prepared));

            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.AreEqual(
                0,
                MetaStore.LoadOrNew().GetCount(carriedWeapon.Item),
                "장착 장비는 출정 시 창고에서 빠져 있어야 반환 중복을 검증할 수 있다");
            dungeon.Telemetry.kills = BountyRules.ById("cull").Target;
            dungeon.DebugGiveItem(ItemKind.CoinPouch);
            dungeon.DebugSaveCheckpoint();
            Assert.IsTrue(RunSaveStore.HasSave);
            string runId = dungeon.Telemetry.runId;
            Assert.IsNotEmpty(runId);

            // 메타 저장은 성공했지만 호출자가 체크포인트를 지우기 직전에 프로세스가
            // 종료된 창을 재현한다. private 종료 트랜잭션만 호출하고 씬을 다시 로드한다.
            Assert.IsTrue(InvokeFinalizeRun(
                dungeon,
                RunTelemetryOutcome.Extraction,
                out int firstPayout));
            Assert.Greater(firstPayout, 0);
            Assert.IsTrue(RunSaveStore.HasSave,
                "크래시 창을 재현하려면 구 체크포인트가 그대로 남아 있어야 한다");
            Assert.IsFalse(
                RunSaveStore.CanResume,
                "정산 영수증이 있는 잔여 체크포인트를 일반 이어하기로 노출하면 안 된다");

            string metaPath = Path.Combine(
                DevelopmentSaveProfile.ActiveRootPath,
                DevelopmentSaveProfile.MetaFileName);
            string settledMetaJson = File.ReadAllText(metaPath);
            MetaSaveData once = MetaStore.LoadOrNew();
            Assert.IsTrue(once.TryGetRunSettlement(
                runId,
                out RunSettlementEntry receipt));
            Assert.AreEqual(firstPayout, receipt.payout);
            Assert.AreEqual(
                ItemCatalog.GoldValue(ItemKind.CoinPouch) +
                BountyRules.ById("cull").RewardGold,
                receipt.payout);
            Assert.Greater(receipt.recordsGained, 0);
            Assert.AreEqual(receipt.recordsGained, once.records);
            Assert.AreEqual(1, once.GetCount(carriedWeapon.Item));
            Assert.IsFalse(BountyRules.HasActiveBounties(once));
            Assert.AreEqual(1, once.settledRuns.Count);

            RunSaveStore.ContinueRequested = true;
            yield return LoadScene(FrontEndFlow.DungeonScene);

            IsoPrototypeDemo resumed = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(resumed);
            Assert.AreEqual(runId, resumed.Telemetry.runId);
            resumed.ExtractRun();
            yield return null;

            Assert.IsTrue(resumed.RunSummary.Extracted);
            Assert.AreEqual(firstPayout, resumed.RunSummary.GoldBanked);
            Assert.IsFalse(RunSaveStore.HasSave);
            Assert.AreEqual(
                settledMetaJson,
                File.ReadAllText(metaPath),
                "같은 runId 재개 정산은 메타 파일을 다시 쓰거나 보상을 더하면 안 된다");

            MetaSaveData twice = MetaStore.LoadOrNew();
            Assert.AreEqual(1, twice.settledRuns.Count);
            Assert.IsTrue(twice.TryGetRunSettlement(runId, out _));
        }

        private static void SetRangedCharges(
            IsoPrototypeDemo dungeon,
            RangedChargeState state)
        {
            Assert.NotNull(RangedChargesField);
            RangedChargesField.SetValue(dungeon, state);
        }

        private static RangedChargeState GetRangedCharges(IsoPrototypeDemo dungeon)
        {
            Assert.NotNull(RangedChargesField);
            return (RangedChargeState)RangedChargesField.GetValue(dungeon);
        }

        private static bool InvokeFinalizeRun(
            IsoPrototypeDemo dungeon,
            RunTelemetryOutcome outcome,
            out int payout)
        {
            MethodInfo finalize = typeof(IsoPrototypeDemo).GetMethod(
                "TryFinalizeRun",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(finalize);
            object[] arguments = { outcome, "", 0 };
            bool result = (bool)finalize.Invoke(dungeon, arguments);
            payout = (int)arguments[2];
            return result;
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
