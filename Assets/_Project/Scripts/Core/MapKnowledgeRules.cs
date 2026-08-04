using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 실제 FOV 상태와 무관하게 지도 실루엣에 공개할 수 있는 지형 범주.
    /// 특수 타일의 정확한 종류와 현재 상태는 이 단계에서 의도적으로 접는다.
    /// </summary>
    public enum MapSilhouetteKind
    {
        Floor,
        Barrier,
        Door,
        Gap
    }

    /// <summary>
    /// 현재 층의 지도 실루엣에 공개할 수 있는 정보만 결정하는 순수 규칙.
    /// Visible/Explored/Unknown은 입력받지 않는다 — 실제 FOV와 지도 윤곽은 서로 다른 축이다.
    /// </summary>
    public static class MapKnowledgeRules
    {
        /// <summary>
        /// 생성기가 숨은 방의 내부로 기록한 좌표인가. 비밀문 좌표와 이 목록의 방 내부는
        /// 모두 지도 윤곽에서 제외하고, 공개 이벤트가 일어난 뒤 Gameplay가 함께 추가한다.
        /// </summary>
        public static bool IsHiddenSecretRoomTile(
            DungeonFloorInfo floor,
            GridPos position)
        {
            if (floor == null) return false;

            foreach (GridPos secretTile in floor.SecretRoomTiles)
            {
                if (secretTile == position) return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 층의 좌표를 정보 누설 없는 지도 실루엣 범주로 축약한다.
        /// false면 다른 층·타일 부재·비밀문 좌표·숨은 방 내부이므로 윤곽에 포함하지 않는다.
        /// </summary>
        public static bool TryGetSilhouette(
            DungeonFloorInfo currentFloor,
            int tileFloorIndex,
            GridPos position,
            TileData tile,
            out MapSilhouetteKind silhouette)
        {
            silhouette = default;
            if (currentFloor == null || tile == null || tileFloorIndex != currentFloor.FloorIndex)
                return false;

            // 비밀문은 생성 경계 밖으로 한 칸 돌출될 수 있어 별도 mapped 셀로 그리면
            // 위치 자체가 표식이 된다. 인접한 일반 외곽의 암시적 장벽에 묻고 좌표는 제외한다.
            if (tile.kind == TileKind.SecretDoor) return false;

            if (IsHiddenSecretRoomTile(currentFloor, position))
                return false;

            silhouette = SilhouetteFor(tile.kind);
            return true;
        }

        /// <summary>
        /// 실제 타일 종류를 지도에 공개 가능한 일반 범주로 접는다.
        /// 비밀방 공개처럼 이미 지도 포함 여부가 확정된 좌표의 표현을 갱신할 때 쓴다.
        /// </summary>
        public static MapSilhouetteKind SilhouetteFor(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Floor:
                case TileKind.WeakFloor:
                case TileKind.Stairs:
                case TileKind.StairsUp:
                case TileKind.StairsDown:
                case TileKind.Ladder:
                case TileKind.SecretPassage:
                case TileKind.WindowBroken:
                    return MapSilhouetteKind.Floor;

                case TileKind.Wall:
                case TileKind.SecretDoor:
                case TileKind.Window:
                    return MapSilhouetteKind.Barrier;

                case TileKind.DoorClosed:
                case TileKind.DoorOpen:
                    return MapSilhouetteKind.Door;

                case TileKind.Empty:
                case TileKind.Hole:
                    return MapSilhouetteKind.Gap;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>
        /// mapped 장거리 이동 경로의 노드로 안전하게 사용할 수 있는가.
        /// 층 전환 계단은 진입 즉시 다른 층으로 보내므로 중간 노드에서는 막고, 실제 FOV로
        /// 확인한 계단 자체를 명시적으로 탭했을 때만 목적지로 허용한다. 비밀문은 항상 막는다.
        /// </summary>
        public static bool CanUseForMappedTravelPath(
            TileKind kind,
            bool isExplicitKnownTarget)
        {
            if (kind == TileKind.SecretDoor) return false;
            if (kind == TileKind.StairsUp || kind == TileKind.StairsDown)
                return isExplicitKnownTarget;
            return true;
        }
    }
}
