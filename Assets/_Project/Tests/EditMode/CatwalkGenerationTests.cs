using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 건물형 수직성(v0.3): 깊은 층에 +2단 캐치워크가 생기고(높이 인식 FOV가 차폐를 발동하는
    /// 큰 단차), 사다리 링크로 도달 가능하다. 얕은 층엔 없다. (도달성 불변식은 ProceduralDungeonTests가 병행)
    /// </summary>
    public class CatwalkGenerationTests
    {
        [Test]
        public void Generator_PlacesReachableCatwalk_OnDeeperFloorsOnly()
        {
            int totalCatwalks = 0;
            for (int seed = 0; seed < 16; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);

                // 도달성 판정을 위해 문을 연다(ProceduralDungeonTests의 불변식 검사와 동일 절차).
                foreach (DungeonFloorInfo floor in layout.Floors)
                foreach (GridPos door in floor.Doors)
                    map.Set(door, TileKind.DoorOpen);
                foreach (DungeonFloorInfo floor in layout.Floors)
                    if (floor.SecretDoor.HasValue)
                        SecretRoomRules.TryReveal(map, floor.SecretDoor.Value);

                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    int depth = -floor.FloorIndex;
                    int catElev = layout.Height.Elevation(floor.FloorIndex) + 2;

                    var catwalks = new List<GridPos>();
                    for (int x = 0; x < 13; x++)
                    for (int y = 0; y < 13; y++)
                    {
                        var pos = new GridPos(x, y, catElev);
                        if (map.Get(pos)?.kind == TileKind.Floor) catwalks.Add(pos);
                    }

                    if (depth < 3)
                    {
                        Assert.IsEmpty(catwalks, $"seed {seed} depth {depth}: 얕은 층엔 +2 캐치워크 없음");
                        continue;
                    }

                    foreach (GridPos c in catwalks)
                    {
                        totalCatwalks++;
                        Assert.IsTrue(map.Get(c).IsWalkable, "캐치워크는 걷는 고지대");
                        Assert.Greater(
                            GridPathfinder.FindPath(map, floor.Entry, c).Count, 0,
                            $"seed {seed}: 캐치워크는 사다리 링크로 도달 가능해야 한다");
                    }
                }
            }

            Assert.Greater(totalCatwalks, 0, "깊은 층에 +2 캐치워크가 실제로 배치된다");
        }
    }
}
