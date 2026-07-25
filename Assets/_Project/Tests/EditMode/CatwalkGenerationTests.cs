using System.Collections.Generic;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 건물형 수직성(v0.3): 사다리 위 +2단 캐치워크. 길이는 밴드 프로파일이 소유하고,
    /// 얕은 밴드와 최심층 아레나에는 놓지 않는다. 큰 단차는 높이 인식 FOV 차폐가 실제로
    /// 발동하는 무대이며, 사다리 링크로 도달 가능해야 한다.
    /// (도달성 불변식 전반은 ProceduralDungeonTests가 병행)
    /// </summary>
    public class CatwalkGenerationTests
    {
        [Test]
        public void Generator_PlacesBandLengthCatwalk_ReachableAndNotOnArena()
        {
            int maxObservedLength = 0;
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

                    bool arena = DungeonBossArenaRules.IsArenaFloor(depth, layout.Floors.Count);
                    int bandLength = DungeonBandProfiles.ForDepth(depth).CatwalkLength;

                    if (arena)
                    {
                        Assert.IsEmpty(catwalks, $"seed {seed} depth {depth}: 아레나는 결투 공간을 비운다");
                        continue;
                    }

                    if (bandLength == 0)
                    {
                        Assert.IsEmpty(catwalks, $"seed {seed} depth {depth}: 얕은 밴드엔 +2 캐치워크 없음");
                        continue;
                    }

                    // 올라온 단은 항상 두 줄(RaisedY = height-2)이라 길이 2까지는 그대로 들어간다.
                    Assert.AreEqual(bandLength, catwalks.Count,
                        $"seed {seed} depth {depth}: 밴드가 정한 길이만큼 놓인다");
                    maxObservedLength = System.Math.Max(maxObservedLength, catwalks.Count);

                    foreach (GridPos c in catwalks)
                    {
                        totalCatwalks++;
                        Assert.AreEqual(catwalks[0].x, c.x, "캐치워크는 사다리 컬럼에서 한 줄로 이어진다");
                        Assert.IsTrue(map.Get(c).IsWalkable, "캐치워크는 걷는 고지대");
                        Assert.IsNotNull(map.Get(c.WithElevation(c.elevation - 1)),
                            "허공에 뜬 발판이 아니라 올라온 단 위에 얹힌다");
                        Assert.Greater(
                            GridPathfinder.FindPath(map, floor.Entry, c).Count, 0,
                            $"seed {seed}: 캐치워크는 사다리 링크로 도달 가능해야 한다");
                    }
                }
            }

            Assert.Greater(totalCatwalks, 0, "깊은 층에 +2 캐치워크가 실제로 배치된다");
            Assert.AreEqual(2, maxObservedLength, "Deep 밴드에서는 두 칸짜리 통로가 실제로 생긴다");
        }
    }
}
