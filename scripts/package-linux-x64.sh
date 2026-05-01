#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="${ROOT_DIR}/artifacts/publish/linux-x64"
PACKAGE_DIR="${ROOT_DIR}/artifacts/packages"
VERSION="${1:-0.1.0}"
PACKAGE_NAME="proxima-linux-x64-v${VERSION}.tar.gz"
PACKAGE_PATH="${PACKAGE_DIR}/${PACKAGE_NAME}"

"${ROOT_DIR}/scripts/publish-linux-x64.sh"

mkdir -p "${PACKAGE_DIR}"
tar -C "${PUBLISH_DIR}" -czf "${PACKAGE_PATH}" .

echo "Packaged Linux artifact: ${PACKAGE_PATH}"
