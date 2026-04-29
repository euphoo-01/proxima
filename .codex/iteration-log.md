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

Pending
