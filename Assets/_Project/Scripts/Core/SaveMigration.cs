using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 세이브 스키마 변환. 순수 C# — UnityEngine 비의존.
    ///
    /// <para>
    /// <b>v0 → v1</b>: 소모품 충전 도입. `ItemStack.count`가 세던 것이 "개수"에서
    /// "총 사용 횟수"로 바뀌었으므로, 구세이브의 개수에 `ChargesPerItem`을 곱해야
    /// 플레이어가 갖고 있던 회분이 보존된다. 곱수가 1인 종류는 그대로다.
    /// </para>
    /// <para>
    /// <b>왜 배수를 주입받나.</b> `ItemCatalog`는 `static readonly` 표라 테스트가 값을
    /// 바꿀 수 없다. 배수를 인자로 받으면 테스트가 <b>진짜</b> 변환을 검증할 수 있고,
    /// 카탈로그의 실제 값이 나중에 바뀌어도 이 규칙 자체는 흔들리지 않는다.
    /// 프로덕션은 <see cref="ItemCatalog.ChargesPerItem"/>을 넘긴다.
    /// </para>
    /// <para>
    /// <b>칸당 충전 값을 바꾸는 것만으로는 새 버전이 필요하지 않다.</b> v0 는 단위가
    /// 달랐기 때문에(개수) 변환이 필요했지만, v1 이후의 `count`는 이미 <b>충전</b>이고
    /// 충전은 칸당 값이 바뀌어도 같은 뜻이다 — 달라지는 것은 `ceil(충전 / 칸당)`으로
    /// <b>파생</b>되는 칸수뿐이며, 그게 바로 이 기능의 목적이다. 그래서 물약 2 → 통조림 3
    /// 같은 후속 조정은 플레이어의 회분을 정확히 보존한 채 칸만 줄인다.
    /// 여기에 배수를 한 번 더 곱하면 보존이 아니라 <b>소지량 증정</b>이 된다.
    /// 새 버전이 필요한 것은 `count`가 <b>세는 대상</b> 자체가 바뀔 때뿐이다.
    /// </para>
    /// <para>
    /// <b>배선은 네 곳뿐이다</b> — `MetaStore`/`RunSaveStore`의 로드 직후와 저장 직전.
    /// `AtomicJsonStore`를 직접 부르는 다른 프로덕션 코드는 없다. 새 저장 경로가 생기면
    /// 이 변환을 조용히 우회하므로, 경로를 늘릴 때 반드시 함께 배선한다.
    /// </para>
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>현재 스키마 버전. 구세이브(필드 없음)는 0으로 로드된다.</summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 창고·로드아웃을 최신 스키마로 올린다. 이미 최신이면 아무것도 하지 않는다(멱등).
        /// </summary>
        /// <returns>실제로 변환했으면 true.</returns>
        public static bool Migrate(MetaSaveData data, Func<ItemKind, int> chargesPerItem)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (chargesPerItem == null) throw new ArgumentNullException(nameof(chargesPerItem));
            if (data.schemaVersion >= CurrentVersion) return false;

            ScaleToCharges(data.stash, chargesPerItem);
            ScaleToCharges(data.loadout, chargesPerItem);
            data.schemaVersion = CurrentVersion;
            return true;
        }

        /// <summary>진행 중이던 런의 백팩을 최신 스키마로 올린다.</summary>
        public static bool Migrate(RunSaveData data, Func<ItemKind, int> chargesPerItem)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (chargesPerItem == null) throw new ArgumentNullException(nameof(chargesPerItem));
            if (data.schemaVersion >= CurrentVersion) return false;

            ScaleToCharges(data.items, chargesPerItem);
            data.schemaVersion = CurrentVersion;
            return true;
        }

        /// <summary>저장 직전에 현재 버전을 찍는다.</summary>
        public static void Stamp(MetaSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.schemaVersion = CurrentVersion;
        }

        /// <summary>저장 직전에 현재 버전을 찍는다.</summary>
        public static void Stamp(RunSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.schemaVersion = CurrentVersion;
        }

        /// <summary>
        /// v0 개수를 v1 충전으로 환산한다. 미등록 종류(enum이 바뀐 세이브)는 건드리지 않는다 —
        /// 여기서 던지면 세이브 하나가 게임 전체를 못 열게 만든다.
        /// </summary>
        private static void ScaleToCharges(
            List<ItemStack> stacks, Func<ItemKind, int> chargesPerItem)
        {
            if (stacks == null) return;

            // ItemStack 은 class 라 제자리에서 고친다(재대입 불필요).
            foreach (ItemStack stack in stacks)
            {
                if (stack == null || stack.count <= 0) continue;

                int per;
                try { per = chargesPerItem(stack.kind); }
                catch (ArgumentOutOfRangeException) { continue; }
                if (per <= 1) continue;

                stack.count *= per;
            }
        }
    }
}
