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
        public void TryApproachCue_OnlyOnFloorAboveArena_AndNamesTheBoss()
        {
            Assert.IsTrue(
                DungeonBossArenaRules.TryApproachCue(
                    "묘지기", DungeonProgressDirection.Descend, 8, 10, bossDefeated: false, out string message));
            StringAssert.Contains("묘지기", message);

            Assert.IsFalse(
                DungeonBossArenaRules.TryApproachCue(
                    "묘지기", DungeonProgressDirection.Descend, 7, 10, bossDefeated: false, out _),
                "두 층 위에서는 아직 알리지 않는다");
            Assert.IsFalse(
                DungeonBossArenaRules.TryApproachCue(
                    "묘지기", DungeonProgressDirection.Descend, 9, 10, bossDefeated: false, out _),
                "아레나 층에서는 전조가 아니라 실물이 기다린다");
        }

        /// <summary>
        /// 전조가 가리키는 쪽은 던전 방향을 타야 한다. 문구가 "한 층 아래"로 고정이던 시절
        /// 상승 던전(폐병원)에서 정반대를 가리켰다 — 규칙이 아니라 <b>안내</b>의 결함이라
        /// 테스트가 없으면 다시 굳는다.
        /// </summary>
        [Test]
        public void TryApproachCue_PointsTheWayProgressActuallyGoes()
        {
            Assert.IsTrue(DungeonBossArenaRules.TryApproachCue(
                "감시자", DungeonProgressDirection.Ascend, 8, 10, bossDefeated: false, out string up));
            StringAssert.Contains("위", up, "상승 던전에서 보스는 한 층 위에 있다");
            Assert.IsFalse(up.Contains("아래"), "상승 던전에서 '아래'는 거짓말이다");

            Assert.IsTrue(DungeonBossArenaRules.TryApproachCue(
                "감시자", DungeonProgressDirection.Descend, 8, 10, bossDefeated: false, out string down));
            StringAssert.Contains("아래", down);

            // Inward 는 고도가 진행 축이 아니라 위/아래 어휘 자체를 쓰지 않는다.
            Assert.IsTrue(DungeonBossArenaRules.TryApproachCue(
                "감시자", DungeonProgressDirection.Inward, 8, 10, bossDefeated: false, out string inward));
            StringAssert.Contains("구역", inward);
            Assert.IsFalse(inward.Contains("아래") || inward.Contains("위에서"),
                "고도가 진행 축이 아닌 던전에 층 방향 어휘를 붙이지 않는다");
        }

        [Test]
        public void TryApproachCue_SilentWhenBossIsGoneOrAbsent()
        {
            Assert.IsFalse(
                DungeonBossArenaRules.TryApproachCue(
                    "묘지기", DungeonProgressDirection.Descend, 8, 10, bossDefeated: true, out _),
                "이미 처치한 위협은 다시 예고하지 않는다");
            Assert.IsFalse(
                DungeonBossArenaRules.TryApproachCue(
                    null, DungeonProgressDirection.Descend, 8, 10, bossDefeated: false, out _),
                "보스 없는 던전은 전조도 없다");
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
