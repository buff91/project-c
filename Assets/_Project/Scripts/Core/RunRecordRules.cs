using System;

namespace ProjectC.Core
{
    /// <summary>
    /// <b>기록</b> — 죽음이 먹이는 유일한 축.
    ///
    /// <para>
    /// <b>왜 필요한가.</b> 세 진행 축(도구 해금·NPC 구출·골드)이 전부 성공을 요구해서,
    /// 초반에 죽는 플레이어는 골드 0 · 해금 0 · 캠프 변화 0 을 받는다. 문서에는
    /// "실패한 판도 전진"이라 적혀 있었지만 실현된 적이 없다 — 해금 조건이 <b>한 판 기준</b>이라
    /// 화상을 11번 입히고 죽으면 그 판이 통째로 버려졌다.
    /// </para>
    /// <para>
    /// <b>왜 물자가 아니라 기록인가.</b> 죽으면 소지품 전손이 익스트랙션의 긴장이고
    /// (다크앤다커 모델), GDD §11 은 영구 스탯 강화를 금지한다. 둘 다 건드리지 않으려면
    /// 죽음이 남기는 것은 물자도 능력치도 아니어야 한다. 돌아오지 못한 원정자가 쉘터에
    /// 실제로 남기는 것은 <b>정보</b>다 — 다음 사람이 덜 죽는다. 그래서 기록이고,
    /// 늘어나는 것은 숫자가 아니라 선택지다.
    /// </para>
    /// <para>
    /// <b>반복 파밍을 막는 것이 이 규칙의 핵심 제약이다.</b> 도달 층에만 비례하면 1~3층을
    /// 무한히 왕복하는 것이 최적 전략이 된다. 그래서 큰 몫은 <b>역대 최고를 넘은 층</b>에만
    /// 붙이고, 반복에는 작은 몫만 남긴다 — 작지만 <b>0은 아니다</b>. 0이면 원래 문제로 돌아간다.
    /// </para>
    /// </summary>
    public static class RunRecordRules
    {
        /// <summary>이미 가 본 구간을 다시 밟았을 때의 층당 기록. 작지만 0이 아니다.</summary>
        public const int RepeatRatePerFloor = 1;

        /// <summary>역대 최고를 넘어선 층당 기록. 전진이 반복보다 확실히 이득이어야 한다.</summary>
        public const int FrontierRatePerFloor = 3;

        /// <summary>숨은 방 하나당 기록. 판마다 새로 생기므로 반복 보상이지만 탐색을 요구한다.</summary>
        public const int SecretRoomRate = 2;

        /// <summary>
        /// 이번 판이 남기는 기록. <b>정산과 무관하다</b> — 죽든 생환하든 같은 식으로 계산한다
        /// (죽음만 보상하면 자살이 전략이 되고, 생환만 보상하면 원래 문제로 돌아간다).
        /// </summary>
        /// <param name="reachedFloors">이번 판에 도달한 층 수(첫 층 도달 = 1).</param>
        /// <param name="bestFloorsEver">지난 판들에서 도달한 최대 층 수. 첫 판은 0.</param>
        /// <param name="secretRoomsFound">이번 판에 찾은 숨은 방 수.</param>
        public static int Award(int reachedFloors, int bestFloorsEver, int secretRoomsFound)
        {
            int reached = Math.Max(0, reachedFloors);
            int best = Math.Max(0, bestFloorsEver);
            int secrets = Math.Max(0, secretRoomsFound);

            int frontier = Math.Max(0, reached - best);
            int repeated = reached - frontier;

            return repeated * RepeatRatePerFloor +
                   frontier * FrontierRatePerFloor +
                   secrets * SecretRoomRate;
        }

        /// <summary>
        /// 이번 판의 도달 층 수. 진행 지수는 0부터라 <b>+1</b> 해야 "몇 층까지 갔나"가 된다 —
        /// 첫 층에서 죽어도 한 층은 갔다(기록 0 이 아니어야 한다).
        /// </summary>
        public static int ReachedFloors(int deepestProgressIndex) =>
            Math.Max(0, deepestProgressIndex) + 1;

        /// <summary>
        /// 조건이 열렸는가. <b>역대 최고 + 투입 기록</b>이 목표를 넘으면 된다.
        /// <para>
        /// 예전에는 이번 판 값만 봤다. 그래서 목표에 하나 모자란 판이 통째로 버려졌고,
        /// 최고 기록(<c>MetaSaveData.unlockProgress</c>)은 기록실 표시용으로만 쓰였다.
        /// 그 값을 판정에 넣는 것만으로 부분 진행이 살아난다 — 기록 투입은 그 위에 얹는
        /// <b>느린 길</b>이고, 한 판에 몰아치는 도전은 <b>빠른 길</b>로 그대로 남는다.
        /// </para>
        /// </summary>
        public static bool IsConditionMet(int bestProgress, int investedRecords, int target) =>
            Math.Max(0, bestProgress) + Math.Max(0, investedRecords) >= target;
    }
}
