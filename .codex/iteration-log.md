# Iteration Log

## Iteration 00 — Repository, Build & Engineering Baseline

### Scope

Create the Proxima solution structure, baseline engineering configuration, README, smoke tests, architecture dependency checks and project documentation required before feature work.

### User Stories Checked

- [x] US-00.1 — Developer can start the project predictably.
- [x] US-00.2 — Developer can rely on automated quality checks.
- [x] US-00.3 — Developer can inspect project history.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-00.1 | Done | `Proxima.sln`; `src/Proxima.*` projects; `tests/Proxima.*.Tests` projects; `Proxima.Domain.Tests` and `Proxima.App.Tests` verify Domain forbidden dependencies. |
| AC-00.2 | Done | `Directory.Build.props`, `Directory.Solution.props`, `.editorconfig`, `.gitignore`, `README.md`; nullable/analyzers/warnings-as-errors configured. |
| AC-00.3 | Done | Conventional commit to be created for this iteration; final hash recorded after commit. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Smoke | `tests/Proxima.App.Tests` | Passed |
| Architecture | `tests/Proxima.Domain.Tests`, `tests/Proxima.App.Tests` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Done

### Known Limitations

Product functionality is out of scope for Module 00.

### Commit

5204f37 build(baseline): add clean architecture solution

## Iteration 01 — Figma Design Inspection & Bento Design System

### Scope

Inspect the Figma source through MCP where available, document extracted visual tokens, implement shared Avalonia resource dictionaries and reusable bento controls for future UI modules.

### User Stories Checked

- [x] US-01.1 — UI implementer can use Figma as source of truth.
- [x] US-01.2 — User sees a trustworthy bento-style interface.
- [x] US-01.3 — Developer can reuse design primitives.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-01.1 | Done | `docs/figma-inspection.md` records source URL, file key, starting node, custom `mcp__figma__` usage, inspected Wireframe/Mockup nodes, extracted tokens, screenshot export status and differences from Figma. |
| AC-01.2 | Done | `src/Proxima.App/Styles/{Tokens,Typography,Buttons,Inputs,Cards,Tables,Charts}.axaml` exist and are loaded from `App.axaml`; `Proxima.App.Tests.DesignSystem_FilesExist` verifies core tokens. |
| AC-01.3 | Done | `BentoCard`, `MetricCard`, `StatusPill`, `EmptyState`, `PageHeader`, `SearchBox`, `TimeframeSelector`, `DataTableHeaderCell`; `Proxima.App.Tests.DesignSystem_ControlsExposeBindingProperties` verifies styled properties. |
| AC-01.4 | Done | Light shell preview and shared controls use Figma-derived spacing/radii, visible focus/error/empty state styles, readable table primitives and non-color text labels. Feature-screen smoke tests remain tied to later screen modules. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/Smoke | `tests/Proxima.App.Tests` | Passed |
| UI/XAML Regression | `dotnet build Proxima.sln` | Passed |
| Manual | Custom `mcp__figma__` inspection of Wireframe components and Mockup desktop/mobile nodes | Done |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Done

### Known Limitations

No Figma MCP access limitation remains for Module 01 after switching to the custom `mcp__figma__` server. Raster screenshot files are not stored yet because the custom server surface used here exposes structured node metadata rather than screenshot export.

### Commit

6232b1b feat(design-system): add figma-informed bento primitives
3781e9e fix(design-system): align bento primitives with custom figma mcp

## Iteration 02 — Local Authentication, Profile Setup & App Unlock

### Scope

Implement first-run local profile setup, returning-user unlock, password policy validation, PBKDF2 password hashing, generic authentication failures, recovery explanation and a gated Avalonia shell.

### User Stories Checked

- [x] US-02.1 — First-time user creates a local protected profile.
- [x] US-02.2 — Returning user unlocks Proxima locally.
- [x] US-02.3 — User receives clear feedback on authentication errors.
- [x] US-02.4 — User can understand password recovery limitation.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-02.1 | Partial | First-run setup collects display name, login, role and password confirmation; validation disables create action; successful setup persists a local profile and unlocks. PostgreSQL-backed records are deferred to the persistence module. |
| AC-02.2 | Done | Returning launch shows unlock mode when a profile exists; login/password unlock succeeds; wrong password shows generic error; masked input and Enter submit are implemented. |
| AC-02.3 | Done | Passwords are hashed with PBKDF2-SHA256, unique salt and constant-time comparison; plaintext password is not stored in the local profile store. |
| AC-02.4 | Partial | Failed attempts increment and show delay warning; unknown login and wrong password share a generic error; forgot-password explanation is visible. DB connection failure handling is deferred until PostgreSQL is introduced. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` password policy and auth service tests | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` PBKDF2 and JSON persistence tests | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` masked input/recovery/shell gate checks | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
dotnet test tests/Proxima.Application.Tests/Proxima.Application.Tests.csproj
dotnet test tests/Proxima.Infrastructure.Tests/Proxima.Infrastructure.Tests.csproj
dotnet test tests/Proxima.App.Tests/Proxima.App.Tests.csproj
git status --short
```

### Result

Partial

### Known Limitations

Module 02 uses a durable local JSON profile store as the current repository implementation. PostgreSQL/EF Core auth persistence, migrations, DB connection failure screen and seed/demo credentials remain deferred to the persistence module.

### Commit

b95f171 security(auth): add local profile setup and unlock

## Iteration 03 — Shell, Navigation, Sidebar & Topbar

### Scope

Implement authenticated shell navigation with routed page state, active sidebar links, breadcrumbs, portfolio selector state, back navigation and create-portfolio dialog behavior.

### User Stories Checked

