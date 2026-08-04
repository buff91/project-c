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
    public sealed class DungeonNavigationPlayModeTests
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
        public IEnumerator TilePicker_PicksMappedUnknownFloorBehindClosedDoor()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            Assert.IsFalse(demo.hubMode);
            yield return null;

            DoorRouteFixture route = PrepareDoorRoute(demo);
            IsoTapInput input = demo.GetComponent<IsoTapInput>();
            Camera camera = Camera.main;
            Assert.NotNull(input);
            Assert.NotNull(camera);
            Assert.NotNull(input.TilePicker, "실제 월드 타일 picker가 입력 레이어에 연결돼야 한다");

            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            Assert.AreEqual(
                demo.ActiveFloorIndex,
                dungeon.Height.FloorIndex(route.Target.elevation),
                "다른 층 실루엣을 현재 층 입력으로 집으면 안 된다");
            Assert.IsTrue(
                GetField<HashSet<GridPos>>(demo, "_mappedTiles").Contains(route.Target),
                "일반 방 바닥은 FOV와 별개로 지도 윤곽에 포함돼야 한다");
            Assert.IsFalse(
                GetField<HashSet<GridPos>>(demo, "_visibleTiles").Contains(route.Target),
                "닫힌 문 너머 목적지는 아직 Visible이면 안 된다");
            Assert.IsFalse(
                GetField<HashSet<GridPos>>(demo, "_exploredTiles").Contains(route.Target),
                "닫힌 문 너머 목적지는 아직 Explored이면 안 된다");
            Dictionary<GridPos, SpriteRenderer> mappedRenderers =
                GetField<Dictionary<GridPos, SpriteRenderer>>(
                    demo,
                    "_mappedSilhouetteRenderers");
            Assert.IsTrue(mappedRenderers.TryGetValue(route.Target, out SpriteRenderer mapped));
            Assert.IsTrue(mapped.enabled);
            StringAssert.StartsWith("Map Knowledge ", mapped.sprite.name,
                "현재 층 Unknown은 다른 층 실제 표면이 아니라 전용 선·점 지도 문법을 써야 한다");

            Vector3 world = InvokeVisualPosition(demo, route.Target);
            Vector3 projected = camera.WorldToScreenPoint(world);
            GridPos? picked = input.TilePicker(new Vector2(projected.x, projected.y));

            Assert.AreEqual(route.Target, picked,
                "Unknown이어도 그려진 현재 층 지도 실루엣은 실제 포인터로 선택돼야 한다");
        }

        [UnityTest]
        public IEnumerator MappedUnknownTap_OpensDoorAsTurn_ThenCrossesToTarget()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            Assert.IsFalse(demo.hubMode);
            yield return null;

            DoorRouteFixture route = PrepareDoorRoute(demo);
            GridManager grid = GetField<GridManager>(demo, "_grid");
            int turnBefore = demo.DebugTurnNumber;
            int doorInteractionsBefore = demo.Telemetry.doorInteractions;

            InvokeTileTap(demo, route.Target);

            float deadline = Time.realtimeSinceStartup + 10f;
            yield return new WaitUntil(() =>
                demo.PlayerState.Position == route.Target &&
                demo.DebugTurnNumber >= turnBefore + 3 &&
                !GetField<bool>(demo, "_resolvingAction") ||
                Time.realtimeSinceStartup >= deadline);

            Assert.Less(Time.realtimeSinceStartup, deadline,
                "지도 자동 이동이 닫힌 문에서 멈추거나 완료되지 않았다");
            Assert.AreEqual(TileKind.DoorOpen, grid.Map.Get(route.Door)?.kind,
                "일반 닫힌 문은 통과 판정만 우회하지 말고 실제 열림 상태가 돼야 한다");
            Assert.AreEqual(route.Target, demo.PlayerState.Position);
            Assert.IsTrue(
                GetField<HashSet<GridPos>>(demo, "_visibleTiles").Contains(route.Target) ||
                GetField<HashSet<GridPos>>(demo, "_exploredTiles").Contains(route.Target),
                "도착한 칸은 실제 FOV 지식으로 승격돼야 한다");
            Assert.AreEqual(
                doorInteractionsBefore + 1,
                demo.Telemetry.doorInteractions,
                "자동 이동의 문 열기도 일반 문 상호작용 계측에 포함돼야 한다");
            Assert.AreEqual(
                turnBefore + 3,
                demo.DebugTurnNumber,
                "문 열기 1턴 + 문 칸 이동 1턴 + 목적지 이동 1턴이어야 한다");
        }

        [UnityTest]
        public IEnumerator MappedUnknownSilhouette_RemembersCategoryAcrossUnseenMutation()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            Assert.IsFalse(demo.hubMode);
            yield return null;

            DoorRouteFixture route = PrepareDoorRoute(demo);
            GridManager grid = GetField<GridManager>(demo, "_grid");
            TileData targetTile = grid.Map.Get(route.Target);
            Assert.NotNull(targetTile);
            Assert.AreEqual(
                MapSilhouetteKind.Floor,
                InvokeMappedSilhouette(demo, route.Target),
                "fixture의 미탐색 목적지는 공용 Floor 범주여야 한다");

            TileKind originalKind = targetTile.kind;
            try
            {
                // WeakFloor 붕괴처럼 플레이어가 보지 못한 곳의 live 상태가 바뀌어도
                // 지도 지식은 마지막으로 공개한 공용 범주를 유지해야 한다.
                targetTile.kind = TileKind.Hole;
                Assert.AreEqual(
                    MapSilhouetteKind.Floor,
                    InvokeMappedSilhouette(demo, route.Target),
                    "시야 밖 live TileKind를 다시 읽으면 Floor→Gap으로 상태가 누설된다");
            }
            finally
            {
                targetTile.kind = originalKind;
            }
        }

        [UnityTest]
        public IEnumerator VisibleEnemyApproach_MovesOneStepWithoutAttacking()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            yield return null;

            Assert.IsTrue(
                TryPrepareVisibleEnemyApproach(
                    demo,
                    out CombatantState target,
                    out List<GridPos> approach),
                "현재 층에서 시야 안 적까지 두 칸 이상 필요한 접근 fixture를 찾지 못했다");

            int hpBefore = target.Hp;
            int turnBefore = demo.DebugTurnNumber;
            GridPos expectedStep = approach[1];
            InvokeTileTap(demo, target.Position);

            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                (!GetField<bool>(demo, "_resolvingAction") &&
                 demo.DebugTurnNumber > turnBefore) ||
                Time.realtimeSinceStartup >= deadline);

            Assert.Less(Time.realtimeSinceStartup, deadline);
            Assert.AreEqual(turnBefore + 1, demo.DebugTurnNumber,
                "위협 중 자동 접근은 플레이어/적 턴 한 쌍만 소비해야 한다");
            Assert.AreEqual(expectedStep, demo.PlayerState.Position,
                "위협 중에는 목적지까지 달리지 않고 첫 걸음에서 멈춰야 한다");
            Assert.AreEqual(hpBefore, target.Hp,
                "한 칸 이동한 입력에서 같은 적을 이어서 공격하면 안 된다");
        }

        [UnityTest]
        public IEnumerator TravelModeChip_RefreshesAfterEnemyPhaseRemovesThreat()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            PrototypeHudController hud = Object.FindAnyObjectByType<PrototypeHudController>();
            Assert.NotNull(demo);
            Assert.NotNull(hud);
            yield return null;

            Assert.IsTrue(
                TryPrepareVisibleEnemyApproach(
                    demo,
                    out _,
                    out List<GridPos> approach));
            GridManager grid = GetField<GridManager>(demo, "_grid");
            PositionPlayer(demo, grid, approach[approach.Count - 1]);
            yield return null;

            Label travelMode = hud.GetComponent<UIDocument>()
                .rootVisualElement.Q<Label>("travel-mode-label");
            Assert.NotNull(travelMode);
            Assert.AreEqual("위협 · 1행동", travelMode.text);

            demo.DebugKillAllOnFloor();
            int turnBefore = demo.DebugTurnNumber;
            demo.WaitTurn();
            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                (!GetField<bool>(demo, "_resolvingAction") &&
                 demo.DebugTurnNumber > turnBefore) ||
                Time.realtimeSinceStartup >= deadline);

            Assert.Less(Time.realtimeSinceStartup, deadline);
            Assert.AreEqual("자동 이동", travelMode.text,
                "적 페이즈 종료 뒤 다음 입력의 실제 이동 예산을 즉시 표시해야 한다");
        }

        [UnityTest]
        public IEnumerator AdjacentRescue_ConsumesOneTurn()
        {
            IsoPrototypeDemo demo = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(demo);
            yield return null;

            Assert.IsTrue(
                TryPositionNextToRescueNpc(demo, out GridPos rescuePosition),
                "구출 NPC 옆의 안전한 접근 칸을 찾지 못했다");
            int turnBefore = demo.DebugTurnNumber;
            InvokeTileTap(demo, rescuePosition);

            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                (!GetField<bool>(demo, "_resolvingAction") &&
                 demo.RescuedThisRun != null) ||
                Time.realtimeSinceStartup >= deadline);

            Assert.Less(Time.realtimeSinceStartup, deadline);
            Assert.IsNotNull(demo.RescuedThisRun);
            Assert.AreEqual(turnBefore + 1, demo.DebugTurnNumber,
                "이미 인접한 구출도 행동 하나와 적 페이즈 하나를 소비해야 한다");
        }

        private static bool TryPrepareVisibleEnemyApproach(
            IsoPrototypeDemo demo,
            out CombatantState target,
            out List<GridPos> approach)
        {
            target = null;
            approach = null;
            GridManager grid = GetField<GridManager>(demo, "_grid");
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            if (!dungeon.TryGetFloor(demo.ActiveFloorIndex, out DungeonFloorInfo floor))
                return false;

            SuppressItemsOnFloor(demo, dungeon, floor.FloorIndex);
            MethodInfo findEnemy = typeof(IsoPrototypeDemo).GetMethod(
                "FindLivingEnemyAt",
                PrivateInstance);
            Assert.NotNull(findEnemy);

            var enemies = new List<object>();
            foreach (object enemy in (IEnumerable)GetUntypedField(demo, "_enemies"))
            {
                enemies.Add(enemy);
                FieldInfo brain = enemy.GetType().GetField("Brain");
                Assert.NotNull(brain);
                brain.SetValue(enemy, null); // 적 페이즈의 위치 변화 없이 이동 예산만 검증한다.
            }

            foreach (object enemy in enemies)
            {
                FieldInfo stateField = enemy.GetType().GetField("State");
                Assert.NotNull(stateField);
                var state = (CombatantState)stateField.GetValue(enemy);
                if (state == null || !state.IsAlive ||
                    dungeon.Height.FloorIndex(state.Position.elevation) != floor.FloorIndex)
                    continue;

                foreach (KeyValuePair<GridPos, TileData> pair in grid.Map.All())
                {
                    GridPos start = pair.Key;
                    if (pair.Value.kind != TileKind.Floor ||
                        dungeon.Height.FloorIndex(start.elevation) != floor.FloorIndex ||
                        findEnemy.Invoke(demo, new object[] { start }) != null)
                        continue;

                    List<GridPos> candidate = InteractionApproachRules.FindPathToAdjacent(
                        grid.Map,
                        start,
                        state.Position,
                        pos => findEnemy.Invoke(demo, new object[] { pos }) != null);
                    if (candidate.Count < 3) continue;

                    PositionPlayer(demo, grid, start);
                    if (!GetField<HashSet<GridPos>>(demo, "_visibleTiles")
                            .Contains(state.Position))
                        continue;

                    target = state;
                    approach = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryPositionNextToRescueNpc(
            IsoPrototypeDemo demo,
            out GridPos rescuePosition)
        {
            rescuePosition = default;
            GridManager grid = GetField<GridManager>(demo, "_grid");
            foreach (object agent in (IEnumerable)GetUntypedField(demo, "_rescueNpcs"))
            {
                FieldInfo positionField = agent.GetType().GetField("Pos");
                FieldInfo floorField = agent.GetType().GetField("FloorIndex");
                Assert.NotNull(positionField);
                Assert.NotNull(floorField);
                GridPos target = (GridPos)positionField.GetValue(agent);
                int floorIndex = (int)floorField.GetValue(agent);

                foreach (GridPos candidate in new[]
                         {
                             target.North,
                             target.East,
                             target.South,
                             target.West
                         })
                {
                    if (!grid.Map.IsWalkable(candidate)) continue;

                    demo.DebugJumpFloor(floorIndex - demo.ActiveFloorIndex);
                    demo.DebugKillAllOnFloor();
                    PositionPlayer(demo, grid, candidate);
                    rescuePosition = target;
                    return true;
                }
            }

            return false;
        }

        private static DoorRouteFixture PrepareDoorRoute(IsoPrototypeDemo demo)
        {
            GridManager grid = GetField<GridManager>(demo, "_grid");
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            Assert.IsTrue(dungeon.TryGetFloor(
                demo.ActiveFloorIndex,
                out DungeonFloorInfo floor));

            demo.DebugKillAllOnFloor();
            SuppressItemsOnFloor(demo, dungeon, floor.FloorIndex);

            HashSet<GridPos> reserved = BuildReservedCells(demo, dungeon, floor);
            foreach (GridPos door in floor.Doors)
            {
                if (grid.Map.Get(door)?.kind != TileKind.DoorClosed) continue;

                foreach (GridPos[] opposite in new[]
                         {
                             new[] { door.North, door.South },
                             new[] { door.East, door.West }
                         })
                {
                    GridPos first = opposite[0];
                    GridPos second = opposite[1];
                    if (!IsPlainUnreservedFloor(grid, dungeon, floor, reserved, first) ||
                        !IsPlainUnreservedFloor(grid, dungeon, floor, reserved, second))
                        continue;

                    if (TryPositionForUnknownTarget(demo, floor, door, first, second))
                        return new DoorRouteFixture(door, first, second);
                    if (TryPositionForUnknownTarget(demo, floor, door, second, first))
                        return new DoorRouteFixture(door, second, first);
                }
            }

            Assert.Fail(
                "생성된 현재 층에서 일반 닫힌 문과 양쪽의 안전한 Floor, " +
                "그리고 문 너머 Unknown 목적지 조합을 찾지 못했다");
            return default;
        }

        private static bool TryPositionForUnknownTarget(
            IsoPrototypeDemo demo,
            DungeonFloorInfo floor,
            GridPos door,
            GridPos start,
            GridPos target)
        {
            DungeonLayout dungeon = GetField<DungeonLayout>(demo, "_dungeon");
            GridManager grid = GetField<GridManager>(demo, "_grid");
            if (!MapKnowledgeRules.TryGetSilhouette(
                    floor,
                    dungeon.Height.FloorIndex(target.elevation),
                    target,
                    grid.Map.Get(target),
                    out MapSilhouetteKind silhouette) ||
                silhouette != MapSilhouetteKind.Floor)
                return false;

            List<GridPos> directPath = GridPathfinder.FindPath(
                grid.Map,
                start,
                target,
                openClosedDoors: true);
            if (directPath.Count != 3 || directPath[1] != door)
                return false;

            HashSet<GridPos> visible = GetField<HashSet<GridPos>>(demo, "_visibleTiles");
            HashSet<GridPos> explored = GetField<HashSet<GridPos>>(demo, "_exploredTiles");
            visible.Clear();
            explored.Clear();
            PositionPlayer(demo, grid, start);

            return visible.Contains(start) &&
                   visible.Contains(start.ManhattanTo(target) == 2
                       ? new GridPos(
                           (start.x + target.x) / 2,
                           (start.y + target.y) / 2,
                           start.elevation)
                       : start) &&
                   !visible.Contains(target) &&
                   !explored.Contains(target);
        }

        private static bool IsPlainUnreservedFloor(
            GridManager grid,
            DungeonLayout dungeon,
            DungeonFloorInfo floor,
            HashSet<GridPos> reserved,
            GridPos position)
        {
            return dungeon.Height.FloorIndex(position.elevation) == floor.FloorIndex &&
                   grid.Map.Get(position)?.kind == TileKind.Floor &&
                   !reserved.Contains(position);
        }

        private static HashSet<GridPos> BuildReservedCells(
            IsoPrototypeDemo demo,
            DungeonLayout dungeon,
            DungeonFloorInfo floor)
        {
            var reserved = new HashSet<GridPos>
            {
                floor.Entry
            };
            Add(reserved, floor.UpStairs);
            Add(reserved, floor.DownStairs);
            Add(reserved, floor.RestSite);
            Add(reserved, floor.ExtractionPoint);
            Add(reserved, floor.Landmark);
            Add(reserved, floor.SecretDoor);
            Add(reserved, floor.SecretReward);
            Add(reserved, floor.ElevatorShaft);
            Add(reserved, floor.ElevatorLanding);
            Add(reserved, floor.RescueNpc);
            foreach (GridPos position in floor.HoleTiles) reserved.Add(position);
            foreach (GridPos position in floor.SecretRoomTiles) reserved.Add(position);
            foreach (GridPos position in floor.Windows) reserved.Add(position);
            foreach (ItemSpawn item in floor.Items) reserved.Add(item.Position);

            bool barrelExploded = GetField<bool>(demo, "_barrelExploded");
            GridPos barrel = GetField<GridPos>(demo, "_barrelPos");
            if (!barrelExploded &&
                dungeon.Height.FloorIndex(barrel.elevation) == floor.FloorIndex)
                reserved.Add(barrel);

            return reserved;
        }

        private static void SuppressItemsOnFloor(
            IsoPrototypeDemo demo,
            DungeonLayout dungeon,
            int floorIndex)
        {
            object items = GetUntypedField(demo, "_items");
            foreach (object item in (IEnumerable)items)
            {
                TypeInfo type = item.GetType().GetTypeInfo();
                FieldInfo spawnField = type.GetField("Spawn");
                FieldInfo collectedField = type.GetField("Collected");
                FieldInfo rootField = type.GetField("Root");
                Assert.NotNull(spawnField);
                Assert.NotNull(collectedField);
                Assert.NotNull(rootField);

                ItemSpawn spawn = (ItemSpawn)spawnField.GetValue(item);
                if (dungeon.Height.FloorIndex(spawn.Position.elevation) != floorIndex)
                    continue;

                collectedField.SetValue(item, true);
                if (rootField.GetValue(item) is GameObject root)
                    root.SetActive(false);
            }
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

        private static Vector3 InvokeVisualPosition(
            IsoPrototypeDemo demo,
            GridPos position)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "VisualPosition",
                PrivateInstance);
            Assert.NotNull(method);
            return (Vector3)method.Invoke(demo, new object[] { position });
        }

        private static MapSilhouetteKind InvokeMappedSilhouette(
            IsoPrototypeDemo demo,
            GridPos position)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "TryGetMappedSilhouette",
                PrivateInstance);
            Assert.NotNull(method);
            object[] arguments = { position, default(MapSilhouetteKind) };
            Assert.IsTrue((bool)method.Invoke(demo, arguments));
            return (MapSilhouetteKind)arguments[1];
        }

        private static void InvokeTileTap(IsoPrototypeDemo demo, GridPos target)
        {
            MethodInfo method = typeof(IsoPrototypeDemo).GetMethod(
                "HandleTileTapped",
                PrivateInstance);
            Assert.NotNull(method);
            method.Invoke(demo, new object[] { target, true });
        }

        private static T GetField<T>(IsoPrototypeDemo demo, string name) =>
            (T)GetUntypedField(demo, name);

        private static object GetUntypedField(IsoPrototypeDemo demo, string name)
        {
            FieldInfo field = typeof(IsoPrototypeDemo).GetField(name, PrivateInstance);
            Assert.NotNull(field, name);
            return field.GetValue(demo);
        }

        private static void Add(HashSet<GridPos> cells, GridPos? position)
        {
            if (position.HasValue) cells.Add(position.Value);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }

        private readonly struct DoorRouteFixture
        {
            public DoorRouteFixture(GridPos door, GridPos start, GridPos target)
            {
                Door = door;
                Start = start;
                Target = target;
            }

            public GridPos Door { get; }
            public GridPos Start { get; }
            public GridPos Target { get; }
        }
    }
}
