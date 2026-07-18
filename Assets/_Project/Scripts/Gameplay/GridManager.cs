using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 격자 데이터(GridMap)와 변환 규칙(IsoGrid)을 씬에서 소유하는 얇은 진입점. (M0)
    /// 로직은 Core 에 있고, 여기서는 Unity(카메라/월드) 와 이어주기만 한다.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("변환/정렬 규칙")]
        public IsoGrid iso = new IsoGrid();

        [Header("데모용 자동 생성 (M0 확인용)")]
        [Tooltip("Play 시 작은 데모 층을 생성해 변환/정렬을 눈으로 확인.")]
        public bool buildDemoOnStart = true;
        public int demoSize = 6;

        public GridMap Map { get; } = new GridMap();

        private void Start()
        {
            if (buildDemoOnStart)
                BuildDemoFloor();
        }

        /// <summary>
        /// 데모 층: 평평한 바닥 + 한쪽 계단으로 올라간 상단 + 가운데 구멍.
        /// (다층 격자/elevation/구멍 개념을 한 눈에 검증)
        /// </summary>
        public void BuildDemoFloor()
        {
            Map.Clear();
            int n = demoSize;

            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                // 오른쪽 절반은 한 단 높은 바닥(elevation 1), 계단 한 줄로 연결.
                int e = (x >= n / 2 + 1) ? 1 : 0;
                var kind = TileKind.Floor;

                if (x == n / 2) { e = 0; kind = TileKind.Stairs; }         // 계단 줄
                if (x == n / 2 - 1 && y == n / 2) kind = TileKind.Hole;    // 구멍 하나

                Map.Set(new GridPos(x, y, e), kind);
            }

            Debug.Log($"[GridManager] 데모 층 생성: {Map.Count} 타일");
        }

        // ── 변환 헬퍼 (Unity 연결부) ──────────────────────────────

        public Vector3 GridToWorld(GridPos pos)
        {
            Vector2 w = iso.GridToWorld(pos);
            return new Vector3(w.x, w.y, 0f);
        }

        /// <summary>스크린 좌표(탭/클릭) → 격자. 지정 elevation 평면 기준. (탭→격자 역변환, M0)</summary>
        public GridPos ScreenToGrid(Vector2 screenPoint, Camera cam, int elevation = 0)
        {
            if (cam == null) cam = Camera.main;
            Vector3 world = cam.ScreenToWorldPoint(screenPoint);
            return iso.WorldToGrid(new Vector2(world.x, world.y), elevation);
        }
    }
}
