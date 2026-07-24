using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 창문(건물형 수직성 v0.3): 이동은 막고 시야는 통과(수평 시야 포털), 깨면 통로,
    /// 깨진 창문 밖이 허공이면 밀려 낙하로 이어진다. (GDD §5.2/§5.3)
    /// </summary>
    public class WindowTests
    {
        [Test]
        public void IntactWindow_BlocksMovement_ButNotSight_AndIsBreakable()
        {
            var window = new TileData(TileKind.Window);
            Assert.IsFalse(window.IsWalkable, "온전한 유리는 통과 불가");
            Assert.IsFalse(window.IsSolidGround, "유리 위에 서지 않는다");
            Assert.IsFalse(window.BlocksSight, "시야는 통과 — 수평 시야 포털");
            Assert.IsTrue(window.CanBreak);
            Assert.IsFalse(window.CausesFall, "창문 자체는 낙하 트리거가 아니다");
        }

        [Test]
        public void BrokenWindow_IsPassablePassage_AndTransparent_AndNotReBreakable()
        {
            var broken = new TileData(TileKind.WindowBroken);
            Assert.IsTrue(broken.IsWalkable, "깨진 창문 = 통로");
            Assert.IsTrue(broken.IsSolidGround);
            Assert.IsFalse(broken.BlocksSight);
            Assert.IsFalse(broken.CanBreak, "이미 깨진 창문은 다시 못 깬다");
        }

        [Test]
        public void HasLineOfSight_PassesThroughWindow_ButNotWall()
        {
            var map = new GridMap();
            for (int x = 0; x < 5; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);

            map.Set(new GridPos(2, 0, 0), TileKind.Window);
            Assert.IsTrue(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(4, 0, 0)),
                "창문 너머로 시야가 통한다");

            map.Set(new GridPos(2, 0, 0), TileKind.Wall);
            Assert.IsFalse(CombatRules.HasLineOfSight(map, new GridPos(0, 0, 0), new GridPos(4, 0, 0)));
        }

        [Test]
        public void TryBreak_TurnsWindowIntoPassage_OnceOnly()
        {
            var map = new GridMap();
            map.Set(new GridPos(1, 0, 0), TileKind.Window);

            Assert.IsTrue(WindowRules.TryBreak(map, new GridPos(1, 0, 0)));
            Assert.AreEqual(TileKind.WindowBroken, map.Get(new GridPos(1, 0, 0)).kind);
            Assert.IsTrue(map.Get(new GridPos(1, 0, 0)).IsWalkable);

            Assert.IsFalse(WindowRules.TryBreak(map, new GridPos(1, 0, 0)), "이미 깨졌으면 실패");

            map.Set(new GridPos(2, 0, 0), TileKind.Floor);
            Assert.IsFalse(WindowRules.TryBreak(map, new GridPos(2, 0, 0)), "창문이 아니면 실패");
        }

        [Test]
        public void KnockbackIntoIntactWindow_IsBlocked_LikeWall()
        {
            var map = new GridMap();
            map.Set(new GridPos(0, 0, 0), TileKind.Floor);
            map.Set(new GridPos(1, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.Window);

            // 중심(0,0)에서 (1,0) 대상을 밀면 목적지 (2,0)=온전한 유리 → 막힘.
            Assert.AreEqual(
                KnockbackOutcome.None,
                KnockbackRules.Resolve(map, new GridPos(0, 0, 0), new GridPos(1, 0, 0), null, out _),
                "온전한 유리는 넉백을 막는다");
        }

        [Test]
        public void PushOutBrokenWindow_OverVoid_LeadsToFall()
        {
            var height = new DungeonHeightModel(4);
            var map = new GridMap();
            map.Set(new GridPos(1, 0, 0), TileKind.Floor);          // 안쪽(밀기 중심)
            map.Set(new GridPos(2, 0, 0), TileKind.WindowBroken);   // 깨진 창문(대상이 선 프레임)
            map.Set(new GridPos(3, 0, -4), TileKind.Floor);         // 창밖 아래 지면(한 층 아래)
            var enemy = new CombatantState("raider", new GridPos(2, 0, 0), 10, 1);

            // 안쪽(1,0)에서 프레임 위 적(2,0)을 바깥으로 밀면 (3,0)=허공 → 낙하 신호.
            KnockbackOutcome outcome = KnockbackRules.Resolve(
                map, new GridPos(1, 0, 0), enemy.Position, null, out GridPos dest);
            Assert.AreEqual(KnockbackOutcome.PushedIntoFall, outcome, "창밖 허공으로 밀려난다");
            Assert.AreEqual(new GridPos(3, 0, 0), dest);

            FallResult fall = FallRules.TryFall(map, height, enemy, dest, -4, null);
            Assert.IsNotNull(fall, "창밖으로 떨어져 아래 지면에 착지");
            Assert.AreEqual(new GridPos(3, 0, -4), enemy.Position);
            Assert.AreEqual(2, fall.Damage, "한 층(4칸) 낙하 = 2");
            Assert.AreEqual(8, enemy.Hp);
        }

        [Test]
        public void BrokenInteriorWindow_ConnectsRoomsHorizontally()
        {
            // 실내 창문: 양옆이 같은 높이 바닥 → 깨면 방 사이 수평 통로가 된다.
            var map = new GridMap();
            for (int x = 0; x < 5; x++)
                map.Set(new GridPos(x, 0, 0), x == 2 ? TileKind.Window : TileKind.Floor);

            var a = new GridPos(0, 0, 0);
            var b = new GridPos(4, 0, 0);
            Assert.AreEqual(0, GridPathfinder.FindPath(map, a, b).Count,
                "온전한 유리는 방 사이를 가른다");

            Assert.IsTrue(WindowRules.TryBreak(map, new GridPos(2, 0, 0)));
            System.Collections.Generic.List<GridPos> path = GridPathfinder.FindPath(map, a, b);
            Assert.Greater(path.Count, 0, "깨진 창문으로 수평 통로가 열린다");
            Assert.AreEqual(b, path[path.Count - 1]);
            CollectionAssert.Contains(path, new GridPos(2, 0, 0), "경로가 깨진 창문을 지난다");
        }

        [Test]
        public void Generator_PlacesFallOutWindows_OnValidEdges()
        {
            int totalWindows = 0;
            for (int seed = 0; seed < 24; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);
                int bottom = layout.Height.Elevation(layout.BottomFloorIndex);

                foreach (DungeonFloorInfo floor in layout.Floors)
                foreach (GridPos w in floor.Windows)
                {
                    totalWindows++;
                    TileData tile = map.Get(w);
                    Assert.AreEqual(TileKind.Window, tile.kind);
                    Assert.IsFalse(tile.IsWalkable, "온전한 창문은 이동 차단");
                    Assert.IsFalse(tile.BlocksSight, "창문은 시야 투과");
                    Assert.AreEqual(TileKind.Floor, map.Get(new GridPos(w.x + 1, w.y, w.elevation))?.kind,
                        "창문 안쪽은 방 바닥");
                    Assert.IsNull(map.Get(new GridPos(w.x - 1, w.y, w.elevation)), "창밖은 허공(void)");
                    GridPos? landing = map.FindLandingBelow(new GridPos(w.x - 1, w.y, w.elevation), bottom);
                    Assert.IsTrue(landing.HasValue && map.Get(landing.Value).IsWalkable,
                        "창밖은 한 층 아래 걷는 바닥으로 떨어진다");
                }
            }
            Assert.Greater(totalWindows, 0, "여러 seed에 걸쳐 낙하형 창문이 배치된다");
        }
    }
}
