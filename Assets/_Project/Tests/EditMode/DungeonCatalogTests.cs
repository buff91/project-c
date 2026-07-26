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
