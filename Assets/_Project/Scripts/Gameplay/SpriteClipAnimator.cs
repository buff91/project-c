using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 베이크된 태그 클립(<see cref="ActorAnimationSet"/>)의 경량 재생기 — Animator를 쓰지 않는다.
    ///
    /// 소유권 계약: **renderer.sprite만 만진다.** position·scale·color는 CombatFx 코루틴과
    /// ApplyEnemyVisuals가 소유하므로 여기서 건드리면 싸운다. 시간은 Time.deltaTime 누적 —
    /// 시야 밖(<c>renderer.enabled == false</c>)에서는 얼어붙었다가 다시 보이면 그 프레임부터
    /// 이어가므로 재동기화 로직이 필요 없고, 비활성 렌더러 수만큼의 Update 비용도 프레임 계산
    /// 없이 이른 반환으로 끝난다.
    ///
    /// 태그 규칙: idle·walk는 루프, attack·hit는 종료 시 idle 복귀, death는 마지막 프레임 유지.
    /// 클립이 없는 태그 요청은 전부 no-op — PNG 폴백(정지 1프레임) 액터와 자연스럽게 공존한다.
    /// </summary>
    internal sealed class SpriteClipAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private ActorAnimationSet _set;
        private SpriteClip _current;
        private float _clipTime;
        private bool _holdOnEnd;

        internal void Configure(SpriteRenderer renderer, ActorAnimationSet set)
        {
            _renderer = renderer;
            _set = set;
            StopToIdle();
        }

        /// <summary>루프 태그(walk) 시작. 이미 같은 클립이면 이어 재생한다.</summary>
        internal void PlayLoop(string tag)
        {
            SpriteClip clip = _set?.Find(tag);
            if (clip == null || !clip.IsPlayable) return;
            if (ReferenceEquals(_current, clip)) return;
            StartClip(clip, holdOnEnd: false);
        }

        /// <summary>원샷 태그(attack/hit). 종료 시 idle 복귀. fire-and-forget.</summary>
        internal void PlayOnce(string tag)
        {
            SpriteClip clip = _set?.Find(tag);
            if (clip == null || !clip.IsPlayable) return;
            StartClip(clip, holdOnEnd: false);
        }

        /// <summary>사망 — 종료 시 마지막 프레임을 유지하고 정지한다(시체 페이드는 게임 소유).</summary>
        internal void PlayDeath()
        {
            SpriteClip clip = _set?.Find(SpriteClipTags.Death);
            if (clip == null || !clip.IsPlayable) return;
            StartClip(clip, holdOnEnd: true);
        }

        /// <summary>기본 상태로 — idle 클립이 없으면 정지(마지막 스프라이트 유지).</summary>
        internal void StopToIdle()
        {
            SpriteClip idle = _set?.Find(SpriteClipTags.Idle);
            if (idle == null || !idle.IsPlayable)
            {
                _current = null;
                return;
            }

            StartClip(idle, holdOnEnd: false);
        }

        private void StartClip(SpriteClip clip, bool holdOnEnd)
        {
            _current = clip;
            _clipTime = 0f;
            _holdOnEnd = holdOnEnd;
            if (_renderer != null && _renderer.enabled)
                _renderer.sprite = clip.frames[0];
        }

        private void Update()
        {
            if (_current == null || _renderer == null || !_renderer.enabled) return;

            _clipTime += Time.deltaTime;
            int frame = SpriteClipRules.FrameAt(
                _current.frameStartTimes, _current.length, _current.loop, _clipTime,
                out bool finished);
            _renderer.sprite = _current.frames[frame];

            if (!finished || _current.loop) return;
            if (_holdOnEnd)
                _current = null; // 마지막 프레임 유지 — 이후 색/페이드는 게임 코드 소유.
            else
                StopToIdle();
        }
    }
}
