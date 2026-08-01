#!/usr/bin/env bash
#
# PostToolUse 훅 — ComfyUI 워크플로 쌍(*.workflow.json ↔ *.api.json)을 쓴 직후에
# 두 파일이 여전히 같은 그래프인지 본다. 실패해도 편집을 되돌리지 않고 exit 2로
# 경고를 에이전트에 돌려준다.
#
# 왜 필요한가: 캔버스는 사람이 보는 편집 SSOT, API 는 실제로 /prompt 에 보내는
# 실행본이다. 한쪽만 고치면 ComfyUI 는 조용히 옛 그래프로 생성하고 seed 재현성이
# 무너지는데 아무도 모른다. comfy_batch.py 에 validate 가 있었지만 사람이 기억해서
# 돌려야 했다 — 기억은 방어선이 아니다.
#
# 한계: 에이전트의 파일 편집에만 걸린다. ComfyUI 캔버스에서 직접 고친 뒤 Export 를
# 잊은 경우는 잡지 못하므로, 그때는 인자 없는 전체 스윕을 돌린다:
#   python3 Tools/ArtPipeline/comfy_batch.py validate
set -uo pipefail

INPUT="$(cat)"
FILE="$(printf '%s' "$INPUT" | jq -r '.tool_response.filePath // .tool_input.file_path // empty')"

[ -n "$FILE" ] || exit 0
case "$FILE" in
  *.api.json|*.workflow.json) ;;
  *) exit 0 ;;
esac
case "$FILE" in
  */docs/art-direction/comfyui/*) ;;
  *) exit 0 ;;
esac

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BASE="${FILE%.api.json}"
BASE="${BASE%.workflow.json}"
API="${BASE}.api.json"
CANVAS="${BASE}.workflow.json"

if [ ! -f "$CANVAS" ]; then
  printf '%s\n\n' "[워크플로 쌍 없음] $(basename "$BASE") 의 캔버스(*.workflow.json)가 없다.
  Project-C 는 실행본(.api.json)과 캔버스를 항상 함께 보존한다 — 캔버스가 없으면
  다음 사람이 그래프를 열어 고칠 수 없다. ComfyUI 에서 캔버스를 저장해 짝을 맞춘다." >&2
  exit 2
fi

if [ ! -f "$API" ]; then
  printf '%s\n\n' "[API Export 누락] $(basename "$BASE") 의 실행본(*.api.json)이 없다.
  캔버스만으로는 발주가 돌지 않는다. ComfyUI 에서 Save/Export (API Format) 으로
  같은 basename 의 .api.json 을 만든다." >&2
  exit 2
fi

OUTPUT="$(cd "$REPO_ROOT" && python3 Tools/ArtPipeline/comfy_batch.py validate "$API" 2>&1)"
STATUS=$?
[ $STATUS -eq 0 ] && exit 0

printf '%s\n\n' "[워크플로 쌍 어긋남] $(basename "$API") 와 캔버스가 다른 그래프다.
$OUTPUT

  캔버스에서 고치고 Save/Export (API Format) 으로 .api.json 을 다시 내보낸다.
  API JSON 만 손으로 고치면 실제 실행과 캔버스가 갈라지고, ComfyUI 는 아무 말 없이
  옛 그래프로 생성한다." >&2
exit 2
