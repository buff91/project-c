#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "[deprecated] promote_b2_prop_quality_v1.sh -> promote_b2_prop_quality_v2.sh" >&2
exec "$script_dir/promote_b2_prop_quality_v2.sh"
