using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 창문 상호작용. 온전한 창문(Window)은 이동을 막지만 시야는 통과하는 수평 시야 포털이고,
    /// 깨면(WindowBroken) 통로가 된다(다시 닫을 수 없다). 깨진 창문 밖이 허공/아래층이면
    /// 이동·넉백이 그대로 FallRules 낙하로 이어진다. (GDD §5.2/§5.3, 건물형 수직성 v0.3)
    /// </summary>
    public static class WindowRules
    {
        /// <summary>
        /// 온전한 창문을 깨 통로(WindowBroken)로 만든다. 창문이 아니거나 이미 깨졌으면 false.
        /// 상호작용·폭발·강한 충격 등 어떤 원인이든 이 한 곳으로 수렴시킨다.
        /// </summary>
        public static bool TryBreak(GridMap map, GridPos pos)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            TileData tile = map.Get(pos);
            if (tile == null || !tile.CanBreak) return false;

            tile.kind = TileKind.WindowBroken;
            return true;
        }
    }
}
