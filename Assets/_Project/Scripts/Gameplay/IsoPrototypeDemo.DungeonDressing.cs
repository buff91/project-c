using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 첫 던전 B2 시작 공간의 낮은 비충돌 드레싱.
    /// 별도 장애물을 만들지 않고 선택된 바닥 타일의 완성형 스프라이트만 교체한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private readonly Dictionary<GridPos, Sprite> _dungeonFloorDressing =
            new Dictionary<GridPos, Sprite>();

        private void ResetDungeonDressingForBuild() => _dungeonFloorDressing.Clear();

        private bool TryGetDungeonFloorDressing(GridPos pos, out Sprite sprite) =>
            _dungeonFloorDressing.TryGetValue(pos, out sprite);

        private void PrepareDungeonDressing()
        {
            _dungeonFloorDressing.Clear();
            if (hubMode || _dungeon == null || visualCatalog == null ||
                !_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active) ||
                _dungeon.ProgressIndexFor(active.FloorIndex) != 0)
                return;

            var sprites = new List<Sprite>(2);
            if (visualCatalog.b2ParkingWheelStopFloor != null)
                sprites.Add(visualCatalog.b2ParkingWheelStopFloor);
            if (visualCatalog.b2FallenWayfindingFloor != null)
                sprites.Add(visualCatalog.b2FallenWayfindingFloor);
            if (sprites.Count == 0) return;

            HashSet<GridPos> reserved = BuildDungeonDressingReserved(active);
            var candidates = new List<GridPos>();
            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                if (pair.Value.kind != TileKind.Floor ||
                    pos.elevation != active.Entry.elevation ||
                    _dungeon.Height.FloorIndex(pos.elevation) != active.FloorIndex ||
                    IsFrontEdge(pos))
                    continue;
                candidates.Add(pos);
            }

            // RNG를 소비하지 않는다. 입구에서 먼 외곽부터 보되 동률만 seed 해시로 풀어
            // 같은 seed/방은 언제 다시 그려도 같은 두 칸을 고른다.
            candidates.Sort((left, right) =>
            {
                int distance = right.ManhattanTo(active.Entry)
                    .CompareTo(left.ManhattanTo(active.Entry));
                if (distance != 0) return distance;
                int order = StableDungeonDressingOrder(left)
                    .CompareTo(StableDungeonDressingOrder(right));
                if (order != 0) return order;
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });

            IReadOnlyList<GridPos> selected =
                DungeonDressingPlacementRules.SelectSafePositions(
                    _grid.Map,
                    active.Entry,
                    candidates,
                    reserved,
                    sprites.Count);
            for (int index = 0; index < selected.Count; index++)
                _dungeonFloorDressing[selected[index]] = sprites[index];
        }

        private HashSet<GridPos> BuildDungeonDressingReserved(DungeonFloorInfo active)
        {
            var reserved = new HashSet<GridPos> { active.Entry };
            if (active.UpStairs.HasValue) reserved.Add(active.UpStairs.Value);
            if (active.DownStairs.HasValue) reserved.Add(active.DownStairs.Value);
            if (active.RestSite.HasValue) reserved.Add(active.RestSite.Value);
            if (active.ExtractionPoint.HasValue) reserved.Add(active.ExtractionPoint.Value);
            if (active.RescueNpc.HasValue) reserved.Add(active.RescueNpc.Value);
            if (active.Landmark.HasValue) reserved.Add(active.Landmark.Value);

            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State != null) reserved.Add(enemy.State.Position);
            }
            foreach (ItemAgent item in _items)
                reserved.Add(item.Spawn.Position);
            if (_barrelRenderer != null && !_barrelExploded)
            {
                reserved.Add(_barrelPos);
                reserved.Add(_barrelPos.North);
                reserved.Add(_barrelPos.East);
                reserved.Add(_barrelPos.South);
                reserved.Add(_barrelPos.West);
            }

            ReserveDungeonRoute(active.Entry, active.UpStairs, reserved);
            ReserveDungeonRoute(active.Entry, active.DownStairs, reserved);
            return reserved;
        }

        private void ReserveDungeonRoute(
            GridPos entry,
            GridPos? destination,
            ISet<GridPos> reserved)
        {
            if (!destination.HasValue) return;
            foreach (GridPos pos in GridPathfinder.FindPath(_grid.Map, entry, destination.Value))
                reserved.Add(pos);
        }

        private int StableDungeonDressingOrder(GridPos pos)
        {
            unchecked
            {
                int hash = pos.x * 73856093;
                hash ^= pos.y * 19349663;
                hash ^= pos.elevation * 83492791;
                hash ^= dungeonSeed * 486187739;
                return hash & int.MaxValue;
            }
        }
    }
}
