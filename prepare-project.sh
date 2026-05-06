#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="${1:-.}"
REMOVE_SRC="${REMOVE_SRC:-0}"

PROJECT_ROOT="$(cd "$PROJECT_PATH" && pwd)"
PROJECT_NAME="$(basename "$PROJECT_ROOT")"
PARENT_DIR="$(dirname "$PROJECT_ROOT")"
TIMESTAMP="$(date +"%Y%m%d_%H%M%S")"

ARCHIVE_PATH="$PARENT_DIR/${PROJECT_NAME}-prepared-${TIMESTAMP}.tar.zst"

echo "Project root:  $PROJECT_ROOT"
echo "Archive path:  $ARCHIVE_PATH"
echo "Remove src:    $REMOVE_SRC"
echo

if ! command -v tar >/dev/null 2>&1; then
  echo "Error: tar is not installed."
  echo "Install it with: sudo pacman -S tar"
  exit 1
fi

if ! command -v zstd >/dev/null 2>&1; then
  echo "Error: zstd is not installed."
  echo "Install it with: sudo pacman -S zstd"
  exit 1
fi

EXCLUDES=(
  "--exclude=$PROJECT_NAME/.git"
  "--exclude=$PROJECT_NAME/.vs"
  "--exclude=$PROJECT_NAME/.idea"
  "--exclude=$PROJECT_NAME/.vscode"

  "--exclude=$PROJECT_NAME/bin"
  "--exclude=$PROJECT_NAME/obj"
  "--exclude=$PROJECT_NAME/build"
  "--exclude=$PROJECT_NAME/Build"
  "--exclude=$PROJECT_NAME/dist"
  "--exclude=$PROJECT_NAME/publish"
  "--exclude=$PROJECT_NAME/out"
  "--exclude=$PROJECT_NAME/TestResults"

  "--exclude=$PROJECT_NAME/node_modules"
  "--exclude=$PROJECT_NAME/.nuxt"
  "--exclude=$PROJECT_NAME/.output"
  "--exclude=$PROJECT_NAME/.cache"

  "--exclude=*/bin"
  "--exclude=*/obj"
  "--exclude=*/build"
  "--exclude=*/Build"
  "--exclude=*/dist"
  "--exclude=*/publish"
  "--exclude=*/out"
  "--exclude=*/TestResults"
  "--exclude=*/node_modules"
  "--exclude=*/.nuxt"
  "--exclude=*/.output"
  "--exclude=*/.cache"

  "--exclude=*.user"
  "--exclude=*.suo"
  "--exclude=*.pdb"
  "--exclude=*.cache"
  "--exclude=*.log"
  "--exclude=*.tmp"
  "--exclude=*.nupkg"
  "--exclude=*.snupkg"
  "--exclude=appsettings.Development.json"
)

if [[ "$REMOVE_SRC" == "1" ]]; then
  EXCLUDES+=("--exclude=$PROJECT_NAME/src")
fi

echo "Creating .tar.zst archive..."

tar \
  --zstd \
  -cf "$ARCHIVE_PATH" \
  -C "$PARENT_DIR" \
  "${EXCLUDES[@]}" \
  "$PROJECT_NAME"

echo
echo "Done."
echo "Prepared archive:"
echo "$ARCHIVE_PATH"
