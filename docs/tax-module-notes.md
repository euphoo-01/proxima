# Module 13 Tax Notes (Belarus Draft)

## Scope

- Added draft tax calculation module for Belarus-oriented reporting flow.
- Tax UI aligned to selected Figma tax screen (`62:498`) using custom `mcp__figma__` server structure.
- Added year/profile inputs, bento metric cards, limits/calendar/disclaimer cards, and export entrypoint button.

## Calculation Assumptions

- Rule set version is embedded (`BY-DRAFT-2026.04`) with effective date metadata.
- Calculation is deterministic for identical inputs.
- Current draft separates:
  - realized gains (sell transactions);
  - dividends;
  - fees/taxes;
  - currency effects;
  - losses;
  - exemption amount.
- Legal profile `Other` is treated as unconfigured and returns a recoverable prompt.

## Exchange Rates

- Exchange rate abstraction: `IExchangeRateProvider`.
- Current provider: `MockNbrbExchangeRateProvider` (explicitly marked mock in UI).
- Rate source/date are surfaced in calculation details.
- Provider failure returns recoverable user-facing error.

## Export

- Tax export action is wired in UI and ViewModel.
- Current implementation shows explicit “not implemented yet” message; PDF generation remains in reporting module scope.

## Legal/Compliance Notes

- Output is explicitly labeled as draft/informational.
- No hardcoded claim that rules are guaranteed to match current law.
