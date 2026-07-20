using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 스프라이트 정렬과 화면상 겹침을 함께 사용해 플레이어 가림 후보를 판정한다.
    /// </summary>
    public static class SpriteOcclusion
    {
        public static bool ShouldFade(
            Bounds occluderBounds,
            Bounds playerBounds,
            int occluderSortingOrder,
            int playerSortingOrder)
        {
            return occluderSortingOrder >= playerSortingOrder &&
                   occluderBounds.Intersects(playerBounds);
        }
    }
}
