using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>감전: 물을 도체 삼아 이어진 웅덩이 전체로 전파, 그 위 대상을 지진다. (GDD §5.5)</summary>
    public class ShockRulesTests
    {
        private static GridMap WetRow(int length)
        {
            var map = new GridMap();
            for (int x = 0; x < length; x++)
            {
                var pos = new GridPos(x, 0, 0);
                map.Set(pos, TileKind.Floor);
                map.Get(pos).wet = true;
            }
            return map;
        }

        [Test]
        public void Discharge_ConductsThroughConnectedPuddle_ButNotOntoDryGround()
        {
            GridMap map = WetRow(6);                       // (0,0)~(5,0) 젖은 통로
            map.Set(new GridPos(5, 5, 0), TileKind.Floor); // 마른 외딴 칸

            var near = new CombatantState("near", new GridPos(1, 0, 0), 10, 1); // 블라스트+젖음
            var far = new CombatantState("far", new GridPos(5, 0, 0), 10, 1);   // 통전 전파 끝
            var dry = new CombatantState("dry", new GridPos(5, 5, 0), 10, 1);   // 마른 칸, 무관

            List<GridPos> energized = ShockRules.Discharge(
                map, new GridPos(1, 0, 0), new[] { near, far, dry }, damage: 3);

            Assert.AreEqual(7, near.Hp, "블라스트 안 대상 감전");
            Assert.AreEqual(7, far.Hp, "이어진 웅덩이 끝까지 통전");
            Assert.AreEqual(10, dry.Hp, "마른 칸으로는 전파되지 않는다");
            CollectionAssert.Contains(energized, new GridPos(5, 0, 0));
            CollectionAssert.DoesNotContain(energized, new GridPos(5, 5, 0));
        }

        [Test]
        public void Discharge_DamagesEachTargetOnce_EvenIfBlastAndPuddleOverlap()
        {
            GridMap map = WetRow(3);
            var victim = new CombatantState("v", new GridPos(1, 0, 0), 10, 1); // 블라스트이자 젖은 칸

            ShockRules.Discharge(map, new GridPos(1, 0, 0), new[] { victim }, damage: 4);

            Assert.AreEqual(6, victim.Hp, "직접+통전 중복 없이 한 번만");
        }

        [Test]
        public void Discharge_NoWater_OnlyDirectBlastHits()
        {
            var map = new GridMap();
            for (int x = 0; x < 6; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor); // 전부 마름

            var inBlast = new CombatantState("in", new GridPos(2, 0, 0), 10, 1);
            var outside = new CombatantState("out", new GridPos(5, 0, 0), 10, 1);

            List<GridPos> energized = ShockRules.Discharge(
                map, new GridPos(1, 0, 0), new[] { inBlast, outside }, damage: 3);

            Assert.AreEqual(7, inBlast.Hp, "마른 바닥이어도 직접 블라스트는 맞는다");
            Assert.AreEqual(10, outside.Hp, "물이 없으면 전파 없음");
            Assert.IsEmpty(energized);
        }
    }
}
