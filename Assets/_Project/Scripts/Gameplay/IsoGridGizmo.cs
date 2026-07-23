using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 스프라이트 에셋 없이도 Scene 뷰에서 아이소 격자를 눈으로 검증하기 위한 Gizmo. (M0)
    /// 각 타일을 다이아몬드로 그리고 elevation 별로 색을 달리한다.
    /// 아트 파이프라인 붙이기 전, 좌표 변환/정렬 규칙이 맞는지 확인하는 용도.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class IsoGridGizmo : MonoBehaviour
    {
        public bool drawInPlayMode = true;
        public Color floorColor = new Color(0.4f, 0.7f, 1f, 1f);
        public Color raisedColor = new Color(1f, 0.8f, 0.3f, 1f);
        public Color holeColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        public Color stairsColor = new Color(0.6f, 1f, 0.6f, 1f);
        public Color ladderColor = new Color(0.95f, 0.7f, 0.2f, 1f);

        private GridManager _gm;

        private void OnDrawGizmos()
        {
            if (_gm == null) _gm = GetComponent<GridManager>();
            if (_gm == null) return;
            if (Application.isPlaying && !drawInPlayMode) return;

            // 데이터가 아직 없으면(에디트 모드) 미리보기용 데모를 임시 생성해 그림.
            if (_gm.Map.Count == 0 && !Application.isPlaying)
                _gm.BuildDemoFloor();

            foreach (var kv in _gm.Map.All())
            {
                var pos = kv.Key;
                var tile = kv.Value;
                Gizmos.color = ColorFor(tile.kind, pos.elevation);
                DrawDiamond(_gm.GridToWorld(pos));
            }
        }

        private Color ColorFor(TileKind kind, int elevation)
        {
            switch (kind)
            {
                case TileKind.Hole:   return holeColor;
                case TileKind.Stairs: return stairsColor;
                case TileKind.Ladder: return ladderColor;
                default:              return elevation > 0 ? raisedColor : floorColor;
            }
        }

        private void DrawDiamond(Vector3 center)
        {
            float hw = _gm.iso.tileWidth * 0.5f;
            float hh = _gm.iso.tileHeight * 0.5f;
            Vector3 top    = center + new Vector3(0, hh, 0);
            Vector3 right  = center + new Vector3(hw, 0, 0);
            Vector3 bottom = center + new Vector3(0, -hh, 0);
            Vector3 left   = center + new Vector3(-hw, 0, 0);
            Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(right, bottom);
            Gizmos.DrawLine(bottom, left);
            Gizmos.DrawLine(left, top);
        }
    }
}
