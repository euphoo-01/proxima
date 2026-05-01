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

## Database Requirement

Application runtime does not require Docker, but it still requires a reachable PostgreSQL instance.

For local development you can use:

- existing local PostgreSQL service; or
- Docker Compose (`docker compose up -d`) only as a convenient database host.

Default local connection endpoint:

`Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima`

Override via `PROXIMA_DB_CONNECTION`.

## Installer Status

Current baseline provides self-contained binaries and distributable archives.
Native installer formats (e.g., MSIX/DEB/RPM) are tracked as a follow-up release step.
