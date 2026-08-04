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

        [Tooltip("시점을 시계 방향으로 돌린 횟수. 0..3의 90도 단위.")]
        [Range(0, 3)] public int viewQuarterTurns;

        [Tooltip("시점 회전 중심이 되는 격자 좌표.")]
        public float viewPivotX;
        public float viewPivotY;

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
        /// 같은 elevation 안에서 (x+y) 깊이를 sortingOrder 정수로 양자화하는 해상도.
        /// GridPos는 정수 좌표고 시점 피벗은 방 중앙(정수/반정수)이므로 90도 회전 후에도
        /// view.x + view.y 깊이는 항상 정수다. 따라서 1이면 서로 다른 대각선을 모두 구분한다.
        /// </summary>
        public const int DepthResolution = 1;

        /// <summary>
        /// elevation 하나가 차지하는 깊이 대역. 최대 20×20 맵의 깊이 범위 0..38보다
        /// 크게 유지해 인접 elevation이 절대 섞이지 않게 한다.
        /// </summary>
        public const int ElevationSortBand = 39;

        /// <summary>
        /// 한 깊이 안에서 microOffset이 차지하는 하위 대역. 공식 슬롯 -2..+2를
        /// 인접 깊이와 섞이지 않게 정확히 5칸으로 둔다.
        /// </summary>
        public const int MicroResolution = 5;

        /// <summary>
        /// 격자 → 월드 좌표. (타일 중심)
        /// x가 커지면 화면 오른쪽-아래, y가 커지면 화면 왼쪽-아래로. elevation 은 위로 들어올린다.
        /// </summary>
        public Vector2 GridToWorld(GridPos pos)
        {
            return GridToWorld(pos, viewQuarterTurns);
        }

        /// <summary>
        /// 현재 상태를 바꾸지 않고 지정한 90도 시점에서 격자 중심을 투영한다.
        /// 네 시점의 카메라 경계를 비교하는 프레젠테이션 계산처럼, 실제 입력/정렬 시점을
        /// 임시로 돌리면 안 되는 읽기 전용 경로에서 사용한다.
        /// </summary>
        public Vector2 GridToWorld(GridPos pos, int quarterTurns)
        {
            Vector2 view = RotateToView(pos.x, pos.y, quarterTurns);
            float wx = (view.x - view.y) * HalfW;
            float wy = -(view.x + view.y) * HalfH + pos.elevation * elevationStep;
            return new Vector2(wx, wy);
        }

        /// <summary>
        /// target이 origin보다 화면 오른쪽에 투영되는지 반환한다.
        /// 방향성 스프라이트는 월드 축이 아니라 현재 회전된 화면 좌우를 기준으로 고른다.
        /// </summary>
        public bool ProjectsToScreenRight(GridPos origin, GridPos target)
        {
            return GridToWorld(target).x > GridToWorld(origin).x;
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

            float viewX = (a + b) * 0.5f;
            float viewY = (b - a) * 0.5f;
            Vector2 grid = RotateFromView(viewX, viewY);

            return new GridPos(Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.y), elevation);
        }

        /// <summary>
        /// 정렬 순서. 값이 클수록 앞(카메라 쪽)에 그려진다.
        /// 규칙(§9 대응): elevation 이 우선(위층이 아래층 앞) → 같은 elevation 안에서는 (x+y) 가 큰(앞쪽) 게 위.
        /// </summary>
        public int SortingOrder(GridPos pos)
        {
            Vector2 view = RotateToView(pos.x, pos.y);
            int viewDepth = Mathf.RoundToInt((view.x + view.y) * DepthResolution);
            return pos.elevation * ElevationSortBand + viewDepth;
        }

        /// <summary>
        /// 임의의 서브셀 오프셋(같은 타일 안에서 살짝 앞/뒤 미세조정)까지 반영한 정렬.
        /// 예: 같은 칸의 바닥 데칼 vs 그 위 캐릭터.
        /// </summary>
        public int SortingOrder(GridPos pos, int microOffset)
        {
            return SortingOrder(pos) * MicroResolution + microOffset; // 공식 micro 슬롯: -2..+2
        }

        /// <summary>
        /// 한 칸 이동 중인 스프라이트의 정렬 순서. 발 피벗이 두 칸의 화면상 경계를
        /// 넘는 절반 지점까지는 출발 칸, 그 뒤에는 도착 칸의 순서를 쓴다.
        /// 도착 순서를 이동 시작부터 적용하면 뒤쪽으로 걷는 액터가 아직 출발 칸에
        /// 있는데도 앞 타일 아래로 들어가는 한 프레임 가림이 생긴다.
        /// </summary>
        public int SortingOrderDuringMove(
            GridPos from,
            GridPos to,
            float progress,
            int microOffset)
        {
            GridPos anchor = Mathf.Clamp01(progress) < 0.5f ? from : to;
            return SortingOrder(anchor, microOffset);
        }

        public void RotateView(int direction)
        {
            viewQuarterTurns = NormalizeQuarterTurns(viewQuarterTurns + direction);
        }

        public void SetViewRotation(int quarterTurns)
        {
            viewQuarterTurns = NormalizeQuarterTurns(quarterTurns);
        }

        public Vector2 RotateToView(float x, float y)
        {
            return RotateToView(x, y, viewQuarterTurns);
        }

        /// <summary>현재 <see cref="viewQuarterTurns"/>를 바꾸지 않는 명시적 시점 회전.</summary>
        public Vector2 RotateToView(float x, float y, int quarterTurns)
        {
            float dx = x - viewPivotX;
            float dy = y - viewPivotY;
            switch (NormalizeQuarterTurns(quarterTurns))
            {
                case 1: return new Vector2(viewPivotX + dy, viewPivotY - dx);
                case 2: return new Vector2(viewPivotX - dx, viewPivotY - dy);
                case 3: return new Vector2(viewPivotX - dy, viewPivotY + dx);
                default: return new Vector2(x, y);
            }
        }

        /// <summary>
        /// 화면(뷰) 기준 방향 델타를 격자 델타로 되돌린다 (피벗 무관 순수 회전).
        /// 방향키 이동: 뷰 (0,-1)=화면 오른쪽 위, (1,0)=오른쪽 아래, (0,1)=왼쪽 아래, (-1,0)=왼쪽 위.
        /// </summary>
        public Vector2 RotateDeltaFromView(float dx, float dy)
        {
            switch (NormalizeQuarterTurns(viewQuarterTurns))
            {
                case 1: return new Vector2(-dy, dx);
                case 2: return new Vector2(-dx, -dy);
                case 3: return new Vector2(dy, -dx);
                default: return new Vector2(dx, dy);
            }
        }

        public Vector2 RotateFromView(float x, float y)
        {
            float dx = x - viewPivotX;
            float dy = y - viewPivotY;
            switch (NormalizeQuarterTurns(viewQuarterTurns))
            {
                case 1: return new Vector2(viewPivotX - dy, viewPivotY + dx);
                case 2: return new Vector2(viewPivotX - dx, viewPivotY - dy);
                case 3: return new Vector2(viewPivotX + dy, viewPivotY - dx);
                default: return new Vector2(x, y);
            }
        }

        private static int NormalizeQuarterTurns(int value)
        {
            int normalized = value % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }
    }
}
