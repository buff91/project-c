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
        public int herbs;
        public int powders;
        public int frostShards;
        public int kills;
        public int deepestFloorIndex;
    }

    /// <summary>새 판과 이어하기가 시작할 던전 내부 깊이를 결정한다.</summary>
    public static class RunStartRules
    {
        /// <summary>새 판은 반드시 B1(0), 이어하기만 저장된 음수 floor index를 깊이로 변환한다.</summary>
        public static int ResolvePreviewDepth(RunSaveData continueData) =>
            continueData == null ? 0 : Math.Max(0, -continueData.currentFloorIndex);
    }
}
