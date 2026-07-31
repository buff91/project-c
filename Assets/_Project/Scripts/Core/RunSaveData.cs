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
        /// <summary>
        /// 세이브 스키마 버전. <b>이니셜라이저를 붙이지 않는다</b> —
        /// <c>JsonUtility.FromJson</c>은 JSON에 <b>있는</b> 필드만 덮어쓰므로,
        /// <c>= 1</c>로 두면 이 필드가 없는 구세이브가 <b>자기를 최신이라고 선언</b>하고
        /// 마이그레이션이 통째로 건너뛰어진다. 기본값 0이 곧 "변환 전"이다.
        /// (<c>RunTelemetry.schemaVersion</c>은 이니셜라이저를 쓰지만 그건 쓰기 전용이라
        /// 무해하다 — 그 패턴을 여기 복사하면 안 된다.)
        /// 스탬프는 저장 직전에 <see cref="SaveMigration.Stamp"/>가 찍는다.
        /// </summary>
        public int schemaVersion;

        public string dungeonId;
        public int seed;
        public int roomSize;
        public int floorCount;
        public int elevationsPerFloor;
        public int stageCount = 1;
        public int stageIndex = 1;
        public int currentFloorIndex;

        /// <summary>
        /// 현재 층의 진행 지수(0부터). <see cref="currentFloorIndex"/>로 역산할 수 없어 따로 저장한다 —
        /// 상승·비단조 던전에서는 고도와 진행 순서가 일치하지 않는다(GDD §5.1).
        /// </summary>
        public int currentProgressIndex;

        public bool bossDefeated;
        public int hp;
        public int kills;
        public int deepestFloorIndex;

        /// <summary>
        /// 가장 멀리 간 <b>진행 지수</b>. 이어하기에서 도달 층이 되돌아가지 않게 함께 저장한다 —
        /// 층 인덱스만으로는 어느 쪽이 더 멀리 간 것인지 알 수 없다(상승·비단조 던전).
        /// 옛 세이브는 0 으로 들어오며, 그 경우 첫 층 이동이 도달 층을 다시 세운다.
        /// </summary>
        public int deepestProgressIndex;

        /// <summary>이번 원정에 반입한 장비. 죽으면 잃으므로 런 상태로 들고 다닌다.</summary>
        public string carriedWeaponId = "";
        public string carriedGearId = "";

        /// <summary>배고픔은 판 전체를 관통한다 — 층·던전이 바뀌어도 이어진다.</summary>
        public HungerState hunger = new HungerState();

        /// <summary>
        /// 사격 충전도 판을 관통한다. 층 전환마다 만충으로 리셋되면 계단 앞에서 기다렸다
        /// 내려가는 것이 최적해가 되고, 재충전을 기다리게 만든 이유가 사라진다.
        /// (옛 세이브에는 없다 — null 이면 만충으로 시작한다)
        /// </summary>
        public RangedChargeState rangedCharges;
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

    /// <summary>새 판과 이어하기가 시작할 던전 내부 진행 지수를 결정한다.</summary>
    public static class RunStartRules
    {
        /// <summary>
        /// 새 판은 반드시 첫 층(0), 이어하기는 체크포인트에 <b>저장된</b> 진행 지수를 쓴다.
        /// 예전에는 <c>Max(0, -currentFloorIndex)</c>로 역산했는데, 상승 던전에서는 전부 0이 되고
        /// 비단조 경로에서는 애초에 역산할 수 없다.
        /// </summary>
        public static int ResolvePreviewDepth(RunSaveData continueData) =>
            continueData == null ? 0 : Math.Max(0, continueData.currentProgressIndex);
    }
}
