using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 절차 생성 스프라이트의 공용 캐시. 같은 키를 두 번 그리지 않게만 막는다.
    ///
    /// 키는 그린 결과를 결정하는 값 전부를 담아야 한다 — 팔레트·진행 지수·회전 방향이 키에서
    /// 빠지면 다른 그림이 같은 캐시를 덮어쓴다. 캐시 생명주기는 소유자(`IsoPrototypeDemo`)와 같다.
    /// </summary>
    internal sealed class PrototypeSpriteCache
    {
        private readonly Dictionary<string, Sprite> _entries = new Dictionary<string, Sprite>();

        internal bool TryGetValue(string key, out Sprite sprite) => _entries.TryGetValue(key, out sprite);

        internal Sprite this[string key]
        {
            get => _entries[key];
            set => _entries[key] = value;
        }

        internal int Count => _entries.Count;
    }
}
