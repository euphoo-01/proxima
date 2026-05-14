# EF Migrations + DDD/MVVM Refactor Summary

Date: 2026-05-15

## Completed

- Removed legacy raw SQL bootstrap scripts from `scripts/sql`.
- Replaced script-based database startup with EF Core migrations:
  - `DatabaseBootstrapService` now calls `Database.MigrateAsync()`.
  - Added design-time `ProximaDbContextFactory` for `dotnet ef`.
  - Added EF migration files under `src/Proxima.Infrastructure/Persistence/Migrations`.
- Removed legacy `Proxima.Sync` module:
  - deleted `src/Proxima.Sync`;
  - removed project from `Proxima.sln`;
  - removed project references from App and App tests;
  - removed sync/snapshot settings UI and ViewModel commands;
  - removed sync settings fields from Application settings contracts and persistence mapping.
- Cleaned active PostgreSQL schema:
  - removed EF model mappings for dead tables;
  - added cleanup migration for unused/sync tables;
  - added cleanup of obsolete settings columns.
- Preserved and clarified project architecture:
  - Domain remains technology-free;
  - Application owns use-case services and ports;
  - Infrastructure owns EF/PostgreSQL repositories and external adapters;
  - App remains Avalonia MVVM and composition root;
  - documented rules in `docs/architecture-ddd-mvvm-services.md`.
- Updated README, persistence docs, architecture docs and relevant tests.

## Active EF tables

- `users`
- `portfolios`
- `assets`
- `tags`
- `asset_tags`
- `transactions`
- `asset_prices`
- `goals`
- `quote_cache`
- `user_settings`
- `notifications`
- `audit_log`

## Legacy tables dropped by migration

- `sync_snapshots`
- `tax_profiles`
- `tax_reports`
- `import_sessions`
- `import_rows`

## Legacy columns dropped by migration

- `user_settings.sync_enabled`
- `user_settings.last_snapshot_at`
- `user_settings.finnhub_api_key_protected`

## Manual validation commands

```bash
dotnet restore
dotnet build Proxima.sln
dotnet test Proxima.sln
dotnet ef database update \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj
```

## Note

The editing environment used for this refactor does not contain the .NET SDK, so `dotnet build`, `dotnet test` and `dotnet ef` could not be executed here. Static checks were performed for stale project references, removed SQL script directory, old sync references and changed settings constructor signatures.