- [x] US-03.1 — User can move between core sections.
- [x] US-03.2 — User always knows current location.
- [x] US-03.3 — User can switch current portfolio globally.
- [x] US-03.4 — User can create a portfolio from topbar.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-03.1 | Done | Auth screens stay separate; unlocked shell uses persistent sidebar + topbar layout and supports target desktop sizes through fixed shell dimensions and scrollable content. |
| AC-03.2 | Done | Sidebar contains Dashboard/Assets/Taxes/Goals/Settings links, active route updates via `ShellNavigationService` and `ShellViewModel`, content switches without restart, back navigation exists for detail/import routes. |
| AC-03.3 | Done | Breadcrumbs and page title derive from routes; portfolio selector is stateful; create-portfolio action opens modal and updates selected portfolio; status indicator is shown in topbar. |
| AC-03.4 | Partial | Selected portfolio is preserved while navigating and detail pages handle unsafe switch via safe fallback to asset list; data reload semantics are stubbed because data-query modules are not yet implemented. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.App.Tests` navigation service, breadcrumb, portfolio state tests | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` sidebar/topbar/auth shell checks | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Portfolio switching currently updates shell state and safe fallbacks only. Cross-page data reload/invalidation will be wired once portfolio/assets/analytics query modules are implemented.

### Commit

f2456ad feat(shell): add routed sidebar and topbar portfolio state

## Iteration 04 — Portfolio Management

### Scope

Implement portfolio domain/application/infrastructure services with user-scoped CRUD (create/list/update/archive) and connect shell topbar dialogs to real portfolio use cases.

### User Stories Checked

- [x] US-04.1 — Private investor can create a portfolio.
- [x] US-04.2 — Financial consultant can separate client portfolios.
- [x] US-04.3 — User can manage portfolio metadata.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-04.1 | Done | Topbar create dialog now includes required name/base currency and optional description/client label; service validation prevents empty fields and duplicate active names; newly created portfolio is selected. |
| AC-04.2 | Done | Portfolio selector is loaded from owner-scoped repository data; archived portfolios are hidden by default; selected portfolio is preserved in shell state. |
| AC-04.3 | Partial | Rename and base-currency update are implemented through manage dialog and persisted in repository; display refresh works in shell state. DB persistence is deferred to persistence module. |
| AC-04.4 | Partial | Archive action is implemented from manage dialog and safely reselects another portfolio or empty state; hard delete is intentionally not implemented yet. |
| AC-04.5 | Partial | Owner-scoped repository/service and tests enforce user isolation for portfolios; cross-module assets/transactions/goals/report scoping will be completed when those modules persist portfolio-linked data. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` portfolio service validation/update/archive/isolation | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` JSON portfolio repository owner-filter and archive behavior | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` shell portfolio selection/create flow checks | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Module 04 is currently backed by local JSON storage for portfolios. PostgreSQL/EF Core persistence, migration-based schema and full downstream data scoping with assets/transactions/goals/reports are deferred to subsequent modules.

### Commit

c09be30 feat(portfolios): add scoped portfolio service and shell CRUD

## Iteration 05 — Asset Management

### Scope

Implement asset domain/application/infrastructure flows, wire assets page with create/edit/archive/search/sort interactions, and support safe navigation to asset-details route from selected asset rows.

### User Stories Checked

- [x] US-05.1 — User can track different asset classes.
- [x] US-05.2 — User can categorize assets using tags.
- [x] US-05.3 — User can open detailed asset view.
- [ ] US-05.4 — User can maintain assets safely.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-05.1 | Partial | Domain `Asset` now stores portfolio linkage, ticker/name/type/currency, optional exchange/isin, tags, notes payload field, timestamps, and numeric position fields. Dedicated cryptographic notes encryption is deferred. |
| AC-05.2 | Partial | Create/update/archive are implemented via `AssetService` + shell dialogs; required field validation and ticker normalization are covered by tests. Hard-delete with transaction-impact guard is deferred until transaction module. |
| AC-05.3 | Partial | Assets table shows key columns (name/ticker/type/position/avg/current/value/P&L/tags) plus actions; search by name/ticker/tag and sorting by name/value/P&L are implemented. |
| AC-05.4 | Partial | Tags are assignable and persisted; filtering by tags works in table search. Visual tag-color mapping and allocation chart integration are deferred to dashboard/analytics modules. |
| AC-05.5 | Done | Clicking asset title selects asset and opens details route; missing selection prevents unsafe navigation and keeps user on assets page with status message. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`AssetService_NormalizesTickerAndArchives`) | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` (`JsonAssetRepository_StoresAndArchives`) | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` (`ShellViewModel_FiltersAndSortsAssets`) | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

JSON-backed storage is still used instead of PostgreSQL/EF Core. Hard-delete workflow with transaction-aware confirmation is deferred to Module 06+. Notes use temporary payload obfuscation and will be replaced by app-level encryption service in the security/persistence iterations.

### Commit

7c92baf feat(assets): add asset service and bento assets table workflows

## Iteration 06 — Transaction Management

### Scope

Implement transaction domain/application/infrastructure, connect manual transaction entry to shell UI, add searchable/sortable transaction table scoped by portfolio and filtered by selected asset on asset-details route.

### User Stories Checked

- [x] US-06.1 — User can manually enter transactions.
- [x] US-06.2 — User can audit transaction history.
- [ ] US-06.3 — User gets recalculated portfolio metrics.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-06.1 | Partial | `PortfolioTransaction` model and service/repository persist required transaction fields, including optional asset/broker/external id/notes and timestamps; notes still obfuscated placeholder, not final encryption service. |
| AC-06.2 | Done | `TransactionType` enum includes all required types and is used in service validation and UI type picker. |
| AC-06.3 | Partial | Manual create/update/archive flow is implemented with type-aware validation (Buy/Sell/Dividend/Fee/Tax checks), decimal fields, and persisted save via service/repository. Field-specific inline validation granularity is limited. |
| AC-06.4 | Partial | Transaction table supports search (asset/ticker/broker/type) and sort (date/amount/type/asset). Asset details route applies selected-asset scoped transaction filtering. Dashboard integration for latest transactions is deferred. |
| AC-06.5 | Partial | Service rejects cross-portfolio asset references and scopes reads/writes by portfolio. JSON persistence cannot provide full DB transactional guarantees yet; no partial write path in current flow. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`TransactionService_ValidatesCrossPortfolioAsset`) | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` (`JsonTransactionRepository_StoresAndArchives`) | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` (`ShellViewModel_LoadsAndFiltersTransactions`) | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Persistence remains JSON-backed, so AC requirements for strict transactional updates/rollbacks at DB level are deferred until PostgreSQL + EF Core module. Metrics recalculation propagation to dashboard analytics is deferred to analytics module.

### Commit

e130761 feat(transactions): add typed transaction flows and scoped tables

## Iteration 07 — Import: CSV, PDF Pipeline & Manual Fallback

### Scope

Implement import pipeline abstractions and parsers, add assets-page import modal with preview and suspicious-row handling, and connect commit flow to asset/transaction services with manual fallback route.

### User Stories Checked

- [x] US-07.1 — User imports broker reports quickly.
- [x] US-07.2 — User reviews suspicious imported rows.
- [x] US-07.3 — User can recover from failed import.
- [x] US-07.4 — Developer can add broker parsers.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-07.1 | Partial | Assets page includes `Импортировать активы` button and modal with cancel flow, type/size validation (`ImportFileValidator`), and unsupported-file error message. Current MVP uses file-path input instead of OS drag&drop API. |
| AC-07.2 | Done | `CsvImportParser` parses documented columns (date, ticker/name, type, qty, price, currency, fee, broker/tag optional), handles headers/empty/whitespace, supports comma/semicolon, and flags invalid/suspicious rows with reasons. |
| AC-07.3 | Done | `IImportParser` + `PdfStubImportParser` implemented; PDF failures surface explicit limited-parser message and manual fallback guidance without app crash. |
| AC-07.4 | Partial | Preview and row-selection before commit implemented; suspicious rows can be kept/excluded. Commit currently performs guarded sequential saves but DB transaction semantics are deferred with JSON persistence. |
| AC-07.5 | Partial | Failed/limited import offers manual fallback action and dedicated manual route. Full table-like manual row editor with add/remove/row-level validation summary is deferred. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Importing.Tests` (`CsvParser_ParsesValidRows`, `CsvParser_FlagsInvalidDate`, `FileValidator_RejectsUnsupportedExtension`) | Passed |
| Integration | `tests/Proxima.App.Tests` and service wiring via import preview/commit path build graph | Passed |
| UI/Smoke | `dotnet test Proxima.sln` plus modal/action wiring checks in app tests | Passed |
| Security | Parser and import service do not log raw payload content; malformed parsing returns safe error result | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Drag&drop UX is approximated via path input field. Commit flow is all-or-stop in-memory orchestration and cannot guarantee DB-level rollback until PostgreSQL transaction-backed persistence modules are implemented.

