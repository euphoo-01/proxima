# Deployment Notes (Cross-Platform)

Proxima is a cross-platform desktop app and should run on Linux and Windows.

## Build Self-Contained Package (No Docker Runtime Required)

Linux x64:

```bash
./scripts/publish-linux-x64.sh
```

Windows x64 (from PowerShell):

```powershell
./scripts/publish-win-x64.ps1
```

Published outputs:

- `artifacts/publish/linux-x64`
- `artifacts/publish/win-x64`

## Build Distributable Archives

Linux tarball:

```bash
./scripts/package-linux-x64.sh 0.1.0
```

Windows zip (PowerShell):

```powershell
./scripts/package-win-x64.ps1 -Version 0.1.0
```

Package outputs:

- `artifacts/packages/proxima-linux-x64-v<version>.tar.gz`
- `artifacts/packages/proxima-win-x64-v<version>.zip`

## Native Installer Baseline

Debian package:

```bash
./scripts/package-linux-deb.sh 0.1.0 amd64
```

Output:

- `artifacts/packages/proxima_0.1.0_amd64.deb`

Windows MSIX package (run on Windows with Windows SDK installed):

```powershell
./scripts/package-win-msix.ps1 -Version 0.1.0.0 -Publisher "CN=ProximaDev"
```

Output:

- `artifacts/packages/msix/Proxima_0.1.0.0_x64.msix`

Notes:

- MSIX script builds an unsigned package. Code signing should be done in CI/release pipeline.
- Windows SDK tool `makeappx.exe` is required for MSIX packaging.

## Database Requirement

Application runtime does not require Docker, but it still requires a reachable PostgreSQL instance.

For local development you can use:

- existing local PostgreSQL service; or
- Docker Compose (`docker compose up -d`) only as a convenient database host.

Default local connection endpoint:

`Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima`

Override via `PROXIMA_DB_CONNECTION`.

## Installer Status

Current baseline provides self-contained binaries, archives, and installer skeletons for DEB/MSIX.
RPM remains a follow-up step.
