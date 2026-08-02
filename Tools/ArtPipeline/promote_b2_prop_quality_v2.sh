#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/../.." && pwd)"
environment_root="$project_root/Assets/_Project/Art/Environment"
runtime_root="$project_root/Assets/_Project/Art/Runtime"
source_root="$project_root/Assets/_Project/Art/Source/Aseprite"
wall_candidate_root="/private/tmp/project-c-b2-v4"
floor_candidate_root="/private/tmp/project-c-b2-floor-v3"

python3 "$script_dir/process_b2_prop_quality_v4.py"
python3 "$script_dir/process_b2_parking_dressing_v3.py"

wall_assets=(
  env-wall-rising-right
  env-wall-rising-left
  env-wall-torch-rising-right
  env-wall-torch-rising-left
  env-wall-pipes-rising-right
  env-wall-pipes-rising-left
  env-wall-window-rising-right
  env-wall-window-rising-left
  env-wall-cabinet-rising-right
  env-wall-cabinet-rising-left
  env-wall-b2-service-segment-0-rising-right
  env-wall-b2-service-segment-0-rising-left
  env-wall-b2-service-segment-1-rising-right
  env-wall-b2-service-segment-1-rising-left
  env-wall-b2-service-segment-2-rising-right
  env-wall-b2-service-segment-2-rising-left
)

for asset_name in "${wall_assets[@]}"; do
  /bin/cp "$wall_candidate_root/$asset_name.png" "$environment_root/$asset_name.png"
  "$script_dir/aseprite_conform.sh" \
    "$environment_root/$asset_name.png" \
    "$source_root/$asset_name.aseprite" \
    64 112 strict
done

/bin/cp \
  "$wall_candidate_root/prop-explosive-barrel.png" \
  "$runtime_root/prop-explosive-barrel.png"
"$script_dir/aseprite_conform.sh" \
  "$runtime_root/prop-explosive-barrel.png" \
  "$source_root/prop-explosive-barrel.aseprite" \
  128 128 strict

for prop_name in env-floor-b2-parking-stop env-floor-b2-fallen-sign; do
  for view in 0 1 2 3; do
    asset_name="$prop_name-view-$view"
    /bin/cp "$floor_candidate_root/$asset_name.png" "$environment_root/$asset_name.png"
    "$script_dir/aseprite_conform.sh" \
      "$environment_root/$asset_name.png" \
      "$source_root/$asset_name.aseprite" \
      128 64 strict
  done
  /bin/cp \
    "$floor_candidate_root/$prop_name-view-0.png" \
    "$environment_root/$prop_name.png"
done

/bin/cp \
  "$wall_candidate_root/b2-prop-quality-v4-assets.png" \
  "$project_root/docs/captures/b2-prop-quality-conform-preview-v2.png"
/bin/cp \
  "$floor_candidate_root/b2-floor-v3-comparison.png" \
  "$project_root/docs/captures/b2-right-dressing-conform-preview-v3.png"

echo "promoted ImageGen-backed B2 prop-quality v2 slice to native Aseprite sources"
