# Database — Local PostgreSQL

## Current status

The app uses PostgreSQL through EF Core/Npgsql migrations. The legacy raw SQL bootstrap scripts were removed.

Development setup:

- `docker-compose.yml` starts PostgreSQL.
- `DatabaseBootstrapService` applies EF migrations with `Database.MigrateAsync()` on startup.
- Optional demo seed is applied through EF entities when `EnableSeed` is enabled.

## Technology

- EF Core
- Npgsql provider
- `src/Proxima.Infrastructure/Persistence/ProximaDbContext.cs`
- migrations under `src/Proxima.Infrastructure/Persistence/Migrations`

## Active schema

The active schema is intentionally limited to mapped tables:

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

## Removed legacy schema

Cleanup migrations drop unused/sync tables when they exist:

- `sync_snapshots`
- `tax_profiles`
- `tax_reports`
- `import_sessions`
- `import_rows`

Cleanup migrations also drop removed settings columns:

- `sync_enabled`
- `last_snapshot_at`
- `finnhub_api_key_protected`

## Manual migration command

```bash
dotnet ef database update \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj
```
