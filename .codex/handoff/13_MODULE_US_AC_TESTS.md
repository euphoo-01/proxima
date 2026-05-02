# Module User Stories, Acceptance Criteria & Tests

## Purpose

This document is mandatory for the Codex agent. Every development iteration must implement exactly one coherent module or a clearly bounded submodule.

An iteration is not complete until:

1. User stories for the module are implemented or explicitly marked out of scope.
2. Acceptance criteria are checked one by one.
3. Tests for the module are created or updated.
4. Tests are executed.
5. Regressions are fixed.
6. The module state is documented.
7. Changes are committed with Conventional Commits.

No module may be considered “done” only because the UI compiles or looks close to the design. Functional behavior, data persistence, error handling, security rules, and test coverage are part of done.

## Iteration Completion Protocol

For every module iteration, follow this exact sequence:

```text
1. Inspect relevant docs and Figma nodes.
2. Restate module scope in .codex/iteration-log.md.
3. Implement domain/application/infrastructure/UI changes.
4. Check every User Story.
5. Check every Acceptance Criterion.
6. Write/update tests listed for the module.
7. Run targeted tests for the module.
8. Run broader regression tests where affected.
9. Fix failures.
10. Update docs/iteration-log.md with status.
11. Run build/format/test.
12. Commit with Conventional Commit.
```

Required commands before closing the iteration:

```bash
dotnet format
dotnet build
dotnet test
git status --short
git add .
git commit -m "<type>(<scope>): <summary>"
```

If any acceptance criterion is not implemented, write it to `docs/known-limitations.md` and do not call the module fully done. Use status `partial`.

## Iteration Log Template

Create and maintain `docs/iteration-log.md`.

```md
# Iteration Log

## Iteration N — <Module Name>

### Scope
...

### User Stories Checked
- [ ] US-...

### Acceptance Criteria Checked
- [ ] AC-...

### Tests Added/Updated
- Unit:
- Integration:
- UI:
- E2E/manual:

### Commands Run
```bash
dotnet format
dotnet build
dotnet test
```

### Result
Done / Partial / Failed

### Known Limitations
...

### Commit
<hash> <message>
```

---

# Module 00 — Repository, Build & Engineering Baseline

## User Stories

### US-00.1 — Developer can start the project predictably
As a developer, I want a clean .NET solution structure, so that I can build, test and extend Proxima without guessing project boundaries.

### US-00.2 — Developer can rely on automated quality checks
As a developer, I want formatting, analyzers and test projects configured from the beginning, so that engineering quality is enforced continuously.

### US-00.3 — Developer can inspect project history
As a maintainer, I want all meaningful changes committed using Conventional Commits, so that the development history remains readable and release notes can be generated later.

## Acceptance Criteria

### AC-00.1 Solution structure

- `Proxima.sln` exists.
- Projects exist:
  - `Proxima.App`
  - `Proxima.Domain`
  - `Proxima.Application`
  - `Proxima.Infrastructure`
  - `Proxima.Analytics`
  - `Proxima.Importing`
  - `Proxima.Reporting`
  - `Proxima.Sync`
- Test projects exist:
  - `Proxima.Domain.Tests`
  - `Proxima.Application.Tests`
  - `Proxima.Infrastructure.Tests`
  - `Proxima.Analytics.Tests`
  - `Proxima.Importing.Tests`
- Project references follow dependency rule:
  - Domain references no other Proxima project.
  - Application references Domain.
  - Infrastructure references Application and Domain.
  - App references Application and Infrastructure only through composition where practical.
- No UI or persistence package is referenced by Domain.

### AC-00.2 Build configuration

- Nullable reference types are enabled.
- Implicit usings are enabled.
- Code analysis is enabled.
- Warnings are treated as errors unless a documented exception is needed.
- `.editorconfig` exists.
- `Directory.Build.props` exists.
- `.gitignore` excludes:
  - `bin/`
  - `obj/`
  - `.vs/`
  - `.idea/`
  - `.env`
  - user secrets
  - logs
  - local database data directories
  - raw broker reports
- `README.md` explains prerequisites and startup.

### AC-00.3 Git

- Repository is initialized.
- There is no giant final commit.
- Each module iteration has at least one Conventional Commit.
- Commit message format is `<type>(<scope>): <summary>`.
- `git status --short` is clean or documented after final iteration.

## Tests

### Automated

- `dotnet restore` succeeds.
- `dotnet build` succeeds.
- `dotnet test` succeeds.
- A simple smoke test exists in at least one test project.
- Architecture dependency test checks that `Proxima.Domain` does not reference forbidden assemblies:
  - Avalonia
  - EF Core
  - Npgsql
  - HttpClient-specific infrastructure wrappers
  - UI frameworks

### Manual / Review

- Inspect solution references.
- Inspect `.gitignore`.
- Run `git log --oneline`.
- Verify commits follow Conventional Commits.

---

# Module 01 — Figma Design Inspection & Bento Design System

## User Stories

### US-01.1 — UI implementer can use Figma as source of truth
As a UI implementer, I want the agent to inspect the Figma design through MCP, so that the Avalonia UI follows the actual layout instead of invented approximations.

### US-01.2 — User sees a trustworthy bento-style interface
As a private investor, I want a clean light bento dashboard, so that financial information feels readable, calm and trustworthy.

### US-01.3 — Developer can reuse design primitives
As a developer, I want reusable Avalonia styles and controls, so that screens are visually consistent and easy to maintain.

## Acceptance Criteria

### AC-01.1 Figma inspection

- `docs/figma-inspection.md` exists.
- It contains:
  - Figma URL.
  - File key.
  - Starting node ID.
  - Pages/frames inspected.
  - Screenshots captured or reason why unavailable.
  - Extracted colors.
  - Extracted typography.
  - Extracted spacing.
  - Extracted radii.
  - Extracted shadows.
  - Differences from Figma.
- If Figma MCP is unavailable, the document explicitly says so.
- The implementation does not claim pixel-perfect match if Figma was unavailable.

### AC-01.2 Design tokens

- Avalonia resource dictionaries exist:
  - `Tokens.axaml`
  - `Typography.axaml`
  - `Buttons.axaml`
  - `Inputs.axaml`
  - `Cards.axaml`
  - `Tables.axaml`
  - `Charts.axaml`
- Tokens define:
  - background colors;
  - surface colors;
  - primary/secondary text;
  - accent colors;
  - semantic colors success/warning/danger;
  - border color;
  - focus color;
  - card radii;
  - spacing values;
  - shadows/elevations where supported.
- All major screens consume shared resources instead of hardcoded repeated values.

### AC-01.3 Reusable controls

- Reusable controls exist:
  - `BentoCard`
  - `MetricCard`
  - `StatusPill`
  - `EmptyState`
  - `PageHeader`
  - `SearchBox`
  - `TimeframeSelector`
- Controls support binding-friendly properties.
- Controls are not hardcoded for only one page.
- Controls handle empty/null states safely.

