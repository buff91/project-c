using ProjectC.Core;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 일반 던전 바닥의 좌표 기반 시각 변주. 강한 균열·그레이트 스프라이트를 모든 셀에
    /// 반복하지 않고, 시드와 카메라 방향에 무관한 희소 지점에만 배치한다.
    /// </summary>
    internal static class DungeonFloorPresentation
    {
        internal const int SurfaceVariationCount = 32;

        private const uint DetailSalt = 0xA511E9B3u;

        internal static int SurfaceVariation(GridPos pos, DungeonVisualContext context) =>
            (int)(MixedHash(pos, context.ProgressIndex, 0u) % SurfaceVariationCount);

        /// <summary>
        /// 밴드 전용 손상 타일을 쓸 위치인가. 후보를 4방향 이웃보다 해시가 낮은 지점으로
        /// 제한해 같은 강한 무늬가 맞붙거나 대각선 산술 줄무늬로 보이지 않게 한다.
        /// </summary>
        internal static bool ShouldUseBandDetail(
            GridPos pos,
            DungeonVisualContext context)
        {
            int cutoff;
            switch (context.DepthBand)
            {
                case DungeonDepthBand.Mid:
                    cutoff = 32;
                    break;
                case DungeonDepthBand.Deep:
                    cutoff = 40;
                    break;
                case DungeonDepthBand.Boss:
                    cutoff = 48;
                    break;
                default:
                    return false;
            }

            byte rank = DetailRank(pos, context.ProgressIndex);
            if (rank >= cutoff) return false;

            return rank < DetailRank(
                       new GridPos(pos.x - 1, pos.y, pos.elevation),
                       context.ProgressIndex) &&
                   rank < DetailRank(
                       new GridPos(pos.x + 1, pos.y, pos.elevation),
                       context.ProgressIndex) &&
                   rank < DetailRank(
                       new GridPos(pos.x, pos.y - 1, pos.elevation),
                       context.ProgressIndex) &&
                   rank < DetailRank(
                       new GridPos(pos.x, pos.y + 1, pos.elevation),
                       context.ProgressIndex);
        }

        private static byte DetailRank(GridPos pos, int progressIndex) =>
            (byte)(MixedHash(pos, progressIndex, DetailSalt) >> 24);

        private static uint MixedHash(GridPos pos, int progressIndex, uint salt)
        {
            unchecked
            {
                uint hash = (uint)pos.x * 73856093u;
                hash ^= (uint)pos.y * 19349663u;
                hash ^= (uint)pos.elevation * 83492791u;
                hash ^= (uint)progressIndex * 2654435761u;
                hash ^= salt;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