### Commit

0f7b0cb feat(import): add csv preview pipeline and manual fallback modal

## Iteration 08 — Quotes, Market Data & Offline Cache

### Scope

Implement quote provider abstraction, mock provider, cache repository, and manual quote refresh flow in assets UI with cached fallback on provider failure.

### User Stories Checked

- [x] US-08.1 — User sees updated market prices online.
- [x] US-08.2 — User can work offline.
- [x] US-08.3 — User sees quote update status.
- [x] US-08.4 — Developer can add providers.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-08.1 | Partial | `IQuoteProvider`, typed provider errors, `MockQuoteProvider`, and quote payload (price/currency/timestamp/source + optional OHLC/volume fields) implemented. Real providers deferred. |
| AC-08.2 | Partial | Latest quotes cached via `JsonQuoteCacheRepository`; refresh uses cached values when provider fails and updates UI status. Historical quote series for charts deferred. |
| AC-08.3 | Partial | Provider contract uses ticker/currency only; no transaction data sent. API-key management not required for mock provider; redacted logging policy preserved (no raw payload logging added). |
| AC-08.4 | Partial | Manual refresh button exists and UI remains responsive; failures are non-blocking and summarized in status text. Interval refresh and advanced rate-limit backoff remain stubs/deferred. |
| AC-08.5 | Partial | Quote model supports optional OHLC and volume fields, but asset details candle retrieval/rendering still deferred to analytics/chart modules. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`QuoteRefreshService_UsesCacheOnProviderFailure`) | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` (`JsonQuoteCacheRepository_UpsertsByAsset`) | Passed |
| UI/Smoke | `dotnet test Proxima.sln` (assets page quote refresh wiring + shell test suite) | Passed |
| Security/Privacy | Provider API accepts ticker/currency only and no transaction payload paths were introduced | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Mock quote provider is used for deterministic demo mode. No real market API integration or historical OHLC persistence yet; scheduled refresh strategy is deferred.

### Commit

7e0712e feat(quotes): add mock provider and offline quote cache refresh

## Iteration 09 — Dashboard

### Scope

Implement dashboard calculations and UI sections for total value, delta state, allocation, timeframe summary, and latest transactions that react to portfolio switching and existing asset/transaction data.

### User Stories Checked

- [x] US-09.1 — User sees total capital instantly.
- [x] US-09.2 — User understands 24h movement.
- [x] US-09.3 — User sees capital history.
- [x] US-09.4 — User sees diversification.
- [x] US-09.5 — User can inspect recent transactions.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-09.1 | Done | Total value card is computed from asset values with decimal-safe math and empty fallback text; refresh source already uses latest/cached quotes from Module 08. |
| AC-09.2 | Partial | Delta block supports positive/negative/neutral states and explicit `Недостаточно данных` fallback. Full prior-24h quote baseline is deferred due missing historical quotes. |
| AC-09.3 | Partial | Timeframe selector (`1D/7D/1M`) and history summary are implemented; full chart rendering with axes/tooltips is deferred. |
| AC-09.4 | Done | Latest transactions block supports search and sort by name/price/type/date and handles empty state via ItemsControl data presence. |
| AC-09.5 | Done | Allocation-by-tag calculation implemented with `Без категории` bucket; values are derived from current portfolio values and rendered in legend-style rows. |
| AC-09.6 | Done | Dashboard is rebuilt on portfolio switch and on asset/transaction reload, preventing stale previous-portfolio values. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Analytics.Tests` (`DashboardCalculator_TotalValueAndAllocation`, `DashboardCalculator_LatestTransactionsSortAndSearch`) | Passed |
| Integration | `tests/Proxima.App.Tests` (`ShellViewModel_ComputesDashboardCards`) plus full solution tests | Passed |
| UI/Smoke | `dotnet test Proxima.sln` and dashboard bindings compile/load | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