### AC-01.4 Visual requirements

- UI is light, modern and not overloaded.
- Cards have rounded corners.
- Main information uses strong hierarchy.
- Tables are readable.
- Complex metrics have tooltips/descriptions.
- Focus state is visible.
- Error states are visible.
- UI does not rely on color alone for meaning.

## Tests

### Unit

- ViewModel tests verify theme/UI state properties where applicable.
- Converter tests for:
  - positive/negative/neutral delta visual state;
  - currency formatting;
  - percentage formatting.

### UI / Snapshot / Manual

- Launch app and inspect:
  - login;
  - shell;
  - dashboard;
  - assets;
  - asset details;
  - goals;
  - taxes;
  - settings.
- Verify screenshots against Figma references when available.
- Verify keyboard navigation reaches buttons and inputs.
- Verify UI scale does not break core layout.

### Regression

- `dotnet build`
- Avalonia XAML compile succeeds.
- No duplicated local resource definitions when a global token exists.

---

# Module 02 — Local Authentication, Profile Setup & App Unlock

## User Stories

### US-02.1 — First-time user creates a local protected profile
As a privacy-first user, I want to create a local profile and password on first launch, so that nobody can open my financial data without permission.

### US-02.2 — Returning user unlocks Proxima locally
As a returning user, I want to unlock the app with my local password, so that I can access my portfolios without cloud login.

### US-02.3 — User receives clear feedback on authentication errors
As a user, I want understandable validation and error messages, so that I know what to correct without exposing sensitive details.

### US-02.4 — User can understand password recovery limitation
As a local-first app user, I want the app to explain password recovery limitations, so that I understand that Proxima cannot recover encrypted local data without my password.

## Acceptance Criteria

### AC-02.1 First-run setup

- On empty database, app opens first-run setup.
- User can enter:
  - display name;
  - login;
  - password;
  - password confirmation;
  - role: private investor or financial analyst.
- Password confirmation must match.
- Password minimum policy exists:
  - at least 8 characters;
  - at least one letter;
  - at least one digit or symbol.
- Create profile button is disabled until form is valid.
- Successful setup creates user/profile records.
- Successful setup navigates to dashboard or initial portfolio setup.
- Seed/demo mode may create `demo` user, but production flow must not rely on hardcoded credentials.

### AC-02.2 Unlock

- Returning launch shows login/unlock screen.
- User enters login and password.
- Correct password unlocks application.
- Incorrect password shows generic error.
- Password input is masked.
- Pressing Enter submits.
- UI remains responsive during verification.
- Password is not stored in memory longer than necessary.

### AC-02.3 Password storage

- Password is never stored in plaintext.
- Password is hashed with Argon2id or PBKDF2 with strong parameters.
- Salt is unique per user.
- Hash verification uses safe comparison.
- Logs never include password or hash.

### AC-02.4 Lock/error behavior

- After repeated failed attempts, UI applies delay or warning.
- Unknown login and wrong password return the same generic error.
- App does not crash if DB connection fails; it shows recoverable setup/error screen.
- “Forgot password?” explains local-first limitation.
- If encrypted data exists, password reset must not silently destroy data.

## Tests

### Unit

- Password hash generation returns non-empty hash and salt.
- Same password with different salts produces different hashes.
- Correct password verifies successfully.
- Incorrect password fails.
- Password validator tests:
  - too short;
  - missing digit/symbol;
  - confirmation mismatch;
  - valid password.
- Auth service returns generic error for unknown login/wrong password.

### Integration

- First-run setup persists user.
- Unlock reads stored hash and authenticates.
- Failed unlock does not create session.
- Logs are inspected or logging sink tested for absence of password fields.

### UI

- First-run form validation disables submit.
- Login screen masks password.
- Correct demo credentials navigate to dashboard.
- Wrong password displays error and stays on login.
- Forgot password opens explanation dialog.

---

# Module 03 — Shell, Navigation, Sidebar & Topbar

## User Stories

### US-03.1 — User can move between core sections
As a user, I want persistent navigation between Dashboard, Assets, Taxes, Goals and Settings, so that I can move through the app without losing context.

### US-03.2 — User always knows current location
As a user, I want breadcrumbs and active sidebar state, so that I understand where I am in the application.

### US-03.3 — User can switch current portfolio globally
As a multi-portfolio user, I want to switch portfolio in the topbar, so that every page updates to the selected portfolio.

### US-03.4 — User can create a portfolio from topbar
As a user, I want a “Создать портфель” button in the topbar, so that I can quickly start tracking a new portfolio or client.

## Acceptance Criteria

### AC-03.1 Authenticated shell

- Sidebar appears on all authenticated pages.
- Sidebar does not appear on login/setup screens.
- Main area contains topbar and page content.
- Window layout works at common desktop sizes:
  - 1366×768;
  - 1440×900;
  - 1920×1080.
- Sidebar has Proxima title.
- Sidebar bottom contains avatar/name/role.

### AC-03.2 Navigation

- Sidebar links exist:
  - Дешборд;
  - Все активы;
  - Налоги;
  - Цели;
  - Настройки.
- Clicking a link changes content without app restart.
- Active item is visually highlighted.
- Navigation uses ViewModels/routing service, not direct View instantiation in random code-behind.
- Back navigation is supported where needed, especially asset details/manual import.

### AC-03.3 Topbar

- Breadcrumbs update per page.
- Portfolio selector shows current portfolio.
- Create portfolio button opens modal/dialog.
- Quote/sync status indicator exists if status service is implemented.
- Topbar remains aligned and readable after portfolio name changes.

### AC-03.4 State preservation

- Current selected portfolio is stored in app state.
- Navigating between pages preserves current portfolio.
- Pages reload their data when current portfolio changes.
- Changing portfolio on Dashboard changes Dashboard data.
- Changing portfolio on Asset Details either navigates away or shows safe empty state if selected asset is not in new portfolio.

## Tests

### Unit

- Navigation service route registration tests.
- Shell ViewModel active page changes on navigate.
- Breadcrumb generation tests.
- Portfolio selector state tests.

### UI

- Login has no sidebar.
- Dashboard has sidebar/topbar.
- Clicking sidebar items changes pages.
- Active nav item updates.
- Create portfolio dialog opens.
- Portfolio selector changes current portfolio.

### Integration

- Selected portfolio state is shared across Dashboard/Assets/Goals/Taxes.
- Switching portfolio invalidates/reloads page queries.

---

# Module 04 — Portfolio Management

## User Stories

### US-04.1 — Private investor can create a portfolio
As a private investor, I want to create a portfolio, so that I can group my assets and transactions.

### US-04.2 — Financial consultant can separate client portfolios
As a financial consultant, I want multiple isolated portfolios, so that clients' financial data does not mix.

### US-04.3 — User can manage portfolio metadata
As a user, I want to rename, archive or delete portfolios, so that my workspace stays organized.

## Acceptance Criteria

### AC-04.1 Create portfolio

