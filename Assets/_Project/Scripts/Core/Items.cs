namespace ProjectC.Core
{
    /// <summary>
    /// M1 아이템 최소 셋. 사용 효과/조합은 인벤토리 단계에서 붙인다. (GDD §5.6, §5.8)
    /// </summary>
    public enum ItemKind
    {
        Potion = 0,  // 회복 물약
        Bomb = 1     // 폭탄. M4에서 약한 바닥 파괴/폭발 트리거와 연결.
    }

    /// <summary>던전 생성기가 배치하는 아이템 스폰 지점. 타일이 아니라 타일 위의 오브젝트다.</summary>
    public readonly struct ItemSpawn
    {
        public readonly GridPos Position;
        public readonly ItemKind Kind;

        public ItemSpawn(GridPos position, ItemKind kind)
        {
            Position = position;
            Kind = kind;
        }

        public override string ToString() => $"{Kind}@{Position}";
    }
}
