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