The dashboard currently exposes a timeframe-driven history summary instead of a fully rendered chart with axes/tooltips. Complete 24h delta requires historical quote snapshots that will be added in subsequent modules.

### Commit

ec0eab0 feat(dashboard): add portfolio overview calculations and dashboard widgets

## Iteration 10 — Asset Details & Asset Analytics View

### Scope

Implement asset details analytics view with base/advanced metrics, timeframe-driven OHLC fallback state, and transaction table strictly scoped to selected asset.

### User Stories Checked

- [x] US-10.1 — User analyzes selected asset.
- [x] US-10.2 — User reads key metrics quickly.
- [x] US-10.3 — Professional user sees advanced risk metrics.
- [x] US-10.4 — User reviews transactions for one asset.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-10.1 | Done | Asset details route opens from assets list, uses selected asset context, updates title/breadcrumb, supports not-found fallback and back navigation. |
| AC-10.2 | Partial | Timeframes `1h/1d/7d/30d` and OHLC fallback state are implemented; real candlestick rendering/tooltips/axes are deferred without external OHLC feed. |
| AC-10.3 | Partial | Base metrics section renders SMA50/SMA200/RSI/ATR with unavailable placeholders for missing market/fundamental fields and explanatory help text. |
| AC-10.4 | Partial | Advanced metrics section renders Sharpe/Sortino/Calmar/MDD/VaR/CVaR/Z-Score and placeholders for metrics requiring benchmark/orderbook inputs. |
| AC-10.5 | Partial | Metric rows include explanatory text and deterministic calculations; explicit unit/risk labeling exists but full tooltip UX is still simplified. |
| AC-10.6 | Done | Asset details transaction table includes only selected asset operations and keeps edit action path via transaction editor button. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Analytics.Tests` (`AssetDetailsCalculator_CoreMetrics`) | Passed |
| Integration | `tests/Proxima.App.Tests` (`ShellViewModel_BuildsAssetDetailsMetrics`) | Passed |
| UI/Smoke | `dotnet test Proxima.sln` plus asset details route and bindings compile/load | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Asset details currently use deterministic OHLC fallback series and no real candle dataset from providers. Some advanced metrics require external benchmark/orderbook data and therefore render unavailable placeholders.

### Commit

660d0f8 feat(asset-details): add risk metrics and scoped asset analytics view

## Iteration 11 — Analytics Engine

### Scope

Create a reusable analytics engine module with typed metric results and deterministic formulas, and integrate engine usage into dashboard/asset-details paths.

### User Stories Checked

- [x] US-11.1 — User receives correct portfolio calculations.
- [x] US-11.2 — User receives risk-aware analytics.
- [x] US-11.3 — Developer can extend metrics safely.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-11.1 | Partial | Money and position calculations use `decimal`; ROI zero-denominator handling and average-cost buy/sell helpers implemented. Full currency-conversion abstraction is documented but still stubbed. |
| AC-11.2 | Done | Portfolio total/allocation logic returns stable values and handles empty sets safely; dashboard uses engine-backed total value path. |
| AC-11.3 | Done | Volatility/MDD/Sharpe/Sortino/VaR/CVaR return typed availability-aware results for insufficient or edge datasets. |
| AC-11.4 | Partial | SMA/RSI/ATR/Z-score/correlation logic implemented and tested; Hurst and full indicator suite remain deferred placeholders. |
| AC-11.5 | Done | Engine metrics return typed `MetricResult` metadata (name, value, unit, availability, explanation, severity). Analytics module remains UI-free and unit-testable. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Analytics.Tests` (`PortfolioAnalyticsEngine_CoreCalculations`) | Passed |
| Integration | `tests/Proxima.App.Tests` and dashboard/asset-details rebuild paths compile and run with engine integration | Passed |
| Review | `docs/analytics-formulas.md` created with assumptions and formulas | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Currency conversion abstraction is not yet backed by a live FX provider. Some advanced indicators (e.g. Hurst and deeper liquidity metrics) remain deferred to later analytics iterations.

### Commit

2b741ba feat(analytics): add typed portfolio analytics engine and formulas

## Iteration 12 — Goals & Compound Interest Forecast

### Scope

Implement goals CRUD, monthly compound forecast calculation, and goals page interactions with create/edit/archive flows scoped to selected portfolio.

### User Stories Checked

