using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class VerticalLookPlayModeTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private bool _previousDevelopmentProfile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousDevelopmentProfile = DevelopmentSaveProfile.IsEnabled;
            DevelopmentSaveProfile.SetEnabled(true);
            DevelopmentSaveProfile.ClearDevelopmentData();
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            RunSaveStore.ContinueRequested = false;
            yield return LoadScene(FrontEndFlow.DungeonScene);
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
        public IEnumerator DownLook_BlocksWorldAction_AndVerticalBombUsesOneTurnThenReturns()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            Assert.IsFalse(demo.hubMode);
            Assert.IsTrue(PositionBesideVisibleDownwardHole(demo),
                "생성된 던전에서 인접 한 층 Hole 보기 위치를 찾지 못했다");
            yield return null;

            int activeFloor = demo.ActiveFloorIndex;
            int turn = demo.DebugTurnNumber;
            GridPos playerBefore = demo.PlayerState.Position;

            Assert.IsTrue(demo.CanLookDown);
            {
                HashSet<GridPos> passiveTiles = GetField<HashSet<GridPos>>(
                    demo, "_verticalPreviewTiles");
                DungeonLayout layout = GetField<DungeonLayout>(demo, "_dungeon");
                GridManager grid = GetField<GridManager>(demo, "_grid");
                GridPos passiveTarget = passiveTiles.First(pos =>
                    layout.Height.FloorIndex(pos.elevation) == activeFloor - 1 &&
                    grid.Map.Get(pos).kind == TileKind.Floor);
                InvokeTileHover(demo, passiveTarget);
                SpriteRenderer passiveMarker = GetField<SpriteRenderer>(
                    demo, "_verticalReadOnlyRenderer");
                Assert.IsTrue(passiveMarker.enabled,
                    "passive 인접 층도 클릭 전에 읽기 전용임을 보여야 한다");
                Assert.AreEqual(
                    grid.iso.SortingOrder(passiveTarget, 2),
                    passiveMarker.sortingOrder,
                    "읽기 전용 X는 바닥과 원격 실루엣보다 앞에서 보여야 한다");

                string passiveFeedback = null;
                demo.InteractionFeedback += message => passiveFeedback = message;
                InvokeTileTap(demo, passiveTarget);
                Assert.AreEqual(turn, demo.DebugTurnNumber);
                Assert.AreEqual(playerBefore, demo.PlayerState.Position);
                Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
                StringAssert.Contains("이동할 수 없다", passiveFeedback);
                Assert.Greater(passiveMarker.transform.localScale.x, 1f);
                Assert.NotNull(GameObject.Find("Floating Text VIEW ONLY"));

                passiveTiles.Remove(passiveTarget);
                MethodInfo refresh = typeof(IsoPrototypeDemo).GetMethod(
                    "RefreshVerticalLookAfterVisibility", PrivateInstance);
                Assert.NotNull(refresh);
                refresh.Invoke(demo, null);
                Assert.IsFalse(passiveMarker.enabled,
                    "가시 창에서 빠진 passive 타일의 마커가 남으면 안 된다");
                passiveTiles.Add(passiveTarget);
            }

            demo.LookDown();
            Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);
            Assert.AreEqual(activeFloor - 1, demo.ViewedFloorIndex);
            Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(turn, demo.DebugTurnNumber, "층 보기는 무턴이어야 한다");
            AssertOneActiveHoleLabelPerPresentedOpening(demo);
            AssertHoleLabelFallsBackWhenCanonicalCellIsHidden(demo);

            Camera camera = Camera.main;
            Assert.NotNull(camera);
            float verticalLookSize = camera.orthographicSize;
            for (int view = 0; view < 4; view++)
            {
                demo.RotateView(-1);
                Assert.AreEqual(
                    verticalLookSize,
                    camera.orthographicSize,
                    1e-4f,
                    $"수직 관찰 q{view}→q{(view + 1) % 4}에서 배율이 바뀌었다");
                Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
                Assert.AreEqual(turn, demo.DebugTurnNumber);
                Assert.AreEqual(playerBefore, demo.PlayerState.Position);
                Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);
            }

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            VisualElement hudRoot = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsFalse(hudRoot.Q<Button>("wait-button").enabledInHierarchy);
            Assert.IsFalse(hudRoot.Q<Button>("combat-button").enabledInHierarchy);
            Assert.IsFalse(hudRoot.Q<Button>("potion-button").enabledInHierarchy);
            Assert.AreEqual(
                $"관찰 {demo.ViewedFloorLabel} ▼ · 이동 불가",
                hudRoot.Q<Label>("vertical-view-state").text);
            Assert.AreEqual("관찰", hudRoot.Q<Label>(className: "turn-label").text);
            Assert.IsTrue(hudRoot.Q<VisualElement>("floor-instrument")
                .ClassListContains("is-observing"));
            Assert.IsTrue(hudRoot.Q<Button>("vertical-view-down")
                .ClassListContains("is-selected"));
            Label verticalHint = hudRoot.Q<Label>("vertical-hint-label");
            StringAssert.Contains("관찰 전용", verticalHint.text);
            StringAssert.Contains("직접 이동 불가", verticalHint.text);
            Assert.IsTrue(verticalHint.parent.ClassListContains("is-observing"));

            string rejectedFeedback = null;
            demo.InteractionFeedback += message => rejectedFeedback = message;
            GridPos readOnlyTarget = GetField<HashSet<GridPos>>(
                demo, "_verticalLookTiles").First();
            InvokeTileHover(demo, readOnlyTarget);
            SpriteRenderer readOnlyMarker = GetField<SpriteRenderer>(
                demo, "_verticalReadOnlyRenderer");
            Assert.NotNull(readOnlyMarker);
            Assert.IsTrue(readOnlyMarker.enabled);
            Assert.AreEqual(
                readOnlyTarget,
                GetField<GridPos?>(demo, "_verticalReadOnlyTile").Value);

            InvokeTileTap(demo, readOnlyTarget);
            Assert.AreEqual(turn, demo.DebugTurnNumber);
            Assert.AreEqual(playerBefore, demo.PlayerState.Position);
            Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);
            StringAssert.Contains("이동할 수 없다", rejectedFeedback);
            Assert.Greater(readOnlyMarker.transform.localScale.x, 1f);
            Assert.NotNull(GameObject.Find("Floating Text VIEW ONLY"));

            demo.WaitTurn();
            demo.InteractAdjacent();
            yield return null;
            Assert.AreEqual(turn, demo.DebugTurnNumber, "층 보기 중 대기/상호작용은 잠겨야 한다");
            Assert.AreEqual(playerBefore, demo.PlayerState.Position);

            demo.DebugGiveItem(ItemKind.ThrowingKnife);
            demo.ToggleAim(ItemKind.ThrowingKnife);
            Assert.IsFalse(demo.BombAiming, "투척 볼트는 다른 층 조준을 열면 안 된다");
            Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);

            int bombsBefore = demo.BombCount;
            demo.DebugGiveItem(ItemKind.Bomb);
            Assert.Greater(demo.BombCount, bombsBefore);
            bombsBefore = demo.BombCount;
            demo.ToggleBombAim();
            Assert.IsTrue(demo.BombAiming);
            InvokeTileHover(demo, readOnlyTarget);
            Assert.IsFalse(readOnlyMarker.enabled,
                "광역 조준 중에는 읽기 전용 X 대신 유효 투척 마커를 보여야 한다");

            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            var markers = GetField<Dictionary<SpriteRenderer, GridPos>>(
                demo, "_throwRangeMarkers");
            Assert.Greater(markers.Count, 0, "실제 층간 투척 가능 칸이 하나 이상 보여야 한다");
            GridPos target = markers.Values.First(pos =>
                dungeon.Height.FloorIndex(pos.elevation) == demo.ViewedFloorIndex);

            InvokeTileTap(demo, target);
            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                demo.DebugTurnNumber > turn || Time.realtimeSinceStartup >= deadline);

            Assert.AreEqual(turn + 1, demo.DebugTurnNumber);
            Assert.AreEqual(bombsBefore - 1, demo.BombCount);
            Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(VerticalLookMode.Current, demo.VerticalLook);
            Assert.AreEqual(activeFloor, demo.ViewedFloorIndex);
            Assert.IsFalse(demo.BombAiming);
            Assert.IsTrue(hudRoot.Q<Button>("wait-button").enabledInHierarchy);
            Assert.AreEqual(
                "자동 이동",
                hudRoot.Q<Label>(className: "turn-label").text,
                "PC에서 수직 관찰을 끝내면 현재 이동 예산 표시로 돌아와야 한다");
            Assert.IsFalse(hudRoot.Q<VisualElement>("floor-instrument")
                .ClassListContains("is-observing"));
            Assert.IsFalse(verticalHint.parent.ClassListContains("is-observing"));
            Assert.IsTrue(hudRoot.Q<Button>("vertical-view-current")
                .ClassListContains("is-selected"));
            Assert.AreEqual(
                demo.playCameraSize,
                camera.orthographicSize,
                0.0001f,
                "수직 관찰을 끝내면 일반 PLAY 2.3 배율로 즉시 복귀해야 한다");
        }

        [UnityTest]
        public IEnumerator UpLook_ShowsReadOnlyHoverAndKeepsPlayerOnActiveFloor()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            Assert.IsTrue(PositionBesideVisibleUpwardHole(demo),
                "생성된 던전에서 인접 윗층 Hole 보기 위치를 찾지 못했다");
            yield return null;

            int activeFloor = demo.ActiveFloorIndex;
            int turn = demo.DebugTurnNumber;
            GridPos player = demo.PlayerState.Position;
            Assert.IsTrue(demo.CanLookUp);

            demo.LookUp();
            Assert.AreEqual(VerticalLookMode.Up, demo.VerticalLook);
            Assert.AreEqual(activeFloor + 1, demo.ViewedFloorIndex);

            GridPos target = GetField<HashSet<GridPos>>(
                demo, "_verticalLookTiles").First();
            InvokeTileHover(demo, target);
            SpriteRenderer marker = GetField<SpriteRenderer>(
                demo, "_verticalReadOnlyRenderer");
            Assert.NotNull(marker);
            Assert.IsTrue(marker.enabled);

            InvokeTileTap(demo, target);
            Assert.AreEqual(turn, demo.DebugTurnNumber);
            Assert.AreEqual(player, demo.PlayerState.Position);
            Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(VerticalLookMode.Up, demo.VerticalLook);

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.AreEqual(
                $"관찰 {demo.ViewedFloorLabel} ▲ · 이동 불가",
                root.Q<Label>("vertical-view-state").text);
            Assert.IsTrue(root.Q<Button>("vertical-view-up")
                .ClassListContains("is-selected"));

            demo.LookCurrent();
            Assert.IsFalse(marker.enabled);
            Assert.AreEqual(VerticalLookMode.Current, demo.VerticalLook);
        }

        private static bool PositionBesideVisibleDownwardHole(IsoPrototypeDemo demo)
        {
            GridManager grid = GetField<GridManager>(demo, "_grid");
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            CombatantState player = demo.PlayerState;
            GameObject playerObject = GetField<GameObject>(demo, "_player");
            MethodInfo sync = typeof(IsoPrototypeDemo).GetMethod(
                "SyncPlayerView", PrivateInstance);
            Assert.NotNull(sync);

            int minimumElevation = grid.Map.All().Min(pair => pair.Key.elevation);
            foreach (KeyValuePair<GridPos, TileData> pair in grid.Map.All())
            {
                GridPos opening = pair.Key;
                if (pair.Value.kind != TileKind.Hole) continue;
                GridPos? landing = grid.Map.FindLandingBelow(opening, minimumElevation);
                if (!landing.HasValue) continue;

                int openingFloor = dungeon.Height.FloorIndex(opening.elevation);
                if (dungeon.Height.FloorIndex(landing.Value.elevation) != openingFloor - 1)
                    continue;

                foreach (GridPos neighbor in new[]
                         { opening.North, opening.East, opening.South, opening.West })
                {
                    if (!grid.Map.IsWalkable(neighbor)) continue;

                    demo.DebugJumpFloor(openingFloor - demo.ActiveFloorIndex);
                    demo.DebugKillAllOnFloor();
                    player.MoveTo(neighbor);
                    playerObject.transform.position = grid.GridToWorld(neighbor);
                    sync.Invoke(demo, new object[] { neighbor, false });
                    if (demo.CanLookDown) return true;
                }
            }

            return false;
        }

        private static bool PositionBesideVisibleUpwardHole(IsoPrototypeDemo demo)
        {
            GridManager grid = GetField<GridManager>(demo, "_grid");
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            CombatantState player = demo.PlayerState;
            GameObject playerObject = GetField<GameObject>(demo, "_player");
            MethodInfo sync = typeof(IsoPrototypeDemo).GetMethod(
                "SyncPlayerView", PrivateInstance);
            Assert.NotNull(sync);

            int minimumElevation = grid.Map.All().Min(pair => pair.Key.elevation);
            foreach (KeyValuePair<GridPos, TileData> pair in grid.Map.All())
            {
                GridPos opening = pair.Key;
                if (pair.Value.kind != TileKind.Hole) continue;
                GridPos? landing = grid.Map.FindLandingBelow(opening, minimumElevation);
                if (!landing.HasValue) continue;

                int landingFloor = dungeon.Height.FloorIndex(landing.Value.elevation);
                if (dungeon.Height.FloorIndex(opening.elevation) != landingFloor + 1)
                    continue;

                foreach (GridPos candidate in new[]
                         {
                             landing.Value,
                             landing.Value.North,
                             landing.Value.East,
                             landing.Value.South,
                             landing.Value.West
                         })
                {
                    if (!grid.Map.IsWalkable(candidate) ||
                        dungeon.Height.FloorIndex(candidate.elevation) != landingFloor)
                        continue;

                    demo.DebugJumpFloor(landingFloor - demo.ActiveFloorIndex);
                    demo.DebugKillAllOnFloor();
                    player.MoveTo(candidate);
                    playerObject.transform.position = grid.GridToWorld(candidate);
                    sync.Invoke(demo, new object[] { candidate, false });
                    if (demo.CanLookUp) return true;
                }
            }

            return false;
        }

        private static T GetField<T>(IsoPrototypeDemo demo, string name)
        {
            FieldInfo field = typeof(IsoPrototypeDemo).GetField(name, PrivateInstance);
            Assert.NotNull(field, name);
            return (T)field.GetValue(demo);
        }

        private static void AssertOneActiveHoleLabelPerPresentedOpening(
            IsoPrototypeDemo demo)
        {
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            var rootsByFloor = new Dictionary<int, int>();
            var labelsByFloor = new Dictionary<int, int>();
            foreach (object agent in (IEnumerable)GetField<object>(demo, "_verticalLandmarks"))
            {
                System.Type type = agent.GetType();
                TileKind kind = (TileKind)type.GetField("Kind").GetValue(agent);
                if (kind != TileKind.Hole) continue;

                GameObject root = (GameObject)type.GetField("Root").GetValue(agent);
                if (root == null || !root.activeInHierarchy) continue;
                GridPos anchor = (GridPos)type.GetField("Anchor").GetValue(agent);
                int floor = dungeon.Height.FloorIndex(anchor.elevation);
                rootsByFloor.TryGetValue(floor, out int rootCount);
                rootsByFloor[floor] = rootCount + 1;

                TextMesh label = (TextMesh)type.GetField("Label").GetValue(agent);
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                labelsByFloor.TryGetValue(floor, out int labelCount);
                labelsByFloor[floor] = labelCount + 1;
            }

            Assert.Greater(rootsByFloor.Count, 0);
            foreach (KeyValuePair<int, int> pair in rootsByFloor)
            {
                labelsByFloor.TryGetValue(pair.Key, out int labelCount);
                Assert.AreEqual(1, labelCount,
                    $"표시 중인 {pair.Key}층 개구부 {pair.Value}셀은 목적 층 라벨 하나를 공유해야 한다");
            }
        }

        private static void AssertHoleLabelFallsBackWhenCanonicalCellIsHidden(
            IsoPrototypeDemo demo)
        {
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            object[] agents = ((IEnumerable)GetField<object>(demo, "_verticalLandmarks"))
                .Cast<object>()
                .ToArray();
            MethodInfo shouldShow = typeof(IsoPrototypeDemo).GetMethod(
                "ShouldShowVerticalLandmarkLabel",
                PrivateInstance);
            Assert.NotNull(shouldShow);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (floor.HoleTiles.Count < 2) continue;
                object canonical = agents.FirstOrDefault(agent =>
                    (TileKind)agent.GetType().GetField("Kind").GetValue(agent) == TileKind.Hole &&
                    (GridPos)agent.GetType().GetField("Anchor").GetValue(agent) == floor.HoleTiles[0]);
                object sibling = agents.FirstOrDefault(agent =>
                    (TileKind)agent.GetType().GetField("Kind").GetValue(agent) == TileKind.Hole &&
                    (GridPos)agent.GetType().GetField("Anchor").GetValue(agent) == floor.HoleTiles[1]);
                if (canonical == null || sibling == null) continue;

                GameObject canonicalRoot =
                    (GameObject)canonical.GetType().GetField("Root").GetValue(canonical);
                GameObject siblingRoot =
                    (GameObject)sibling.GetType().GetField("Root").GetValue(sibling);
                bool canonicalWasActive = canonicalRoot.activeSelf;
                bool siblingWasActive = siblingRoot.activeSelf;
                try
                {
                    canonicalRoot.SetActive(false);
                    siblingRoot.SetActive(true);
                    Assert.IsTrue((bool)shouldShow.Invoke(demo, new[] { sibling }),
                        "정적 첫 Hole 셀이 가려지면 보이는 형제가 라벨 대표를 넘겨받아야 한다");
                }
                finally
                {
                    canonicalRoot.SetActive(canonicalWasActive);
                    siblingRoot.SetActive(siblingWasActive);
                }
                return;
            }

            Assert.Fail("2셀 이상 Hole 개구부 fixture를 찾지 못했다");
        }

        private static void InvokeTileTap(IsoPrototypeDemo demo, GridPos target)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "HandleTileTapped", PrivateInstance);
            Assert.NotNull(method);
            method.Invoke(demo, new object[] { target, true });
        }

        private static void InvokeTileHover(IsoPrototypeDemo demo, GridPos? target)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "HandleTileHovered", PrivateInstance);
            Assert.NotNull(method);
            method.Invoke(demo, new object[] { target });
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }
    }
}
