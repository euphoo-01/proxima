#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${ROOT_DIR}/artifacts/publish/linux-x64"

dotnet publish "${ROOT_DIR}/src/Proxima.App/Proxima.App.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o "${OUT_DIR}"

echo "Published Linux build to: ${OUT_DIR}"
