# Proxima Architecture — DDD + MVVM + Services

## Target structure

Proxima is split into strict layers. Dependencies must point inward only:

```text
Proxima.App          -> Proxima.Core.Application, Proxima.Infrastructure, Proxima.Core.Application.Analytics, Proxima.Core.Application.Importing, Proxima.Core.Application.Reporting
Proxima.Infrastructure -> Proxima.Core.Application, Proxima.Core.Domain
Proxima.Core.Application -> Proxima.Core.Domain
Proxima.Core.Domain      -> no project dependencies
```

## Domain layer

`src/Proxima.Core.Domain` contains domain records, enums and business terms only. It must not know about Avalonia, EF Core, PostgreSQL, HTTP clients, PDF libraries, file systems or UI commands.

Current domain modules:

- `Auth`
- `Portfolios`
- `Assets`
- `Transactions`
- `Goals`
- `Notifications`

## Application layer

`src/Proxima.Core.Application` contains use-case services, repository ports, request/result contracts and orchestration logic. This layer owns business workflows, not infrastructure details.

Rules:

- interfaces for repositories and providers live here;
- services consume interfaces, not concrete PostgreSQL/HTTP implementations;
- DTO/request/result contracts live near their use case;
- no EF Core, Npgsql or Avalonia references.

## Infrastructure layer

`src/Proxima.Infrastructure` contains adapters for external technology:

- PostgreSQL persistence via `ProximaDbContext`;
- EF Core migrations in `Persistence/Migrations`;
- repository implementations in `Persistence/Repositories`;
- external market-data/currency providers;
- password hashing and technical security helpers.

Database startup is migration-based. `DatabaseBootstrapService` calls `Database.MigrateAsync()` and applies optional demo seed through EF entities. Raw SQL bootstrap scripts are removed.

## App layer: MVVM

`src/Proxima.App` is the Avalonia composition root and UI layer.

Rules:

- `Views/*/*.axaml` contains layout only;
- `Views/*/*ViewModel.cs` contains presentation state and commands;
- ViewModels call Application services and never access `DbContext` directly;
- infrastructure registrations are allowed only in composition/root wiring;
- shared UI primitives live in `DesignSystem`, `Controls`, `Shell`, `Navigation`, `Notifications`.

## Service modules

Specialized modules remain independent service assemblies:

- `Proxima.Core.Application.Analytics` — financial calculations and read-model shaping;
- `Proxima.Core.Application.Importing` — import parsing/preview logic;
- `Proxima.Core.Application.Reporting` — PDF/report generation.

These modules must not own persistence schema. Persistent writes go through Application ports and Infrastructure repositories.

## Removed legacy sync module

The legacy `Proxima.Sync` project was removed from the solution. Snapshot export/import UI and sync settings were deleted. Database cleanup migrations remove legacy sync tables and settings columns:

- `removed legacy table`;
- `removed legacy setting`;
- `removed legacy timestamp`.

## Removed unused persistence tables

The active PostgreSQL schema is now limited to tables that are mapped by the current EF model. Cleanup migrations drop unused empty legacy tables:

- `removed legacy tax table`;
- `removed legacy tax table`;
- `removed legacy import table`;
- `removed legacy import table`.

Import and tax flows now use runtime services/read models instead of owning dead persistence tables.

## EF Core commands

From repository root:

```bash
dotnet restore
dotnet ef database update \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj
```

To create the next migration after changing `ProximaDbContext`:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Proxima.Infrastructure/Proxima.Infrastructure.csproj \
  --startup-project src/Proxima.App/Proxima.App.csproj \
  --output-dir Persistence/Migrations
```
