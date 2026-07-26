using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class DungeonCatalogTests
    {
        [Test]
        public void Catalog_HasUniqueIdsAndOnePlayableDefault()
        {
            Assert.AreEqual(DungeonCatalog.All.Count,
                DungeonCatalog.All.Select(dungeon => dungeon.Id).Distinct().Count());

            DungeonDefinition selected = DungeonCatalog.ById(DungeonCatalog.DefaultId);
            Assert.IsTrue(selected.IsAvailable);
            Assert.Greater(selected.Seed, 0);
            Assert.AreEqual(10, selected.FloorCount);
            Assert.NotNull(selected.Boss);
            Assert.AreSame(MonsterRoster.GraveWarden, selected.Boss.Archetype);
            StringAssert.DoesNotContain("기사", selected.RouteLabel);
            StringAssert.Contains("추락 대비 장비", selected.RouteLabel);
        }

        [Test]
        public void ById_UnknownId_FallsBackToDefault()
        {
            Assert.AreSame(
                DungeonCatalog.ById(DungeonCatalog.DefaultId),
                DungeonCatalog.ById("missing-dungeon"));
        }

        [Test]
        public void FirstDungeonExit_RequiresBossDefeat()
        {
            DungeonDefinition dungeon = DungeonCatalog.ById(DungeonCatalog.DefaultId);

            Assert.IsFalse(DungeonBossRules.CanUseExit(dungeon, bossDefeated: false));
            Assert.IsTrue(DungeonBossRules.CanUseExit(dungeon, bossDefeated: true));
        }

        [Test]
        public void FloodedVault_IsPlayableAndUsesBosslessInwardRules()
        {
            DungeonDefinition dungeon = DungeonCatalog.ById("flooded-vault");

            Assert.IsTrue(dungeon.IsAvailable);
            Assert.AreEqual(DungeonRegionProfile.Flooded, dungeon.Region);
            Assert.AreEqual(DungeonProgressDirection.Inward, dungeon.Direction);
            Assert.IsNull(dungeon.Boss);
            Assert.IsTrue(dungeon.HasEntryCue);
            StringAssert.Contains("냉기 장비", dungeon.RouteLabel);
            Assert.IsTrue(DungeonBossRules.CanUseExit(dungeon, bossDefeated: false));

            Assert.IsFalse(DungeonCatalog.ById("ember-keep").IsAvailable);
        }

        /// <summary>
        /// 보스 표시명은 아키타입 하나에서만 나온다. 예전에는 아키타입이 <c>displayName</c>을
        /// 안 받아 <c>GraveWarden.DisplayName</c>이 코드 ID 로 떨어졌고, 화면 문자열 "감시자"는
        /// 카탈로그에 따로 박혀 있었다 — 한쪽만 고치면 조용히 갈라진다.
        /// </summary>
        [Test]
        public void BossDisplayName_ComesFromTheArchetype()
        {
            Assert.AreEqual("감시자", MonsterRoster.GraveWarden.DisplayName,
                "아키타입이 표시명을 들지 않으면 코드 ID 가 화면에 뜬다.");

            DungeonBossDefinition boss = DungeonCatalog.ById(DungeonCatalog.DefaultId).Boss;
            Assert.AreEqual(MonsterRoster.GraveWarden.DisplayName, boss.DisplayName);
        }

        [Test]
        public void BossSpawn_UsesCandidateFarthestFromEntry()
        {
            var entry = new GridPos(1, 1, 0);
            var candidates = new[]
            {
                new GridPos(2, 1, 0),
                new GridPos(8, 8, 0),
                new GridPos(4, 5, 0)
            };

            Assert.IsTrue(DungeonBossRules.TrySelectSpawn(entry, candidates, out GridPos spawn));
            Assert.AreEqual(candidates[1], spawn);
        }
    }
}
