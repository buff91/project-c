using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 턴제 행동 피드백을 몇 줄 보관하는 고정 용량 링. 순수 C# — UnityEngine 비의존.
    ///
    /// 왜 필요한가: SPD 계보 턴제인데 지금은 <c>InteractionFeedback</c> 한 줄이 3초 뒤
    /// 사라질 뿐이라, 직전 턴에 무슨 일이 있었는지(범프 공격 결과·함정·상태 틱·낙하)를
    /// 되짚을 방법이 없다. 이벤트는 이미 다 쏘고 있으므로 버리지 말고 쌓기만 하면 된다.
    ///
    /// 연속 중복은 "text ×N"으로 접는다. 같은 적을 세 번 때리면 세 줄이 아니라 한 줄이
    /// 되어야 4줄짜리 창이 한 행동으로 가득 차지 않는다.
    /// </summary>
    public sealed class MessageLog
    {
        private struct Entry
        {
            public string Text;
            public int Repeat;
        }

        private readonly List<Entry> _entries;
        private readonly int _capacity;

        public MessageLog(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _entries = new List<Entry>(capacity);
        }

        /// <summary>보관 중인 줄 수. 접힌 중복은 한 줄로 센다.</summary>
        public int Count => _entries.Count;

        public int Capacity => _capacity;

        /// <summary>가장 최근 줄. 비어 있으면 null.</summary>
        public string Newest => _entries.Count == 0 ? null : Render(_entries[_entries.Count - 1]);

        /// <summary>
        /// 한 줄 추가한다. 직전 줄과 같은 문자열이면 새 줄을 만들지 않고 반복 수만 올린다.
        /// null·빈 문자열은 무시한다 — 이벤트가 빈 피드백을 쏘는 경로가 있다.
        /// </summary>
        public void Add(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_entries.Count > 0)
            {
                Entry last = _entries[_entries.Count - 1];
                if (string.Equals(last.Text, text, StringComparison.Ordinal))
                {
                    last.Repeat++;
                    _entries[_entries.Count - 1] = last;
                    return;
                }
            }

            _entries.Add(new Entry { Text = text, Repeat = 1 });
            while (_entries.Count > _capacity) _entries.RemoveAt(0);
        }

        /// <summary>오래된 줄부터 최신 줄 순서로 렌더한다.</summary>
        public IReadOnlyList<string> Lines()
        {
            var lines = new List<string>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++) lines.Add(Render(_entries[i]));
            return lines;
        }

        public void Clear() => _entries.Clear();

        private static string Render(Entry entry) =>
            entry.Repeat > 1 ? entry.Text + " ×" + entry.Repeat : entry.Text;
    }
}
