using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 판 사이에 유지되는 메타 창고 (extraction 규칙의 저장소).
    /// 생환 시: 전리품은 골드로 환산해 적립, 남은 소모품은 여기 보관된다.
    /// 새 판 시작 시 소모품은 전량 반입("가지고 들어간다") — 죽으면 그 판 소지품은 소실.
    /// </summary>
    [Serializable]
    public class MetaSaveData
    {
        public int gold;
        public int potions;
        public int bombs;
        public int frostBombs;
        public int oilFlasks;
        public int knives;
        public int scrolls;

        public int GetCount(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return potions;
                case ItemKind.Bomb: return bombs;
                case ItemKind.FrostBomb: return frostBombs;
                case ItemKind.OilFlask: return oilFlasks;
                case ItemKind.ThrowingKnife: return knives;
                case ItemKind.RecallScroll: return scrolls;
                default: return 0; // 전리품은 보관하지 않는다 — 항상 골드로 환산
            }
        }

        public void AddCount(ItemKind kind, int amount)
        {
            switch (kind)
            {
                case ItemKind.Potion: potions += amount; break;
                case ItemKind.Bomb: bombs += amount; break;
                case ItemKind.FrostBomb: frostBombs += amount; break;
                case ItemKind.OilFlask: oilFlasks += amount; break;
                case ItemKind.ThrowingKnife: knives += amount; break;
                case ItemKind.RecallScroll: scrolls += amount; break;
            }
        }

        public void ClearItems()
        {
            potions = bombs = frostBombs = oilFlasks = knives = scrolls = 0;
        }
    }
}