- User can create portfolio from Topbar.
- Required fields:
  - name;
  - base currency.
- Optional fields:
  - description;
  - client label.
- Duplicate names are allowed only if UI disambiguates them or forbidden with clear validation.
- Newly created portfolio becomes selectable.
- Empty portfolio shows empty states, not errors.

### AC-04.2 Read/list/select

- Portfolio selector lists portfolios for current user only.
- Archived portfolios are hidden by default.
- Current selection persists for the app session.
- Selection affects all scoped queries.

### AC-04.3 Update

- User can rename portfolio.
- User can change base currency.
- Changing base currency triggers recalculation/display update.
- Validation prevents empty names.
- Update persists in DB.

### AC-04.4 Delete/archive

- Destructive action requires confirmation.
- Prefer archive over hard delete.
- If hard delete is implemented, it must be explicit.
- Deleting/archiving current portfolio selects another available portfolio or shows no-portfolio state.
- Data loss warning is clear.

### AC-04.5 Isolation

- Assets, transactions, goals and reports are scoped to portfolio.
- A user cannot see data from another user's portfolio.
- Tests verify no cross-portfolio leakage.

## Tests

### Unit

- Portfolio validation tests.
- Portfolio selection state tests.
- Base currency change event tests.

### Integration

- Create persists portfolio.
- Rename persists.
- Archive hides from selector.
- Portfolio-scoped asset query does not return another portfolio’s assets.
- Current portfolio selector handles deleted current portfolio.

### UI

- Create portfolio dialog validation.
- Portfolio appears in selector after creation.
- Empty portfolio dashboard shows empty state.
- Archive/delete confirmation appears.

---

# Module 05 — Asset Management

## User Stories

### US-05.1 — User can track different asset classes
As an investor, I want to add stocks, crypto, currencies, cash, bonds and ETFs, so that my capital is represented in one place.

### US-05.2 — User can categorize assets using tags
As a user, I want to assign tags/categories to assets, so that allocation charts match my own diversification logic.

### US-05.3 — User can open detailed asset view
As a user, I want to click an asset and see its details, so that I can analyze performance and risk.

### US-05.4 — User can maintain assets safely
As a user, I want to edit, archive or delete assets through a clear menu, so that data stays accurate.

## Acceptance Criteria

### AC-05.1 Asset fields

For each asset, system stores:

- id;
- portfolio id;
- ticker;
- name;
- type;
- currency;
- exchange optional;
- ISIN optional;
- tags;
- notes optional encrypted;
- created/updated timestamps.

### AC-05.2 Asset CRUD

- Create asset form validates required fields.
- Ticker is normalized where appropriate.
- Edit asset updates DB.
- Delete/archive requires confirmation.
- Asset cannot be deleted silently if transactions exist; system offers archive or warns about impact.
- Asset table refreshes after changes.

### AC-05.3 Asset table

- Shows:
  - asset name;
  - ticker;
  - type;
  - quantity/position;
  - average buy price;
  - current price;
  - value;
  - P&L;
  - tags;
  - actions menu.
- Search filters by name/ticker/tag.
- Column headers sort table.
- Empty state shown if no assets.

### AC-05.4 Tags/categories

- User can assign one or more tags.
- New tags can be created.
- Tag colors are shown.
- Allocation chart uses tags/categories.
- Removing a tag updates allocation.

### AC-05.5 Navigation

- Clicking asset title opens Asset Details.
- Asset Details receives asset id safely.
- Missing/deleted asset shows not found state.

## Tests

### Unit

- Asset validation tests.
- Ticker normalization tests.
- Asset table sorting/filtering tests.
- Allocation-by-tag tests.

### Integration

- Create asset persists.
- Edit asset persists.
- Archive asset excludes from active list.
- Asset with transaction cannot be hard-deleted without explicit handling.
- Tags persist and load.

### UI

- Asset form validation.
- Search by ticker.
- Sort by value/P&L/name.
- Three-dot menu actions.
- Click title navigates to details.

---

# Module 06 — Transaction Management

## User Stories

### US-06.1 — User can manually enter transactions
As an investor, I want to manually enter buy/sell/dividend/fee/tax transactions, so that the app reflects my real portfolio.

### US-06.2 — User can audit transaction history
As a user, I want searchable and sortable transaction tables, so that I can find mistakes and review operations.

### US-06.3 — User gets recalculated portfolio metrics
As a user, I want portfolio value and P&L recalculated after transaction changes, so that dashboard data stays correct.

## Acceptance Criteria

### AC-06.1 Transaction model

A transaction stores:

- id;
- portfolio id;
- optional asset id where applicable;
- type;
- trade date/time;
- quantity;
- price;
- gross amount;
- fee amount;
- tax amount;
- currency;
- broker optional;
- external id optional;
- encrypted notes optional;
- created/updated timestamps.

### AC-06.2 Supported transaction types

System supports:

- Buy
- Sell
- Dividend
- Deposit
- Withdrawal
- Fee
- Tax
- Transfer
- Split
- Airdrop
- StakingReward

Unsupported transaction types in import are marked suspicious, not silently ignored.

### AC-06.3 Manual entry

- Required fields depend on transaction type.
- Buy/Sell require asset, quantity, price and date.
- Dividend requires asset or income category, amount and date.
- Fee/Tax require amount and date.
- Quantity must not be negative.
- Monetary amount precision is preserved using decimal.
- Validation messages are field-specific.
- Save persists transaction.
- Save triggers recalculation or data refresh.

### AC-06.4 Transaction table

- Search by asset name, ticker, broker, type.
- Sort by date, name, price, amount, type.
- Recent transactions appear on Dashboard.
- Asset Details shows only selected asset’s transactions.
- Empty state exists.

### AC-06.5 Data integrity

- Transactions are scoped to current portfolio.
- A transaction cannot reference asset from another portfolio.
- Updates are transactional.
- Failed save does not leave partial invalid state.

## Tests

### Unit

- Transaction validation per type.
- Decimal precision tests.
- Sorting/filtering tests.
- Average cost/P&L recalculation after buy/sell.
- Dividend income calculation.

### Integration

- Create transaction persists.
- Update transaction persists.
- Delete/archive transaction updates portfolio metrics.
- Cross-portfolio asset reference is rejected.
- Failed transaction save rolls back.

### UI

- Manual transaction form validation.
- Table search.
- Column sort.
- Dashboard latest transactions update.
- Asset Details transaction table scopes correctly.

---

# Module 07 — Import: CSV, PDF Pipeline & Manual Fallback

## User Stories

### US-07.1 — User imports broker reports quickly
As an investor with many transactions, I want to drag and drop a broker report, so that I do not manually copy operations from Excel/PDF.

### US-07.2 — User reviews suspicious imported rows
As a user, I want suspicious transactions highlighted for confirmation, so that incorrect parser assumptions do not corrupt my portfolio.

### US-07.3 — User can recover from failed import
As a user, I want a manual import fallback, so that I can still add transactions if automatic parsing fails.

