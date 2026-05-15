# Persistence

## Provider

Proxima uses PostgreSQL through EF Core. Runtime persistence code lives only in `Proxima.Infrastructure`.

## DbContext and migrations

```text
src/Proxima.Infrastructure/Persistence/ProximaDbContext.cs
src/Proxima.Infrastructure/Persistence/ProximaDbContextFactory.cs
src/Proxima.Infrastructure/Persistence/Migrations/
src/Proxima.Infrastructure/Persistence/Repositories/
```

Application startup applies migrations with `Database.MigrateAsync`. Raw bootstrap scripts are not used.

## Expected clean schema

A clean database should contain active application tables plus EF history:

```text
__EFMigrationsHistory
users
portfolios
assets
tags
asset_tags
transactions
asset_prices
goals
quote_cache
user_settings
notifications
audit_log
```

## Password storage

`users` stores credentials in one column:

```sql
password_hash text not null
```

The value is encoded as:

```text
$pbkdf2-sha256$v=1$i=210000$<base64-salt>$<base64-derived-hash>
```

## EF commands

```bash
dotnet ef migrations list \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj

 dotnet ef database update \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj
```

Set `PROXIMA_POSTGRES` when the default local connection string is not suitable.