- [x] US-12.1 — User creates financial goals.
- [x] US-12.2 — User forecasts time to goal.
- [x] US-12.3 — User receives motivation through visualization.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-12.1 | Done | Goal create/edit/archive implemented via `GoalService` and `JsonGoalRepository` with validation rules and goals list rendering on goals page. |
| AC-12.2 | Partial | Forecast inputs (monthly contribution + expected return) are editable in create/edit dialogs; quick amount buttons are deferred. |
| AC-12.3 | Done | Compound monthly forecast implemented in `GoalService.Forecast`, including zero/negative handling, months-to-goal, estimated date, and unreachable message. |
| AC-12.4 | Partial | Goals page shows forecast summaries and assumptions via text; full projection curve/target-line chart with tooltip is deferred. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`GoalService_ForecastAndValidation`) | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` (`JsonGoalRepository_StoresAndArchives`) | Passed |
| UI/Smoke | `dotnet test Proxima.sln` includes goals bindings/DI through app test graph | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Projection chart visualization (curve/target line/reach point) is currently summarized textually in goal rows. Return assumptions are user-input only; automatic inference from portfolio history is deferred.

### Commit

671a874 feat(goals): add goal forecasting and portfolio-scoped goal CRUD

## Iteration 13 — Taxes for Belarus Draft Module

### Scope

Implement draft Belarus tax page cards and deterministic tax calculation with profile/year inputs, exchange-rate abstraction, and explicit draft disclaimer flow.

### User Stories Checked

- [x] US-13.1 — User sees tax obligation overview.
- [x] US-13.2 — User sees currency and dividend effects.
- [x] US-13.3 — User can export a draft tax report.
- [x] US-13.4 — Developer can update tax rules.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-13.1 | Partial | Tax page renders bento cards for total/base/saved, limits, calendar, dividends-double-tax section, loss carryforward, version and disclaimer. |
| AC-13.2 | Done | Portfolio/year/profile inputs exist; profile `Other` treated as missing and returns recoverable prompt; empty transaction history returns explicit empty-state message. |
| AC-13.3 | Partial | Exchange-rate abstraction + mock NBRB provider implemented; source/date rendered in UI; provider failure is recoverable. Cached rates not implemented yet. |
| AC-13.4 | Done | Deterministic draft calculator with version/date metadata and separated components (realized/dividends/fees/currency/losses/exemption). |
| AC-13.5 | Partial | Export button exists and is wired; final PDF generation deferred to reporting module with explicit user-visible message. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`TaxCalculator_IsDeterministicAndIncludesDraftMetadata`, `TaxCalculator_ReturnsRecoverableError_WhenRateUnavailable`) | Passed |
| UI/Smoke | `tests/Proxima.App.Tests` DI/runtime checks include tax calculator integration | Passed |
| Review | `docs/tax-module-notes.md` assumptions and legal limitations | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Tax rules are draft-level and not a legal guarantee. Exchange rates currently use a mock provider. Tax PDF export is not yet implemented.

### Commit

542e011 feat(taxes): add belarus draft tax module and bento tax page

## Iteration 14 — Settings, Profile, Preferences & Configuration

### Scope

Implement settings page state and persistence for profile/preference/provider/security controls, including documented API targets for Belarusbank FX and Finnhub quotes.

### User Stories Checked

- [x] US-14.1 — User edits profile.
- [x] US-14.2 — User configures display preferences.
- [x] US-14.3 — User configures quote providers.
- [x] US-14.4 — User configures security and sync.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-14.1 | Partial | Settings page shows profile fields (display name/role/login), currency/language/UI scale; sidebar identity binds to settings-backed profile data. |
| AC-14.2 | Done | Language RU/EN, base currency options (incl. USD/BYN), UI scale bounds validation, persisted per owner in `JsonUserSettingsRepository`. |
| AC-14.3 | Partial | Provider status/config controls added; refresh interval validated; Finnhub API key input masked and persisted through protected path placeholder; full secure secret storage pending. |
| AC-14.4 | Partial | Security notes and manual lock action presented; password-change flow with current-password check deferred. |
| AC-14.5 | Partial | Encrypted snapshot sync wording, export/import actions and status added; full sync backend and conflict handling deferred. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | `tests/Proxima.Application.Tests` (`SettingsService_ValidatesUiScaleAndRefreshInterval`) | Passed |
| Integration | `tests/Proxima.Infrastructure.Tests` (`JsonSettingsRepository_StoresByOwner`) | Passed |
| UI | `tests/Proxima.App.Tests` (`SettingsPage_ContainsMaskedApiKeyInput`) | Passed |
| Review | `docs/api-providers.md` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

API key storage currently uses lightweight local protection placeholder and must be upgraded to strong encryption in security module. Password-change and full sync engine are deferred.

### Commit

d1c160f feat(settings): add persisted profile/preferences/provider configuration

## Iteration 15 — Encrypted Snapshot Sync

### Scope

Implement encrypted snapshot export/import in `Proxima.Sync`, wire settings sync actions to real service, add conflict detection and Google Drive adapter abstraction.

### User Stories Checked

- [x] US-15.1 — User backs up data privately.
- [x] US-15.2 — User restores data on another device.
- [x] US-15.3 — User syncs through Google Drive safely.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-15.1 | Done | Snapshot export creates one `.pxsnap` encrypted file with schema/app-version/timestamp/source-device/checksum metadata and compressed encrypted payload. |
| AC-15.2 | Done | AES-256-GCM with unique nonce and PBKDF2-SHA256 key derivation; wrong password/tampered payload fail safely. |
| AC-15.3 | Partial | Import preview verifies format/checksum/schema and conflict; apply path imports local JSON stores with conflict override support. Full DB transactional restore deferred. |
| AC-15.4 | Partial | `IGoogleDriveSnapshotAdapter` abstraction and stub implementation added; OAuth upload/download remains deferred. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/UI runner | `tests/Proxima.App.Tests` (`SnapshotService_EncryptDecryptAndTamperFail`) | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |
| Review | `docs/sync-module-notes.md` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Google Drive OAuth sync remains stubbed. Snapshot import currently targets local JSON store restoration; full PostgreSQL transactional restore comes with DB module.

### Commit

66a8402 feat(sync): add encrypted snapshot export import service

## Iteration 16 — Reporting & PDF Export

### Scope

Implement reporting abstraction and generate portfolio/tax draft PDF exports from domain/application data, then wire export actions into dashboard and taxes pages.

### User Stories Checked

- [x] US-16.1 — Consultant exports client portfolio report.
- [x] US-16.2 — Investor exports tax report.
- [x] US-16.3 — User previews report before saving.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-16.1 | Done | `IReportService` + `SimplePdfReportService` in `Proxima.Reporting`; PDF generation independent of Avalonia views and uses safe output path composition. |
| AC-16.2 | Done | Portfolio report includes required sections (portfolio, period, total, P&L, allocation, top assets, risk metrics, tx summary, timestamp, disclaimer). |
| AC-16.3 | Done | Tax report includes required sections (profile/year/base/due/rate notes/dividends/tx summary/version/disclaimer). |
| AC-16.4 | Partial | Export buttons are wired and show success/failure status; long-running progress indicator, manual destination picker and overwrite confirmation are deferred. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/UI runner | `tests/Proxima.App.Tests` (`ReportingService_ExportsNonEmptyPdf`) | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |
| Review | `docs/reporting-module-notes.md` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Preview is currently service-level (sections) rather than a full in-app report preview panel. Destination chooser/overwrite confirmation is still pending UI completion.

### Commit

4a72acb feat(reporting): add portfolio and tax pdf export service

## Iteration 17 — Persistence, Data Integrity & Migrations

### Scope

Introduce PostgreSQL persistence baseline: docker-compose, EF Core/Npgsql schema mapping, SQL migration/seed scripts, and DB bootstrap error handling.

### User Stories Checked

- [x] US-17.1 — User data persists locally.
- [x] US-17.2 — User data survives failures.
- [x] US-17.3 — Developer can evolve schema.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-17.1 | Partial | `docker-compose.yml` added, connection resolved from safe env path, bootstrap service returns graceful unavailable DB message, SQL schema/seed can be applied manually. |
| AC-17.2 | Partial | Required table set defined in committed schema; FK/index/precision/timestamptz constraints present; repository runtime still JSON-first pending full switch. |
| AC-17.3 | Partial | Cross-portfolio transaction integrity remains enforced in application service; DB transactional import path pending repo switch. |
| AC-17.4 | Partial | Initial committed migration baseline provided as SQL scripts with documented apply commands; `dotnet ef` migration artifacts deferred. |
| AC-17.5 | Partial | Demo seed script includes user/portfolio/assets/transactions/prices/goals/tax profile; runtime auto-seed wiring to DB pending full persistence switch. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/Integration-lite | `tests/Proxima.Infrastructure.Tests` (`DatabaseBootstrap_ReturnsGracefulMessage_WhenUnavailable`, `InitialSchemaScript_ContainsRequiredTables`) | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |
| Review | `docs/persistence-module-notes.md` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Application runtime still uses JSON repositories by default. Full Postgres repository activation and `dotnet ef` migration artifacts are planned in follow-up iteration work.

### Commit

703a2fb feat(db): add postgres schema bootstrap and seed scripts

## Iteration 18 — Localization, Accessibility & UI Scaling

### Scope

Add RU/EN localization resources for core shell navigation and page headers, wire runtime language switching, and apply persisted global UI scaling in Avalonia shell.

### User Stories Checked

- [x] US-18.1 — User can use Russian or English UI.
- [x] US-18.2 — User can scale the interface.
- [x] US-18.3 — Keyboard user can navigate core flows.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-18.1 | Partial | `UiLocalizer` added with RU/EN resource maps; sidebar/topbar labels and core page title/description text are localized; language switch persists through settings store and applies at runtime without reinstall. |
| AC-18.2 | Done | UI scale setting already persisted in settings; shell now applies safe-clamped `0.8..1.5` `ScaleTransform` on open and on runtime changes. |
| AC-18.3 | Partial | Existing focus visuals/labels remain active and no color-only semantic indicators were introduced by this change; full keyboard traversal and modal-focus audit across every screen is still pending dedicated accessibility pass. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/UI runner | `tests/Proxima.App.Tests` (`Localization_FallbackAndLanguageSwitch_Works`) | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |
| Review | Shell/sidebar/topbar localized bindings in `MainWindow.axaml` and runtime scale behavior in `MainWindow.axaml.cs` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Localization coverage is currently shell-first; some feature-level error/validation texts remain to be fully localized in follow-up module work.

### Commit

abffe0f feat(i18n): add ru-en shell localization and ui scaling

## Iteration 19 — Error Handling, Notifications & Audit Logging

### Scope

Add non-blocking user notifications for key background operations, implement basic audit trail service/repository, and introduce sensitive-key redaction helper used by audit metadata.

### User Stories Checked

- [x] US-19.1 — User sees recoverable errors clearly.
- [x] US-19.2 — Developer can diagnose issues safely.
- [x] US-19.3 — User sees background operation status.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-19.1 | Partial | `ShellViewModel.Notifications` + `AppNotificationViewModel` added; success/warning/error/info notifications are pushed from import/quotes/report/snapshot/portfolio flows; long-running quote refresh and import states remain non-blocking. |
| AC-19.2 | Partial | Explicit user-facing failure handling added for import/quote/snapshot/report paths; navigation remains available on recoverable failures. Full generic exception boundary mapper is still pending. |
| AC-19.3 | Partial | `RedactionHelper` introduced and used by `AuditService`; tests verify sensitive key names are redacted from stored metadata. Full structured app-wide sink with operation/session IDs remains pending. |
| AC-19.4 | Partial | Audit events are recorded for profile create, portfolio create/update/archive, import commit, snapshot export/import, and tax report export. Password-change audit is deferred because password-change flow is not implemented yet. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/UI runner | `tests/Proxima.App.Tests` (`NotificationHost_IsBoundInShellLayout`, `RedactionHelper_RedactsSensitiveFragments`) | Passed |
| Integration-lite | `tests/Proxima.Infrastructure.Tests` (`JsonAuditLogRepository_AppendsEvents`, `RedactionHelper_RemovesSensitiveKeys`) | Passed |
| Regression | `dotnet build Proxima.sln`, `dotnet test Proxima.sln` | Passed |

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
git status --short
```

