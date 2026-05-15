# Testing

## Commands

```bash
dotnet restore
dotnet build Proxima.sln
dotnet test Proxima.sln
```

## Test project

All active tests are consolidated into:

```text
tests/Proxima.Tests
```

## Covered checks

`Proxima.Tests` covers:

- architecture dependency rules;
- absence of removed project fragments in active source and project files;
- Core independence from UI, EF, PostgreSQL and PDFSharp;
- EF model metadata for active tables and credential columns;
- PBKDF2 encoded password hashing and verification;
- analytics calculations for value, P&L, ROI, drawdown, volatility and Sharpe;
- CSV import preview behavior;
- PDF report export smoke behavior.

Tests are behavior or architecture checks, not construction-only assertions.
