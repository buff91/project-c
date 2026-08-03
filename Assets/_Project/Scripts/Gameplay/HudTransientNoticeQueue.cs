using System;
using System.Collections.Generic;

namespace ProjectC.Gameplay
{
    internal readonly struct HudTransientNotice : IEquatable<HudTransientNotice>
    {
        internal HudTransientNotice(string title, string detail, string variant)
        {
            Title = title;
            Detail = detail;
            Variant = variant;
        }

        internal string Title { get; }
        internal string Detail { get; }
        internal string Variant { get; }

        public bool Equals(HudTransientNotice other) =>
            string.Equals(Title, other.Title, StringComparison.Ordinal) &&
            string.Equals(Detail, other.Detail, StringComparison.Ordinal) &&
            string.Equals(Variant, other.Variant, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is HudTransientNotice other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Title != null ? Title.GetHashCode() : 0;
                hash = (hash * 397) ^ (Detail != null ? Detail.GetHashCode() : 0);
                hash = (hash * 397) ^ (Variant != null ? Variant.GetHashCode() : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// 발견/입장 카드를 순서대로 보관한다. 같은 프레임에 두 이벤트가 와도 앞 카드를
    /// 덮어쓰지 않고, 활성·대기 중인 완전 중복은 한 번만 보여 준다.
    /// </summary>
    internal sealed class HudTransientNoticeQueue
    {
        private readonly Queue<HudTransientNotice> _pending =
            new Queue<HudTransientNotice>();
        private HudTransientNotice _active;

        internal bool HasActive { get; private set; }
        internal int PendingCount => _pending.Count;

        internal bool Enqueue(HudTransientNotice notice)
        {
            if (string.IsNullOrEmpty(notice.Title) && string.IsNullOrEmpty(notice.Detail))
                return false;
            if (HasActive && _active.Equals(notice))
                return false;
            foreach (HudTransientNotice pending in _pending)
                if (pending.Equals(notice)) return false;

            _pending.Enqueue(notice);
            return true;
        }

        internal bool TryGetOrActivate(out HudTransientNotice notice)
        {
            if (HasActive)
            {
                notice = _active;
                return true;
            }

            if (_pending.Count == 0)
            {
                notice = default;
                return false;
            }

            _active = _pending.Dequeue();
            HasActive = true;
            notice = _active;
            return true;
        }

        internal void CompleteActive()
        {
            HasActive = false;
            _active = default;
        }

        internal void Clear()
        {
            _pending.Clear();
            CompleteActive();
        }
    }
}
