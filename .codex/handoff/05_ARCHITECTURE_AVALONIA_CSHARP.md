# Architecture — Avalonia UI + C# + Clean Architecture

## Target Runtime

Use the currently supported **.NET LTS** available in the environment. Prefer .NET 10 LTS if installed; otherwise .NET 8 LTS is acceptable.

Project must enable:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

## Architectural Style

Use:

- Clean Architecture;
- MVVM;
- DDD-inspired domain model;
- CQRS-like use cases where helpful;
- Repository + Unit of Work over EF Core;
- Strategy pattern for quote/tax/import providers;
- Factory pattern for parsers/reports;
- Adapter pattern for external APIs;
- Specification pattern for query filters where useful;
- Observer/event pattern for domain/application events.

## Solution Structure

```text
src/
  Proxima.App/
    Views/
    ViewModels/
    Controls/
    Styles/
    Converters/
    Behaviors/
    Services/
    Assets/
  Proxima.Domain/
    Entities/
    ValueObjects/
    Enums/
    Events/
    Services/
    Exceptions/
  Proxima.Application/
    Abstractions/
    UseCases/
    DTOs/
    Validation/
    Mapping/
  Proxima.Infrastructure/
    Persistence/
    Security/
    Quotes/
    Tax/
    Files/
    Sync/
    Logging/
  Proxima.Analytics/
    Portfolio/
    Risk/
    TechnicalIndicators/
    Forecasting/
  Proxima.Importing/
    Csv/
    Pdf/
    BrokerProfiles/
    Validation/
  Proxima.Reporting/
    Pdf/
    Templates/
    Snapshots/
    GoogleDrive/
```

## Dependency Rule

```text
App -> Application -> Domain
App -> Infrastructure through DI abstractions
Infrastructure -> Application + Domain
Analytics -> Domain/Application abstractions
Importing -> Application/Domain abstractions
Reporting -> Application/Domain abstractions
```

Domain must not reference Avalonia, EF Core, Npgsql, HTTP clients, file system, logging frameworks, or UI-specific types.

## MVVM Rules

- Views: AXAML only + minimal code-behind for UI-specific events when absolutely necessary.
- ViewModels: state, commands, validation, navigation intents.
- Use cases/services: business operations.
- No SQL in ViewModels.
- No HTTP in ViewModels.
- No financial calculation in Views.

Recommended packages:

- `CommunityToolkit.Mvvm` for observable properties and commands.
- `Microsoft.Extensions.DependencyInjection`.
- `Microsoft.Extensions.Hosting`.
- `Microsoft.Extensions.Logging`.
- `Serilog` optional.
- `FluentValidation`.
- `Npgsql.EntityFrameworkCore.PostgreSQL`.
- `LiveChartsCore.SkiaSharpView.Avalonia` or equivalent for charts.
- `Avalonia.Headless` for UI tests where feasible.

## Navigation

Create an application-level navigation service:

```csharp
public interface INavigationService
{
    Task NavigateToAsync<TViewModel>(object? parameter = null);
    Task GoBackAsync();
}
```

Use route names:

- Login
- Dashboard
- Assets
- AssetDetails
- Goals
- Taxes
- Settings
- ManualImport

## Domain Model — Draft

Entities:

- UserProfile
- Portfolio
- Asset
- Transaction
- AssetPrice
- Goal
- TaxProfile
- TaxReport
- ImportSession
- ImportRow
- SyncSnapshot
- AuditLogEntry

Value Objects:

- Money
- Currency
- Percentage
- Ticker
- DateRange
- Quantity
- EncryptedString
- PortfolioId
- AssetId

Enums:

- AssetType
- TransactionType
- CurrencyCode
- UserRole
- LegalEntityType
- ImportStatus
- TaxReportStatus
- QuoteProviderType

## Service Boundaries

### Application Services

- PortfolioService
- AssetService
- TransactionService
- DashboardQueryService
- GoalForecastService
- TaxCalculationService
- ImportOrchestrator
- QuoteUpdateService
- SyncService
- AuthenticationService

### Domain Services

- PortfolioValuationService
- AverageCostCalculator
- PnlCalculator
- RiskMetricsCalculator
- CompoundInterestCalculator
- AllocationCalculator

### Infrastructure Services

- PostgresDbContext
- NbrbExchangeRateProvider
- MockQuoteProvider
- ExternalQuoteProvider
- PasswordHasher
- DataEncryptor
- FilePickerService
- PdfReportGenerator
- EncryptedSnapshotStore

## Error Handling

Use typed result objects for recoverable application errors:

```csharp
public sealed record Result<T>(bool IsSuccess, T? Value, Error? Error);
```

Exceptions are reserved for unexpected failures.

## Logging

- Use structured logging.
- Never log passwords, tokens, raw imported broker files, exact portfolio holdings unless explicitly redacted.
- Include correlation/import session IDs.

## Background Tasks

Use hosted services or application-level async services for:

- quote refresh;
- import parsing;
- snapshot sync;
- report generation.

Long operations must show progress and cancellation where practical.

## Configuration

Use strongly typed options:

- DatabaseOptions
- SecurityOptions
- QuoteOptions
- SyncOptions
- TaxOptions
- UiOptions

Do not commit real API keys.
