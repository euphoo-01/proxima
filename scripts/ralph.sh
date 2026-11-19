#!/usr/bin/env bash
set -euo pipefail
ralph_script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if [[ -x "$ralph_script_dir/../.tools/codex/codex" ]]; then
  export PATH="$ralph_script_dir/../.tools/codex:$PATH"
fi
exec python3 "$ralph_script_dir/ralph/ralph.py" "$@"
