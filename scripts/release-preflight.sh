#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIR}"

echo "[preflight] dotnet build"
dotnet build Proxima.sln -m:1 -nr:false

echo "[preflight] dotnet test"
if ! dotnet test Proxima.sln -m:1 -nr:false; then
  echo "[preflight] dotnet test failed."
  echo "[preflight] In restricted environments this can fail because infrastructure tests require local DB socket access."
  exit 1
fi

echo "[preflight] checking packaging tools"
command -v tar >/dev/null 2>&1 || { echo "tar not found"; exit 1; }
command -v dpkg-deb >/dev/null 2>&1 || { echo "dpkg-deb not found"; exit 1; }
command -v rpmbuild >/dev/null 2>&1 || { echo "rpmbuild not found"; exit 1; }
command -v sha256sum >/dev/null 2>&1 || { echo "sha256sum not found"; exit 1; }

echo "[preflight] OK"
