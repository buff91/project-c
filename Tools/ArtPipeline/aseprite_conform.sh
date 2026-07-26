#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 4 || $# -gt 5 ]]; then
  echo "usage: $0 INPUT.png OUTPUT.aseprite WIDTH HEIGHT [strict|nearest]" >&2
  exit 2
fi

input_path="$1"
output_path="$2"
expected_width="$3"
expected_height="$4"
resize_mode="${5:-strict}"

if [[ ! "$expected_width" =~ ^[0-9]+$ || ! "$expected_height" =~ ^[0-9]+$ ]]; then
  echo "WIDTH and HEIGHT must be positive integers" >&2
  exit 2
fi
if [[ "$resize_mode" != "strict" && "$resize_mode" != "nearest" ]]; then
  echo "resize mode must be strict or nearest" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/../.." && pwd)"
palette_path="$project_root/Assets/_Project/Art/Source/Aseprite/project-c-torchstone.gpl"
lua_script="$script_dir/aseprite_conform.lua"

aseprite_bin="${PROJECTC_ASEPRITE_BIN:-}"
if [[ -z "$aseprite_bin" ]]; then
  candidates=(
    "/Applications/Aseprite.app/Contents/MacOS/aseprite"
    "$HOME/Applications/Aseprite.app/Contents/MacOS/aseprite"
    "$HOME/Library/Application Support/Steam/steamapps/common/Aseprite/Aseprite.app/Contents/MacOS/aseprite"
  )
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]]; then
      aseprite_bin="$candidate"
      break
    fi
  done
fi

if [[ -z "$aseprite_bin" || ! -x "$aseprite_bin" ]]; then
  echo "Aseprite CLI not found. Set PROJECTC_ASEPRITE_BIN to the executable." >&2
  exit 1
fi
if [[ ! -f "$input_path" ]]; then
  echo "input does not exist: $input_path" >&2
  exit 1
fi

mkdir -p "$(dirname "$output_path")"

"$aseprite_bin" \
  --batch \
  --script-param "source=$(cd "$(dirname "$input_path")" && pwd)/$(basename "$input_path")" \
  --script-param "output=$(cd "$(dirname "$output_path")" && pwd)/$(basename "$output_path")" \
  --script-param "palette=$palette_path" \
  --script-param "width=$expected_width" \
  --script-param "height=$expected_height" \
  --script-param "resize=$resize_mode" \
  --script-param "alpha_cutoff=80" \
  --script "$lua_script"
