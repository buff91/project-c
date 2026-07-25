using System;
using System.Collections.Generic;

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
        public string dungeonId;
        public int seed;
        public int roomSize;
        public int floorCount;
        public int elevationsPerFloor;
        public int stageCount = 1;
        public int stageIndex = 1;
        public int currentFloorIndex;
        public bool bossDefeated;
        public int hp;
        public int kills;
        public int deepestFloorIndex;

        /// <summary>이번 원정에 반입한 장비. 죽으면 잃으므로 런 상태로 들고 다닌다.</summary>
        public string carriedWeaponId = "";
        public string carriedGearId = "";

        /// <summary>배고픔은 판 전체를 관통한다 — 층·던전이 바뀌어도 이어진다.</summary>
        public HungerState hunger = new HungerState();
        public List<int> usedRestFloorIndices = new List<int>();
        public RunTelemetry telemetry;

        /// <summary>
        /// 백팩 내용. 종류별 필드 대신 목록 하나라 아이템을 늘려도 세이브를 손대지 않는다
        /// (연산은 <see cref="ItemStorage"/> 공유). 전리품도 그대로 담는다 —
        /// 런 인벤토리는 아직 환금 전이기 때문이다.
        /// </summary>
        public List<ItemStack> items = new List<ItemStack>();

        public void WriteItems(Inventory inventory) => ItemStorage.CopyFrom(items, inventory);

        /// <summary>세이브에 담긴 종류별 수량을 인벤토리에 더한다(WriteItems 의 역).</summary>
        public void AddItemsTo(Inventory inventory) => ItemStorage.AddTo(items, inventory);
    }

    /// <summary>새 판과 이어하기가 시작할 던전 내부 깊이를 결정한다.</summary>
    public static class RunStartRules
    {
        /// <summary>새 판은 반드시 B1(0), 이어하기만 저장된 음수 floor index를 깊이로 변환한다.</summary>
        public static int ResolvePreviewDepth(RunSaveData continueData) =>
            continueData == null ? 0 : Math.Max(0, -continueData.currentFloorIndex);
    }
}
