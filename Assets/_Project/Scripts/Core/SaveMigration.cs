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
    /// <b>v1 → v2</b>: 원거리 충전 상태의 구세이브 호환. Unity <c>JsonUtility</c>는
    /// JSON에 중첩 필드가 없어도 빈 객체(0/0)를 만들기 때문에, 로더가 원문 JSON에서
    /// 필드 존재 여부를 넘긴다. 필드가 정말 없을 때만 복원용 null로 바꾸고, 실제로 저장된
    /// 0/0(완전 방전 직후)을 포함한 모든 상태는 그대로 보존한다.
    /// </para>
    /// <para>
    /// <b>v2 → v3</b>: 런 종료 정산 영수증을 메타 저장에 추가한다. 구세이브는 빈 목록이
    /// 곧 올바른 초기값이라 값 변환은 없고, 이후 정산부터 보상과 같은 원자적 JSON에 기록한다.
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
        public const int CurrentVersion = 3;
        private const int ChargeUnitVersion = 1;
        private const int RangedChargeVersion = 2;

        /// <summary>
        /// 현재 빌드가 안전하게 다시 쓸 수 없는 미래 버전의 메타 세이브인지 판정한다.
        /// 알 수 없는 필드는 역직렬화 순간 사라지므로, 단순히 버전 번호만 유지한 채 저장해서는 안 된다.
        /// </summary>
        public static bool HasFutureSchema(MetaSaveData data) =>
            data != null && data.schemaVersion > CurrentVersion;

        /// <summary>
        /// 런 루트 또는 중첩 텔레메트리 중 하나라도 미래 버전이면 전체 체크포인트를 읽기 전용으로 본다.
        /// 현재 타입으로 다시 직렬화하면 어느 쪽의 알 수 없는 필드든 유실될 수 있기 때문이다.
        /// </summary>
        public static bool HasFutureSchema(RunSaveData data) =>
            data != null &&
            (data.schemaVersion > CurrentVersion ||
             data.telemetry != null &&
             data.telemetry.schemaVersion > RunTelemetry.CurrentSchemaVersion);

        /// <summary>
        /// 창고·로드아웃을 최신 스키마로 올린다. 이미 최신이면 아무것도 하지 않는다(멱등).
        /// </summary>
        /// <returns>실제로 변환했으면 true.</returns>
        public static bool Migrate(MetaSaveData data, Func<ItemKind, int> chargesPerItem)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (chargesPerItem == null) throw new ArgumentNullException(nameof(chargesPerItem));
            if (data.schemaVersion >= CurrentVersion) return false;

            // v2는 런 전용 원거리 상태, v3는 빈 정산 영수증 목록 추가다. 공유 버전을
            // 올렸다고 v1 창고를 다시 곱하면 소모품이 복제되므로 각 변환은 자기 도입
            // 버전보다 오래됐을 때만 돈다.
            if (data.schemaVersion < ChargeUnitVersion)
            {
                ScaleToCharges(data.stash, chargesPerItem);
                ScaleToCharges(data.loadout, chargesPerItem);
            }
            data.schemaVersion = CurrentVersion;
            return true;
        }

        /// <summary>
        /// 이미 메모리에 만들어진 런을 최신 스키마로 올린다. 중첩 상태가 null이 아니면
        /// 직렬화 원문에도 필드가 있었다고 간주한다. 파일 로드는 필드 존재 여부를 정확히
        /// 아는 세 인자 오버로드를 사용해야 한다.
        /// </summary>
        public static bool Migrate(RunSaveData data, Func<ItemKind, int> chargesPerItem)
        {
            return Migrate(
                data,
                chargesPerItem,
                rangedChargesFieldWasPresent: data != null && data.rangedCharges != null);
        }

        /// <summary>진행 중이던 런의 백팩과 중첩 계측을 최신 스키마로 올린다.</summary>
        public static bool Migrate(
            RunSaveData data,
            Func<ItemKind, int> chargesPerItem,
            bool rangedChargesFieldWasPresent)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (chargesPerItem == null) throw new ArgumentNullException(nameof(chargesPerItem));
            if (data.schemaVersion > CurrentVersion) return false;

            bool telemetryChanged =
                data.telemetry != null && data.telemetry.FreezeFloorLabels();
            if (data.schemaVersion == CurrentVersion) return telemetryChanged;

            if (data.schemaVersion < ChargeUnitVersion)
                ScaleToCharges(data.items, chargesPerItem);

            if (data.schemaVersion < RangedChargeVersion &&
                !rangedChargesFieldWasPresent)
            {
                // JsonUtility가 필드 누락을 빈 객체(0/0)로 만든 경우에만 null로 되돌린다.
                // 실제 0/0 상태는 원문에 키가 있으므로 이 분기를 타지 않는다.
                data.rangedCharges = null;
            }
            data.schemaVersion = CurrentVersion;
            return true;
        }

        /// <summary>저장 직전에 현재 버전을 찍는다.</summary>
        public static void Stamp(MetaSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion > CurrentVersion) return;
            data.schemaVersion = CurrentVersion;
        }

        /// <summary>저장 직전에 현재 버전을 찍는다.</summary>
        public static void Stamp(RunSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion > CurrentVersion) return;
            data.telemetry?.FreezeFloorLabels();
            data.schemaVersion = CurrentVersion;
        }

        /// <summary>
        /// 원문 JSON에 원거리 충전 필드가 실제로 있었는지 확인한다. 필드가 없는 구 JSON과
        /// 값이 0/0인 JSON은 역직렬화 뒤 모양이 같으므로 반드시 역직렬화 전에 판정한다.
        /// </summary>
        public static bool HasSerializedRangedCharges(string json) =>
            HasTopLevelJsonProperty(json, "rangedCharges");

        private static bool HasTopLevelJsonProperty(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
                return false;

            int objectDepth = 0;
            int arrayDepth = 0;
            bool inString = false;
            bool escaped = false;
            int stringStart = -1;

            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current != '"') continue;

                    inString = false;
                    if (objectDepth != 1 || arrayDepth != 0 ||
                        !JsonStringEquals(json, stringStart, i, propertyName))
                        continue;

                    int next = i + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && json[next] == ':') return true;
                    continue;
                }

                switch (current)
                {
                    case '"':
                        inString = true;
                        escaped = false;
                        stringStart = i + 1;
                        break;
                    case '{':
                        objectDepth++;
                        break;
                    case '}':
                        objectDepth--;
                        break;
                    case '[':
                        arrayDepth++;
                        break;
                    case ']':
                        arrayDepth--;
                        break;
                }
            }

            return false;
        }

        /// <summary>
        /// JSON 문자열 조각을 디코딩하며 비교한다. 속성명은 보통 그대로 기록되지만
        /// <c>ranged\u0043harges</c>처럼 합법적인 유니코드 이스케이프도 같은 키다.
        /// </summary>
        private static bool JsonStringEquals(
            string json,
            int start,
            int endExclusive,
            string expected)
        {
            int expectedIndex = 0;
            for (int i = start; i < endExclusive; i++)
            {
                char decoded = json[i];
                if (decoded == '\\')
                {
                    i++;
                    if (i >= endExclusive) return false;

                    switch (json[i])
                    {
                        case '"': decoded = '"'; break;
                        case '\\': decoded = '\\'; break;
                        case '/': decoded = '/'; break;
                        case 'b': decoded = '\b'; break;
                        case 'f': decoded = '\f'; break;
                        case 'n': decoded = '\n'; break;
                        case 'r': decoded = '\r'; break;
                        case 't': decoded = '\t'; break;
                        case 'u':
                            if (i + 4 >= endExclusive) return false;
                            int codePoint = 0;
                            for (int digit = 1; digit <= 4; digit++)
                            {
                                int hex = HexValue(json[i + digit]);
                                if (hex < 0) return false;
                                codePoint = codePoint * 16 + hex;
                            }
                            decoded = (char)codePoint;
                            i += 4;
                            break;
                        default:
                            return false;
                    }
                }
                else if (decoded < 0x20)
                {
                    return false;
                }

                if (expectedIndex >= expected.Length ||
                    decoded != expected[expectedIndex])
                    return false;
                expectedIndex++;
            }

            return expectedIndex == expected.Length;
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
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
