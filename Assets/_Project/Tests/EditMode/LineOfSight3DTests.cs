using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 3D 시야선 1단계: 높이 인식 HasLineOfSight + 높이차 사격(도달 비용). 평면 케이스는
    /// 기존 판정과 동일해야 하고(회귀), 높이차는 보간된 복셀의 차폐/도달 비용으로 판정한다.
    /// </summary>
    public class LineOfSight3DTests
    {
        // --- 1A: HasLineOfSight ---

        [Test]
        public void Flat_ClearRow_HasSight_WallBlocks()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            Assert.IsTrue(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(4, 0, 0)));

            map.Set(new GridPos(2, 0, 0), TileKind.Wall);
            Assert.IsFalse(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(4, 0, 0)));
        }

        [Test]
        public void DownSlope_ClearAlongDescendingSurface()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 2), TileKind.Floor);
            map.Set(new GridPos(1, 0, 1), TileKind.Floor); // 보간된 시선 높이의 복셀
            map.Set(new GridPos(2, 0, 0), TileKind.Floor);

            Assert.IsTrue(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 2), new GridPos(2, 0, 0)));
        }

        [Test]
        public void DownSlope_BlockedByVoidVoxel()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 2), TileKind.Floor);
            // (1,0,1) 을 비워 둔다(void) — 높이차 사선 위의 빈 칸은 불투명.
            map.Set(new GridPos(2, 0, 0), TileKind.Floor);

            Assert.IsFalse(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 2), new GridPos(2, 0, 0)));
        }

        [Test]
        public void CrossElevation_BlockedByWallVoxel()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 2), TileKind.Floor);
            map.Set(new GridPos(1, 0, 1), TileKind.Wall);
            map.Set(new GridPos(2, 0, 0), TileKind.Floor);

            Assert.IsFalse(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 2), new GridPos(2, 0, 0)));
        }

        [Test]
        public void SameColumn_SolidFloorAbove_StaysBlocked()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(0, 0, 2), TileKind.Floor);
            // 같은 (x,y)의 수직 판정은 2단계에서 열렸지만, 온전한 바닥은 여전히 막는다.
            // 개구부를 통한 수직 시야는 SightRulesTests 가 고정한다.
            Assert.IsFalse(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(0, 0, 2)));
            Assert.IsTrue(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(0, 0, 0)));
        }

        // --- 1B: 도달 비용 + 높이차 사격 ---

        [TestCase(0, 0, 0, 3, 0, 0, 3)] // 수평만: 맨해튼 3
        [TestCase(0, 0, 4, 0, 0, 0, 4)] // 수직만: 높이차 4
        [TestCase(0, 0, 0, 3, 0, 1, 4)] // 혼합: 3 + 1
        public void RangedReachCost_AddsVerticalDistance(
            int fx, int fy, int fe, int tx, int ty, int te, int expected)
        {
            Assert.AreEqual(expected,
                CombatRules.RangedReachCost(new GridPos(fx, fy, fe), new GridPos(tx, ty, te)));
        }

        [Test]
        public void TryRanged_HighGround_HitsLowerTarget_WhenReachAllows()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 1), TileKind.Floor);
            map.Set(new GridPos(1, 0, 1), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.Floor);
            var archer = new CombatantState("archer", new GridPos(0, 0, 1), 5, 2);
            var target = new CombatantState("target", new GridPos(2, 0, 0), 5, 1);

            Assert.IsTrue(CombatRules.TryRanged(archer, target, map, 4, out int damage),
                "고지대에서 도달 비용(3) 안이면 아래 대상을 맞힌다");
            Assert.AreEqual(2, damage);
        }

        [Test]
        public void TryRanged_RejectedWhenVerticalCostExceedsRange()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 3), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.Floor);
            var archer = new CombatantState("archer", new GridPos(0, 0, 3), 5, 2);
            var target = new CombatantState("target", new GridPos(2, 0, 0), 5, 1);

            // 도달 비용 = 2 + 3 = 5 > 3 → 높이 이점이 사거리 예산을 넘겨 거부(카이팅 억제).
            Assert.IsFalse(CombatRules.TryRanged(archer, target, map, 3, out _));
            Assert.AreEqual(target.MaxHp, target.Hp);
        }
    }
}