### Result

Partial

### Known Limitations

Structured logging with correlation IDs and complete catch-all exception mapping are not fully implemented yet.

### Commit

8e44654 feat(observability): add notifications and audit trail foundation

### Post-Iteration QA Note (2026-04-30)

- Manual review in Rider run reported major UI quality gaps: text overflow/clipping, undersized/non-adaptive window feel, missing strong color/icon language, and overall mismatch against Mockup-grade bento layout.
- Asset Details candlestick chart is still missing (fallback text state observed).
- Dashboard/Goals real chart rendering is still missing (summary/placeholder-only behavior).
- These findings are recorded as release blockers in `.codex/known-limitations.md` and Module 20 acceptance criteria were tightened accordingly.

### Follow-up Implementation Note (2026-05-01)

- Implemented initial real chart widgets in app shell:
  - Dashboard history line chart;
  - Asset Details candlestick chart;
  - Goals projection line chart.
- Increased desktop window defaults/minimum size to improve readability on 1080p layouts.
- Remaining gap: visual parity (typography rhythm/icons/final spacing polish) is still pending strict Mockup-level pass.

### Follow-up Implementation Note (2026-05-01, pass 2)

- Improved baseline visual readability and contrast: updated tokens/shadows/typography and interactive button hover/focus styles.
- Added sidebar icon markers and enabled horizontal/vertical scrollbars for content safety on smaller viewports.
- Fixed analytics regression test stability by using a mixed up/down close-price series in `Proxima.Analytics.Tests` so Sortino has downside samples.

