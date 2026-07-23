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
        }

        [Test]
        public void ById_UnknownId_FallsBackToDefault()
        {
            Assert.AreSame(
                DungeonCatalog.ById(DungeonCatalog.DefaultId),
                DungeonCatalog.ById("missing-dungeon"));
        }
    }
}
