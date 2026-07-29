using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 투척 조준 미리보기가 "어느 칸을 사거리로, 어느 칸을 영향 범위로 칠하나"를 정하는 규칙.
    /// 사거리는 <see cref="BombRules.ForEachThrowTarget"/>, 영향 범위는
    /// <see cref="BombRules.ForEachBlastCell"/>을 그대로 재사용한다 — 미리보기가 자기 사본을
    /// 갖는 순간 <see cref="BombRules.BlastRadius"/>를 바꿀 때 화면과 판정이 조용히 갈린다.
    /// </summary>
    public static class ThrowAimPreviewRules
    {
        /// <summary>
        /// 영향 범위가 있는 투척물인가. 단검은 칸이 아니라 적 하나를 맞히므로 3×3이 없다.
        /// 폭탄·냉기 폭탄·기름 병은 전부 같은 3×3(<see cref="BombRules.ForEachBlastCell"/>)을 쓴다.
        /// </summary>
        public static bool HasBlast(ItemKind kind) =>
            kind == ItemKind.Bomb ||
            kind == ItemKind.FrostBomb ||
            kind == ItemKind.OilFlask;

        /// <summary>
        /// 조준점이 <b>실제로 던질 수 있는 칸</b>일 때만 영향 범위의 중심이 된다.
        /// 사거리 밖·시야 막힘 칸에 3×3을 그리면 "여기 던질 수 있다"는 거짓말이 된다 —
        /// 판정은 투척 실행과 같은 <see cref="BombRules.CanThrow"/> 하나를 쓴다.
        /// </summary>
        public static bool TryResolveBlastCenter(
            GridMap map,
            GridPos from,
            GridPos? aim,
            ItemKind kind,
            int maxRange,
            out GridPos center)
        {
            center = default;
            if (!aim.HasValue || !HasBlast(kind)) return false;
            if (!BombRules.CanThrow(map, from, aim.Value, maxRange)) return false;

            center = aim.Value;
            return true;
        }

        /// <summary>
        /// 폭발이 닿는 칸 중 <b>맵에 존재하는</b> 칸만 훑는다(그릴 바닥이 없으면 표시도 없다).
        /// 걷기 가능 여부로 거르지 않는다 — 폭발은 유리를 깨고 약한 바닥을 무너뜨리므로
        /// 벽·창문 칸도 영향 범위 안이다(<see cref="BombRules.Detonate"/>).
        /// </summary>
        public static void ForEachBlastPreviewCell(GridMap map, GridPos center, Action<GridPos> visit)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            BombRules.ForEachBlastCell(center, pos =>
            {
                if (map.Has(pos)) visit(pos);
            });
        }
    }
}
