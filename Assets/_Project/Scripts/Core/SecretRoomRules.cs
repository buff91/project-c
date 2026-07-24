using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 비밀문 발견 규칙. 생성 데이터와 타일 상태만 다루며 연출·입력은 Gameplay가 맡는다.
    /// 비밀문은 공개되기 전까지 벽처럼 막고, 공개되면 열린 문으로 바뀐다.
    /// </summary>
    public static class SecretRoomRules
    {
        /// <summary>첫 던전 10층은 세 곳, 짧은 테스트 던전도 최소 한 곳을 보장한다.</summary>
        public static int DesiredCount(int floorCount)
        {
            if (floorCount <= 0) return 0;
            if (floorCount >= 8) return 3;
            if (floorCount >= 4) return 2;
            return 1;
        }

        public static bool IsSecretDoor(TileData tile) =>
            tile != null && tile.kind == TileKind.SecretDoor;

        /// <summary>조사는 같은 elevation의 4방향 인접 칸에서만 가능하다.</summary>
        public static bool CanInvestigate(GridPos player, GridPos secretDoor) =>
            player.elevation == secretDoor.elevation &&
            player.ManhattanTo(secretDoor) == 1;

        /// <summary>비밀문을 열린 통로로 바꾼다. 이미 공개된 문에는 중복 성공하지 않는다.</summary>
        public static bool TryReveal(GridMap map, GridPos secretDoor)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            TileData tile = map.Get(secretDoor);
            if (!IsSecretDoor(tile)) return false;

            // 타일 인스턴스를 유지해 향후 상태 플래그가 추가돼도 공개 과정에서 잃지 않는다.
            tile.kind = TileKind.SecretPassage;
            return true;
        }

        /// <summary>3×3 폭발 안의 비밀문을 모두 공개하고 공개된 좌표를 반환한다.</summary>
        public static List<GridPos> RevealInBlast(GridMap map, GridPos center)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var revealed = new List<GridPos>();
            for (int dx = -BombRules.BlastRadius; dx <= BombRules.BlastRadius; dx++)
            for (int dy = -BombRules.BlastRadius; dy <= BombRules.BlastRadius; dy++)
            {
                GridPos pos = center.Offset(dx, dy);
                if (TryReveal(map, pos))
                    revealed.Add(pos);
            }
            return revealed;
        }
    }
}
