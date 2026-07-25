#!/usr/bin/env bash
#
# Stop 훅 — .cs 를 건드린 세션이 테스트를 돌리지 않고 끝나는 것을 막는다.
#
# 왜: 이 리포는 "673/673 통과"라고 기록된 상태로 컴파일되지 않는 테스트를 커밋한 이력이 있다.
# 검증을 사람의 기억이나 에이전트의 주장에 맡기지 않고, 세션이 끝나는 지점에서 기계가 확인한다.
#
# CI를 release 브랜치 한정으로 두기로 했으므로(main은 무방비), 이 훅이 로컬의 유일한 자동 방어선이다.
set -uo pipefail

INPUT="$(cat)"

# 훅이 스스로를 다시 깨우는 무한 루프를 막는다.
if [ "$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false')" = "true" ]; then
  exit 0
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT" || exit 0

# 작업 트리에 손댄 .cs 가 없으면 볼 것이 없다.
if ! git status --porcelain -- '*.cs' 2>/dev/null | grep -q .; then
  exit 0
fi

DOTNET=""
if command -v dotnet >/dev/null 2>&1; then
  DOTNET="$(command -v dotnet)"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET="$HOME/.dotnet/dotnet"
fi

# dotnet이 없으면 여기서 설치하지 않는다 — Stop 훅에서 수 분을 쓰는 건 과하다.
if [ -z "$DOTNET" ]; then
  echo '{"systemMessage":"Core 테스트를 건너뛰었다 (dotnet 없음). ./Tools/CoreTests/run-core-tests.sh 가 설치까지 해준다."}'
  exit 0
fi

# verbosity는 minimal 이상이어야 실패한 테스트의 assertion 출력이 나온다(quiet는 요약만 남긴다).
OUTPUT="$("$DOTNET" test "$REPO_ROOT/Tools/CoreTests/ProjectC.CoreTests.csproj" \
  --nologo --verbosity minimal 2>&1)"
STATUS=$?

if [ $STATUS -eq 0 ]; then
  SUMMARY="$(printf '%s' "$OUTPUT" | grep -oE 'Passed![^-]*- *[^-]*' | tail -1 | tr -s ' ')"
  echo "{\"systemMessage\":$(printf '%s' "Core 테스트 통과. ${SUMMARY}" | jq -Rs .)}"
  exit 0
fi

# 실패 — 에이전트에게 돌려주고 세션을 끝내지 못하게 한다.
{
  echo "Core 규칙 테스트가 실패한 상태로 세션을 끝내려 한다. 아래를 처리하고 끝낸다."
  echo
  printf '%s\n' "$OUTPUT" | grep -E 'error|Failed|Error Message|Expected|But was|Assert' | head -40
  echo
  echo "판정 순서: 코드 버그인가, 낡은 테스트인가."
  echo "낡은 테스트로 판정하려면 GDD.md / docs/SYSTEMS.md / CLAUDE.md에서 근거를 인용한다."
  echo "설계 SSOT가 애매하면 고치지 말고 사용자에게 묻는다."
  echo "전체 출력: ./Tools/CoreTests/run-core-tests.sh"
} >&2
exit 2