### US-07.4 — Developer can add broker parsers
As a developer, I want import pipeline interfaces and broker parser strategies, so that future broker formats can be added without rewriting the feature.

## Acceptance Criteria

### AC-07.1 Import modal

- Assets page has “Импортировать активы” button.
- Button opens drag&drop modal.
- Modal accepts supported file types:
  - `.csv`
  - `.pdf`
- Unsupported files show error.
- File size limit exists.
- User can cancel import.

### AC-07.2 CSV import

- CSV demo format is documented.
- CSV parser reads:
  - date;
  - ticker/name;
  - transaction type;
  - quantity;
  - price;
  - currency;
  - fees;
  - broker optional;
  - tag optional.
- Parser handles:
  - header row;
  - empty rows;
  - whitespace;
  - comma/semicolon separator where possible;
  - invalid numeric values.
- Valid rows are mapped to import preview.
- Invalid/suspicious rows are flagged with reason.

### AC-07.3 PDF import pipeline

- PDF parser interface exists.
- At least one placeholder/stub parser exists.
- PDF parsing failure does not crash app.
- If real PDF parser is not implemented, UI clearly says that PDF parser is limited and routes to manual import.
- Raw imported content is not logged.
- Raw payload is encrypted if stored.

### AC-07.4 Review and save

- User sees parsed rows before committing.
- User can edit suspicious rows.
- User can confirm/reject rows.
- Saving valid rows uses DB transaction.
- If commit fails, no partial data is saved unless partial mode is explicitly implemented and shown.
- After successful import, dashboard and assets update.

### AC-07.5 Manual fallback

- Failed import offers “Ввести вручную”.
- Manual import screen is table-like.
- User can add/remove rows.
- User can assign asset/tags.
- Validation summary shows row errors.
- Valid manual rows can be saved.

## Tests

### Unit

- CSV parser valid file.
- CSV parser empty file.
- CSV parser invalid date.
- CSV parser invalid amount.
- CSV parser unsupported transaction type.
- CSV parser suspicious row confidence.
- Import row validator.
- File type validator.
- File size validator.

### Integration

- Import session is created.
- Valid import rows persist.
- Failed batch rolls back.
- Suspicious rows are not committed until confirmed.
- Import from another portfolio does not leak.

### UI

- Drag file opens preview.
- Unsupported file shows error.
- Suspicious row is highlighted.
- Edit row validation works.
- Failed import opens manual import option.
- Save import updates asset/transaction tables.

### Security

- Raw file content absent from logs.
- Raw payload stored encrypted where stored.
- Malformed file does not crash parser.

---

# Module 08 — Quotes, Market Data & Offline Cache

## User Stories

### US-08.1 — User sees updated market prices online
As an investor, I want Proxima to update asset prices from external services, so that portfolio value reflects current market data.

### US-08.2 — User can work offline
As a user, I want cached prices when offline, so that I can still open dashboard and analyze portfolio.

### US-08.3 — User sees quote update status
As a user, I want clear quote update status and errors, so that I know whether data is fresh or cached.

### US-08.4 — Developer can add providers
As a developer, I want quote provider abstractions, so that stock/crypto/currency APIs can be added or replaced safely.

## Acceptance Criteria

### AC-08.1 Provider abstraction

- Interface exists for quotes.
- Mock provider exists for demo mode.
- Provider result includes:
  - price;
  - currency;
  - timestamp;
  - source;
  - optional OHLC;
  - optional volume.
- Provider errors are typed and recoverable.

### AC-08.2 Cache

- Latest quotes are stored in DB.
- Historical quotes can be stored for charts.
- Offline mode uses last cached value.
- UI shows “cached” or stale status when data is old.
- Cache does not mix assets across portfolios incorrectly.

### AC-08.3 Privacy

- Quote API calls do not send full portfolio composition.
- Only needed ticker/provider query is sent.
- No transaction history is sent.
- API keys are not committed.
- Provider logs are redacted.

### AC-08.4 Update flow

- User can trigger refresh manually.
- App can refresh on interval if configured.
- Errors show non-blocking notification.
- One failed provider does not break whole app.
- Rate-limit handling exists or is stubbed with clear behavior.
- UI remains usable during update.

### AC-08.5 OHLC/Candles

- Asset Details can request OHLC data by timeframe.
- If OHLC unavailable, chart shows fallback/empty state.
- Candlestick chart does not crash on sparse data.

## Tests

### Unit

- Mock quote provider returns deterministic prices.
- Quote freshness calculation.
- Cache stale detection.
- Provider error mapping.
- Rate limit error handling.

### Integration

- Quote update stores latest price.
- Offline mode loads cached quote.
- Failed update preserves old price.
- Asset prices scoped by asset id.

### UI

- Refresh button shows loading.
- Error notification appears on provider failure.
- Dashboard uses cached values if offline.
- Asset Details shows empty state if candles unavailable.

### Security/Privacy

- Test provider spy verifies no transaction data passed to provider.
- Logs do not include API key.

---

# Module 09 — Dashboard

## User Stories

### US-09.1 — User sees total capital instantly
As a visual-focused investor, I want one large total portfolio number, so that I immediately understand my financial status.

### US-09.2 — User understands 24h movement
As a user, I want 24h growth/fall displayed in value and percent, so that I know what changed since yesterday.

### US-09.3 — User sees capital history
As a user, I want a portfolio value chart with 1D/7D/1M steps, so that I can understand short-term dynamics.

### US-09.4 — User sees diversification
As a user, I want a category/tag allocation chart, so that I can avoid keeping all assets in one basket.

### US-09.5 — User can inspect recent transactions
As a user, I want latest transactions with search and sorting, so that I can quickly audit recent activity.

## Acceptance Criteria

### AC-09.1 Total value card

- Shows total current portfolio value in selected base currency.
- Uses latest quotes or cached quotes.
- Shows loading state while data loads.
- Shows empty state for empty portfolio.
- Uses decimal-safe calculations.
- Does not crash when quote missing.

### AC-09.2 24h delta

- Shows absolute value delta.
- Shows percent delta.
- Labels period as `24h`.
- Positive, negative and neutral states are visually distinct.
- State includes icon/text, not only color.
- Missing prior price shows “Недостаточно данных”.

### AC-09.3 Portfolio chart

- Shows portfolio value over time.
- Timeframe selector:
  - 1D
  - 7D
  - 1M
- Empty/sparse data state exists.
- Chart renders under target performance for seed dataset.
- Chart uses readable axes/tooltips.

### AC-09.4 Latest transactions

- Shows recent transactions table.
- Search filter exists.
- Sort by name, price, type, date.
- Clicking asset opens Asset Details.
- Table handles no transactions.

### AC-09.5 Allocation chart

- Donut/pie chart by tags/categories.
- Assets without tag go to “Без категории”.
- Chart values equal portfolio current values.
- Legend is readable.
- Clicking category may filter if implemented; otherwise no dead affordance.

