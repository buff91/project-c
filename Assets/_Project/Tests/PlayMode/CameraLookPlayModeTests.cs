using System.Collections;
using System.Collections.Generic;
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
    public sealed class CameraLookPlayModeTests
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
        public IEnumerator Pan_IsReadOnly_PreservesScale_AndActionsRestoreFollow()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Camera camera = Camera.main;
            Assert.NotNull(demo);
            Assert.NotNull(camera);
            Assert.IsFalse(demo.hubMode);
            yield return null;

            Vector3 followPosition = camera.transform.position;
            float followSize = camera.orthographicSize;
            int turnBefore = demo.DebugTurnNumber;
            int floorBefore = demo.ActiveFloorIndex;
            GridPos playerBefore = demo.PlayerState.Position;
            var visibleBefore = new HashSet<GridPos>(
                GetField<HashSet<GridPos>>(demo, "_visibleTiles"));
            var exploredBefore = new HashSet<GridPos>(
                GetField<HashSet<GridPos>>(demo, "_exploredTiles"));

            int viewBefore = demo.ViewQuarterTurns;
            for (int i = 0; i < 4; i++)
            {
                demo.RotateView(1);
                Assert.AreEqual(
                    followSize,
                    camera.orthographicSize,
                    0.0001f,
                    $"B2 히어로룸 Q 회전 {i + 1}회에서 배율이 바뀌면 안 된다");
                Assert.AreEqual(turnBefore, demo.DebugTurnNumber);
                Assert.AreEqual(floorBefore, demo.ActiveFloorIndex);
                Assert.AreEqual(playerBefore, demo.PlayerState.Position);
            }
            Assert.AreEqual(viewBefore, demo.ViewQuarterTurns);
            CollectionAssert.AreEquivalent(
                visibleBefore,
                GetField<HashSet<GridPos>>(demo, "_visibleTiles"));
            CollectionAssert.AreEquivalent(
                exploredBefore,
                GetField<HashSet<GridPos>>(demo, "_exploredTiles"));

            bool moved = false;
            foreach (Vector2 drag in new[]
                     {
                         new Vector2(180f, 0f),
                         new Vector2(-180f, 0f),
                         new Vector2(0f, 120f),
                         new Vector2(0f, -120f)
                     })
            {
                demo.RecenterCamera();
                Assert.IsTrue(demo.TryPanCamera(drag));
                if ((camera.transform.position - followPosition).sqrMagnitude > 0.0001f)
                {
                    moved = true;
                    break;
                }
            }

            Assert.IsTrue(moved, "탐색된 시작방 안에서 카메라 중심이 움직여야 한다");
            Assert.IsTrue(demo.IsCameraLookingAround);
            Assert.AreEqual(turnBefore, demo.DebugTurnNumber, "카메라 팬은 무턴이어야 한다");
            Assert.AreEqual(floorBefore, demo.ActiveFloorIndex);
            Assert.AreEqual(playerBefore, demo.PlayerState.Position);
            Assert.AreEqual(followSize, camera.orthographicSize, 0.0001f, "팬은 줌을 바꾸지 않는다");
            CollectionAssert.AreEquivalent(
                visibleBefore,
                GetField<HashSet<GridPos>>(demo, "_visibleTiles"),
                "카메라 팬이 FOV를 다시 계산하면 안 된다");
            CollectionAssert.AreEquivalent(
                exploredBefore,
                GetField<HashSet<GridPos>>(demo, "_exploredTiles"),
                "카메라 팬이 새 타일을 탐색하면 안 된다");

            SpriteRenderer atmosphere =
                GetField<SpriteRenderer>(demo, "_dungeonAtmosphereBackdrop");
            if (atmosphere != null)
            {
                Assert.AreEqual(camera.transform.position.x, atmosphere.transform.position.x, 0.0001f);
                Assert.AreEqual(camera.transform.position.y, atmosphere.transform.position.y, 0.0001f);
            }

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            Button minimapPlayerMarker = hud.GetComponent<UIDocument>()
                .rootVisualElement.Q<Button>("minimap-player-marker");
            Assert.NotNull(minimapPlayerMarker);
            Assert.AreEqual(DisplayStyle.Flex, minimapPlayerMarker.resolvedStyle.display);
            Assert.Greater(minimapPlayerMarker.worldBound.width, 0f);
            Assert.Greater(minimapPlayerMarker.worldBound.height, 0f);
            InvokeClick(minimapPlayerMarker);
            Assert.IsFalse(demo.IsCameraLookingAround);
            Assert.AreEqual(followPosition.x, camera.transform.position.x, 0.0001f);
            Assert.AreEqual(followPosition.y, camera.transform.position.y, 0.0001f);
            Assert.AreEqual(followSize, camera.orthographicSize, 0.0001f);

            Assert.IsTrue(demo.TryPanCamera(new Vector2(180f, 0f)));
            demo.enabled = false;
            Assert.IsFalse(demo.IsCameraLookingAround, "disable은 팬 상태를 남기면 안 된다");
            Assert.AreEqual(followPosition.x, camera.transform.position.x, 0.0001f);
            Assert.AreEqual(followPosition.y, camera.transform.position.y, 0.0001f);
            demo.enabled = true;
            yield return null;

            int floorBeforeJump = demo.ActiveFloorIndex;
            Assert.IsTrue(demo.TryPanCamera(new Vector2(-180f, 0f)));
            demo.DebugJumpFloor(1);
            Assert.AreEqual(floorBeforeJump + 1, demo.ActiveFloorIndex);
            Assert.IsFalse(demo.IsCameraLookingAround, "개발 층 점프도 새 층에서 추종을 되찾아야 한다");

            Assert.IsTrue(demo.TryPanCamera(new Vector2(180f, 0f)));
            demo.RotateView(1);
            Assert.IsFalse(demo.IsCameraLookingAround, "시점 회전은 플레이어 추적으로 복귀해야 한다");
            Assert.AreEqual(turnBefore, demo.DebugTurnNumber, "시점 회전도 무턴이어야 한다");

            Assert.IsTrue(demo.TryPanCamera(new Vector2(-180f, 0f)));
            demo.WaitTurn();
            Assert.IsFalse(demo.IsCameraLookingAround, "수락된 플레이어 행동은 추적으로 복귀해야 한다");

            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                demo.DebugTurnNumber > turnBefore || Time.realtimeSinceStartup >= deadline);
            Assert.AreEqual(turnBefore + 1, demo.DebugTurnNumber);
        }

        [UnityTest]
        public IEnumerator B2DoorBoundary_KeepsFixedPlayScaleAcrossAllViews()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Camera camera = Camera.main;
            Assert.NotNull(demo);
            Assert.NotNull(camera);
            Assert.IsFalse(demo.hubMode);
            yield return null;

            GridManager grid = GetField<GridManager>(demo, "_grid");
            B2HeroRoomLayout room = GetField<B2HeroRoomLayout>(demo, "_b2HeroRoomLayout");
            Assert.NotNull(room, "기본 B2에는 시작 히어로룸 배치가 있어야 한다");
            Assert.IsTrue(
                TryFindDoorBoundary(grid, room, out GridPos inside, out GridPos door, out GridPos outside),
                "B2 시작방과 외부를 잇는 문 경계를 찾지 못했다");

            int turnBefore = demo.DebugTurnNumber;
            int floorBefore = demo.ActiveFloorIndex;
            int viewBefore = demo.ViewQuarterTurns;
            float expectedSize = demo.playCameraSize;
            Assert.AreEqual(2.3f, expectedSize, 0.0001f,
                "PC 허브/던전 공용 플레이 배율의 씬 계약이 바뀌면 안 된다");
            for (int view = 0; view < 4; view++)
            {
                PositionPlayer(demo, grid, inside);
                Assert.AreEqual(
                    expectedSize,
                    camera.orthographicSize,
                    0.0001f,
                    $"q{demo.ViewQuarterTurns} 시작방 안에서 기본 플레이 배율이어야 한다");

                AssertDestinationCameraSize(demo, camera, door, expectedSize);
                AssertDestinationCameraSize(demo, camera, outside, expectedSize);

                PositionPlayer(demo, grid, outside);
                Assert.AreEqual(
                    expectedSize,
                    camera.orthographicSize,
                    0.0001f,
                    $"q{demo.ViewQuarterTurns} 문 밖에서 기본 플레이 배율이어야 한다");

                AssertDestinationCameraSize(demo, camera, door, expectedSize);
                AssertDestinationCameraSize(demo, camera, inside, expectedSize);
                demo.RotateView(1);
            }

            Assert.AreEqual(viewBefore, demo.ViewQuarterTurns);
            Assert.AreEqual(turnBefore, demo.DebugTurnNumber, "카메라 경계 검증은 턴을 쓰면 안 된다");
            Assert.AreEqual(floorBefore, demo.ActiveFloorIndex);
        }

        private static bool TryFindDoorBoundary(
            GridManager grid,
            B2HeroRoomLayout room,
            out GridPos inside,
            out GridPos door,
            out GridPos outside)
        {
            inside = default;
            door = default;
            outside = default;
            foreach (KeyValuePair<GridPos, TileData> pair in grid.Map.All())
            {
                TileKind kind = pair.Value.kind;
                if (kind != TileKind.DoorClosed && kind != TileKind.DoorOpen) continue;

                if (TryUseOppositeSides(
                        grid,
                        room,
                        pair.Key.North,
                        pair.Key.South,
                        out inside,
                        out outside) ||
                    TryUseOppositeSides(
                        grid,
                        room,
                        pair.Key.East,
                        pair.Key.West,
                        out inside,
                        out outside))
                {
                    door = pair.Key;
                    return true;
                }
            }

            return false;
        }

        private static bool TryUseOppositeSides(
            GridManager grid,
            B2HeroRoomLayout room,
            GridPos first,
            GridPos second,
            out GridPos inside,
            out GridPos outside)
        {
            inside = default;
            outside = default;
            if (room.ContainsRoomCell(first) &&
                !room.ContainsRoomCell(second) &&
                grid.Map.IsWalkable(second))
            {
                inside = first;
                outside = second;
                return true;
            }

            if (room.ContainsRoomCell(second) &&
                !room.ContainsRoomCell(first) &&
                grid.Map.IsWalkable(first))
            {
                inside = second;
                outside = first;
                return true;
            }

            return false;
        }

        private static void AssertDestinationCameraSize(
            IsoPrototypeDemo demo,
            Camera camera,
            GridPos destination,
            float expectedSize)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "TryGetPlayerCameraFrame",
                PrivateInstance);
            Assert.NotNull(method);
            object[] arguments = { camera, destination, default(OrthographicCameraFrame) };
            Assert.IsTrue((bool)method.Invoke(demo, arguments));
            OrthographicCameraFrame frame = (OrthographicCameraFrame)arguments[2];
            Assert.AreEqual(
                expectedSize,
                frame.Size,
                0.0001f,
                $"문 경계 목적지 {destination}의 보행 카메라 배율이 달라지면 안 된다");
        }

        private static void PositionPlayer(
            IsoPrototypeDemo demo,
            GridManager grid,
            GridPos position)
        {
            demo.PlayerState.MoveTo(position);
            GameObject player = GetField<GameObject>(demo, "_player");
            player.transform.position = grid.GridToWorld(position);

            MethodInfo sync = typeof(IsoPrototypeDemo).GetMethod(
                "SyncPlayerView",
                PrivateInstance);
            Assert.NotNull(sync);
            sync.Invoke(demo, new object[] { position, false });
        }

        private static T GetField<T>(IsoPrototypeDemo demo, string name)
        {
            FieldInfo field = typeof(IsoPrototypeDemo).GetField(name, PrivateInstance);
            Assert.NotNull(field, name);
            return (T)field.GetValue(demo);
        }

        private static void InvokeClick(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(invoke, "Unity UI Toolkit Clickable.Invoke contract changed.");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }
    }
}
