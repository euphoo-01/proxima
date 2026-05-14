# Proxima

Proxima is a local-first desktop financial analytics application for private investors and financial consultants.

## Stack

- .NET 10 LTS
- C#
- Avalonia UI
- Clean Architecture + MVVM
- PostgreSQL with EF Core/Npgsql migrations

## Cross-Platform Requirement

Proxima is a desktop app that must run on both Linux and Windows.  
All runtime file locations should use platform-safe APIs (for example, `Environment.SpecialFolder` + `Path.Combine`) instead of hardcoded OS-specific absolute paths.

## Repository Layout

```text
src/
  Proxima.App/
  Proxima.Domain/
  Proxima.Application/
  Proxima.Infrastructure/
  Proxima.Analytics/
  Proxima.Importing/
  Proxima.Reporting/
tests/
  Proxima.Domain.Tests/
  Proxima.Application.Tests/
  Proxima.Infrastructure.Tests/
  Proxima.Analytics.Tests/
  Proxima.Importing.Tests/
  Proxima.App.Tests/
```

## Prerequisites

- .NET SDK 10.0.104 or compatible .NET 10 SDK
- PostgreSQL 16+ for application persistence

## Build and Test

```bash
dotnet restore
dotnet build
dotnet test
```

## PostgreSQL Dev Setup

```bash
docker compose up -d
```

Default connection (override via `PROXIMA_DB_CONNECTION`):

`Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima`

EF Core migrations are applied automatically by `DatabaseBootstrapService` on application startup. Architecture rules are documented in `docs/architecture-ddd-mvvm-services.md`. To run migrations manually:

```bash
dotnet ef database update --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj --startup-project src/Proxima.App/Proxima.App.csproj
```

`Proxima.sln` uses `Proxima.App.Tests` as the solution entrypoint so `dotnet build` and `dotnet test` build the full project graph consistently in restricted local environments. Individual projects can still be opened and run directly from `src/` and `tests/`.

## Run

```bash
dotnet run --project src/Proxima.App/Proxima.App.csproj
```

## Publish (Without Docker Runtime)

Self-contained publish scripts:

- Linux: `./scripts/publish-linux-x64.sh`
- Windows (PowerShell): `./scripts/publish-win-x64.ps1`

Both produce standalone outputs in `artifacts/publish/*`.
Docker is optional and is used only as a convenient way to host PostgreSQL locally.

Distributable archives:

- Linux tarball: `./scripts/package-linux-x64.sh 0.1.0`
- Windows zip: `./scripts/package-win-x64.ps1 -Version 0.1.0`

See `docs/deployment.md` for packaging details and current installer status.

Installer baseline:

- Linux DEB: `./scripts/package-linux-deb.sh 0.1.0 amd64`
- Linux RPM: `./scripts/package-linux-rpm.sh 0.1.0 1 x86_64`
- Linux all formats: `./scripts/package-linux-all.sh 0.1.0`
- Windows MSIX (on Windows SDK host): `./scripts/package-win-msix.ps1 -Version 0.1.0.0`

Release gate:

- Preflight checks: `./scripts/release-preflight.sh`
- Artifact manifest + SHA256: `./scripts/generate-release-manifest.sh`

The app currently supports local first-run setup, portfolio/asset/transaction management, and a CSV/PDF import preview flow with manual fallback routing. Local profile, portfolio, asset, transaction, settings, notifications and quote-cache data are persisted in PostgreSQL through EF Core migrations. Passwords are saved only as PBKDF2-SHA256 metadata, salt and hash.

### CSV Demo Import Format

Supported header columns (comma or semicolon separators):

```csv
date,ticker,name,type,quantity,price,currency,fee,broker,tag
2026-01-01,AAPL,Apple,Buy,2,100,USD,1,Broker A,tech
2026-01-05,BND,US Bond ETF,Dividend,0,0,USD,0,Broker B,bonds
```

- `type` supports values from transaction enum: `Buy`, `Sell`, `Dividend`, `Deposit`, `Withdrawal`, `Fee`, `Tax`, `Transfer`, `Split`, `Airdrop`, `StakingReward`.
- Invalid or suspicious rows are flagged in preview before commit.

## Figma

Figma source of truth:

```text
https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0
```

UI implementation modules must inspect the Figma file through MCP before implementing authenticated screens. If MCP is unavailable, the UI must be explicitly documented as approximated from the design-system spec.

Module 01 used the custom `mcp__figma__` server for final inspection and captured structured node metadata for the final Mockup desktop/mobile screens plus shared Wireframe components. The current design system is Figma-derived reusable groundwork; individual production screens are implemented in later module iterations.

## External APIs

- Currency rates: Belarusbank developer API (`docs/api-providers.md`).
- Market quotes: Twelve Data API (`docs/api-providers.md`).

Current settings module includes provider selection and masked Twelve Data API key input for local configuration.
Current reporting module supports portfolio and tax draft PDF export to the local reports directory.
