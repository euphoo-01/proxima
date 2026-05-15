# Module 16 Reporting Notes

## Implemented

- `Proxima.Core.Application.Reporting` report abstraction:
  - `IReportService`
  - `SimplePdfReportService`
  - portfolio/tax report request+preview/export DTOs
- Export is independent from Avalonia views and does not rely on UI screenshots.
- PDF files are generated to a safe default directory:
  - `%AppData%/Proxima/reports` (platform-resolved via `Environment.SpecialFolder` + `Path.Combine`).
- Portfolio report includes:
  - portfolio name
  - period
  - total value
  - P&L
  - allocation section
  - top assets
  - risk metrics
  - transaction summary
  - generation timestamp
  - disclaimer
- Tax report includes:
  - user/profile summary
  - year
  - taxable base
  - total tax due
  - exchange/currency notes
  - dividends
  - transaction summary
  - calculation version
  - legal disclaimer

## UI Wiring

- Dashboard: `Экспорт портфеля PDF` button.
- Taxes: `Экспорт PDF` button now generates report via reporting service.
- Success/failure paths show user-visible status text.

## Deferred

- Interactive in-app report preview UI (current preview is service-level sections check).
- File chooser / overwrite confirmation dialog (current flow uses default output directory and timestamped filenames).
