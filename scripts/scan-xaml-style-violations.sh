#!/usr/bin/env bash
set -euo pipefail

TARGET="${1:-src/Proxima.App/Views}"

if [ ! -d "$TARGET" ]; then
  echo "Style guard target does not exist yet: $TARGET"
  echo "Treat as pass only during guardrail setup. Once Views exist, this command must scan them."
  exit 0
fi

echo "Scanning AXAML style violations in $TARGET"
fail=0

check_pattern() {
  local pattern="$1"
  local message="$2"

  if grep -RIn --include="*.axaml" -E "$pattern" "$TARGET"; then
    echo "FAIL: $message"
    fail=1
  fi
}

check_pattern '#[0-9A-Fa-f]{6,8}' 'Raw hex colors are forbidden in production Views.'
check_pattern 'Background="' 'Inline Background is forbidden in production Views.'
check_pattern 'Foreground="' 'Inline Foreground is forbidden in production Views.'
check_pattern 'BorderBrush="' 'Inline BorderBrush is forbidden in production Views.'
check_pattern 'CornerRadius="' 'Inline CornerRadius is forbidden in production Views.'
check_pattern 'FontSize="' 'Inline FontSize is forbidden in production Views.'
check_pattern 'FontWeight="' 'Inline FontWeight is forbidden in production Views.'
check_pattern 'BoxShadow="' 'Inline BoxShadow is forbidden in production Views.'
check_pattern '<UserControl\.Styles>|<Window\.Styles>' 'Page-local reusable styles are forbidden in production Views.'

if [ "$fail" -ne 0 ]; then
  echo "AXAML style guard failed."
  exit 1
fi

echo "AXAML style guard passed."
