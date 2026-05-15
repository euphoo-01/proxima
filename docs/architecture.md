# Proxima architecture

## Final project boundaries

```text
src/Proxima.App
src/Proxima.Core
src/Proxima.Infrastructure
tests/Proxima.Tests
```

## Dependency rules

```text
Proxima.App            -> Proxima.Core, Proxima.Infrastructure
Proxima.Infrastructure -> Proxima.Core
Proxima.Core           -> no project dependencies
Proxima.Tests          -> Proxima.Core, Proxima.Infrastructure, Proxima.App
```

`Proxima.Core` is the independent business/application core. It must not reference Avalonia, EF Core, PostgreSQL/Npgsql, PDFSharp, concrete HTTP clients, file pickers or OS-specific adapters.

`Proxima.Infrastructure` implements Core ports: repositories, Unit of Work, EF Core DbContext, migrations, Twelve Data, NBRB, Belarusbank, PBKDF2 hashing, CSV/PDF parsing and PDFSharp exporting.

`Proxima.App` is the Avalonia presentation layer: Views, ViewModels, Controls, Converters, Styles, Navigation, Dialogs, Notifications and composition root.

## MVVM location

All ViewModels live in `src/Proxima.App/Views/**` or `src/Proxima.App/ViewModels/**`. They depend on Core application services and UI abstractions. They must not depend directly on EF repositories, DbContext or provider clients.

## Business logic location

Business logic lives in `src/Proxima.Core`:

```text
Domain/                 # entities, value objects, enums
Application/            # services, ports, DTOs, results
Application/Analytics/  # portfolio and asset calculations
Application/Importing/  # import contracts and preview models
Application/Reporting/  # report contracts and report DTOs
Application/Taxes/      # tax calculations and rule sets
```

## Technical adapter location

Technical implementation code lives in `src/Proxima.Infrastructure`:

```text
Persistence/            # DbContext, repositories, UoW, migrations
Auth/                   # PBKDF2 password hasher
Importing/              # CSV/PDF parser implementations
Reporting/              # PDFSharp exporters
Quotes/MarketData/Taxes # external providers
```

## Removed module decision

Cross-device synchronization is not part of the active product scope. The app remains local-first; backup/export must be implemented as explicit user-controlled flows, not implicit data replication.
