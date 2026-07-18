using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 격자 위에 놓이는 실제 스프라이트(캐릭터/오브젝트/타일)의 위치와 정렬을 격자 규칙에 맞춘다. (M0 정렬)
    /// GridPos 를 바꾸면 월드 위치와 SpriteRenderer.sortingOrder 가 IsoGrid 규칙대로 갱신된다.
    /// "아이소 정렬 지옥"(§9) 방지: 정렬 계산을 개별 오브젝트가 제각각 하지 않고 IsoGrid 한 곳에 위임.
    /// </summary>
    [ExecuteAlways]
    public class GridSortingObject : MonoBehaviour
    {
        public GridManager grid;

        [Tooltip("격자 좌표.")]
        public int x, y, elevation;

        [Tooltip("같은 칸 안에서의 미세 앞/뒤 조정 (예: 바닥=-2, 캐릭터=0).")]
        public int microOffset = 0;

        private SpriteRenderer _sr;

        public GridPos Pos
        {
            get => new GridPos(x, y, elevation);
            set { x = value.x; y = value.y; elevation = value.elevation; Apply(); }
        }

        private void OnEnable()
        {
            _sr = GetComponent<SpriteRenderer>();
            Apply();
        }

        private void OnValidate() => Apply();

        public void Apply()
        {
            if (grid == null) return;
            transform.position = grid.GridToWorld(Pos);

            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _sr.sortingOrder = grid.iso.SortingOrder(Pos, microOffset);
        }
    }
}
