using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class HeroRosterTests
    {
        [Test]
        public void All_HasUniqueIds_AndValidStats()
        {
            var seen = new HashSet<string>();
            foreach (HeroArchetype hero in HeroRoster.All)
            {
                Assert.IsTrue(seen.Add(hero.Id), $"중복 id: {hero.Id}");
                Assert.Greater(hero.MaxHp, 0, hero.Id);
                Assert.Greater(hero.Attack, 0, hero.Id);
                Assert.Greater(hero.RangedDamage, 0, hero.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(hero.DisplayName), hero.Id);
            }
        }

        [Test]
        public void ById_FindsHero_AndFallsBackToFirst()
        {
            Assert.AreEqual("ranger", HeroRoster.ById("ranger").Id);
            Assert.AreEqual(HeroRoster.All[0].Id, HeroRoster.ById(null).Id);
            Assert.AreEqual(HeroRoster.All[0].Id, HeroRoster.ById("no-such-hero").Id);
        }
    }
}
