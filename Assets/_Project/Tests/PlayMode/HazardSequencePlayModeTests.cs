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
    public sealed class HazardSequencePlayModeTests
    {
        private static readonly MethodInfo ResolveExplosionMethod =
            typeof(IsoPrototypeDemo).GetMethod(
                "ResolveExplosion",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(GridPos), typeof(int), typeof(bool) },
                modifiers: null);

        private static readonly FieldInfo BarrelExplodedField =
            typeof(IsoPrototypeDemo).GetField(
                "_barrelExploded",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BarrelPositionField =
            typeof(IsoPrototypeDemo).GetField(
                "_barrelPos",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo GridField =
            typeof(IsoPrototypeDemo).GetField(
                "_grid",
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
        public IEnumerator FireExplosion_AfterGodModeRestoresHp_StillAppliesBurn()
        {
            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.NotNull(ResolveExplosionMethod);
            Assert.NotNull(BarrelExplodedField);
            Assert.IsFalse(dungeon.PlayerState.Statuses.Has(StatusKind.Burn));

            dungeon.DebugToggleGodMode();
            Assert.IsTrue(dungeon.DebugGodMode);
            int hpBefore = dungeon.PlayerState.Hp;

            // 이 테스트는 한 번의 폭발 단계만 검증한다. 생성된 배럴 위치와 무관하게
            // 유폭 재귀를 닫아 갓 모드 복구 → 상태 계획 순서만 고정한다.
            BarrelExplodedField.SetValue(dungeon, true);
            yield return RunExplosion(dungeon, dungeon.PlayerPos, hpBefore, fiery: true);

            Assert.AreEqual(hpBefore, dungeon.PlayerState.Hp, "갓 모드는 폭발 피해를 되돌린다");
            Assert.IsTrue(
                dungeon.PlayerState.Statuses.Has(StatusKind.Burn),
                "상태 대상은 피격 연출 뒤 다시 계산해 복구된 플레이어도 포함해야 한다");
            Assert.AreEqual(
                HazardSequenceService.ExplosionStatusTurns,
                dungeon.PlayerState.Statuses.RemainingTurns(StatusKind.Burn));
        }

        [UnityTest]
        public IEnumerator ExplosionKnockbackIntoHole_ContinuesThroughGameplayFallSequence()
        {
            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            yield return RunKnockbackFallScenario(dungeon, TileKind.Hole);
        }

        [UnityTest]
        public IEnumerator ExplosionKnockbackOntoWeakFloor_CollapsesAndContinuesIntoFall()
        {
            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            yield return RunKnockbackFallScenario(dungeon, TileKind.WeakFloor);
        }

        [UnityTest]
        public IEnumerator BarrelAtBlastCenter_ChainsOnlyForFire()
        {
            IsoPrototypeDemo dungeon = Object.FindAnyObjectByType<IsoPrototypeDemo>();
            Assert.NotNull(dungeon);
            Assert.NotNull(BarrelPositionField);
            GridPos barrelPosition = dungeon.PlayerPos;
            BarrelPositionField.SetValue(dungeon, barrelPosition);
            int hpBefore = dungeon.PlayerState.Hp;

            Assert.IsFalse((bool)BarrelExplodedField.GetValue(dungeon));

            yield return RunExplosion(dungeon, barrelPosition, damage: 0, fiery: false);
            Assert.IsFalse((bool)BarrelExplodedField.GetValue(dungeon),
                "냉기 폭발은 배럴을 유폭하지 않는다");
            Assert.AreEqual(hpBefore, dungeon.PlayerState.Hp,
                "냉기 폭발은 같은 위치의 배럴을 통한 2차 피해가 없다");

            yield return RunExplosion(dungeon, barrelPosition, damage: 0, fiery: true);
            Assert.IsTrue((bool)BarrelExplodedField.GetValue(dungeon),
                "불 폭발은 반경 안 배럴을 한 번 유폭한다");
            Assert.AreEqual(hpBefore - dungeon.bombDamage, dungeon.PlayerState.Hp,
                "바깥 폭발 피해가 0이어도 배럴 재귀 폭발은 bombDamage를 적용해야 한다");
        }

        private static IEnumerator RunKnockbackFallScenario(
            IsoPrototypeDemo dungeon,
            TileKind trigger)
        {
            Assert.NotNull(GridField);
            Assert.NotNull(BarrelExplodedField);
            GridManager grid = GridField.GetValue(dungeon) as GridManager;
            Assert.NotNull(grid);

            int bottomElevation = dungeon.PlayerPos.elevation;
            int fallsBefore = dungeon.Telemetry.playerFalls;
            dungeon.DebugJumpFloor(1);
            yield return null;
            Assert.AreEqual(1, dungeon.ActiveFloorIndex);

            dungeon.DebugKillAllOnFloor();
            GridPos start = dungeon.PlayerPos;
            GridPos center = start.Offset(-1, 0);
            GridPos hazard = start.Offset(1, 0);
            grid.Map.Set(hazard, trigger);
            grid.Map.Set(hazard.WithElevation(bottomElevation), TileKind.Floor);
            BarrelExplodedField.SetValue(dungeon, true);

            yield return RunExplosion(dungeon, center, damage: 0, fiery: false);

            Assert.AreEqual(TileKind.Hole, grid.Map.Get(hazard).kind,
                "약한 바닥은 충격 시 구멍으로 바뀌고 기존 구멍은 유지된다");
            Assert.AreEqual(0, dungeon.ActiveFloorIndex);
            Assert.AreEqual(bottomElevation, dungeon.PlayerPos.elevation);
            Assert.AreEqual(fallsBefore + 1, dungeon.Telemetry.playerFalls,
                "폭발 넉백이 실제 FallPlayer 배선까지 이어져야 한다");
        }

        private static IEnumerator RunExplosion(
            IsoPrototypeDemo dungeon,
            GridPos center,
            int damage,
            bool fiery)
        {
            Assert.NotNull(ResolveExplosionMethod);
            var explosion = ResolveExplosionMethod.Invoke(
                dungeon,
                new object[] { center, damage, fiery }) as IEnumerator;
            Assert.NotNull(explosion);
            yield return dungeon.StartCoroutine(explosion);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
            yield return null;
        }
    }
}
