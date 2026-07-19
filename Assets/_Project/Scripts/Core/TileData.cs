namespace ProjectC.Core
{
    /// <summary>
    /// 타일 종류. 로직용 최소 셋 — 아트/스프라이트 매핑은 나중에 ScriptableObject 로 분리. (GDD §5.6)
    /// </summary>
    public enum TileKind
    {
        Empty = 0,   // 아무것도 없는 공간(허공). 아래로 낙하 가능.
        Floor,       // 단단한 바닥. 이동 가능, 서 있을 수 있음.
        Wall,        // 벽. 이동/시야 차단.
        WeakFloor,   // 약한 바닥. 밟거나 충격 시 붕괴 → 낙하 트리거. (GDD §5.3)
        Hole,        // 구멍. 아래층으로 뚫려 있음. FOV 포털이자 낙하 지점. (GDD §5.2)
        Stairs,      // 계단/경사로. 같은 층 내 elevation 변화. (GDD §5.1)
        StairsUp,    // 던전 위층으로 이어지는 명시적 링크 타일.
        StairsDown,  // 던전 아래층으로 이어지는 명시적 링크 타일.
        DoorClosed, // 같은 층의 방/복도 경계. 이동·시야 차단, 상호작용으로 연다.
        DoorOpen    // 열린 문. 일반 바닥처럼 이동·시야 통과.
    }

    /// <summary>
    /// 격자 한 칸의 로직 데이터. 비주얼은 포함하지 않는다(로직↔비주얼 분리).
    /// 상태이상 플래그(화상/젖음 등)는 M4에서 여기(또는 별도 컴포넌트)에 확장. (GDD §5.5)
    /// </summary>
    public class TileData
    {
        public TileKind kind;

        public TileData(TileKind kind = TileKind.Empty)
        {
            this.kind = kind;
        }

        /// <summary>서 있을 수 있는 단단한 바닥인가. (착지/정지 판정용)</summary>
        public bool IsSolidGround => kind == TileKind.Floor ||
                                     kind == TileKind.Stairs ||
                                     kind == TileKind.StairsUp ||
                                     kind == TileKind.StairsDown ||
                                     kind == TileKind.DoorClosed ||
                                     kind == TileKind.DoorOpen;

        /// <summary>이동으로 진입 가능한가.</summary>
        public bool IsWalkable => (IsSolidGround && kind != TileKind.DoorClosed) ||
                                  kind == TileKind.WeakFloor;

        /// <summary>시야를 차단하는가. (FOV / 조준용)</summary>
        public bool BlocksSight => kind == TileKind.Wall || kind == TileKind.DoorClosed;

        public bool CanOpen => kind == TileKind.DoorClosed;
        public bool CanClose => kind == TileKind.DoorOpen;

        /// <summary>이 칸에 발을 디디면 아래로 떨어지는가. (TryFall 트리거 근거, GDD §5.3)</summary>
        public bool CausesFall => kind == TileKind.Empty || kind == TileKind.Hole;
    }
}