### AC-09.6 Portfolio switching

- Dashboard updates when portfolio selector changes.
- Previous portfolio data does not remain displayed after switch.
- Loading/empty/error states are correct.

## Tests

### Unit

- Total value calculation.
- 24h delta calculation.
- Percentage delta division-by-zero handling.
- Allocation by tag.
- Latest transaction sorting/filtering.
- Missing quote handling.

### Integration

- Dashboard query service returns correct data for seeded portfolio.
- Empty portfolio returns empty dashboard model.
- Portfolio switching changes dashboard data.
- Cached quotes used when no fresh quote.

### UI

- Dashboard loads after login.
- Total card visible.
- Timeframe toggle changes chart data.
- Search filters transaction table.
- Sorting by column works.
- Allocation chart visible with legend.
- Empty portfolio dashboard shows empty state.

### Performance

- Dashboard query for seeded multi-year data completes within target or documented.
- Chart renders without UI freeze in demo dataset.

---

# Module 10 — Asset Details & Asset Analytics View

## User Stories

### US-10.1 — User analyzes selected asset
As an investor, I want a detailed page for an asset, so that I can evaluate price behavior, transactions and metrics in one place.

### US-10.2 — User reads key metrics quickly
As a user, I want the most important metrics as separate bento cards, so that I do not drown in complex financial terms.

### US-10.3 — Professional user sees advanced risk metrics
As a professional investor, I want Sharpe, Sortino, MDD, VaR and other metrics, so that I can assess risk-adjusted performance.

### US-10.4 — User reviews transactions for one asset
As a user, I want a transaction table scoped to the selected asset, so that I can audit all operations for it.

## Acceptance Criteria

### AC-10.1 Page loading

- Page opens from All Assets.
- Page receives asset id.
- Missing asset shows not found state.
- Page title includes asset name/ticker.
- Breadcrumbs update.
- Back navigation works.

### AC-10.2 Candlestick/OHLC chart

- Chart supports timeframes:
  - 1h
  - 1d
  - 7d
  - 30d
- If OHLC data exists, candlesticks render.
- If only close prices exist, fallback line chart or empty state is shown.
- Chart has readable axes/tooltips.
- Loading and error states exist.

### AC-10.3 Base metrics

Display when data available:

- Market Cap.
- FDV.
- P/E or P/S for stocks.
- FDV/TVL or Market Cap/Fees for crypto where available.
- 24h Volume.
- Circulating Supply vs Total Supply.
- SMA 50.
- SMA 200.
- RSI.

If data unavailable:

- Show “Нет данных” or “Недоступно”.
- Do not show fake values as real values.
- Tooltip explains metric.

### AC-10.4 Advanced metrics

Display:

- Sharpe Ratio.
- Sortino Ratio.
- Calmar Ratio.
- Max Drawdown.
- VaR.
- CVaR.
- Skewness.
- Kurtosis.
- Beta.
- Historical Volatility.
- Implied Volatility placeholder if unavailable.
- ATR.
- Turnover.
- Spread/depth placeholder if unavailable.
- Hurst exponent.
- Z-Score.
- Pearson correlation.

### AC-10.5 Metric explanations

- Complex metrics have concise tooltip/help text.
- Values include units/period assumptions.
- Risk labels are clear:
  - low;
  - medium;
  - high;
  - unavailable.
- Calculations are deterministic for same input.

### AC-10.6 Asset transaction table

- Shows only selected asset transactions.
- Search exists.
- Sort by date/type/price/amount.
- CRUD action is available or navigates to transaction editor.
- Empty state if no transactions.

## Tests

### Unit

- SMA calculation.
- RSI calculation.
- ATR calculation.
- Sharpe ratio.
- Sortino ratio.
- Calmar ratio.
- Max drawdown.
- VaR.
- CVaR.
- Z-Score.
- Beta.
- Correlation.
- Missing/sparse data handling.
- Asset-specific transaction filtering.

### Integration

- Asset Details query returns asset + prices + metrics + transactions.
- Missing asset returns not found result.
- Metric service handles asset with no prices.
- Asset transaction table does not include other assets.

### UI

- Clicking asset opens detail page.
- Timeframe selector changes chart query.
- Metrics render with fallback states.
- Tooltips visible.
- Transaction search/sort works.

### Numerical Regression

- Fixed test dataset produces expected known values within tolerance.
- Decimal/money calculations use `decimal`; statistical calculations may use `double` internally with documented conversion.

---

# Module 11 — Analytics Engine

## User Stories

### US-11.1 — User receives correct portfolio calculations
As a user, I want Proxima to calculate value, P&L, ROI and average cost correctly, so that I can trust the dashboard.

### US-11.2 — User receives risk-aware analytics
As a user, I want volatility, drawdown and risk-adjusted return metrics, so that I can understand whether returns justify risk.

### US-11.3 — Developer can extend metrics safely
As a developer, I want analytics isolated in its own module, so that new metrics can be added without breaking UI or persistence.

## Acceptance Criteria

### AC-11.1 Money calculations

- Use `decimal` for money, quantity and prices where precision matters.
- Average cost supports buys, sells, fees.
- P&L supports realized/unrealized where implemented.
- ROI handles zero invested amount safely.
- Currency conversion uses exchange rate abstraction.
- Results are rounded only for display, not internal storage.

### AC-11.2 Portfolio calculations

- Total current value = sum of current positions × latest price + cash.
- Allocation values sum to total portfolio asset value.
- Latest quote missing uses cached/fallback state.
- Empty portfolio returns zero values without exceptions.

### AC-11.3 Risk calculations

- Volatility handles insufficient data.
- Max drawdown handles monotonic increasing/decreasing series.
- Sharpe handles zero volatility.
- Sortino handles no downside deviation.
- VaR/CVaR assumptions are documented.
- Advanced metrics return `Unavailable` when not enough data.

### AC-11.4 Technical indicators

- SMA 50/200 require enough observations or return unavailable.
- RSI period is documented.
- ATR requires OHLC data.
- Z-Score handles zero standard deviation.
- Hurst returns unavailable for insufficient sample.
- Correlation handles mismatched series.

### AC-11.5 Architecture

- Analytics module does not reference Avalonia.
- Analytics functions are unit-testable.
- Metrics return typed result objects, not magic numbers.
- Metric metadata includes:
  - name;
  - value;
  - unit;
  - availability;
  - explanation;
  - severity optional.

## Tests

### Unit

- Average cost after multiple buys.
- Average cost after sell.
- Fees included/excluded according to documented rule.
- P&L positive/negative/zero.
- ROI zero denominator.
- Allocation sums.
- Volatility fixed dataset.
- Max drawdown fixed dataset.
- Sharpe fixed dataset.
- Sortino fixed dataset.
- VaR/CVaR fixed dataset.
- SMA fixed dataset.
- RSI fixed dataset.
- ATR fixed dataset.
- Z-score fixed dataset.
- Correlation fixed dataset.
- Insufficient data returns unavailable.

