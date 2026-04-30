# Engineering Standards

## General

- Code must be readable, testable, maintainable.
- Prefer simple design over clever abstractions.
- Avoid premature generalization.
- Apply SOLID pragmatically.
- Use DRY, but not at the cost of clarity.
- Use KISS and YAGNI.

## C# Standards

- Nullable reference types enabled.
- Async suffix for async methods.
- Use `decimal` for money.
- Use `DateOnly`/`TimeOnly` where appropriate.
- Use `DateTimeOffset` for timestamps.
- Avoid static mutable state.
- Avoid service locator.
- Avoid `dynamic`.
- Avoid swallowing exceptions.
- Use cancellation tokens for IO/long tasks.
- Use guard clauses.

## Formatting

- `.editorconfig`.
- `dotnet format`.
- Analyzers enabled.
- Warnings as errors where practical.

## Testing

Minimum tests:

- portfolio valuation;
- average cost;
- P&L;
- ROI;
- max drawdown;
- compound interest forecast;
- CSV import parser;
- password hash verify;
- repository integration test if Testcontainers available.

Preferred stack:

- xUnit or NUnit;
- FluentAssertions;
- Testcontainers.PostgreSql;
- Avalonia.Headless for UI tests.

## UI Engineering

- Reusable controls.
- Styles in XAML resources.
- No copy-paste styling across screens.
- Use binding and commands.
- ViewModels should be unit-testable.
- UI errors should be visible.
- Loading states for async work.
- Empty states for empty tables.

## Data Engineering

- EF migrations.
- Strong entity configuration.
- No raw SQL unless justified.
- Transactions for batch imports.
- Indexes for dashboard queries.
- Decimal precision configured explicitly.
- No cascade delete surprises for critical user data.
- Cross-platform filesystem handling only (`Path.Combine`, `Environment.SpecialFolder`, no OS-specific absolute path hardcoding).

## API Engineering

- Typed clients.
- Retry/backoff for transient quote API errors.
- Cache external data.
- Do not leak portfolio data to providers.
- Provider abstractions for testability.

## Security Engineering

- Secrets out of source.
- Redacted logs.
- Local password hash.
- App-level encryption for sensitive fields/snapshots.
- Validate imported files.
- Principle of least privilege.

## Documentation

README must include:

- prerequisites;
- setup;
- PostgreSQL start;
- migrations;
- run;
- tests;
- demo credentials;
- limitations;
- security notes.

## Definition of Production-Oriented MVP

It does not mean every possible feature is complete. It means:

- clear architecture;
- working core flows;
- safe failure behavior;
- tests for core logic;
- no hardcoded secrets;
- no prototype-only hacks blocking extension;
- clear next steps.
