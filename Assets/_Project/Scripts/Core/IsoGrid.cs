using System;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// 아이소메트릭 격자의 좌표 변환 & 정렬 규칙. (GDD §5.1, M0)
    /// - 격자(GridPos) ↔ 월드 좌표 변환 (elevation 높이 반영)
    /// - 월드/화면 좌표 → 격자 역변환 (탭 입력 처리)
    /// - elevation 을 반영한 정렬(sorting order) 계산
    ///
    /// 순수 계산 클래스(MonoBehaviour 아님) — Unity 없이 로직 테스트 가능(Vector 타입만 사용).
    /// 정렬 리스크(§9 "아이소 정렬 지옥") 대응: 규칙을 여기 한 곳에 초기 확립.
    /// </summary>
    [Serializable]
    public class IsoGrid
    {
        // 2:1 다이아몬드 타일 기준 기본값. 월드 유닛(=타일 스프라이트의 논리 크기) 단위.
        [Tooltip("타일 다이아몬드의 가로 폭(월드 유닛).")]
        public float tileWidth = 1.0f;

        [Tooltip("타일 다이아몬드의 세로 높이(월드 유닛). 보통 tileWidth 의 절반(2:1).")]
        public float tileHeight = 0.5f;

        [Tooltip("elevation 1당 화면상 들어올리는 월드 높이.")]
        public float elevationStep = 0.25f;

        [Tooltip("정렬 시 한 elevation 층이 차지하는 sortingOrder 대역. x+y 범위보다 커야 층이 안 섞임.")]
        public int elevationSortBand = 1000;

        public IsoGrid() { }

        public IsoGrid(float tileWidth, float tileHeight, float elevationStep)
        {
            this.tileWidth = tileWidth;
            this.tileHeight = tileHeight;
            this.elevationStep = elevationStep;
        }

        private float HalfW => tileWidth * 0.5f;
        private float HalfH => tileHeight * 0.5f;

        /// <summary>
        /// 격자 → 월드 좌표. (타일 중심)
        /// x가 커지면 화면 오른쪽-아래, y가 커지면 화면 왼쪽-아래로. elevation 은 위로 들어올린다.
        /// </summary>
        public Vector2 GridToWorld(GridPos pos)
        {
            float wx = (pos.x - pos.y) * HalfW;
            float wy = -(pos.x + pos.y) * HalfH + pos.elevation * elevationStep;
            return new Vector2(wx, wy);
        }

        /// <summary>
        /// 월드 좌표 → 격자. 어느 elevation 평면을 클릭했는지 알아야 하므로 elevation 을 인자로 받는다.
        /// (탭→격자 역변환의 핵심. 여러 높이 후보는 호출부에서 위→아래로 시도.)
        /// </summary>
        public GridPos WorldToGrid(Vector2 world, int elevation = 0)
        {
            // 해당 elevation 이 들어올린 높이를 먼저 제거.
            float wy = world.y - elevation * elevationStep;

            // wx = (x - y) * HalfW  ->  a = x - y
            // wy = -(x + y) * HalfH ->  b = x + y
            float a = world.x / HalfW;
            float b = -wy / HalfH;

            float fx = (a + b) * 0.5f;
            float fy = (b - a) * 0.5f;

            return new GridPos(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy), elevation);
        }

        /// <summary>
        /// 정렬 순서. 값이 클수록 앞(카메라 쪽)에 그려진다.
        /// 규칙(§9 대응): elevation 이 우선(위층이 아래층 앞) → 같은 elevation 안에서는 (x+y) 가 큰(앞쪽) 게 위.
        /// </summary>
        public int SortingOrder(GridPos pos)
        {
            return pos.elevation * elevationSortBand + (pos.x + pos.y);
        }

        /// <summary>
        /// 임의의 서브셀 오프셋(같은 타일 안에서 살짝 앞/뒤 미세조정)까지 반영한 정렬.
        /// 예: 같은 칸의 바닥 데칼 vs 그 위 캐릭터.
        /// </summary>
        public int SortingOrder(GridPos pos, int microOffset)
        {
            return SortingOrder(pos) * 8 + microOffset; // microOffset: -3..+3 정도 권장
        }
    }
}