### Integration

- Dashboard service uses analytics engine.
- Asset details service uses analytics engine.
- Seed data analytics results are internally consistent.

### Review

- Metric formulas documented in `docs/analytics-formulas.md`.
- Assumptions documented:
  - risk-free rate;
  - period;
  - annualization;
  - data frequency.

---

# Module 12 — Goals & Compound Interest Forecast

## User Stories

### US-12.1 — User creates financial goals
As a user, I want to create goals with target amounts, so that I can track progress toward major purchases or financial freedom.

### US-12.2 — User forecasts time to goal
As a user, I want to enter monthly contribution and expected return, so that I can see when I will reach the target.

### US-12.3 — User receives motivation through visualization
As a visual user, I want a clear forecast chart, so that I feel motivated to keep investing instead of impulse spending.

## Acceptance Criteria

### AC-12.1 Goal CRUD

- User can create goal with:
  - title;
  - target amount;
  - target currency;
  - monthly contribution;
  - expected annual return optional;
  - target date optional.
- Title required.
- Target amount must be positive.
- Monthly contribution cannot be negative.
- Goal appears in goals list after save.
- User can edit and archive/delete goal.
- Three-dot menu exists per goal.

### AC-12.2 Forecast input

- Monthly contribution field exists and is easy to change.
- Quick amount buttons may exist:
  - +100;
  - +500;
  - +1000;
  - custom.
- Expected annual return can be edited or inferred from portfolio history if implemented.
- Forecast updates when input changes.
- Zero return is handled correctly.

### AC-12.3 Forecast calculation

- Uses compound interest formula.
- Monthly compounding.
- Current portfolio value included.
- Contributions applied monthly.
- Returns time to goal:
  - months;
  - estimated date;
  - projected final value.
- If goal cannot be reached under inputs, shows clear message.
- Negative expected return handled with warning.

### AC-12.4 Visualization

- Chart shows:
  - current value;
  - projected value curve;
  - target line;
  - estimated reach point.
- Empty state if no portfolio/goal.
- Tooltip explains projection assumptions.
- Forecast is labeled as not guaranteed.

## Tests

### Unit

- Compound interest with positive rate.
- Compound interest with zero rate.
- Monthly contribution only.
- Current capital only.
- Time-to-goal calculation.
- Unreachable goal handling.
- Negative return handling.
- Goal validation.

### Integration

- Create goal persists.
- Edit goal persists.
- Forecast query uses current portfolio value.
- Goal list scoped to current portfolio.

### UI

- Add goal modal validation.
- Goal appears after save.
- Monthly contribution change updates chart.
- Goal menu edit/delete works.
- Forecast chart empty state.

---

# Module 13 — Taxes for Belarus Draft Module

## User Stories

### US-13.1 — User sees tax obligation overview
As a Belarus resident investor, I want to see a clear estimated tax summary, so that I can prepare for declaration without manual spreadsheets.

### US-13.2 — User sees currency and dividend effects
As a user, I want currency differences and dividends shown separately, so that I can understand what affects my tax base.

### US-13.3 — User can export a draft tax report
As a user, I want to export a PDF report, so that I can review or share the calculation.

### US-13.4 — Developer can update tax rules
As a developer, I want tax rules versioned and configurable, so that changing legislation does not require rewriting the whole module.

## Acceptance Criteria

### AC-13.1 Tax page layout

Tax page shows bento cards:

- Total Tax Due.
- Taxable Base.
- Tax Saved / exemptions.
- Crypto status.
- Dividends / double taxation.
- Currency effect.
- Surtax/gauge if applicable.
- Deadlines.
- Deductions.
- Loss carryforward.
- Calculation version.
- Draft/legal disclaimer.

### AC-13.2 Tax inputs

- User can select:
  - portfolio;
  - report year;
  - legal/tax profile type.
- Supported legal profile enum:
  - PhysicalPerson;
  - SelfEmployed;
  - IndividualEntrepreneur;
  - LLC;
  - JSC;
  - Other.
- Missing tax profile shows setup prompt.
- Missing transaction history shows empty state.

### AC-13.3 Exchange rates

- Exchange rate provider abstraction exists.
- NBRB provider interface exists.
- If real provider not implemented, mock provider is clearly labeled.
- Exchange rate errors show recoverable UI error.
- Cached exchange rates may be used if available.
- Rate source/date shown in calculation details.

### AC-13.4 Calculation

- Tax calculation is deterministic for same inputs.
- Tax rules have version/date metadata.
- Calculation separates:
  - realized gains;
  - dividends;
  - fees/commissions if accounted;
  - currency effects;
  - losses;
  - exemptions.
- Report clearly says “draft / informational”.
- No outdated law claim is hardcoded as guaranteed current law.

### AC-13.5 Export

- Export button exists if reporting module implemented.
- Generated PDF includes:
  - portfolio;
  - year;
  - calculation summary;
  - transactions summary;
  - exchange rate notes;
  - disclaimer.
- Export failure shows error.

## Tests

### Unit

- Tax profile validation.
- Tax rule version selection.
- Basic realized gain calculation.
- Loss offset handling if implemented.
- Dividend handling.
- Currency conversion by transaction date.
- Missing exchange rate returns recoverable error.
- Draft disclaimer included in report model.

### Integration

- Tax report generated from seeded transactions.
- Tax report persists.
- Exchange rate provider called with expected dates/currencies.
- Cached exchange rate used when provider unavailable if implemented.

### UI

- Year selector updates report.
- Tax profile prompt shown if missing.
- Tax cards render.
- Export button generates file or shows not implemented state.
- Error state for provider failure.

### Review

- `docs/tax-module-notes.md` documents assumptions and limitations.
- No final legal guarantee language.

---

# Module 14 — Settings, Profile, Preferences & Configuration

## User Stories

### US-14.1 — User edits profile
As a user, I want to edit my profile and avatar, so that the sidebar reflects my identity.

### US-14.2 — User configures display preferences
As a user, I want to choose language, base currency and UI scale, so that Proxima fits my workflow.

### US-14.3 — User configures quote providers
As an advanced user, I want to configure quote update intervals and providers, so that I can manage API limits.

### US-14.4 — User configures security and sync
As a privacy-first user, I want clear security and encrypted sync settings, so that I can control where my data goes.

## Acceptance Criteria

### AC-14.1 Profile

- Settings page shows:
  - avatar;
  - display name;
  - role;
  - login;
  - preferred currency;
  - language;
  - UI scale.
- User can update display name.
- User can update role if allowed.
- Avatar can be selected or placeholder generated.
- Sidebar updates after profile changes.

### AC-14.2 Preferences

- Language options:
  - RU;
  - EN.
- Base currency options include:
  - USD;
  - BYN.
- UI scale can be changed within safe range.
- Preferences persist.
- Invalid settings rejected.

### AC-14.3 Provider settings

