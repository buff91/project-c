#!/usr/bin/env bash
#
# PostToolUse 훅 — .cs 를 쓰거나 고친 직후에 기계로 검사 가능한 규칙만 본다.
# 실패해도 편집을 되돌리지 않는다(이미 일어난 일이다). exit 2로 경고를 에이전트에 돌려준다.
#
# 검사 대상은 CLAUDE.md의 규칙 중 사람이 놓치기 쉬운 셋뿐이다:
#   1. Scripts/Core 의 UnityEngine 의존 (dotnet shim이 도는 근거를 지킨다)
#   2. Assets 아래 새 .cs 의 .meta 누락 (실제로 빠뜨린 이력이 있다)
#   3. EditMode 테스트가 Unity 타입을 쓰면 shim 제외 목록에 넣어야 한다는 안내
set -uo pipefail

INPUT="$(cat)"
FILE="$(printf '%s' "$INPUT" | jq -r '.tool_response.filePath // .tool_input.file_path // empty')"

[ -n "$FILE" ] || exit 0
case "$FILE" in
  *.cs) ;;
  *) exit 0 ;;
esac
[ -f "$FILE" ] || exit 0

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSPROJ="$REPO_ROOT/Tools/CoreTests/ProjectC.CoreTests.csproj"
WARNINGS=()

# 1. Core는 UnityEngine에 의존하지 않는다 — IsoGrid.cs만 예외로 허용된다.
case "$FILE" in
  */Assets/_Project/Scripts/Core/*)
    if grep -q 'using UnityEngine' "$FILE" && [ "$(basename "$FILE")" != "IsoGrid.cs" ]; then
      WARNINGS+=("[Core 순수성] $(basename "$FILE") 가 UnityEngine에 의존한다.
  Scripts/Core는 순수 C# 로직이라는 게 CLAUDE.md 아키텍처 규칙이고, dotnet shim이
  Unity 없이 도는 근거다. UnityEngine 타입이 꼭 필요하면 그 로직은 Scripts/Gameplay에
  속한다. 정말 Core에 남겨야 한다면 Tools/CoreTests/ProjectC.CoreTests.csproj의
  Compile Remove에 추가해야 하고, 그만큼 shim 커버리지가 줄어든다.")
    fi
    ;;
esac

# 2. Assets 아래 .cs 는 .meta 가 함께 커밋돼야 한다 (에디터 없는 세션에서는 생성되지 않는다).
case "$FILE" in
  */Assets/*)
    if [ ! -f "${FILE}.meta" ]; then
      WARNINGS+=("[.meta 누락] $(basename "$FILE").meta 가 없다.
  Unity 에디터가 열리면 생성되지만, 에디터 없는 세션에서는 만들어지지 않는다.
  이 상태로 커밋하면 다른 환경에서 스크립트 참조가 끊긴다 — 커밋 전에 에디터를 한 번 열거나,
  누락을 사용자에게 알린다.")
    fi
    ;;
esac

# 3. EditMode 테스트가 Unity 타입을 쓰면 shim 빌드가 깨진다 — 제외 목록 갱신이 필요하다.
case "$FILE" in
  */Assets/_Project/Tests/EditMode/*)
    BASE="$(basename "$FILE")"
    if grep -qE 'UnityEngine|UnityEditor|ProjectC\.Gameplay' "$FILE" \
       && [ -f "$CSPROJ" ] && ! grep -q "$BASE" "$CSPROJ"; then
      WARNINGS+=("[shim 제외 필요] $BASE 가 Unity/Gameplay 타입을 쓴다.
  이 파일은 에디터에서만 돌아가므로 Tools/CoreTests/ProjectC.CoreTests.csproj의
  Compile Remove 목록에 추가한다. 아니면 ./Tools/CoreTests/run-core-tests.sh 가
  컴파일 에러로 깨진다.")
    fi
    ;;
esac

[ ${#WARNINGS[@]} -eq 0 ] && exit 0

for w in "${WARNINGS[@]}"; do
  printf '%s\n\n' "$w" >&2
done
exit 2