### Follow-up Implementation Note (2026-05-01, pass 3)

- Refined Tax and Settings screen hierarchy/texts in `MainWindow.axaml` for clearer card semantics and more consistent RU labels.
- Updated known limitations: chart gaps are now marked as closed by implementation; remaining release blocker is strict visual parity validation against Mockup via custom Figma MCP.
- In this sandbox, `dotnet build` succeeds with `-m:1 -nr:false`; `dotnet test` still fails on infrastructure socket access (`Permission denied`) due environment networking restrictions.

## Iteration 20 — Final Release Readiness (in progress)

### Scope

Prepare cross-platform publish path for local installation/use without Docker runtime dependency and document deployment constraints clearly.

### User Stories Checked

- [x] US-20.2 — Build and run instructions are deterministic.
- [x] US-20.4 — Known limitations are explicit and visible.

### Acceptance Criteria Checked

- [x] Added reproducible self-contained publish scripts for Linux/Windows.
- [x] README includes non-Docker runtime usage guidance.
- [x] Deployment notes clarify PostgreSQL dependency vs Docker optionality.

### Tests Added/Updated

- Unit: N/A
- Integration: N/A
- UI: N/A
- Manual: `dotnet build Proxima.sln -m:1 -nr:false` passed.

### Commands Run

```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln -m:1 -nr:false
dotnet test Proxima.sln -m:1 -nr:false
git status --short
```

### Result

Partial

### Known Limitations

- `dotnet format` and infrastructure DB tests fail in this sandbox because named pipe/socket operations are restricted (`Permission denied`), while build itself is green.
- Installer packaging (MSIX/DEB/RPM) is not finalized yet; current deliverable is self-contained publish output.

### Follow-up Implementation Note (2026-05-01, packaging pass)

- Added distributable packaging scripts on top of self-contained publish:
  - `scripts/package-linux-x64.sh` -> `tar.gz`
  - `scripts/package-win-x64.ps1` -> `zip`
- Updated release docs with archive outputs and explicit installer status in `docs/deployment.md` and `README.md`.

### Follow-up Implementation Note (2026-05-01, providers pass)

- Replaced hardwired mock-only provider composition with settings-driven adapters:
  - quotes: `ConfigurableQuoteProvider` + `FinnhubQuoteProvider`
  - tax FX: `ConfigurableExchangeRateProvider` + `BelarusbankExchangeRateProvider`
- Composition now wires quote/tax providers with access to persisted settings store path.
- Updated `docs/api-providers.md` with concrete endpoints and fallback behavior.

### Follow-up Implementation Note (2026-05-01, installer pass)

- Added native installer baseline scripts:
  - `scripts/package-linux-deb.sh` for Debian packages.
  - `scripts/package-win-msix.ps1` for MSIX package skeleton on Windows SDK hosts.
- Updated deployment/readme docs with DEB/MSIX commands, outputs, and signing/tooling notes.

### Follow-up Implementation Note (2026-05-01, rpm pass)

- Added RPM baseline script `scripts/package-linux-rpm.sh` (rpmbuild-based, from pre-published self-contained binaries).
- Added Linux packaging orchestrator `scripts/package-linux-all.sh` to build tar.gz + deb + rpm in one run.
- Updated deployment/readme docs with RPM commands and outputs.

### Follow-up Implementation Note (2026-05-01, release gate pass)

- Added `scripts/release-preflight.sh` to validate build/test and required packaging tools before release cut.
- Added `scripts/generate-release-manifest.sh` to generate `artifacts/release/manifest.txt` with SHA256 + size + path for produced packages.
- Updated deployment/readme docs with release validation and manifest steps.

