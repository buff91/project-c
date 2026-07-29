using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class ThrowAimPreviewRulesTests
    {
        private static GridMap Room(int size = 9)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void HasBlast_TrueForAreaThrowables_FalseForKnife()
        {
            Assert.IsTrue(ThrowAimPreviewRules.HasBlast(ItemKind.Bomb));
            Assert.IsTrue(ThrowAimPreviewRules.HasBlast(ItemKind.FrostBomb));
            Assert.IsTrue(ThrowAimPreviewRules.HasBlast(ItemKind.OilFlask));
            Assert.IsFalse(
                ThrowAimPreviewRules.HasBlast(ItemKind.ThrowingKnife),
                "단검은 단일 대상이라 영향 범위 미리보기가 없다");
        }

        [Test]
        public void TryResolveBlastCenter_RequiresAimOnALegalThrowTarget()
        {
            GridMap map = Room();
            var from = new GridPos(4, 4, 0);

            Assert.IsTrue(ThrowAimPreviewRules.TryResolveBlastCenter(
                map, from, new GridPos(6, 4, 0), ItemKind.Bomb, maxRange: 4, out GridPos center));
            Assert.AreEqual(new GridPos(6, 4, 0), center);

            Assert.IsFalse(
                ThrowAimPreviewRules.TryResolveBlastCenter(
                    map, from, null, ItemKind.Bomb, maxRange: 4, out _),
                "조준점이 없으면 범위도 없다");
            Assert.IsFalse(
                ThrowAimPreviewRules.TryResolveBlastCenter(
                    map, from, new GridPos(6, 4, 0), ItemKind.ThrowingKnife, maxRange: 4, out _),
                "단검은 범위가 없다");
            Assert.IsFalse(
                ThrowAimPreviewRules.TryResolveBlastCenter(
                    map, from, new GridPos(8, 8, 0), ItemKind.Bomb, maxRange: 4, out _),
                "사거리 밖에는 범위를 그리지 않는다");
        }

        [Test]
        public void TryResolveBlastCenter_BlockedLineOfSight_DrawsNoBlast()
        {
            GridMap map = Room();
            var from = new GridPos(4, 4, 0);
            map.Set(new GridPos(5, 4, 0), TileKind.Wall);

            Assert.IsFalse(
                ThrowAimPreviewRules.TryResolveBlastCenter(
                    map, from, new GridPos(7, 4, 0), ItemKind.Bomb, maxRange: 4, out _),
                "던질 수 없는 칸에 3×3을 그리면 거짓말이 된다");
        }

        [Test]
        public void ForEachBlastPreviewCell_CoversTheFullThreeByThree()
        {
            GridMap map = Room();
            var cells = new List<GridPos>();

            ThrowAimPreviewRules.ForEachBlastPreviewCell(map, new GridPos(4, 4, 0), cells.Add);

            Assert.AreEqual(9, cells.Count);
            CollectionAssert.Contains(cells, new GridPos(3, 3, 0));
            CollectionAssert.Contains(cells, new GridPos(5, 5, 0));
        }

        [Test]
        public void ForEachBlastPreviewCell_KeepsWallsButSkipsCellsWithNoTile()
        {
            GridMap map = Room();
            var wall = new GridPos(3, 4, 0);
            map.Set(wall, TileKind.Wall);
            map.Remove(new GridPos(3, 3, 0)); // 허공 — 그릴 바닥이 없다

            var cells = new List<GridPos>();
            ThrowAimPreviewRules.ForEachBlastPreviewCell(map, new GridPos(4, 4, 0), cells.Add);

            Assert.AreEqual(8, cells.Count);
            CollectionAssert.Contains(cells, wall, "폭발은 유리를 깨고 약한 바닥을 무너뜨린다 — 벽 칸도 범위 안이다");
            CollectionAssert.DoesNotContain(cells, new GridPos(3, 3, 0));
        }

        [Test]
        public void ForEachBlastPreviewCell_MatchesDetonationFootprint()
        {
            GridMap map = Room();
            var center = new GridPos(4, 4, 0);
            var cells = new List<GridPos>();

            ThrowAimPreviewRules.ForEachBlastPreviewCell(map, center, cells.Add);

            // 미리보기와 실제 판정이 갈리면 안 된다 — 둘 다 BombRules.InBlast 로 검산한다.
            foreach (GridPos cell in cells)
                Assert.IsTrue(BombRules.InBlast(center, cell), $"{cell} 는 폭발 반경 밖인데 그려졌다");
        }
    }
}
