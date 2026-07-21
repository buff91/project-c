using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 층 단위 체크포인트 저장 데이터. (로그라이트 이어하기)
    /// 던전은 seed 로 재생성하므로 지형·적·아이템 배치는 저장하지 않는다 —
    /// 이어하기는 "현재 층을 층 입구에서 다시 시작"하는 의미다.
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public string heroId;
        public int seed;
        public int roomSize;
        public int floorCount;
        public int elevationsPerFloor;
        public int stageIndex = 1;
        public int currentFloorIndex;
        public int hp;
        public int potions;
        public int bombs;
        public int frostBombs;
        public int oilFlasks;
        public int knives;
        public int scrolls;
        public int coinPouches;
        public int gemstones;
        public int relics;
        public int kills;
        public int deepestFloorIndex;
    }
}
