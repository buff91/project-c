#!/usr/bin/env bash
# ComfyUI 백엔드를 Electron 셸(Comfy Desktop) 없이 띄운다.
#
# Desktop 앱은 렌더러 프로세스 몇 개를 상주시키는데, 18GB 통합 메모리에서 SDXL
# 배치를 돌리면 그만큼이 스왑으로 밀린다. 발주 세션에서는 백엔드만 띄우고 UI 가
# 필요할 때만 Desktop 을 쓴다.
#
#   ./Tools/ArtPipeline/run_comfy_headless.sh            # 일반 모드
#   COMFY_LOWVRAM=1 ./Tools/ArtPipeline/run_comfy_headless.sh
#
# COMFY_LOWVRAM=1 은 기본으로 쓰지 않는다. 2026-07-31 짝 비교에서 normal 233초 /
# lowvram 282초로 오히려 느렸다 — 이 머신은 메모리가 병목이 아니라 모델을 쪼개도
# 얻을 게 없고 전송 비용만 붙는다. 조건이 달라졌다 싶으면 손잡이를 쓰되, 반드시
# 같은 시점에 짝지어 재라 (장당 시간이 배경 부하로 229~358초까지 흔들린다).
set -euo pipefail

comfy_root="${COMFY_ROOT:-$HOME/ComfyUI-Installs/Comfy Local}"
python_bin="$comfy_root/ComfyUI/.venv/bin/python3"
model_paths="${COMFY_MODEL_PATHS:-$HOME/Library/Application Support/Comfy Desktop/shared_model_paths.yaml}"
input_dir="${COMFY_INPUT_DIR:-$HOME/Documents/ComfyUI/input}"
output_dir="${COMFY_OUTPUT_DIR:-$HOME/Documents/ComfyUI/output}"
port="${COMFY_PORT:-8188}"

if [[ ! -x "$python_bin" ]]; then
    echo "ComfyUI 파이썬을 찾을 수 없다: $python_bin" >&2
    echo "COMFY_ROOT 로 설치 경로를 지정한다." >&2
    exit 1
fi

if pgrep -f '[C]omfyUI/main.py' >/dev/null 2>&1; then
    echo "ComfyUI 백엔드가 이미 떠 있다. 먼저 내린 뒤 다시 실행한다." >&2
    exit 1
fi

args=(
    -s ComfyUI/main.py
    --listen 127.0.0.1
    --port "$port"
    --extra-model-paths-config "$model_paths"
    --input-directory "$input_dir"
    --output-directory "$output_dir"
)
if [[ -n "${COMFY_LOWVRAM:-}" ]]; then
    args+=(--lowvram)
fi

cd "$comfy_root"
echo "ComfyUI 백엔드 기동: http://127.0.0.1:$port ${COMFY_LOWVRAM:+(--lowvram)}"
exec "$python_bin" "${args[@]}"
