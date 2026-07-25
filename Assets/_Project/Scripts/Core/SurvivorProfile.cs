namespace ProjectC.Core
{
    /// <summary>
    /// 원정자 한 명의 기본값. <b>직업도 프리셋도 없다 — 모든 원정자는 같다.</b>
    ///
    /// <para>
    /// <b>왜 영웅을 걷어냈나.</b> 이 게임은 익스트랙션 모델이고 스킬 트리가 없다.
    /// 정체성을 지는 것은 캐릭터가 아니라 <b>장비</b>다 — 그리고 장비는 이미
    /// "숫자가 아니라 규칙을 바꾼다"로 설계돼 있다(긴 파이프=사거리 2, 대형 렌치=넉백,
    /// 표지판 방패=피해 -1, 완충 부츠=안전 낙하 +2). 옛 영웅 3종은 HP/공격력 <b>숫자만</b>
    /// 달랐으므로, 고르는 행위가 전술이 아니라 난이도 선택에 가까웠다.
    /// 무엇을 걸고 나갈지가 판돈인 게임에서 캐릭터를 먼저 고르게 할 이유가 없다.
    /// </para>
    /// <para>
    /// <b>값은 옛 기사 그대로다</b> — 기사는 해금이 필요 없는 기본 영웅이었으므로,
    /// 이 값을 쓰면 기본 경로를 밟던 플레이어의 밸런스가 그대로 유지된다.
    /// 여기가 플레이어 기본 수치의 단일 출처다.
    /// </para>
    /// </summary>
    public static class SurvivorProfile
    {
        public const string DisplayName = "원정자";

        public const int MaxHp = 10;
        public const int Attack = 3;
        public const int RangedDamage = 1;

        /// <summary>맨몸으로 나가도 쥐어 주는 것. 첫 판이 즉사로 끝나지 않게 하는 최소한이다.</summary>
        public const int StartPotions = 1;

        /// <summary>기본 지급품 수량. 지급하지 않는 종류는 0.</summary>
        public static int StarterCount(ItemKind kind) =>
            kind == ItemKind.Potion ? StartPotions : 0;
    }
}
