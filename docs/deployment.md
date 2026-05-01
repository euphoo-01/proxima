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

## Database Requirement

Application runtime does not require Docker, but it still requires a reachable PostgreSQL instance.

For local development you can use:

- existing local PostgreSQL service; or
- Docker Compose (`docker compose up -d`) only as a convenient database host.

Default local connection endpoint:

`Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima`

Override via `PROXIMA_DB_CONNECTION`.