- User can view quote provider status.
- User can configure refresh interval.
- User can enter API key only through secure storage path if implemented.
- API key is never shown after saving except masked.
- Missing provider config falls back to mock/demo mode with label.

### AC-14.4 Security settings

- User can change local password.
- Changing password requires current password.
- Password change re-encrypts necessary key material/snapshots if applicable.
- User can see security notes.
- User can lock app manually if implemented.

### AC-14.5 Sync settings

- Google Drive sync is presented as encrypted snapshot sync.
- Raw DB sync is not offered.
- User can export encrypted snapshot.
- User can import encrypted snapshot.
- Sync status and last snapshot time shown.
- Conflict warning shown when importing older snapshot.

## Tests

### Unit

- Preferences validation.
- UI scale bounds.
- Language setting persistence model.
- Password change validator.
- Refresh interval validation.

### Integration

- Profile update persists.
- Preferences persist across app restart simulation.
- Password change updates auth hash.
- Settings scoped to current user.

### UI

- Settings page loads.
- Display name edit updates sidebar.
- Language/base currency controls save.
- UI scale does not break layout.
- API key field masked.
- Export snapshot button behavior.

### Security

- API key not logged.
- Password change does not log password.
- Raw DB sync option absent.

---

# Module 15 — Encrypted Snapshot Sync & Google Drive Adapter

## User Stories

### US-15.1 — User backs up data privately
As a user, I want to export an encrypted snapshot, so that I can back up my data without exposing financial information.

### US-15.2 — User restores data on another device
As a user, I want to import an encrypted snapshot, so that I can move my data while keeping it protected.

### US-15.3 — User syncs through Google Drive safely
As a user, I want optional Google Drive synchronization of encrypted snapshots, so that I can use multiple devices without uploading plaintext financial data.

## Acceptance Criteria

### AC-15.1 Snapshot export

- Export produces one encrypted snapshot file.
- Snapshot contains schema version.
- Snapshot contains app version.
- Snapshot contains created timestamp.
- Snapshot contains source device id.
- Snapshot contains checksum.
- Snapshot payload is encrypted and compressed if practical.
- Plain JSON export is not produced unless explicit developer debug mode and excluded from production.

### AC-15.2 Snapshot encryption

- AES-256-GCM or equivalent authenticated encryption.
- Unique nonce per snapshot.
- Key derived from password or secure key material.
- Wrong password fails safely.
- Tampered snapshot fails integrity check.
- Snapshot does not expose portfolio data in filename or metadata.

### AC-15.3 Snapshot import

- User selects snapshot file.
- App verifies format/version.
- App verifies checksum/integrity.
- App decrypts after password/key verification.
- App previews import metadata before applying.
- Import detects conflicts:
  - older snapshot;
  - same device stale snapshot;
  - schema mismatch.
- Import can be cancelled.
- Import applies data transactionally.

### AC-15.4 Google Drive adapter

- Adapter abstraction exists.
- Google Drive implementation may be stubbed if OAuth not completed.
- If implemented:
  - only encrypted snapshot uploaded;
  - OAuth tokens stored securely;
  - upload/download errors are recoverable.
- UI never suggests syncing PostgreSQL data folder.

## Tests

### Unit

- Snapshot metadata serialization.
- Encrypt/decrypt roundtrip.
- Wrong password fails.
- Tampered payload fails.
- Conflict detection.
- Schema version compatibility check.

### Integration

- Export snapshot from seeded DB.
- Import snapshot into empty DB.
- Import rollback on failure.
- Sync metadata persists.

### UI

- Export snapshot flow.
- Import snapshot preview.
- Wrong password error.
- Conflict warning.
- Google Drive unavailable/stub message.

### Security

- Snapshot file inspected as binary/encrypted.
- No plaintext transaction strings visible.
- Logs redacted.

---

# Module 16 — Reporting & PDF Export

## User Stories

### US-16.1 — Consultant exports client portfolio report
As a financial consultant, I want to generate a clean PDF portfolio report, so that I can send professional updates to clients.

### US-16.2 — Investor exports tax report
As an investor, I want to export a tax draft PDF, so that I can review calculations outside the app.

### US-16.3 — User previews report before saving
As a user, I want to preview report contents, so that I can avoid exporting incorrect information.

## Acceptance Criteria

### AC-16.1 Report generator abstraction

- Report service interface exists.
- Report generator is independent from Avalonia Views.
- Report templates are versioned or clearly organized.
- Report generation works without taking screenshots of UI.
- PDF output path chosen safely.

### AC-16.2 Portfolio report

Includes:

- portfolio name;
- period;
- total value;
- P&L;
- allocation chart/table;
- top assets;
- risk metrics;
- transaction summary;
- generation timestamp;
- disclaimer if applicable.

### AC-16.3 Tax report

Includes:

- user/tax profile summary;
- year;
- taxable base;
- total tax due;
- currency/exchange rate notes;
- dividend notes;
- transaction summary;
- calculation version;
- legal disclaimer.

### AC-16.4 Export UX

- Export button visible where relevant.
- User can choose destination.
- Success notification shows path.
- Failure notification shows reason.
- Long generation shows progress/loading state.
- Existing file overwrite requires confirmation.

## Tests

### Unit

- Report model validation.
- Report filename sanitization.
- Report template includes required sections.
- Disclaimer included.

### Integration

- Generate portfolio PDF from seed data.
- Generate tax PDF from seed tax report.
- File exists and non-empty.
- Export failure handled.

### UI

- Export button starts generation.
- Success notification.
- Failure notification.
- Preview displays expected sections if implemented.

---

# Module 17 — Persistence, Data Integrity & Migrations

## User Stories

### US-17.1 — User data persists locally
As a user, I want my portfolios, assets, transactions and settings to persist between launches, so that Proxima is reliable.

### US-17.2 — User data survives failures
As a user, I want Proxima to protect data integrity during crashes or failed imports, so that partial operations do not corrupt my portfolio.

### US-17.3 — Developer can evolve schema
As a developer, I want EF Core migrations, so that database changes are tracked and repeatable.

## Acceptance Criteria

### AC-17.1 PostgreSQL setup

- `docker-compose.yml` exists for local dev.
- Connection string configured through safe config path.
- App handles unavailable DB gracefully.
- Migrations can be applied.
- Seed data can be created.

### AC-17.2 Schema

Required tables exist:

- users;
- portfolios;
- assets;
- tags;
- asset_tags;
- transactions;
- asset_prices;
- goals;
- tax_profiles;
- tax_reports;
- import_sessions;
- import_rows;
- quote_cache;
- sync_snapshots;
- audit_log.

### AC-17.3 Integrity

- Foreign keys enforce relationships.
- Transactions cannot reference asset from different portfolio.
- Batch import uses DB transaction.
- Decimal precision configured explicitly.
- Timestamps use `timestamptz`/`DateTimeOffset`.
- Indexes exist for dashboard-critical queries.
- No cascade delete causing surprise data loss without explicit design.

### AC-17.4 Migrations

