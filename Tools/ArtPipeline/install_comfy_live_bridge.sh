#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_dir="$script_dir/comfyui_project_c_live"

discover_running_custom_nodes_dir() {
    local pid=""
    local process_cwd=""

    if command -v pgrep >/dev/null 2>&1 && command -v lsof >/dev/null 2>&1; then
        pid="$(pgrep -f '[C]omfyUI/main.py' | head -n 1 || true)"
        if [[ -n "$pid" ]]; then
            process_cwd="$(
                lsof -a -p "$pid" -d cwd -Fn 2>/dev/null \
                    | sed -n 's/^n//p' \
                    | head -n 1
            )"
            if [[ -d "$process_cwd/ComfyUI/custom_nodes" ]]; then
                printf '%s\n' "$process_cwd/ComfyUI/custom_nodes"
                return 0
            fi
            if [[ -d "$process_cwd/custom_nodes" ]]; then
                printf '%s\n' "$process_cwd/custom_nodes"
                return 0
            fi
        fi
    fi

    return 1
}

if [[ -n "${COMFYUI_CUSTOM_NODES_DIR:-}" ]]; then
    custom_nodes_dir="$COMFYUI_CUSTOM_NODES_DIR"
elif custom_nodes_dir="$(discover_running_custom_nodes_dir)"; then
    :
elif [[ -d "$HOME/ComfyUI-Installs/Comfy Local/ComfyUI/custom_nodes" ]]; then
    custom_nodes_dir="$HOME/ComfyUI-Installs/Comfy Local/ComfyUI/custom_nodes"
else
    custom_nodes_dir="$HOME/Documents/ComfyUI/custom_nodes"
fi

target="$custom_nodes_dir/project-c-live"

if [[ -L "$target" && "$(readlink "$target")" == "$source_dir" ]]; then
    echo "Project-C ComfyUI live bridge is already installed: $target"
elif [[ -e "$target" || -L "$target" ]]; then
    echo "Refusing to replace existing path: $target" >&2
    exit 1
else
    mkdir -p "$custom_nodes_dir"
    ln -s "$source_dir" "$target"
    echo "Installed Project-C ComfyUI live bridge: $target -> $source_dir"
fi

if python3 "$script_dir/comfy_batch.py" \
    --url "${COMFYUI_URL:-http://127.0.0.1:8188}" \
    sync-workflows; then
    echo "Published Project-C workflows to the ComfyUI Workflows sidebar."
else
    echo "Could not publish workflows while ComfyUI is offline." >&2
    echo "Run this installer again after ComfyUI starts." >&2
fi

echo "Restart ComfyUI Desktop once to activate it."
echo "Restart the long-running art review worker after its current job finishes."
