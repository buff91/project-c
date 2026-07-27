#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/../.." && pwd)"
template="$script_dir/launchd/com.project-c.art-review.plist.template"
launch_agents_dir="$HOME/Library/LaunchAgents"
destination="$launch_agents_dir/com.project-c.art-review.plist"
domain="gui/$(id -u)"

usage() {
  echo "usage: $0 install|uninstall|status" >&2
}

case "${1:-}" in
  install)
    mkdir -p "$launch_agents_dir" "$script_dir/.art-review"
    PROJECTC_PROJECT_ROOT="$project_root" /usr/bin/python3 -c '
import os
import pathlib
import sys

template = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
pathlib.Path(sys.argv[2]).write_text(
    template.replace("__PROJECT_ROOT__", os.environ["PROJECTC_PROJECT_ROOT"]),
    encoding="utf-8",
)
' "$template" "$destination"
    launchctl bootout "$domain/com.project-c.art-review" 2>/dev/null || true
    for _attempt in {1..20}; do
      if ! launchctl print "$domain/com.project-c.art-review" \
        >/dev/null 2>&1; then
        break
      fi
      sleep 0.25
    done
    loaded=false
    for _attempt in {1..5}; do
      if launchctl bootstrap "$domain" "$destination"; then
        loaded=true
        break
      fi
      sleep 1
    done
    if [[ "$loaded" != true ]]; then
      echo "failed to bootstrap: $destination" >&2
      exit 1
    fi
    launchctl enable "$domain/com.project-c.art-review"
    echo "installed: $destination"
    ;;
  uninstall)
    launchctl bootout "$domain/com.project-c.art-review" 2>/dev/null || true
    if [[ -f "$destination" ]]; then
      mv "$destination" "$destination.disabled"
      echo "disabled: $destination.disabled"
    fi
    ;;
  status)
    launchctl print "$domain/com.project-c.art-review"
    ;;
  *)
    usage
    exit 2
    ;;
esac
