#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/../.." && pwd)"
venv_python="$project_root/.venv-art-review/bin/python"

if [[ -x "$venv_python" ]]; then
  python_bin="$venv_python"
else
  python_bin="$(command -v python3)"
fi

# python.org 배포판은 macOS Keychain 인증서를 자동으로 사용하지 않을 수 있다.
# Slack SDK의 urllib 연결이 검증된 CA 번들을 사용하도록 명시한다.
if [[ -z "${SSL_CERT_FILE:-}" ]]; then
  SSL_CERT_FILE="$("$python_bin" -c 'import certifi; print(certifi.where())')"
  export SSL_CERT_FILE
fi

cd "$project_root"
exec "$python_bin" Tools/ArtPipeline/art_slack_bot.py "$@"
