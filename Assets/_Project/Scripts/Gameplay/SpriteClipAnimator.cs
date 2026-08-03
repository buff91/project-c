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
    /// 태그 규칙: idle·walk는 루프, attack·hit는 종료 시 idle 복귀, fall은 월드 낙하가 끝날 때까지
    /// 호출자가 마지막 프레임을 유지할 수 있고, death는 마지막 프레임 유지.
    /// 클립이 없는 태그 요청은 전부 no-op — PNG 폴백(정지 1프레임) 액터와 자연스럽게 공존한다.
    /// </summary>
    internal sealed class SpriteClipAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private ISpriteClipSet _set;
        private SpriteClip _current;
        private string _currentBaseTag;
        private float _clipTime;
        private float _playbackRate = 1f;
        private bool _holdOnEnd;
        private bool _holdingLastFrame;
        private ActorFacing4 _facing = ActorFacing4.East;

        internal bool HasDirectionalClips =>
            _set is ActorAnimationSet actorSet && actorSet.HasDirectionalClips;

        internal void Configure(SpriteRenderer renderer, ISpriteClipSet set)
        {
            _renderer = renderer;
            _set = set;
            StopToIdle();
        }

        /// <summary>루프 태그(walk) 시작. 이미 같은 클립이면 이어 재생한다.</summary>
        internal void PlayLoop(string tag)
        {
            SpriteClip clip = FindClip(tag);
            if (clip == null || !clip.IsPlayable) return;
            if (ReferenceEquals(_current, clip))
            {
                _playbackRate = 1f;
                return;
            }
            StartClip(clip, tag, holdOnEnd: false);
        }

        /// <summary>
        /// 한 번의 월드 동작 시간 안에 루프 한 주기를 정확히 맞춘다. 짧은 한 칸 이동에서도
        /// authored walk 키포즈가 끝까지 노출되고, 여러 칸이면 같은 박자로 반복된다.
        /// </summary>
        internal void PlayLoopForDuration(string tag, float cycleDuration)
        {
            SpriteClip clip = FindClip(tag);
            if (clip == null || !clip.IsPlayable) return;
            float rate = cycleDuration > 0f && clip.length > 0f
                ? clip.length / cycleDuration
                : 1f;
            if (!ReferenceEquals(_current, clip))
                StartClip(clip, tag, holdOnEnd: false);
            _playbackRate = rate;
        }

        /// <summary>원샷 태그(attack/hit). 종료 시 idle 복귀. fire-and-forget.</summary>
        internal void PlayOnce(string tag)
        {
            SpriteClip clip = FindClip(tag);
            if (clip == null || !clip.IsPlayable) return;
            StartClip(clip, tag, holdOnEnd: false);
        }

        /// <summary>
        /// 원샷을 끝 프레임에서 멈춘다. 클립 길이보다 긴 월드 연출(fall)이 끝난 뒤
        /// 호출자가 <see cref="StopToIdle"/>로 해제한다.
        /// </summary>
        internal void PlayOnceAndHold(string tag)
        {
            SpriteClip clip = FindClip(tag);
            if (clip == null || !clip.IsPlayable) return;
            StartClip(clip, tag, holdOnEnd: true);
        }

        /// <summary>사망 — 종료 시 마지막 프레임을 유지하고 정지한다(시체 페이드는 게임 소유).</summary>
        internal void PlayDeath()
        {
            SpriteClip clip = FindClip(SpriteClipTags.Death);
            if (clip == null || !clip.IsPlayable) return;
            StartClip(clip, SpriteClipTags.Death, holdOnEnd: true);
        }

        /// <summary>기본 상태로 — idle 클립이 없으면 정지(마지막 스프라이트 유지).</summary>
        internal void StopToIdle()
        {
            SpriteClip idle = FindClip(SpriteClipTags.Idle);
            if (idle == null || !idle.IsPlayable)
            {
                _current = null;
                _currentBaseTag = SpriteClipTags.Idle;
                _holdOnEnd = false;
                _holdingLastFrame = false;
                return;
            }

            StartClip(idle, SpriteClipTags.Idle, holdOnEnd: false);
        }

        /// <summary>
        /// 월드 이동/공격이 정한 방향을 현재 뷰 방향으로 받아 같은 상태의 방향 클립을 교체한다.
        /// 루프는 위상을 보존하고 원샷은 호출 직전에 방향이 정해지는 것이 기본 계약이다.
        /// </summary>
        internal void SetFacing(ActorFacing4 facing)
        {
            if (_facing == facing) return;
            _facing = facing;
            if (_current == null || string.IsNullOrEmpty(_currentBaseTag)) return;

            SpriteClip replacement = FindClip(_currentBaseTag);
            if (replacement == null || !replacement.IsPlayable ||
                ReferenceEquals(replacement, _current))
                return;

            bool wasHoldingLastFrame = _holdingLastFrame;
            float phase = _current.length > 0f
                ? _current.loop
                    ? Mathf.Repeat(_clipTime, _current.length) / _current.length
                    : Mathf.Clamp01(_clipTime / _current.length)
                : 0f;
            bool hold = _holdOnEnd;
            string baseTag = _currentBaseTag;
            float playbackRate = _playbackRate;
            StartClip(replacement, baseTag, hold);
            _playbackRate = playbackRate;
            if (hold && wasHoldingLastFrame)
            {
                _clipTime = replacement.length;
                _holdingLastFrame = true;
                if (_renderer != null)
                    _renderer.sprite = replacement.frames[replacement.frames.Length - 1];
            }
            else
            {
                _clipTime = phase * replacement.length;
                ApplyCurrentFrame();
            }
        }

        private SpriteClip FindClip(string baseTag)
        {
            return _set is ActorAnimationSet actorSet
                ? actorSet.Find(baseTag, _facing)
                : _set?.Find(baseTag);
        }

        private void StartClip(SpriteClip clip, string baseTag, bool holdOnEnd)
        {
            _current = clip;
            _currentBaseTag = baseTag;
            _clipTime = 0f;
            _playbackRate = 1f;
            _holdOnEnd = holdOnEnd;
            _holdingLastFrame = false;
            if (_renderer != null)
                _renderer.sprite = clip.frames[0];
        }

        private void ApplyCurrentFrame()
        {
            if (_current == null || _renderer == null) return;
            int frame = SpriteClipRules.FrameAt(
                _current.frameStartTimes,
                _current.length,
                _current.loop,
                _clipTime,
                out _);
            _renderer.sprite = _current.frames[frame];
        }

        private void Update()
        {
            if (_current == null || _renderer == null || !_renderer.enabled ||
                _holdingLastFrame)
                return;

            _clipTime += Time.deltaTime * _playbackRate;
            int frame = SpriteClipRules.FrameAt(
                _current.frameStartTimes, _current.length, _current.loop, _clipTime,
                out bool finished);
            _renderer.sprite = _current.frames[frame];

            if (!finished || _current.loop) return;
            if (_holdOnEnd)
            {
                _clipTime = _current.length;
                _holdingLastFrame = true;
            }
            else
                StopToIdle();
        }
    }
}
