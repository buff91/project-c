using UnityEngine;
using DG.Tweening;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// DOTween 전역 초기화/설정. 씬에 오브젝트를 두지 않아도 게임 시작 시 자동 실행.
    /// 모바일(하한선) 기준으로 안전하게: 안전모드 ON, 재활용 ON, 용량 사전 확보.
    ///
    /// 배치 원칙: 트윈은 '연출'만. 게임 상태(GridPos/HP 등)는 Core 로직이 이미 바꾼 뒤,
    /// View 가 그 결과를 트윈으로 따라가게 한다. (GDD: 로직↔비주얼 분리)
    /// </summary>
    public static class DOTweenBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            DOTween.Init(
                recycleAllByDefault: true,   // 트윈 재활용 → 모바일 GC 부담 감소
                useSafeMode: true,           // 대상이 파괴돼도 예외 대신 무시 (턴 중 몬스터 사망 등)
                logBehaviour: LogBehaviour.ErrorsOnly
            );

            // 동시에 살아있을 트윈/시퀀스 상한 미리 확보(런타임 재할당 방지). 콘텐츠 늘면 조정.
            DOTween.SetTweensCapacity(tweenersCapacity: 500, sequencesCapacity: 100);

            // 픽셀아트/턴제 기본값
            DOTween.defaultEaseType = Ease.Linear;   // 픽셀 이동은 Linear 가 깔끔 (필요 시 개별 override)
            DOTween.defaultAutoPlay = AutoPlay.All;
        }
    }
}
