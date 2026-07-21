using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class WaterRulesTests
    {
        private static GridMap MakeFloor(int size = 9)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        private static void Wet(GridMap map, params (int x, int y)[] cells)
        {
            foreach ((int x, int y) in cells)
                map.Get(new GridPos(x, y, 0)).wet = true;
        }

        [Test]
        public void ChainFreeze_SpreadsAcrossConnectedPuddle_BeyondBlast()
        {
            GridMap map = MakeFloor();
            // 폭발 반경(중심 4,4의 3×3) 안은 (5,4) 하나뿐이지만, 웅덩이는 동쪽으로 이어진다.
            Wet(map, (5, 4), (6, 4), (7, 4), (7, 5));

            var frozen = WaterRules.ChainFreeze(map, new GridPos(4, 4, 0));

            Assert.AreEqual(4, frozen.Count);
        }

        [Test]
        public void ChainFreeze_IgnoresDisconnectedPuddles()
        {
            GridMap map = MakeFloor();
            Wet(map, (5, 4));          // 반경 안 웅덩이
            Wet(map, (0, 0), (1, 0));  // 멀리 떨어진 별개 웅덩이

            var frozen = WaterRules.ChainFreeze(map, new GridPos(4, 4, 0));

            Assert.AreEqual(1, frozen.Count);
            Assert.AreEqual(new GridPos(5, 4, 0), frozen[0]);
        }

        [Test]
        public void ChainFreeze_NoWetInBlast_ReturnsEmpty()
        {
            GridMap map = MakeFloor();
            Wet(map, (8, 8));

            Assert.IsEmpty(WaterRules.ChainFreeze(map, new GridPos(1, 1, 0)));
        }

        [Test]
        public void Evaporate_DriesOnlyBlastRadius()
        {
            GridMap map = MakeFloor();
            Wet(map, (4, 4), (5, 4), (7, 4));

            var dried = WaterRules.Evaporate(map, new GridPos(4, 4, 0));

            Assert.AreEqual(2, dried.Count);
            Assert.IsFalse(map.Get(new GridPos(4, 4, 0)).wet);
            Assert.IsFalse(map.Get(new GridPos(5, 4, 0)).wet);
            Assert.IsTrue(map.Get(new GridPos(7, 4, 0)).wet);
        }

        [Test]
        public void OilSplash_SkipsWetTiles()
        {
            GridMap map = MakeFloor();
            Wet(map, (4, 4));

            var splashed = OilRules.Splash(map, new GridPos(4, 4, 0));

            Assert.IsFalse(map.Get(new GridPos(4, 4, 0)).oiled);
            Assert.IsFalse(splashed.Contains(new GridPos(4, 4, 0)));
            Assert.IsTrue(map.Get(new GridPos(5, 4, 0)).oiled);
        }

        [Test]
        public void GeneratedDungeons_PuddlesAvoidEntryAndSpecialTiles()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var map = new GridMap();
                DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, seed: seed);

                foreach (var floor in dungeon.Floors)
                    Assert.IsFalse(map.Get(floor.Entry).wet, $"seed {seed}: 입구가 젖어 있음");

                foreach (var pair in map.All())
                {
                    if (!pair.Value.wet) continue;
                    Assert.AreEqual(TileKind.Floor, pair.Value.kind,
                        $"seed {seed}: {pair.Key} 특수 타일이 젖어 있음");
                }
            }
        }
    }
}
