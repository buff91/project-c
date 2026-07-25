using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public static partial class DungeonGenerator
    {

        /// <summary>
        /// 적 스폰은 문 뒤(북쪽 방)에만 둔다 — 입구·동쪽 방에 두면 층 진입 즉시 인접 전투가
        /// 강제되고, "문을 열기 전에는 차단" 불변식도 깨진다. 수는 깊이에 따라 1~4.
        /// </summary>
        private static void PickEnemySpawns(GridMap map, Random random, FloorPlan p, int floorCount)
        {
            var candidates = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                if (x == p.VerticalX && y == p.UpperMinY) continue;
                var pos = new GridPos(x, y, p.BaseElevation);
                if (pos == p.RestSite) continue;
                if (map.Get(pos)?.kind == TileKind.Floor)
                    candidates.Add(pos);
            }

            int depth = -p.FloorIndex;
            int bandExtra = DungeonBandProfiles.ForDepth(depth).ExtraEnemies;
            int desired = 1 + random.Next(0, 2) + depth / 2 + bandExtra + AreaSpawnBonus(p.Width, p.Height);
            p.EnemySpawns.AddRange(TakeRandom(candidates, desired, random));

            // 하행 계단 경비병: 완주 동선(남쪽 방→하행 계단)이 전투를 완전히
            // 우회하지 못하게 한다. 계단 인접(체비셰프 3)에 배치해 카이팅 거리를 줄이고,
            // 수는 1+depth 로 깊을수록 무거운 관문. 샤프트 교대 규칙상 하행 방은 도착
            // 방과 항상 다르고 입구에서 수평 문 뒤라 "문 열기 전 차단" 불변식 유지.
            // (밸런스 시뮬 600판 근거 — 직행 정책 완주율 94%)
            // 최심층(아레나)은 경비병 무리 대신 보스 1:1을 위해 하행 경비를 생략한다.
            bool arenaFloor = DungeonBossArenaRules.IsArenaFloor(depth, floorCount);
            if (p.Down.HasValue && !arenaFloor)
            {
                bool eastRoom = p.Down.Value.x != 1;
                int guardMinX = eastRoom ? p.RightMinX : 0;
                int guardMaxX = eastRoom ? p.Width - 1 : p.LeftMaxX;
                var guardPool = new List<GridPos>();
                for (int x = guardMinX; x <= guardMaxX; x++)
                for (int y = 0; y <= p.LowerMaxY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (pos == p.Entry) continue;
                    if (pos == p.RestSite) continue;
                    if (pos.ChebyshevTo(p.Down.Value) > 3) continue;
                    if (map.Get(pos)?.kind != TileKind.Floor) continue;
                    if (p.EnemySpawns.Contains(pos)) continue;
                    guardPool.Add(pos);
                }
                p.EnemySpawns.AddRange(TakeRandom(guardPool, 1 + depth, random));
            }

            // 북쪽 방 바닥이 전부 특수 타일로 채워지는 경우는 없지만, 방어적으로 높은 단을 쓴다.
            if (p.EnemySpawns.Count == 0)
                p.EnemySpawns.Add(p.At(p.StairX, p.RaisedY));
        }

        /// <summary>
        /// 최심층 보스 아레나의 랜드마크(제단) 한 칸. 뒤쪽 올라온 단(dais)의 후면-중앙
        /// Floor 타일을 결정론적으로 고른다 — RNG를 쓰지 않아 생성 스트림을 흔들지 않는다.
        /// 적·아이템은 낮은 북쪽 방(BaseElevation)에만 스폰되므로 올라온 단과 겹치지 않는다.
        /// </summary>
        private static void PlaceBossLandmark(GridMap map, FloorPlan p, int floorCount)
        {
            if (!DungeonBossArenaRules.IsArenaFloor(-p.FloorIndex, floorCount)) return;

            for (int y = p.Height - 1; y >= p.RaisedY; y--)
            for (int dx = 0; dx <= p.UpperMaxX - p.UpperMinX; dx++)
            {
                foreach (int x in new[] { p.StairX - dx, p.StairX + dx })
                {
                    if (x < p.UpperMinX || x > p.UpperMaxX) continue;
                    var pos = new GridPos(x, y, p.BaseElevation + 1);
                    if (map.Get(pos)?.kind != TileKind.Floor) continue;
                    p.Landmark = pos;
                    return;
                }
            }
        }

        /// <summary>
        /// 던전 장비 배치 — 층당 최대 하나. 깊이 게이트·확률·종류는 <see cref="EquipmentDropRules"/>가
        /// 소유한다. 주운 장비는 백팩 면적을 먹고, 살아 나와야 창고로 들어간다(익스트랙션).
        /// 아이템 배치가 끝난 뒤 남은 빈 칸에만 놓아 기존 스폰과 겹치지 않는다.
        /// </summary>
        private static void PlaceEquipment(GridMap map, Random random, FloorPlan p)
        {
            EquipmentDefinition equipment = EquipmentDropRules.Roll(-p.FloorIndex, random);
            if (equipment == null) return;

            var candidates = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                if (x == p.VerticalX && y == p.UpperMinY) continue;
                var pos = new GridPos(x, y, p.BaseElevation);
                if (IsFreeForSpawn(map, p, pos)) candidates.Add(pos);
            }

            foreach (GridPos pos in TakeRandom(candidates, 1, random))
                p.Items.Add(new ItemSpawn(pos, equipment.Item));
        }

        /// <summary>스폰이 겹치지 않는 빈 바닥인가. 아이템·장비 배치가 공유하는 판정.</summary>
        private static bool IsFreeForSpawn(GridMap map, FloorPlan p, GridPos pos)
        {
            if (map.Get(pos)?.kind != TileKind.Floor) return false;
            if (pos == p.Entry || pos == p.RestSite) return false;
            if (p.EnemySpawns.Contains(pos)) return false;
            foreach (ItemSpawn spawn in p.Items)
                if (spawn.Position == pos) return false;
            return true;
        }

        /// <summary>
        /// 건물형 수직성(v0.3): 사다리 위에 얹는 +2단 캐치워크.
        /// 길이는 밴드 프로파일이 소유하고(얕은 밴드 0), 최심층 아레나는 1:1 결투 공간을
        /// 비우려 놓지 않는다. 사다리 링크로만 올라가므로 도달성 불변식이 유지되고,
        /// RNG를 쓰지 않아 생성 스트림도 흔들지 않는다.
        ///
        /// 큰 단차(+2)는 높이 인식 FOV 차폐(<see cref="SightRules.HeightBlockThreshold"/>)와
        /// 내려치기·고지대 사격이 실제로 발동하는 층 내부 무대다.
        /// </summary>
        private static void PlaceCatwalk(GridMap map, FloorPlan p, int floorCount)
        {
            int depth = -p.FloorIndex;
            if (DungeonBossArenaRules.IsArenaFloor(depth, floorCount)) return;

            int length = DungeonBandProfiles.ForDepth(depth).CatwalkLength;
            if (length <= 0) return;

            var ladderTop = new GridPos(p.LadderX, p.RaisedY, p.BaseElevation + 1);
            for (int i = 0; i < length; i++)
            {
                int y = p.RaisedY + i;
                if (y >= p.Height) break;
                // 아래에 올라온 단이 실제로 있는 칸에만 얹는다 — 허공에 뜬 발판을 만들지 않는다.
                if (map.Get(new GridPos(p.LadderX, y, p.BaseElevation + 1)) == null) break;

                var catwalk = new GridPos(p.LadderX, y, p.BaseElevation + 2);
                map.Set(catwalk, TileKind.Floor);
                // 첫 칸만 사다리와 명시적 링크로 잇는다. 나머지는 같은 높이 평면 이웃이라 그냥 걷는다.
                if (i == 0) map.Connect(ladderTop, catwalk);
            }
        }

        /// <summary>
        /// 건물형 수직성(v0.3): 북쪽 방 왼쪽 외벽에 낙하형 창문 하나(위치 잠정, RNG 미사용).
        /// 유리는 이동을 막지만 시야는 통과하고, 깨고 넘어(넉백) 창밖이 한 층 아래 바닥이면
        /// 낙하로 이어진다. 창문 자리는 원래 void(벽)라 이동·도달성 불변 — 시야만 바깥으로 연다.
        /// 창밖(outward)이 실제 한 층 아래 걷는 바닥으로 떨어지는(구멍과 같은 검증) 자리에만 둔다.
        /// </summary>
        private static void PlaceWindows(
            GridMap map, DungeonHeightModel heightModel, FloorPlan p, int bottomElevation)
        {
            if (p.UpperMinX < 2) return;
            int wallX = p.UpperMinX - 1;   // 방 왼쪽 바로 바깥(문 반대쪽 외벽)
            int outX = p.UpperMinX - 2;    // 창밖(허공)

            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                var inside = new GridPos(p.UpperMinX, y, p.BaseElevation);
                var wall = new GridPos(wallX, y, p.BaseElevation);
                var outward = new GridPos(outX, y, p.BaseElevation);

                if (map.Get(inside)?.kind != TileKind.Floor) continue; // 안쪽이 방 바닥
                if (map.Get(wall) != null) continue;                   // 창 자리가 void(벽)
                if (map.Get(outward) != null) continue;                // 창밖도 허공
                if (!LandsOneFloorBelow(map, heightModel, outward, bottomElevation, p.FloorIndex))
                    continue;                                          // 창밖이 한 층 아래로 떨어짐

                map.Set(wall, TileKind.Window);
                p.Windows.Add(wall);
                return; // 층당 하나(잠정)
            }
        }

        /// <summary>
        /// 물 웅덩이 배치 (GDD §5.5 — 물+빙결 광역 결빙의 무대).
        /// 층마다 절반 확률로 남쪽 방 평지에 2~4칸짜리 이어진 웅덩이 하나를 만든다.
        /// 입구·계단·특수 타일은 피하고 순수 Floor 만 적신다.
        /// </summary>
        private static void PlacePuddle(GridMap map, Random random, FloorPlan p)
        {
            // 웅덩이 확률도 깊이 밴드별(깊을수록 물+빙결 무대 증가).
            int puddleChance = DungeonBandProfiles.ForDepth(-p.FloorIndex).PuddleChancePercent;
            if (random.Next(0, 100) >= puddleChance) return;

            var seeds = new List<GridPos>();
            for (int x = 0; x <= p.LeftMaxX; x++)
            for (int y = 0; y <= p.LowerMaxY; y++)
            {
                var pos = new GridPos(x, y, p.BaseElevation);
                if (pos != p.Entry && pos != p.RestSite && map.Get(pos)?.kind == TileKind.Floor)
                    seeds.Add(pos);
            }
            if (seeds.Count == 0) return;

            GridPos current = seeds[random.Next(seeds.Count)];
            int size = 2 + random.Next(0, 3);
            for (int i = 0; i < size; i++)
            {
                map.Get(current).wet = true;
                var neighbors = new List<GridPos>();
                foreach (GridPos next in new[]
                         { current.Offset(1, 0), current.Offset(-1, 0), current.Offset(0, 1), current.Offset(0, -1) })
                {
                    TileData tile = map.Get(next);
                    if (next != p.Entry && next != p.RestSite &&
                        tile != null && tile.kind == TileKind.Floor && !tile.wet)
                        neighbors.Add(next);
                }
                if (neighbors.Count == 0) break;
                current = neighbors[random.Next(neighbors.Count)];
            }
        }

        /// <summary>
        /// 아이템 스폰. 막다른 분기 방이 있으면 보상 아이템 하나를 보장하고,
        /// 나머지는 북쪽·동쪽 방의 빈 바닥에 1~2개 흩뿌린다. 적 스폰과는 겹치지 않는다.
        /// </summary>
        private static void PlaceItems(GridMap map, Random random, FloorPlan p)
        {
            ItemKind RollKind()
            {
                // 분배(/21): 물약3 · 폭탄3 · 냉기1 · 기름1 · 단검1 · 두루마리1 ·
                // 통조림3(배고픔의 해답 — 굶어 죽는 게 기본값이 되지 않게 넉넉히) ·
                // 동전2 · 보석1 · 유물1(깊은 층 한정, 얕으면 동전으로 강등) ·
                // 약초2 · 화약1 · 서리 수정1(조합 재료, GDD §5.6)
                int roll = random.Next(0, 21);
                if (roll < 3) return ItemKind.Potion;
                if (roll < 6) return ItemKind.Bomb;
                if (roll < 7) return ItemKind.FrostBomb;
                if (roll < 8) return ItemKind.OilFlask;
                if (roll < 9) return ItemKind.ThrowingKnife;
                if (roll < 10) return ItemKind.RecallScroll;
                if (roll < 13) return ItemKind.CannedFood;
                if (roll < 15) return ItemKind.CoinPouch;
                if (roll < 16) return ItemKind.Gemstone;
                if (roll < 17) return p.FloorIndex <= -2 ? ItemKind.Relic : ItemKind.CoinPouch;
                if (roll < 19) return ItemKind.Herb;
                if (roll < 20) return ItemKind.BlastPowder;
                return ItemKind.FrostShard;
            }

            bool IsFree(GridPos pos) =>
                map.Get(pos)?.kind == TileKind.Floor &&
                pos != p.Entry &&
                pos != p.RestSite &&
                !p.EnemySpawns.Contains(pos);

            if (p.HasBranch)
            {
                var branchTiles = new List<GridPos>();
                for (int x = p.BranchMinX; x <= p.BranchMaxX; x++)
                for (int y = p.BranchMinY; y <= p.BranchMaxY; y++)
                {
                    var pos = new GridPos(x, y, p.BaseElevation);
                    if (IsFree(pos)) branchTiles.Add(pos);
                }
                foreach (GridPos pos in TakeRandom(branchTiles, 1, random))
                {
                    ItemKind kind = p.BranchIsSecret
                        ? (p.FloorIndex <= -3 ? ItemKind.Relic : ItemKind.Gemstone)
                        : RollKind();
                    p.Items.Add(new ItemSpawn(pos, kind));
                    if (p.BranchIsSecret)
                        p.SecretReward = pos;
                }
            }

            var scatter = new List<GridPos>();
            for (int x = p.UpperMinX; x <= p.UpperMaxX; x++)
            for (int y = p.UpperMinY; y < p.RaisedY; y++)
            {
                if (x == p.VerticalX && y == p.UpperMinY) continue;
                var pos = new GridPos(x, y, p.BaseElevation);
                if (IsFree(pos)) scatter.Add(pos);
            }
            for (int x = p.RightMinX; x < p.Width; x++)
            for (int y = 0; y <= p.LowerMaxY; y++)
            {
                var pos = new GridPos(x, y, p.BaseElevation);
                if (IsFree(pos)) scatter.Add(pos);
            }

            int scatterCount = 1 + random.Next(0, 2) + AreaSpawnBonus(p.Width, p.Height) / 2;
            foreach (GridPos pos in TakeRandom(scatter, scatterCount, random))
                p.Items.Add(new ItemSpawn(pos, RollKind()));
        }
    }
}
