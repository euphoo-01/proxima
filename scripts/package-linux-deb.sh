#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0}"
ARCH="${2:-amd64}"
APP_NAME="proxima"
PUBLISH_DIR="${ROOT_DIR}/artifacts/publish/linux-x64"
PKG_ROOT="${ROOT_DIR}/artifacts/packages/deb/${APP_NAME}_${VERSION}_${ARCH}"
OUT_DIR="${ROOT_DIR}/artifacts/packages"
OUT_FILE="${OUT_DIR}/${APP_NAME}_${VERSION}_${ARCH}.deb"

if ! command -v dpkg-deb >/dev/null 2>&1; then
  echo "dpkg-deb is required. Install it and retry."
  exit 1
fi

"${ROOT_DIR}/scripts/publish-linux-x64.sh"

rm -rf "${PKG_ROOT}"
mkdir -p "${PKG_ROOT}/DEBIAN" "${PKG_ROOT}/opt/Proxima" "${PKG_ROOT}/usr/share/applications" "${PKG_ROOT}/usr/bin" "${OUT_DIR}"

cp -R "${PUBLISH_DIR}/." "${PKG_ROOT}/opt/Proxima/"

cat > "${PKG_ROOT}/DEBIAN/control" <<EOF
Package: ${APP_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: Proxima Team
Description: Proxima local-first desktop financial analytics app
EOF

cat > "${PKG_ROOT}/usr/share/applications/proxima.desktop" <<'EOF'
[Desktop Entry]
Name=Proxima
Comment=Local-first financial analytics
Exec=/opt/Proxima/Proxima.App
Icon=proxima
Terminal=false
Type=Application
Categories=Office;Finance;
EOF

cat > "${PKG_ROOT}/usr/bin/proxima" <<'EOF'
#!/usr/bin/env bash
exec /opt/Proxima/Proxima.App "$@"
EOF
chmod +x "${PKG_ROOT}/usr/bin/proxima"

dpkg-deb --build "${PKG_ROOT}" "${OUT_FILE}"
echo "Built Debian package: ${OUT_FILE}"
