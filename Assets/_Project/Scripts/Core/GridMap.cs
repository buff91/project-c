using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 다층 격자 데이터 저장소. (GDD §5.1, M0)
    /// Dictionary 기반 희소(sparse) 저장 — 넓고 대부분 비어있는 다층 던전에 유리.
    /// "렌더링되는 높이 ≠ 시뮬레이션되는 범위" 분리를 위해 데이터는 여기서만 다룬다. (GDD §5.7)
    /// 순수 C# — Unity 없이 테스트 가능.
    /// </summary>
    public class GridMap
    {
        private readonly Dictionary<GridPos, TileData> _tiles = new Dictionary<GridPos, TileData>();

        public int Count => _tiles.Count;

        /// <summary>해당 좌표에 타일이 정의돼 있으면 반환, 없으면 null.</summary>
        public TileData Get(GridPos pos) => _tiles.TryGetValue(pos, out var t) ? t : null;

        public bool TryGet(GridPos pos, out TileData tile) => _tiles.TryGetValue(pos, out tile);

        public bool Has(GridPos pos) => _tiles.ContainsKey(pos);

        /// <summary>타일 설정(덮어쓰기).</summary>
        public void Set(GridPos pos, TileData tile) => _tiles[pos] = tile;

        public void Set(GridPos pos, TileKind kind) => _tiles[pos] = new TileData(kind);

        public bool Remove(GridPos pos) => _tiles.Remove(pos);

        public void Clear() => _tiles.Clear();

        public IEnumerable<KeyValuePair<GridPos, TileData>> All() => _tiles;

        /// <summary>이동 진입 가능 여부. 타일이 없으면(허공) 불가.</summary>
        public bool IsWalkable(GridPos pos) => TryGet(pos, out var t) && t.IsWalkable;

        /// <summary>단단한 바닥 여부. 낙하 착지 판정에 사용.</summary>
        public bool IsSolidGround(GridPos pos) => TryGet(pos, out var t) && t.IsSolidGround;

        /// <summary>
        /// pos 에서 아래로 내려가며 첫 단단한 바닥의 elevation 을 찾는다. (GDD §5.3 낙하 처리)
        /// 찾으면 착지 좌표를, 못 찾으면(무저갱) null.
        /// </summary>
        public GridPos? FindLandingBelow(GridPos from, int minElevation)
        {
            for (int e = from.elevation - 1; e >= minElevation; e--)
            {
                var below = from.WithElevation(e);
                if (IsSolidGround(below))
                    return below;
            }
            return null;
        }
    }
}
