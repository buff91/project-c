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
            demo.LookDown();
            Assert.AreEqual(VerticalLookMode.Down, demo.VerticalLook);
            Assert.AreEqual(activeFloor - 1, demo.ViewedFloorIndex);
            Assert.AreEqual(activeFloor, demo.ActiveFloorIndex);
            Assert.AreEqual(turn, demo.DebugTurnNumber, "층 보기는 무턴이어야 한다");

            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(hud);
            VisualElement hudRoot = hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsFalse(hudRoot.Q<Button>("wait-button").enabledInHierarchy);
            Assert.IsFalse(hudRoot.Q<Button>("combat-button").enabledInHierarchy);
            Assert.IsFalse(hudRoot.Q<Button>("potion-button").enabledInHierarchy);
            Assert.AreEqual(
                $"플레이 {demo.ActiveFloorLabel} · 보기 {demo.ViewedFloorLabel}",
                hudRoot.Q<Label>("vertical-view-state").text);
            Assert.AreEqual("관찰", hudRoot.Q<Label>(className: "turn-label").text);

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
            Assert.AreEqual("내 턴", hudRoot.Q<Label>(className: "turn-label").text);
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

        private static T GetField<T>(IsoPrototypeDemo demo, string name)
        {
            FieldInfo field = typeof(IsoPrototypeDemo).GetField(name, PrivateInstance);
            Assert.NotNull(field, name);
            return (T)field.GetValue(demo);
        }

        private static void InvokeTileTap(IsoPrototypeDemo demo, GridPos target)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "HandleTileTapped", PrivateInstance);
            Assert.NotNull(method);
            method.Invoke(demo, new object[] { target, true });
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }
    }
}
