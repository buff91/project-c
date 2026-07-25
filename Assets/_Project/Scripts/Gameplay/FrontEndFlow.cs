using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>씬 사이에서 유지되는 최소 프런트엔드 상태.</summary>
    public static class FrontEndFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string HubScene = "Hub";
        public const string DungeonScene = "IsoPrototype";

        /// <summary>
        /// 씬을 넘나드는 static 상태를 새 세션 값으로 되돌린다.
        /// <para>
        /// <b>도메인 리로드를 끈 채로 Play 하기 때문에 필요하다</b>(Editor 설정
        /// <c>EnterPlayModeOptions.DisableDomainReload</c>). 그 모드에서는 static 이 이전
        /// Play 의 값을 그대로 들고 있어서, 초기화를 필드 초기화자에만 맡기면
        /// "두 번째 Play 부터 이상하게 동작"하는 버그가 난다.
        /// </para>
        /// <para>
        /// 여기에 넣는 값은 <b>새 도메인에서의 기본값과 같아야</b> 한다 —
        /// 영웅 미선택은 <c>null</c>(호출부가 전부 기본 영웅으로 읽는다), 이어하기 요청은 <c>false</c>.
        /// 씬 사이에 유지돼야 하는 새 static 을 추가하면 이 목록도 함께 늘린다.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationSession()
        {
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            RunSaveStore.ContinueRequested = false;
        }
    }

    /// <summary>
    /// 타이틀 버튼 규칙. 타이틀은 <b>앱을 켤 때 한 번 지나는 문</b>이지 판마다 돌아오는
    /// 착지점이 아니다 — 판이 끝난 뒤의 착지점은 캠프다(게임오버의 "캠프로 돌아가기").
    /// <para>
    /// <b>`게임 시작`은 언제나 캠프로 간다.</b> 첫 실행이든 재접속이든 캠프가 시작점이라
    /// 버튼 이름과 목적지를 저장 상태에 따라 흔들지 않는다.
    /// </para>
    /// <para>
    /// <b>`이어하기`는 던전 중간 저장(층 체크포인트)이 있을 때만 존재한다.</b> 없으면
    /// 비활성 회색으로 남기지 않고 숨긴다 — 죽은 직후 화면에 뜬 회색 "이어하기"는
    /// 정보가 아니라 "원정을 잃었나?"라는 오해다.
    /// </para>
    /// </summary>
    public static class TitleEntryRouting
    {
        /// <summary>`게임 시작`의 목적지. 이후 프롤로그 씬을 넣는다면 여기만 바꾼다.</summary>
        public static string StartScene => FrontEndFlow.HubScene;

        /// <summary>`이어하기`의 목적지 — 체크포인트가 있는 던전으로 직행한다.</summary>
        public static string ResumeScene => FrontEndFlow.DungeonScene;

        public static bool ShowsResume(bool hasRunSave) => hasRunSave;
    }

    /// <summary>허브 던전 선택을 새 게임 생성에 전달한다.</summary>
    public static class DungeonSelection
    {
        public static string SelectedId = DungeonCatalog.DefaultId;
        public static DungeonDefinition Selected => DungeonCatalog.ById(SelectedId);
    }
}
