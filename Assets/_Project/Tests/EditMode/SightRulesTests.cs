using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 3D 시야선 2단계: 수평·경사·수직 판정이 <see cref="SightRules"/> 하나로 모였는지 고정한다.
    /// 개구부 투시(옛 VerticalOpeningRules)와 근접 도달 기하도 같은 출처를 쓴다.
    /// </summary>
    public class SightRulesTests
    {
        private readonly DungeonHeightModel _height = new DungeonHeightModel(4);
        private readonly GridPos _opening = new GridPos(2, 3, 0);
        private readonly GridPos _landing = new GridPos(2, 3, -4);

        // --- 수직 시야 ---

        [Test]
        public void HasVerticalSight_OpenShaft_SeesThroughOpeningOnly()
        {
            GridMap map = CreateOpeningMap();

            Assert.IsTrue(SightRules.HasVerticalSight(map, _opening, _landing),
                "개구부와 착지 사이가 허공이면 뚫려 있다");

            // 개구부를 온전한 바닥으로 메우면 같은 컬럼이 막힌다.
            map.Set(_opening, TileKind.Floor);
            Assert.IsFalse(SightRules.HasVerticalSight(map, _opening, _landing));
        }

        [Test]
        public void HasVerticalSight_IntactFloorBetween_Blocks()
        {
            GridMap map = CreateOpeningMap();
            map.Set(new GridPos(2, 3, -2), TileKind.Floor); // 사이에 낀 바닥

            Assert.IsFalse(SightRules.HasVerticalSight(map, _opening, _landing));
        }

        [Test]
        public void HasLineOfSight_SameColumn_UsesVerticalRule()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(0, 0, 2), TileKind.Floor);

            // 단단한 바닥 위에 선 대상은 그 바닥에 가려 아래에서 보이지 않는다.
            Assert.IsFalse(SightRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(0, 0, 2)));
            Assert.IsTrue(SightRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(0, 0, 0)));

            // 같은 칸이 실제 개구부면 위·아래가 이어진다.
            map.Set(new GridPos(0, 0, 2), TileKind.Hole);
            Assert.IsTrue(SightRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(0, 0, 2)));
        }

        [Test]
        public void HasVerticalSight_DifferentColumn_IsNotVerticalCase()
        {
            GridMap map = CreateOpeningMap();
            Assert.IsFalse(SightRules.HasVerticalSight(map, _opening, new GridPos(3, 3, -4)));
        }

        // --- 근접 도달 기하 (근접·마법 공용) ---

        [TestCase(1, 0, 0, 1, true)]   // 같은 높이 정사각 인접
        [TestCase(1, 1, 0, 1, false)]  // 대각선
        [TestCase(1, 0, 1, 1, true)]   // 한 단 위
        [TestCase(1, 0, 2, 1, false)]  // 두 단 위 — 기본 도달 한계 밖
        [TestCase(1, 0, 2, 2, true)]   // 도달 높이를 키우면 닿는다
        [TestCase(0, 0, 1, 1, false)]  // 같은 칸 수직은 근접 도달이 아니다
        public void CanReachAcross_PlanarNeighborWithinStepHeight(
            int dx, int dy, int de, int maxStepHeight, bool expected)
        {
            Assert.AreEqual(expected, SightRules.CanReachAcross(
                new GridPos(4, 4, 0), new GridPos(4 + dx, 4 + dy, de), maxStepHeight));
        }

        [Test]
        public void CombatRules_MeleeReach_SharesTheSameGeometry()
        {
            var attacker = new CombatantState("a", new GridPos(0, 0, 0), 5, 2);
            var stepUp = new CombatantState("b", new GridPos(1, 0, 1), 5, 2);
            var tooHigh = new CombatantState("c", new GridPos(1, 0, 2), 5, 2);

            Assert.AreEqual(
                SightRules.CanReachAcross(
                    attacker.Position, stepUp.Position, CombatRules.MeleeReachHeight),
                CombatRules.AreAdjacent(attacker, stepUp));
            Assert.IsFalse(CombatRules.AreAdjacent(attacker, tooHigh));
        }

        // --- 개구부 투시 (옛 VerticalOpeningRules) ---

        [Test]
        public void ViewFromFloor_VisibleHoleOnUpperFloor_LooksDown()
        {
            GridMap map = CreateOpeningMap();
            var visible = new HashSet<GridPos> { _opening };

            VerticalOpeningView view = SightRules.ViewFromFloor(
                map, _height, 0, _opening, -8, visible.Contains, out GridPos landing);

            Assert.AreEqual(VerticalOpeningView.Downward, view);
            Assert.AreEqual(_landing, landing);
        }

        [Test]
        public void ViewFromFloor_VisibleLandingOnLowerFloor_LooksUp()
        {
            GridMap map = CreateOpeningMap();
            var visible = new HashSet<GridPos> { _landing };

            VerticalOpeningView view = SightRules.ViewFromFloor(
                map, _height, -1, _opening, -8, visible.Contains, out GridPos landing);

            Assert.AreEqual(VerticalOpeningView.Upward, view);
            Assert.AreEqual(_landing, landing);
        }

        [Test]
        public void ViewFromFloor_HiddenOpeningAndLanding_RevealsNothing()
        {
            GridMap map = CreateOpeningMap();

            VerticalOpeningView view = SightRules.ViewFromFloor(
                map, _height, -1, _opening, -8, _ => false, out _);

            Assert.AreEqual(VerticalOpeningView.None, view);
        }

        [TestCase(TileKind.Stairs)]
        [TestCase(TileKind.Ladder)]
        [TestCase(TileKind.StairsUp)]
        [TestCase(TileKind.StairsDown)]
        public void ViewFromFloor_TraversalConnectors_AreNotSightPortals(TileKind stairKind)
        {
            GridMap map = CreateOpeningMap();
            map.Set(_opening, stairKind);

            VerticalOpeningView view = SightRules.ViewFromFloor(
                map, _height, 0, _opening, -8, _ => true, out _);

            Assert.AreEqual(VerticalOpeningView.None, view);
        }

        [Test]
        public void ViewFromFloor_IntactFloorBetween_RevealsNothing()
        {
            GridMap map = CreateOpeningMap();
            // 개구부 아래에 온전한(걷는) 바닥이 끼면 착지는 더 아래지만 시야는 막힌다.
            map.Set(new GridPos(2, 3, -2), TileKind.WeakFloor);

            VerticalOpeningView view = SightRules.ViewFromFloor(
                map, _height, 0, _opening, -8, _ => true, out _);

            Assert.AreEqual(VerticalOpeningView.None, view);
        }

        private GridMap CreateOpeningMap()
        {
            var map = new GridMap();
            map.Set(_opening, TileKind.Hole);
            map.Set(_landing, TileKind.Floor);
            return map;
        }
    }
}
