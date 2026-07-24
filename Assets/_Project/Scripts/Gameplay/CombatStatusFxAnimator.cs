using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 액터에 붙는 가벼운 픽셀 상태 아이콘 애니메이터.
    /// 파티클 시스템 없이 위치·크기·알파만 갱신해 모바일 비용을 제한한다.
    /// </summary>
    public sealed class CombatStatusFxAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private StatusKind _kind;
        private Vector3 _basePosition;
        private float _phase;

        public void Configure(SpriteRenderer renderer, StatusKind kind, Vector3 basePosition)
        {
            _renderer = renderer;
            _kind = kind;
            _basePosition = basePosition;
            _phase = ((uint)transform.GetEntityId().GetHashCode() % 97u) * 0.031f;
        }

        private void Update()
        {
            if (_renderer == null) return;

            float time = Time.time + _phase;
            if (_kind == StatusKind.Burn)
            {
                float flicker = 0.88f + Mathf.Sin(time * 12f) * 0.12f;
                transform.localPosition = _basePosition + new Vector3(
                    Mathf.Sin(time * 17f) * 0.018f,
                    Mathf.Sin(time * 8f) * 0.035f,
                    0f);
                transform.localScale = new Vector3(
                    flicker,
                    1.05f + Mathf.Sin(time * 10f) * 0.08f,
                    1f);
                _renderer.color = new Color(1f, 1f, 1f, 0.82f + flicker * 0.16f);
                return;
            }

            float pulse = 0.96f + Mathf.Sin(time * 4.5f) * 0.07f;
            transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(time * 3.5f) * 0.018f);
            transform.localScale = new Vector3(pulse, pulse, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.8f) * 4f);
            _renderer.color = new Color(1f, 1f, 1f, 0.84f + pulse * 0.12f);
        }
    }
}
