# Proxima

Proxima is a local-first desktop financial analytics application for private investors and financial consultants.

## Stack

- .NET 10 LTS
- C#
- Avalonia UI
- Clean Architecture + MVVM
- PostgreSQL with EF Core/Npgsql planned for the persistence module

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
  Proxima.Sync/
tests/
  Proxima.Domain.Tests/
  Proxima.Application.Tests/
  Proxima.Infrastructure.Tests/
  Proxima.Analytics.Tests/
  Proxima.Importing.Tests/
```

## Prerequisites

- .NET SDK 10.0.104 or compatible .NET 10 SDK
- PostgreSQL 16+ for later persistence modules

## Build and Test

```bash
dotnet restore
dotnet build
dotnet test
```

`Proxima.sln` uses `Proxima.App.Tests` as the solution entrypoint so `dotnet build` and `dotnet test` build the full project graph consistently in restricted local environments. Individual projects can still be opened and run directly from `src/` and `tests/`.

## Run

```bash
dotnet run --project src/Proxima.App/Proxima.App.csproj
```

The app currently supports local first-run setup, portfolio/asset/transaction management, and a CSV/PDF import preview flow with manual fallback routing. Local profile, portfolio, asset, and transaction data are stored in user local application data JSON stores. Passwords are saved only as PBKDF2-SHA256 metadata, salt and hash. PostgreSQL migrations and EF Core/Npgsql persistence are still implemented in later persistence modules.

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
