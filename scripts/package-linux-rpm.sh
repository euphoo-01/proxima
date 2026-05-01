#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0}"
RELEASE="${2:-1}"
ARCH="${3:-x86_64}"
APP_NAME="proxima"
PUBLISH_DIR="${ROOT_DIR}/artifacts/publish/linux-x64"
RPM_ROOT="${ROOT_DIR}/artifacts/packages/rpm"
TOPDIR="${RPM_ROOT}/rpmbuild"
SPEC_PATH="${RPM_ROOT}/${APP_NAME}.spec"
OUT_DIR="${ROOT_DIR}/artifacts/packages"

if ! command -v rpmbuild >/dev/null 2>&1; then
  echo "rpmbuild is required. Install rpm-build package and retry."
  exit 1
fi

"${ROOT_DIR}/scripts/publish-linux-x64.sh"

rm -rf "${TOPDIR}"
mkdir -p "${TOPDIR}/"{BUILD,BUILDROOT,RPMS,SOURCES,SPECS,SRPMS}
mkdir -p "${OUT_DIR}"

SOURCE_DIR="${RPM_ROOT}/${APP_NAME}-${VERSION}"
rm -rf "${SOURCE_DIR}"
mkdir -p "${SOURCE_DIR}/opt/Proxima" "${SOURCE_DIR}/usr/bin" "${SOURCE_DIR}/usr/share/applications"
cp -R "${PUBLISH_DIR}/." "${SOURCE_DIR}/opt/Proxima/"

cat > "${SOURCE_DIR}/usr/bin/proxima" <<'EOF'
#!/usr/bin/env bash
exec /opt/Proxima/Proxima.App "$@"
EOF
chmod +x "${SOURCE_DIR}/usr/bin/proxima"

cat > "${SOURCE_DIR}/usr/share/applications/proxima.desktop" <<'EOF'
[Desktop Entry]
Name=Proxima
Comment=Local-first financial analytics
Exec=/opt/Proxima/Proxima.App
Icon=proxima
Terminal=false
Type=Application
Categories=Office;Finance;
EOF

tar -C "${RPM_ROOT}" -czf "${TOPDIR}/SOURCES/${APP_NAME}-${VERSION}.tar.gz" "${APP_NAME}-${VERSION}"

cat > "${SPEC_PATH}" <<EOF
Name:           ${APP_NAME}
Version:        ${VERSION}
Release:        ${RELEASE}%{?dist}
Summary:        Proxima local-first desktop financial analytics app
License:        Proprietary
BuildArch:      ${ARCH}
Source0:        %{name}-%{version}.tar.gz

%description
Proxima is a local-first desktop financial analytics application.

%prep
%setup -q

%build
# no build step, binaries are pre-published

%install
mkdir -p %{buildroot}/opt/Proxima %{buildroot}/usr/bin %{buildroot}/usr/share/applications
cp -a opt/Proxima/. %{buildroot}/opt/Proxima/
cp -a usr/bin/proxima %{buildroot}/usr/bin/proxima
cp -a usr/share/applications/proxima.desktop %{buildroot}/usr/share/applications/proxima.desktop

%files
/opt/Proxima
/usr/bin/proxima
/usr/share/applications/proxima.desktop

%changelog
* Thu May 01 2026 Proxima Team - ${VERSION}-${RELEASE}
- Initial RPM packaging baseline
EOF

rpmbuild --define "_topdir ${TOPDIR}" -ba "${SPEC_PATH}"

RPM_OUT="$(find "${TOPDIR}/RPMS" -type f -name "*.rpm" | head -n 1)"
if [[ -z "${RPM_OUT}" ]]; then
  echo "RPM build succeeded but artifact not found."
  exit 1
fi

cp "${RPM_OUT}" "${OUT_DIR}/"
echo "Built RPM package: ${OUT_DIR}/$(basename "${RPM_OUT}")"
