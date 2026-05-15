# Proxima

Proxima is a C# Avalonia MVVM desktop application for local-first portfolio tracking, analytics, imports, Belarus tax calculations, reporting and PostgreSQL persistence.

## Solution structure

```text
src/
  Proxima.App/              # Avalonia UI, MVVM, navigation, dialogs, composition root
  Proxima.Core/             # Domain model, use cases, business services, DTOs, ports
  Proxima.Infrastructure/   # EF Core/PostgreSQL, migrations, providers, import/PDF adapters

tests/
  Proxima.Tests/            # Architecture, persistence metadata and behavior checks
```

Runtime dependency graph:

```text
Proxima.App            -> Proxima.Core, Proxima.Infrastructure
Proxima.Infrastructure -> Proxima.Core
Proxima.Core           -> no project dependencies
Proxima.Tests          -> Proxima.Core, Proxima.Infrastructure, Proxima.App
```

## Layer rules

`Proxima.App` contains Avalonia views, ViewModels, commands, navigation, dialogs, notifications and UI composition. ViewModels call Core application services/use cases; they must not depend directly on `DbContext` or infrastructure repositories.

`Proxima.Core` contains domain entities/value objects/enums, application services, analytics, tax calculation, import/report contracts, DTOs, results and ports. It must not reference Avalonia, EF Core, PostgreSQL/Npgsql, PDFSharp, file pickers, HTTP implementation details or operating-system APIs.

`Proxima.Infrastructure` contains EF Core persistence, migrations, repositories, Unit of Work, Twelve Data/NBRB/Belarusbank providers, PBKDF2 password hashing, CSV/PDF parser implementations, PDFSharp exporters and other technical adapters.

## Persistence

PostgreSQL is the only runtime persistence provider. Schema management is done by EF Core migrations under:

```text
src/Proxima.Infrastructure/Persistence/Migrations/
```

Application startup must migrate the database through `Database.MigrateAsync`; raw SQL bootstrap scripts are not used.

Set a connection string before running the app or EF tooling when the default local PostgreSQL container is not used:

```bash
export PROXIMA_POSTGRES="Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima"
```

Create a migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj \
  --output-dir Persistence/Migrations
```

Apply migrations:

```bash
dotnet ef database update \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj
```

## Password credentials

User credentials are stored in one self-contained `users.password_hash text not null` column. The stored value uses the encoded PBKDF2-SHA256 format:

```text
$pbkdf2-sha256$v=1$i=210000$<base64-salt>$<base64-derived-hash>
```

The salt, work factor and format version are encoded inside the string. Passwords must not be stored as plain SHA hashes.

## Tests

Run:

```bash
dotnet restore
dotnet build Proxima.sln
dotnet test Proxima.sln
```

The consolidated `Proxima.Tests` project verifies layer dependencies, absence of removed project fragments in active code, EF model metadata, encoded password hashing, analytics behavior, import preview behavior and PDF export behavior.

## Architecture decision

The solution uses three runtime projects instead of many narrow runtime assemblies to keep the course project maintainable: UI/MVVM in App, all domain and application logic in Core, and all technical adapters in Infrastructure. This preserves clean boundaries without scattering closely related business modules across separate projects.
