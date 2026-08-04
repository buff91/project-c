using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectC.Tests.PlayMode
{
    public sealed class TacticalMapPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private bool _previousDevelopmentProfile;

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            _previousDevelopmentProfile = DevelopmentSaveProfile.IsEnabled;
            DevelopmentSaveProfile.SetEnabled(true);
            DevelopmentSaveProfile.ClearDevelopmentData();
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            RunSaveStore.ContinueRequested = false;
            yield return LoadScene(FrontEndFlow.DungeonScene);
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            DevelopmentSaveProfile.ClearDevelopmentData();
            DevelopmentSaveProfile.SetEnabled(_previousDevelopmentProfile);
            RunSaveStore.ContinueRequested = false;
            yield return LoadScene(FrontEndFlow.MainMenuScene);
        }

        [UnityTest]
        public IEnumerator MapButton_TogglesAndCleansTemporaryWorldViewsWithoutMutatingRun()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            PrototypeHudController hud =
                Object.FindAnyObjectByType<PrototypeHudController>();
            Camera camera = Camera.main;
            Assert.NotNull(demo);
            Assert.NotNull(hud);
            Assert.NotNull(camera);
            yield return null;

            Assert.AreEqual(HudPresentationMode.Desktop, hud.ActivePresentation,
                "전술 지도 통합 검증은 PC HUD 계약을 사용한다");
            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Button mapButton = root.Q<Button>("tactical-map-open");
            VisualElement modal = root.Q<VisualElement>("tactical-map-modal");
            Assert.NotNull(mapButton);
            Assert.NotNull(modal);
            Assert.IsFalse(IsOpen(modal));

            WorldSnapshot normalView = CaptureWorld(demo, camera);
            Assert.IsTrue(demo.TryPanCamera(new Vector2(180f, 0f)));
            Assert.IsTrue(demo.IsCameraLookingAround);

            InvokeClick(mapButton);

            Assert.IsTrue(IsOpen(modal), "MAP 버튼이 전술 지도를 열어야 한다");
            Assert.IsFalse(demo.IsCameraLookingAround,
                "지도 진입은 월드 자유 팬을 플레이어 추적으로 정리해야 한다");
            AssertWorldUnchanged(normalView, demo, camera, "팬 상태에서 지도 열기");

            InvokeClick(mapButton);
            Assert.IsFalse(IsOpen(modal), "열린 지도에서 MAP 버튼을 다시 누르면 닫혀야 한다");

            Assert.IsTrue(PositionBesideVisibleDownwardHole(demo),
                "수직 관찰을 검증할 실제 한 층 Hole을 찾지 못했다");
            demo.DebugGiveItem(ItemKind.Bomb);
            WorldSnapshot currentFloorView = CaptureWorld(demo, camera);

            demo.LookDown();
            Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);
            demo.ToggleAim(ItemKind.Bomb);
            Assert.IsTrue(demo.BombAiming,
                "광역 투척 조준과 수직 관찰이 함께 열린 fixture여야 한다");

            InvokeClick(mapButton);

            Assert.IsTrue(IsOpen(modal));
            Assert.AreEqual(VerticalLookMode.Current, demo.VerticalLook,
                "지도 진입은 인접 층 관찰을 현재 층으로 되돌려야 한다");
            Assert.IsFalse(demo.BombAiming,
                "지도 진입은 남아 있던 투척 조준을 취소해야 한다");
            AssertWorldUnchanged(
                currentFloorView,
                demo,
                camera,
                "수직 관찰·조준 상태에서 지도 열기");
        }

        [UnityTest]
        public IEnumerator VisitedFloorAndMapTools_AreReadOnly_AndKeyboardClosesOnlyMap()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            PrototypeHudController hud =
                Object.FindAnyObjectByType<PrototypeHudController>();
            Camera camera = Camera.main;
            Assert.NotNull(demo);
            Assert.NotNull(hud);
            Assert.NotNull(camera);
            yield return null;

            Assert.AreEqual(HudPresentationMode.Desktop, hud.ActivePresentation);
            int playFloor = demo.ActiveFloorIndex;
            demo.DebugJumpFloor(1);
            int visitedFloor = demo.ActiveFloorIndex;
            Assert.AreEqual(playFloor + 1, visitedFloor,
                "기본 던전에는 B2 위의 방문 가능 층이 있어야 한다");
            demo.DebugJumpFloor(-1);
            Assert.AreEqual(playFloor, demo.ActiveFloorIndex);
            Assert.IsTrue(demo.CanInspectFloor(visitedFloor));
            yield return null;

            VisualElement root = hud.GetComponent<UIDocument>().rootVisualElement;
            Button mapButton = root.Q<Button>("tactical-map-open");
            VisualElement modal = root.Q<VisualElement>("tactical-map-modal");
            VisualElement gameMenu = root.Q<VisualElement>("game-menu-modal");
            Assert.NotNull(mapButton);
            Assert.NotNull(modal);
            Assert.NotNull(gameMenu);

            WorldSnapshot before = CaptureWorld(demo, camera);
            InvokeClick(mapButton);
            Assert.IsTrue(IsOpen(modal));

            Button visitedButton = FindFloorButton(root, demo.FloorLabel(visitedFloor));
            Assert.NotNull(visitedButton, "방문한 층은 지도 층 목록에 있어야 한다");
            Assert.IsTrue(visitedButton.enabledInHierarchy);
            InvokeClick(visitedButton);

            Assert.AreEqual(playFloor, demo.ActiveFloorIndex,
                "지도 층 선택은 실제 활성 층을 바꾸면 안 된다");
            Assert.AreEqual(
                $"플레이 {demo.FloorLabel(playFloor)} · 기록 {demo.FloorLabel(visitedFloor)} · 이동/전투 불가",
                root.Q<Label>("tactical-map-floor-state").text);
            Assert.IsTrue(FindFloorButton(root, demo.FloorLabel(visitedFloor))
                .ClassListContains("is-selected"));
            AssertWorldUnchanged(before, demo, camera, "방문 층 선택");

            Button currentButton = FindFloorButton(root, demo.FloorLabel(playFloor));
            Assert.NotNull(currentButton);
            InvokeClick(currentButton);
            Assert.AreEqual(playFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(
                $"플레이 {demo.FloorLabel(playFloor)} · 현재 층",
                root.Q<Label>("tactical-map-floor-state").text);

            InvokeClick(root.Q<Button>("tactical-map-zoom-in"));
            Assert.AreEqual("150%", root.Q<Label>("tactical-map-zoom-value").text);
            AssertWorldUnchanged(before, demo, camera, "지도 확대");
            InvokeClick(root.Q<Button>("tactical-map-fit"));
            Assert.AreEqual("100%", root.Q<Label>("tactical-map-zoom-value").text);
            AssertWorldUnchanged(before, demo, camera, "지도 맞춤");

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            yield return PressHudKey(keyboard, keyboard.mKey);
            Assert.IsFalse(IsOpen(modal), "M은 열린 지도만 닫아야 한다");
            Assert.IsFalse(IsOpen(gameMenu));

            yield return PressHudKey(keyboard, keyboard.mKey);
            Assert.IsTrue(IsOpen(modal), "M은 닫힌 지도도 같은 경로로 다시 열어야 한다");
            yield return PressHudKey(keyboard, keyboard.escapeKey);
            Assert.IsFalse(IsOpen(modal), "Escape는 열린 지도를 먼저 닫아야 한다");
            Assert.IsFalse(IsOpen(gameMenu),
                "지도를 닫은 같은 Escape 입력으로 게임 메뉴까지 열면 안 된다");
            AssertWorldUnchanged(before, demo, camera, "M/Escape 지도 토글");
        }

        private static WorldSnapshot CaptureWorld(IsoPrototypeDemo demo, Camera camera)
        {
            return new WorldSnapshot(
                demo.DebugTurnNumber,
                demo.ActiveFloorIndex,
                demo.PlayerState.Position,
                demo.ViewQuarterTurns,
                camera.orthographicSize,
                new HashSet<GridPos>(GetField<HashSet<GridPos>>(demo, "_visibleTiles")),
                new HashSet<GridPos>(GetField<HashSet<GridPos>>(demo, "_exploredTiles")),
                new HashSet<GridPos>(GetField<HashSet<GridPos>>(demo, "_mappedTiles")));
        }

        private static void AssertWorldUnchanged(
            WorldSnapshot expected,
            IsoPrototypeDemo demo,
            Camera camera,
            string context)
        {
            Assert.AreEqual(expected.Turn, demo.DebugTurnNumber, $"{context}: 턴");
            Assert.AreEqual(expected.ActiveFloor, demo.ActiveFloorIndex, $"{context}: 활성 층");
            Assert.AreEqual(expected.Player, demo.PlayerState.Position, $"{context}: 플레이어");
            Assert.AreEqual(expected.ViewQuarterTurns, demo.ViewQuarterTurns, $"{context}: 시점");
            Assert.AreEqual(
                expected.CameraSize,
                camera.orthographicSize,
                0.0001f,
                $"{context}: 월드 카메라 배율");
            CollectionAssert.AreEquivalent(
                expected.Visible,
                GetField<HashSet<GridPos>>(demo, "_visibleTiles"),
                $"{context}: 현재 FOV");
            CollectionAssert.AreEquivalent(
                expected.Explored,
                GetField<HashSet<GridPos>>(demo, "_exploredTiles"),
                $"{context}: 탐색 기억");
            CollectionAssert.AreEquivalent(
                expected.Mapped,
                GetField<HashSet<GridPos>>(demo, "_mappedTiles"),
                $"{context}: 지도 실루엣");
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

        private static Button FindFloorButton(VisualElement root, string label)
        {
            VisualElement list = root.Q<VisualElement>("tactical-map-floor-list");
            Assert.NotNull(list);
            foreach (VisualElement element in list.Children())
            {
                if (element is Button button && button.text == label)
                    return button;
            }

            return null;
        }

        private IEnumerator PressHudKey(Keyboard keyboard, ButtonControl key)
        {
            keyboard.MakeCurrent();
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        private static T GetField<T>(IsoPrototypeDemo demo, string name)
        {
            FieldInfo field = typeof(IsoPrototypeDemo).GetField(name, PrivateInstance);
            Assert.NotNull(field, name);
            return (T)field.GetValue(demo);
        }

        private static void InvokeClick(Button button)
        {
            Assert.NotNull(button);
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(invoke, "Unity UI Toolkit Clickable.Invoke contract changed.");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static bool IsOpen(VisualElement element) =>
            element != null && element.ClassListContains("is-open");

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }

        private readonly struct WorldSnapshot
        {
            public int Turn { get; }
            public int ActiveFloor { get; }
            public GridPos Player { get; }
            public int ViewQuarterTurns { get; }
            public float CameraSize { get; }
            public HashSet<GridPos> Visible { get; }
            public HashSet<GridPos> Explored { get; }
            public HashSet<GridPos> Mapped { get; }

            public WorldSnapshot(
                int turn,
                int activeFloor,
                GridPos player,
                int viewQuarterTurns,
                float cameraSize,
                HashSet<GridPos> visible,
                HashSet<GridPos> explored,
                HashSet<GridPos> mapped)
            {
                Turn = turn;
                ActiveFloor = activeFloor;
                Player = player;
                ViewQuarterTurns = viewQuarterTurns;
                CameraSize = cameraSize;
                Visible = visible;
                Explored = explored;
                Mapped = mapped;
            }
        }
    }
}
