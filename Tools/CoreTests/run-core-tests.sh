#!/usr/bin/env bash
#
# Unity 없이 Core 규칙 테스트를 돌린다. .NET SDK 8이 없으면 사용자 홈에 설치한다.
#
#   ./Tools/CoreTests/run-core-tests.sh              # 전체
#   ./Tools/CoreTests/run-core-tests.sh SightRules   # 이름으로 필터
#
# 이건 Unity Test Runner를 대체하지 않는다 — PlayMode·씬·스프라이트·HUD 검증은 에디터에서 한다.
# 자세한 경계는 Tools/CoreTests/ProjectC.CoreTests.csproj 주석 참조.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$REPO_ROOT/Tools/CoreTests/ProjectC.CoreTests.csproj"
DOTNET_ROOT_DIR="${DOTNET_ROOT:-$HOME/.dotnet}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

find_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then command -v dotnet; return 0; fi
  if [ -x "$DOTNET_ROOT_DIR/dotnet" ]; then echo "$DOTNET_ROOT_DIR/dotnet"; return 0; fi
  return 1
}

if ! DOTNET="$(find_dotnet)"; then
  echo "dotnet SDK가 없다 — .NET 8을 $DOTNET_ROOT_DIR 에 설치한다."
  INSTALLER="$(mktemp)"
  # -L 필수: dot.net 은 301로 리다이렉트한다.
  curl -sSL --max-time 120 -o "$INSTALLER" https://dot.net/v1/dotnet-install.sh
  bash "$INSTALLER" --channel 8.0 --install-dir "$DOTNET_ROOT_DIR" --no-path
  rm -f "$INSTALLER"
  DOTNET="$DOTNET_ROOT_DIR/dotnet"
fi

echo "dotnet: $("$DOTNET" --version)"

if [ "$#" -gt 0 ]; then
  exec "$DOTNET" test "$PROJECT" --filter "FullyQualifiedName~$1"
fi

exec "$DOTNET" test "$PROJECT"
