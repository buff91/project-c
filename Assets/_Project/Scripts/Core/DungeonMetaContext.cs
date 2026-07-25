using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 생성기가 알아야 하는 <b>판을 넘는 진행 상태</b>. 해금 목록을 인자로 하나하나 늘리면
    /// <c>Generate</c>의 매개변수가 열 개를 넘기므로 읽기 전용 값 하나로 묶는다.
    ///
    /// <para>
    /// <b>기본값(default)은 "아무것도 해금되지 않음"이 아니라 "제약 없음"이다.</b>
    /// 테스트와 편집 모드 미리보기가 메타 없이도 예전과 같은 던전을 만들어야 하기 때문이다 —
    /// 게이트를 걸 때는 호출부가 반드시 실제 메타를 넘긴다.
    /// </para>
    /// </summary>
    public readonly struct DungeonMetaContext
    {
        private readonly IReadOnlyCollection<ItemKind> _unlockedItems;
        private readonly IReadOnlyCollection<string> _rescuedNpcs;

        private DungeonMetaContext(
            IReadOnlyCollection<ItemKind> unlockedItems,
            IReadOnlyCollection<string> rescuedNpcs,
            bool gated)
        {
            _unlockedItems = unlockedItems;
            _rescuedNpcs = rescuedNpcs;
            Gated = gated;
        }

        /// <summary>해금 게이트를 적용하는가. false 면 모든 도구가 풀에 있다(제약 없음).</summary>
        public bool Gated { get; }

        /// <summary>해금된 도구들. <see cref="Gated"/>가 false 면 의미 없다.</summary>
        public IReadOnlyCollection<ItemKind> UnlockedItems =>
            _unlockedItems ?? Array.Empty<ItemKind>();

        /// <summary>구출한 동료들. 미구출 NPC가 있는 층에 갇힌 방이 생긴다.</summary>
        public IReadOnlyCollection<string> RescuedNpcs =>
            _rescuedNpcs ?? Array.Empty<string>();

        /// <summary>
        /// 실제 메타에서 만든다 — 이때부터 미해금 도구가 드랍 풀에서 빠진다.
        /// </summary>
        public static DungeonMetaContext FromUnlocked(
            IReadOnlyCollection<ItemKind> unlockedItems,
            IReadOnlyCollection<string> rescuedNpcs = null) =>
            new DungeonMetaContext(unlockedItems, rescuedNpcs, gated: true);

        /// <summary>게이트 없음 — 모든 도구가 나온다. 테스트·미리보기의 기본값.</summary>
        public static DungeonMetaContext Unrestricted => default;

        /// <summary>이 종류가 지금 드랍 풀에 있는가.</summary>
        public bool IsAvailable(ItemKind kind) =>
            !Gated || ItemUnlockRules.IsAvailable(kind, UnlockedItems);

        /// <summary>굴린 결과를 실제로 놓을 종류로 바꾼다(미해금이면 형제로 치환).</summary>
        public ItemKind Resolve(ItemKind rolled) =>
            !Gated ? rolled : ItemUnlockRules.Resolve(rolled, UnlockedItems);

        /// <summary>
        /// 이 층에 갇힌 방을 둘 NPC. 게이트가 없으면(테스트·미리보기) 아무도 두지 않는다 —
        /// 예전과 같은 던전을 유지해야 한다.
        /// </summary>
        public ShelterNpcDefinition PendingNpcAt(int progressIndex) =>
            !Gated ? null : ShelterNpcRoster.PendingAt(progressIndex, RescuedNpcs);

        /// <summary>갇힌 방이 생길 층들 — 숨은 방 후보에서 뺀다(못 찾으면 진행이 막힌다).</summary>
        public HashSet<int> PendingNpcFloors() =>
            !Gated ? new HashSet<int>() : ShelterNpcRoster.PendingFloors(RescuedNpcs);

        /// <summary>이 시설이 쉘터에 있는가. 장비 게이트가 이 값을 본다.</summary>
        public bool IsFacilityOpen(ShelterFacility facility) =>
            !Gated || ShelterNpcRoster.IsFacilityOpen(facility, RescuedNpcs);
    }
}