- Initial migration committed.
- Migration command documented.
- App can apply migrations or documentation clearly tells user how.
- Schema version mismatch handled.

### AC-17.5 Seed

- Demo user exists in development.
- Demo portfolio includes:
  - stocks;
  - crypto;
  - cash;
  - transactions;
  - prices;
  - goals;
  - tax profile.
- Seed can be disabled for production.

## Tests

### Unit

- Entity configuration tests where practical.
- Decimal precision config review/test.
- Cross-portfolio validation logic.

### Integration

- Start PostgreSQL test DB.
- Apply migrations.
- Create/read/update/delete core entities.
- Batch import rollback.
- Seed data loads.
- Dashboard query works on seeded DB.

### Manual

- Stop DB and launch app.
- App shows DB error/setup guidance, not crash.
- Restart app and confirm persisted data.

---

# Module 18 — Localization, Accessibility & UI Scaling

## User Stories

### US-18.1 — User can use Russian or English UI
As a user, I want Russian and English interface support, so that Proxima is usable in my preferred language.

### US-18.2 — User can scale the interface
As a user with different monitor sizes or visual needs, I want UI scaling, so that text and controls remain readable.

### US-18.3 — Keyboard user can navigate core flows
As a keyboard user, I want visible focus and logical tab order, so that I can use Proxima without a mouse.

## Acceptance Criteria

### AC-18.1 Localization

- Resource structure exists for RU and EN.
- Core navigation labels localized.
- Main page titles localized.
- Common buttons localized.
- Error messages localized where practical.
- Language switch persists.
- Language switch does not require reinstall.

### AC-18.2 UI scale

- UI scale setting exists.
- Safe range defined.
- Scale persists.
- Major screens remain usable at min/max scale.
- Text does not clip in common controls.

### AC-18.3 Accessibility

- Focus visuals visible.
- Buttons have clear labels.
- Inputs have labels.
- Charts have textual summaries.
- Color is not the only meaning carrier.
- Critical numbers include labels/units.
- Modal focus behavior is acceptable.

## Tests

### Unit

- Localization key lookup fallback.
- Missing localization key test if implemented.
- UI scale bounds validation.

### UI

- Switch language RU/EN.
- Sidebar/topbar labels update.
- Increase/decrease UI scale.
- Keyboard tab through login.
- Keyboard tab through topbar/sidebar.
- Focus visible.

### Manual

- Review charts for textual summaries.
- Review contrast of text and semantic statuses.

---

# Module 19 — Error Handling, Notifications & Audit Logging

## User Stories

### US-19.1 — User sees recoverable errors clearly
As a user, I want clear non-technical error messages, so that I understand what happened and what to do next.

### US-19.2 — Developer can diagnose issues safely
As a developer, I want structured logs and audit events without sensitive data, so that problems can be diagnosed without privacy leaks.

### US-19.3 — User sees background operation status
As a user, I want import, quote update and sync statuses, so that I know whether long-running operations succeeded.

## Acceptance Criteria

### AC-19.1 Notifications

- App has notification/toast service.
- Supports:
  - success;
  - warning;
  - error;
  - info.
- Long operations show loading/progress.
- Errors do not block unrelated navigation unless critical.
- Critical errors provide next action.

### AC-19.2 Error boundaries

- DB unavailable handled.
- Quote provider failure handled.
- Import parser failure handled.
- Sync failure handled.
- Report generation failure handled.
- Unexpected exceptions logged and shown as generic message.

### AC-19.3 Logging

- Structured logging configured.
- Sensitive data redacted.
- Logs include operation id/import session id where relevant.
- Logs do not include:
  - passwords;
  - API keys;
  - raw broker file content;
  - full transaction history;
  - plaintext encrypted payloads.

### AC-19.4 Audit log

Audit event created for:

- profile created;
- portfolio created/updated/archived;
- import completed/failed;
- tax report generated;
- snapshot exported/imported;
- password changed.

Audit metadata must be non-sensitive.

## Tests

### Unit

- Error-to-user-message mapper.
- Redaction helper.
- Notification ViewModel.
- Audit event factory.

### Integration

- Failed quote update logs redacted event.
- Failed import creates import failure state.
- Audit event persists after portfolio create.
- Password change audit contains no password.

### UI

- Error toast visible.
- Success toast visible after save.
- Import progress visible.
- Quote update failure non-blocking.

### Security

- Log sink test verifies secret values absent.

---

# Module 20 — Final Release Readiness

## User Stories

### US-20.1 — Reviewer can run the app
As a reviewer, I want clear setup instructions, so that I can run Proxima locally without reverse engineering.

### US-20.2 — Reviewer can verify functionality
As a reviewer, I want demo data and acceptance checklist, so that I can evaluate the app quickly.

### US-20.3 — Maintainer can continue development
As a maintainer, I want known limitations and next steps documented, so that future work is clear.

## Acceptance Criteria

### AC-20.1 README

README includes:

- project overview;
- tech stack;
- prerequisites;
- PostgreSQL setup;
- migrations;
- run command;
- test command;
- demo credentials;
- security notes;
- Figma note;
- known limitations.

### AC-20.2 Final checks

- `dotnet format` run.
- `dotnet build` succeeds.
- `dotnet test` succeeds.
- `dotnet list package --vulnerable` run or documented.
- App manually launched.
- Major screens manually checked.
- Git working tree clean or explained.
- UI on Mockup-critical screens is compared against custom `figma_mcp_server` Mockup nodes and has no critical visual regressions (text overflow, clipped controls, broken hierarchy, missing core iconography/colors).
- Asset Details contains a visible candlestick chart (not text-only placeholder).
- Dashboard/Goals contain real chart widgets (not summary-only text placeholders).

### AC-20.3 Documentation

Docs exist:

- `docs/figma-inspection.md`;
- `docs/architecture.md` or equivalent;
- `docs/security.md`;
- `docs/analytics-formulas.md`;
- `docs/tax-module-notes.md`;
- `docs/known-limitations.md`;
- `docs/iteration-log.md`.

### AC-20.4 Final report

Final agent response includes:

- implemented modules;
- partial modules;
- commands run;
- test results;
- setup instructions;
- demo credentials;
- git log;
- known issues.

## Tests

### Full Regression

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build
dotnet test
dotnet list package --vulnerable
```

### Manual Smoke

- Start PostgreSQL.
- Apply migrations.
- Run app.
- Login.
- Open Dashboard.
- Open Assets.
- Add transaction.
- Import CSV.
- Open Asset Details.
- Verify candlestick chart renders in Asset Details.
- Create Goal.
- Verify at least one chart is rendered in Dashboard and Goals screens.
- Open Taxes.
- Open Settings.
- Export snapshot if implemented.
- Export PDF if implemented.

### Review

- Compare UI to Figma.
- Compare specifically against Mockup page through custom `figma_mcp_server`.
- Inspect logs for sensitive data.
- Inspect git history.
- Inspect README.
