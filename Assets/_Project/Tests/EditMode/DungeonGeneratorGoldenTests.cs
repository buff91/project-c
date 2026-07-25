using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 생성기 출력의 <b>골든 지문</b>. 타일·링크·스폰·설비를 한 문자열로 직렬화해 해시한다.
    /// <para>
    /// <b>왜 필요한가.</b> 불변식 테스트는 "규칙을 어겼는가"만 본다 — 규칙을 다 지키면서
    /// 배치가 통째로 달라져도 통과한다. 지역·방향 같은 <b>구조 축</b>을 도입할 때 필요한 것은
    /// 그 반대의 보장이다: "기존 던전의 출력이 1비트도 안 바뀌었다". 이 게임은 seed 재생성
    /// 방식으로 세이브를 복원하므로(<see cref="RunSaveData"/>), 조용한 배치 변화는 곧
    /// 이어하기가 다른 던전을 여는 것이다.
    /// </para>
    /// <para>
    /// <b>지문이 바뀌면</b> 생성 결과가 실제로 달라진 것이다. 의도한 변경이면 아래 상수를
    /// 갱신하되, <b>왜 바뀌었는지 커밋 메시지에 남긴다</b>. 의도치 않았다면 그것이 회귀다.
    /// </para>
    /// </summary>
    public class DungeonGeneratorGoldenTests
    {
        /// <summary>폐병원(상승·10층)의 실제 운영 형상. seed 는 카탈로그 값을 포함한다.</summary>
        private static readonly int[] Seeds = { 1, 7, 23, 1977 };

        /// <summary>
        /// <b>지문 이력</b>
        /// <list type="bullet">
        /// <item><c>2e5d8434a35bd2f2</c> — 최초.</item>
        /// <item><c>f34bd08450973a50</c> — 사다리가 캐치워크(+2)까지 한 번에 잇도록 바뀜.
        /// <b>지형은 그대로고 링크만 달라졌다</b>: <c>PlaceCatwalk</c> 는 RNG 를 쓰지 않으므로
        /// 생성 스트림이 그대로이고, 타일 배치도 동일하다. 실제로 하강 던전 지문은
        /// 안 바뀌었다(얕은 밴드엔 캐치워크가 없어 이 경로를 안 탄다).
        /// 즉 <b>기존 세이브가 같은 던전을 계속 연다</b> — 사다리의 목적지만 달라진다.</item>
        /// <item><c>8fbf82c8067b1cb3</c> / <c>02411906bef8b09f</c> — 개구부가 1칸에서
        /// 최대 3칸으로 자람. <b>RNG 스트림은 안 밀렸다</b>: 성장 루프가 난수를 쓰지 않아
        /// 뽑는 횟수가 그대로다(앵커 1 + 약한 바닥 1). 상한을 1로 되돌리면 위의 옛 지문이
        /// 그대로 재현되는 것으로 확인했다 — 즉 방·계단·적·아이템 배치는 <b>동일</b>하고
        /// 달라진 것은 개구부에 붙은 칸들뿐이다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void Ascending_HospitalShape_MatchesGoldenFingerprint()
        {
            Assert.AreEqual(
                "8fbf82c8067b1cb3",
                Fingerprint(DungeonProgressDirection.Ascend, floorCount: 10, firstBuildingFloor: -2),
                "폐병원 생성 출력이 달라졌다 — 의도한 변경인지 확인하고 지문을 갱신한다");
        }

        [Test]
        public void Descending_ShallowShape_MatchesGoldenFingerprint()
        {
            Assert.AreEqual(
                "02411906bef8b09f",
                Fingerprint(DungeonProgressDirection.Descend, floorCount: 3, firstBuildingFloor: -1),
                "하강 던전 생성 출력이 달라졌다 — 의도한 변경인지 확인하고 지문을 갱신한다");
        }

        /// <summary>
        /// 개구부는 여러 칸이지만 <b>모든 칸</b>이 1칸이던 시절의 불변식을 그대로 지켜야 한다.
        /// 넓히면서 한 칸이라도 규칙 밖으로 나가면 그게 곧 2층 관통이거나 허공 착지다.
        /// </summary>
        [Test]
        public void EveryHoleTile_KeepsTheOneFloorDropInvariant()
        {
            foreach (int seed in Seeds)
            foreach (DungeonProgressDirection direction in new[]
                     {
                         DungeonProgressDirection.Ascend, DungeonProgressDirection.Descend
                     })
            {
                var map = new GridMap();
                DungeonLayout dungeon = DungeonGenerator.Generate(
                    map, 13, 13, 10, seed: seed, direction: direction);

                foreach (DungeonFloorInfo floor in dungeon.Floors)
                {
                    Assert.LessOrEqual(
                        floor.HoleTiles.Count, 3,
                        $"seed {seed} {direction} {floor.FloorIndex}층: 개구부가 상한을 넘었다");

                    foreach (GridPos hole in floor.HoleTiles)
                    {
                        Assert.AreEqual(
                            TileKind.Hole, map.Get(hole).kind,
                            $"seed {seed}: {hole} 가 개구부로 안 뚫렸다");

                        GridPos? landing = map.FindLandingBelow(hole, -100);
                        Assert.IsTrue(landing.HasValue, $"seed {seed}: {hole} 아래에 바닥이 없다");
                        Assert.AreEqual(
                            dungeon.Height.FloorIndex(hole.elevation) - 1,
                            dungeon.Height.FloorIndex(landing.Value.elevation),
                            $"seed {seed}: {hole} 가 두 층을 관통한다");
                        Assert.IsTrue(
                            map.Get(landing.Value).IsWalkable,
                            $"seed {seed}: {hole} 의 착지가 걸을 수 없는 칸이다");
                    }
                }
            }
        }

        /// <summary>
        /// 개구부는 <b>한 덩어리</b>여야 한다 — 흩어지면 "개구부 하나"라는 전제가 깨지고
        /// 샤프트 연출·대표 칸(<c>Hole</c>)이 엉뚱한 곳을 가리킨다.
        /// </summary>
        [Test]
        public void HoleTiles_FormOneConnectedBlob()
        {
            foreach (int seed in Seeds)
            {
                var map = new GridMap();
                DungeonLayout dungeon = DungeonGenerator.Generate(
                    map, 13, 13, 10, seed: seed, direction: DungeonProgressDirection.Ascend);

                foreach (DungeonFloorInfo floor in dungeon.Floors)
                {
                    if (floor.HoleTiles.Count <= 1) continue;

                    var remaining = new HashSet<GridPos>(floor.HoleTiles);
                    var queue = new Queue<GridPos>();
                    queue.Enqueue(floor.HoleTiles[0]);
                    remaining.Remove(floor.HoleTiles[0]);
                    while (queue.Count > 0)
                    {
                        GridPos c = queue.Dequeue();
                        foreach (GridPos n in new[] { c.North, c.East, c.South, c.West })
                            if (remaining.Remove(n)) queue.Enqueue(n);
                    }

                    Assert.IsEmpty(
                        remaining,
                        $"seed {seed} {floor.FloorIndex}층: 개구부가 흩어져 있다");
                }
            }
        }

        /// <summary>
        /// 지역이 <b>생성기까지 실제로 도달하는가</b>. 위의 골든이 "안 바뀌었다"를 증명한다면
        /// 이쪽은 "바뀔 곳은 바뀐다"를 증명한다 — 둘 다 없으면 배관이 끊겨도 전부 통과한다.
        /// 웅덩이를 쓰는 이유는 지역 정체성 다이얼 중 생성기 출력에 직접 나타나는 값이라서다.
        /// </summary>
        [Test]
        public void RegionProfile_ReachesTheGenerator_AndChangesTheWaterStage()
        {
            int facility = CountWetTiles(DungeonRegionProfile.Facility);
            int flooded = CountWetTiles(DungeonRegionProfile.Flooded);
            int ember = CountWetTiles(DungeonRegionProfile.Ember);

            Assert.Greater(flooded, facility, "침수된 금고가 기준 지역보다 젖어 있어야 한다");
            Assert.Less(ember, facility, "잿불 성채는 물이 드물어야 불 연쇄가 선다");
        }

        private static int CountWetTiles(DungeonRegionProfile region)
        {
            int wet = 0;
            foreach (int seed in Seeds)
            {
                var map = new GridMap();
                DungeonGenerator.Generate(map, 13, 13, 10, seed: seed, region: region);
                foreach (KeyValuePair<GridPos, TileData> cell in map.All())
                    if (cell.Value.wet) wet++;
            }
            return wet;
        }

        /// <summary>seed 여러 개의 전체 레이아웃을 직렬화해 FNV-1a 64 로 접는다.</summary>
        private static string Fingerprint(
            DungeonProgressDirection direction, int floorCount, int firstBuildingFloor)
        {
            var text = new StringBuilder();
            foreach (int seed in Seeds)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(
                    map, 13, 13, floorCount, seed: seed,
                    direction: direction, firstBuildingFloor: firstBuildingFloor);

                text.Append("seed=").Append(seed).Append('\n');
                AppendMap(text, map);
                foreach (DungeonFloorInfo floor in layout.Floors) AppendFloor(text, floor);
            }
            return Fold(text.ToString());
        }

        private static void AppendMap(StringBuilder text, GridMap map)
        {
            // 딕셔너리 순회 순서에 기대지 않는다 — 지문이 흔들리면 회귀와 구분할 수 없다.
            var cells = new List<KeyValuePair<GridPos, TileData>>(map.All());
            cells.Sort((a, b) => Compare(a.Key, b.Key));
            foreach (KeyValuePair<GridPos, TileData> cell in cells)
            {
                text.Append(Pos(cell.Key)).Append('=').Append((int)cell.Value.kind);
                if (cell.Value.wet) text.Append('w');
                if (cell.Value.oiled) text.Append('o');

                var links = new List<GridPos>(map.LinksFrom(cell.Key));
                links.Sort(Compare);
                foreach (GridPos link in links) text.Append("->").Append(Pos(link));
                text.Append('\n');
            }
        }

        private static void AppendFloor(StringBuilder text, DungeonFloorInfo floor)
        {
            text.Append("floor ").Append(floor.FloorIndex)
                .Append(" p").Append(floor.ProgressIndex)
                .Append(" entry").Append(Pos(floor.Entry))
                .Append(" up").Append(Pos(floor.UpStairs))
                .Append(" down").Append(Pos(floor.DownStairs))
                .Append(" hole").Append(Pos(floor.Hole))
                .Append(" rest").Append(Pos(floor.RestSite))
                .Append(" exit").Append(Pos(floor.ExtractionPoint))
                .Append(" mark").Append(Pos(floor.Landmark))
                .Append(" lift").Append(Pos(floor.ElevatorShaft))
                .Append(" land").Append(Pos(floor.ElevatorLanding))
                .Append(" npc").Append(Pos(floor.RescueNpc)).Append(floor.RescueNpcId)
                .Append(" secret").Append(Pos(floor.SecretDoor))
                .Append(" reward").Append(Pos(floor.SecretReward))
                .Append('\n');

            AppendPositions(text, "spawns", floor.EnemySpawns);
            AppendPositions(text, "doors", floor.Doors);
            AppendPositions(text, "windows", floor.Windows);
            AppendPositions(text, "secretTiles", floor.SecretRoomTiles);

            text.Append("items");
            foreach (ItemSpawn item in floor.Items)
                text.Append(' ').Append(Pos(item.Position)).Append(':').Append((int)item.Kind);
            text.Append('\n');
        }

        private static void AppendPositions(
            StringBuilder text, string label, IReadOnlyList<GridPos> positions)
        {
            text.Append(label);
            foreach (GridPos pos in positions) text.Append(' ').Append(Pos(pos));
            text.Append('\n');
        }

        private static int Compare(GridPos a, GridPos b)
        {
            if (a.elevation != b.elevation) return a.elevation.CompareTo(b.elevation);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.x.CompareTo(b.x);
        }

        private static string Pos(GridPos pos) => $"({pos.x},{pos.y},{pos.elevation})";

        private static string Pos(GridPos? pos) => pos.HasValue ? Pos(pos.Value) : "-";

        private static string Fold(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }
    }
}
