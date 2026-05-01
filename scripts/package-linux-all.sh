#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0}"

echo "[1/3] Building Linux tar.gz package..."
"${ROOT_DIR}/scripts/package-linux-x64.sh" "${VERSION}"

echo "[2/3] Building Linux .deb package..."
"${ROOT_DIR}/scripts/package-linux-deb.sh" "${VERSION}" amd64

echo "[3/3] Building Linux .rpm package..."
"${ROOT_DIR}/scripts/package-linux-rpm.sh" "${VERSION}" 1 x86_64

echo "All Linux packages built under artifacts/packages"
