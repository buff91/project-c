using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// Recursive Shadowcasting FOV 규칙 검증.
    /// (기본 문/벽/반경 규칙은 GridVisibilityTests가 계속 커버한다)
    /// </summary>
    public class ShadowcastFovTests
    {
        private static GridMap Flat(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void Pillar_CastsExpandingShadow()
        {
            GridMap map = Flat(13);
            map.Set(new GridPos(8, 6, 0), TileKind.Wall);

            var visible = GridVisibility.Compute(map, new GridPos(6, 6, 0), 0, 0, 6);

            Assert.IsTrue(visible.Contains(new GridPos(8, 6, 0)), "기둥 자체는 보인다");
            Assert.IsFalse(visible.Contains(new GridPos(9, 6, 0)), "기둥 바로 뒤 차단");
            Assert.IsFalse(visible.Contains(new GridPos(10, 6, 0)), "그림자는 이어진다");
            Assert.IsTrue(visible.Contains(new GridPos(9, 9, 0)), "기둥 옆 대각은 보인다");
        }

        [Test]
        public void Void_IsOpaque_ClosedDoorRoomStaysUnknown()
        {
            // 이 던전은 방 경계가 벽 타일이 아니라 '타일 부재'다. void가 투명하면
            // 닫힌 문 뒤 방이 빈 공간 너머로 드러나므로, void=불투명이 문 불변식의 핵심.
            var map = new GridMap();
            for (int x = 0; x <= 3; x++)
            for (int y = 0; y <= 3; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);   // 서쪽 방
            map.Set(new GridPos(4, 1, 0), TileKind.DoorClosed);  // 문
            for (int x = 5; x <= 8; x++)
            for (int y = 0; y <= 3; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);   // 동쪽 방 (사이는 void)

            var visible = GridVisibility.Compute(map, new GridPos(1, 1, 0), 0, 0, 8);

            foreach (GridPos pos in visible)
                Assert.IsTrue(
                    pos.x <= 4,
                    $"문이 닫혀 있는데 동쪽 방 {pos} 이 보입니다 (void 투명 누출)");
            Assert.IsTrue(visible.Contains(new GridPos(4, 1, 0)), "문 자체는 보인다");
        }

        [Test]
        public void HoleWeakFloorStairs_DoNotBlockSight()
        {
            var map = new GridMap();
            for (int x = 0; x < 7; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Set(new GridPos(2, 0, 0), TileKind.Hole);
            map.Set(new GridPos(3, 0, 0), TileKind.WeakFloor);
            map.Set(new GridPos(4, 0, 0), TileKind.Stairs);

            var visible = GridVisibility.Compute(map, new GridPos(0, 0, 0), 0, 0, 8);

            Assert.IsTrue(visible.Contains(new GridPos(6, 0, 0)),
                "구멍/약한 바닥/계단 너머는 보여야 한다 (HasLineOfSight와 동일 기준)");
        }

        [Test]
        public void Result_ContainsSurfaceTileGridPos_NotOriginElevation()
        {
            // 결과는 대역 스캔으로 찾은 실제 타일의 GridPos여야 한다.
            // 원점 elevation으로 찍으면 tint/탭 게이트가 전부 miss 된다.
            var map = new GridMap();
            for (int x = 0; x < 4; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            map.Remove(new GridPos(2, 0, 0));
            map.Set(new GridPos(2, 0, 1), TileKind.Floor); // 한 단 위 표면

            var visible = GridVisibility.Compute(map, new GridPos(0, 0, 0), 0, 1, 8);

            Assert.IsTrue(visible.Contains(new GridPos(2, 0, 1)), "표면(위 타일) GridPos 로 노출");
            Assert.IsFalse(visible.Contains(new GridPos(2, 0, 0)), "존재하지 않는 타일은 미포함");
            Assert.IsTrue(visible.Contains(new GridPos(3, 0, 0)), "1단 높이차는 시야를 막지 않는다");
        }

        [Test]
        public void EveryVisibleTile_IsRealAndWithinChebyshevRadius()
        {
            var map = new GridMap();
            DungeonLayout dungeon = DungeonGenerator.Generate(map, 11, 11, 3, 4, seed: 9);
            GridPos origin = dungeon.Entry;
            int minElevation = dungeon.Height.Elevation(dungeon.TopFloorIndex);
            int maxElevation = minElevation + dungeon.Height.ElevationsPerFloor - 1;
            const int radius = 6;

            var visible = GridVisibility.Compute(map, origin, minElevation, maxElevation, radius);

            Assert.IsTrue(visible.Contains(origin));
            foreach (GridPos pos in visible)
            {
                Assert.IsTrue(map.Has(pos), $"존재하지 않는 타일 {pos}");
                Assert.LessOrEqual(origin.ChebyshevTo(pos), radius, $"반경 초과 {pos}");
            }
        }
    }
}
