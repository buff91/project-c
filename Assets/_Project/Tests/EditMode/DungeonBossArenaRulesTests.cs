using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>최심층 보스 아레나 판정과, 생성기가 최심층에만 두는 랜드마크(제단)를 고정한다.</summary>
    public class DungeonBossArenaRulesTests
    {
        [TestCase(0, 10, false)]
        [TestCase(8, 10, false)]
        [TestCase(9, 10, true)]
        [TestCase(1, 2, true)]
        [TestCase(0, 1, true)]
        public void IsArenaFloor_OnlyDeepestFloor(int depth, int floorCount, bool expected)
        {
            Assert.AreEqual(expected, DungeonBossArenaRules.IsArenaFloor(depth, floorCount));
        }

        [TestCase(8, 10, true)]
        [TestCase(9, 10, false)]
        [TestCase(0, 2, true)]
        [TestCase(0, 1, false)]
        public void IsApproachFloor_OnlyOneAboveArena(int depth, int floorCount, bool expected)
        {
            Assert.AreEqual(expected, DungeonBossArenaRules.IsApproachFloor(depth, floorCount));
        }

        [Test]
        public void Generator_PlacesLandmark_OnlyOnDeepestFloor_OnRaisedDais()
        {
            for (int seed = 0; seed < 24; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);

                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    bool deepest = floor.FloorIndex == layout.BottomFloorIndex;
                    Assert.AreEqual(deepest, floor.Landmark.HasValue,
                        $"seed {seed} floor {floor.FloorIndex}: 랜드마크는 최심층에만");

                    if (!floor.Landmark.HasValue) continue;

                    GridPos lm = floor.Landmark.Value;
                    Assert.AreEqual(TileKind.Floor, map.Get(lm)?.kind, "랜드마크는 걷는 Floor 타일");
                    Assert.AreEqual(layout.Height.Elevation(floor.FloorIndex) + 1, lm.elevation,
                        "랜드마크는 뒤쪽 올라온 단(dais) 위에 있다");
                    Assert.AreNotEqual(floor.Entry, lm);
                    Assert.IsFalse(floor.EnemySpawns.Contains(lm), "적 스폰과 겹치지 않는다");
                }
            }
        }

        [Test]
        public void Generator_ArenaFloor_HasNoStairGuardSwarm()
        {
            // 아레나(최심층)는 하행 경비병 무리를 생략한다 — 스폰은 북쪽 방(문 뒤)에만 남는다.
            for (int seed = 0; seed < 16; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);
                DungeonFloorInfo arena = null;
                foreach (DungeonFloorInfo floor in layout.Floors)
                    if (floor.FloorIndex == layout.BottomFloorIndex) arena = floor;

                Assert.IsNotNull(arena);
                Assert.GreaterOrEqual(arena.EnemySpawns.Count, 1, "보스 후보가 될 스폰은 최소 1");
                if (arena.DownStairs.HasValue)
                    foreach (GridPos spawn in arena.EnemySpawns)
                        Assert.Greater(spawn.ChebyshevTo(arena.DownStairs.Value), 3,
                            $"seed {seed}: 아레나엔 하행 계단 인접 경비병이 없다");
            }
        }
    }
}