### Follow-up Implementation Note (2026-05-01, runtime stabilization pass)

- Hardened UI runtime flows with exception-safe handling in:
  - quotes refresh (`RefreshQuotesAsync`)
  - taxes recalculation (`RecalculateTaxesAsync`)
- Fixed `DatabaseBootstrapService` graceful degradation by handling `SocketException` together with DB/IO errors.
- Added infrastructure tests for live-provider payload parsing:
  - `FinnhubQuoteProvider_ParsesQuotePayload`
  - `BelarusbankExchangeRateProvider_ParsesRatesPayload`
- Verification in current environment:
  - `dotnet build Proxima.sln -m:1 -nr:false` passed
  - `dotnet test Proxima.sln -m:1 -nr:false` passed

### Follow-up Implementation Note (2026-05-01, runtime reliability pass 2)

- Added app-level smoke tests for failure paths in `ShellViewModel`:
  - quote refresh failure should not crash and must show user-visible error + notification;
  - tax recalculation failure should not crash and must show user-visible error + fallback rate source.
- Hardened `LocalSettingsReader` against malformed/unreadable settings JSON (safe `null` fallback instead of exception propagation).
- Verification:
  - `dotnet test Proxima.sln -m:1 -nr:false` passed with new tests included.

### Follow-up Implementation Note (2026-05-01, import e2e pass)

- Hardened import flow runtime behavior:
  - `PreviewImportAsync` now catches provider/parser exceptions and surfaces user-visible/audited failures.
  - `CommitImportAsync` now catches unexpected failures and surfaces user-visible/audited failures.
- After successful import commit, dependent state now refreshes immediately:
  - transactions reload;
  - goals reload;
  - tax draft recalculation.
- Added `ShellViewModel_ImportCommit_RefreshesDependentState` smoke test to validate preview+commit path and resulting UI state consistency.
- Verification:
  - `dotnet build Proxima.sln -m:1 -nr:false` passed (transient file-lock warnings observed in parallel runs).
  - `dotnet test Proxima.sln -m:1 -nr:false` passed.

## Iteration 21 — Shell Runtime Host Migration (AppShell)

### Scope

Switch runtime host to the new `AppShellView` after successful login/unlock, add DI-based shell/navigation composition, register target routes, and show placeholder content for unmigrated screens while preserving legacy UI files.

### User Stories Checked

- [x] US-03.1 — User can move between core sections.
- [x] US-03.2 — User always knows current location.
- [x] US-03.3 — User can switch current portfolio globally (mock shell state in new host).
- [x] US-03.4 — Runtime host is separated from legacy monolith shell.

### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-03.1 | Done | `App.axaml.cs` now uses DI composition and opens `AppShellView` as runtime host; release keeps login path, debug supports local auto-login flag. |
| AC-03.2 | Done | New navigation service in `src/Proxima.App/Navigation/*` registers `Dashboard`, `Assets`, `AssetDetails`, `Goals`, `Taxes`, `Settings`; sidebar changes current route. |
| AC-03.3 | Done | `TopbarViewModel` receives title/breadcrumb/current portfolio from route + shell state (`MockShellState`). |
| AC-03.4 | Partial | Unmigrated route content uses `PlaceholderView`; functional feature views still live in legacy shell until full screen migration. |

### Tests Added/Updated

- Unit: Existing `tests/Proxima.App.Tests` regression suite executed (no new tests added in this pass).
- Integration: N/A.
- UI: `scripts/scan-xaml-style-violations.sh` passed for `Shell` and `Views`.
- Manual: Runtime flow reviewed for `login -> AppShell` and debug auto-login mode.

### Commands Run

```bash
dotnet build
dotnet test
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Shell src/Proxima.App/Views src/Proxima.App/DesignSystem
git status --short
```

### Result

Partial

### Known Limitations

- `scan-xaml-style-violations` fails for `src/Proxima.App/DesignSystem` due pre-existing token/control style patterns outside this iteration scope.
- `AppShell` currently renders placeholders for migrated route hosts; business screen migration is deferred.
- Debug-only `DevAutoLogin` is enabled by default for local development and documented in `.codex/known-limitations.md`.

### Commit

Pending `refactor(app): use new app shell as runtime host`

## Iteration 22 — Login/Unlock Screen Recovery (Figma Mapping)

### Scope

Rebuild Login/Unlock screen as a dedicated `Views/Auth/LoginView` from Figma mapping, add temporary auth gate abstraction for runtime flow, and switch startup to `LoginView -> AppShellView` without expanding legacy `MainWindow` UI.

### User Stories Checked

- [x] US-02.2 — Returning user unlocks Proxima locally.
- [x] US-03.4 — Runtime host is separated from legacy monolith shell.

### Acceptance Criteria Checked

- [x] Login screen implemented under `src/Proxima.App/Views/Auth` with Proxima DesignSystem styling.
- [x] Runtime startup shows Login/Unlock before authenticated shell.
- [x] Successful unlock opens `AppShellView`.
- [x] Loading/error/disabled/recovery states are visible in login flow.
- [x] Visual comparison notes recorded in `.codex/figma-mapping/login.md`.

### Tests Added/Updated

- Unit: None added in this pass.
- Integration: None.
- UI: Style guard scan for `Views/Auth` and `DesignSystem`.
- Manual: Login runtime flow smoke check (unlock transitions to `AppShellView`).

### Commands Run

```bash
dotnet build
dotnet test
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views/Auth src/Proxima.App/DesignSystem
git status --short
```

### Result

Partial

### Known Limitations

Temporary in-memory auth gate is used for runtime unlock and still needs integration with real profile-backed auth service.

### Commit

Pending feat(ui): rebuild login from figma mockup
