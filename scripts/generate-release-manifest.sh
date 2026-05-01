#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="${ROOT_DIR}/artifacts/packages"
MANIFEST_DIR="${ROOT_DIR}/artifacts/release"
MANIFEST_PATH="${MANIFEST_DIR}/manifest.txt"

mkdir -p "${MANIFEST_DIR}"

if [[ ! -d "${ARTIFACTS_DIR}" ]]; then
  echo "Artifacts directory not found: ${ARTIFACTS_DIR}"
  exit 1
fi

TMP_FILE="$(mktemp)"
trap 'rm -f "${TMP_FILE}"' EXIT

find "${ARTIFACTS_DIR}" -type f \
  \( -name "*.tar.gz" -o -name "*.zip" -o -name "*.deb" -o -name "*.rpm" -o -name "*.msix" \) \
  | sort > "${TMP_FILE}"

if [[ ! -s "${TMP_FILE}" ]]; then
  echo "No package artifacts found under ${ARTIFACTS_DIR}"
  exit 1
fi

{
  echo "# Proxima Release Manifest"
  echo "# Generated: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "# Format: <sha256> <size_bytes> <relative_path>"
  echo
  while IFS= read -r file; do
    sha="$(sha256sum "${file}" | awk '{print $1}')"
    size="$(stat -c%s "${file}")"
    rel="${file#${ROOT_DIR}/}"
    echo "${sha} ${size} ${rel}"
  done < "${TMP_FILE}"
} > "${MANIFEST_PATH}"

echo "Release manifest written: ${MANIFEST_PATH}"
