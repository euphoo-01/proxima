#!/usr/bin/env bash
set -euo pipefail
ralph_script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "$ralph_script_dir/ralph/ralph.py" "$@"
