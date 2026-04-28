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

Pending
