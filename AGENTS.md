# AGENTS.md — Proxima

## Project role

You are working on **Proxima**, a local-first desktop financial analytics application for private investors and financial consultants.

The app must be built as a production-oriented desktop application, not as a prototype.

## Project docs location

Detailed project instructions are stored in:

- `.codex/handoff/00_MASTER_PROMPT.md`
- `.codex/handoff/01_PRODUCT_CONTEXT.md`
- `.codex/handoff/02_REQUIREMENTS_SPEC.md`
- `.codex/handoff/03_DESIGN_SYSTEM_BENTO_AVALONIA.md`
- `.codex/handoff/04_FIGMA_MCP_PROTOCOL.md`
- `.codex/handoff/05_ARCHITECTURE_AVALONIA_CSHARP.md`
- `.codex/handoff/06_DATABASE_POSTGRES.md`
- `.codex/handoff/07_SECURITY_PRIVACY.md`
- `.codex/handoff/08_FEATURE_SPECS.md`
- `.codex/handoff/09_GIT_WORKFLOW.md`
- `.codex/handoff/10_ENGINEERING_STANDARDS.md`
- `.codex/handoff/11_ACCEPTANCE_CHECKLIST.md`
- `.codex/handoff/12_IMPLEMENTATION_PLAN.md`
- `.codex/handoff/13_MODULE_US_AC_TESTS.md`
- `.codex/handoff/14_ITERATION_QUALITY_GATE.md`

Before implementing anything, read:

1. `.codex/handoff/00_MASTER_PROMPT.md`
2. `.codex/handoff/13_MODULE_US_AC_TESTS.md`
3. `.codex/handoff/14_ITERATION_QUALITY_GATE.md`
4. Any module-specific docs relevant to the current task.

If requirements conflict, priority is:

1. this `AGENTS.md`;
2. `.codex/handoff/00_MASTER_PROMPT.md`;
3. `.codex/handoff/14_ITERATION_QUALITY_GATE.md`;
4. `.codex/handoff/13_MODULE_US_AC_TESTS.md`;
5. other handoff docs;
6. existing implementation.

## Product name

The application is called **Proxima**.

Do not use the old name `WealthVision` in:

- UI;
- namespaces;
- project names;
- file names;
- README;
- installer metadata;
- database seed display names;
- documentation, except when explicitly explaining migration from old analysis notes.

## Non-negotiable stack

Use:

- Avalonia UI;
- C#;
- .NET LTS;
- PostgreSQL;
- EF Core / Npgsql;
- Clean Architecture;
- MVVM.

Do not use:

- React;
- Vue;
- Nuxt;
- Electron;
- Tauri;
- web-wrapper architecture;
- browser-first app architecture.

## Architecture rules

Use Clean Architecture with clear project boundaries.

Expected high-level structure:

```text
src/
  Proxima.App/
  Proxima.Domain/
  Proxima.Application/
  Proxima.Infrastructure/
  Proxima.Analytics/
  Proxima.Importing/
  Proxima.Reporting/
  Proxima.Sync/

tests/
  Proxima.Domain.Tests/
  Proxima.Application.Tests/
  Proxima.Infrastructure.Tests/
  Proxima.Analytics.Tests/
  Proxima.Importing.Tests/
  Proxima.App.Tests/
````

Dependency rules:

* `Proxima.Domain` must not reference Avalonia, EF Core, Npgsql, HTTP clients, file system APIs, or UI frameworks.
* `Proxima.Application` may reference `Domain`.
* `Infrastructure` implements application abstractions.
* `App` composes dependencies and contains Avalonia Views/ViewModels.
* Business logic must not live in Views or code-behind.
* ViewModels must not contain SQL, HTTP calls, cryptography internals, or complex financial formulas.
* Financial calculations belong in `Proxima.Analytics` or domain/application services.
* Import logic belongs in `Proxima.Importing`.
* PDF/report generation belongs in `Proxima.Reporting`.
* Sync logic belongs in `Proxima.Sync`.

Apply SOLID, DRY, KISS and YAGNI pragmatically.

## UI and Figma rules

The UI must be a light, modern, trustworthy **bento UI**.

Before implementing UI, use Figma MCP according to:

* `.codex/handoff/04_FIGMA_MCP_PROTOCOL.md`
* `.codex/handoff/03_DESIGN_SYSTEM_BENTO_AVALONIA.md`

Figma source:

```text
https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0
```

File key:

```text
Drxcen3JN69XP0fnYxkgOi
```

Starting node:

```text
62:497
```

If Figma MCP is unavailable:

* continue from `.codex/handoff/03_DESIGN_SYSTEM_BENTO_AVALONIA.md`;
* document the limitation in `.codex/known-limitations.md`;
* do not claim pixel-perfect Figma matching.

All authenticated pages must use:

* Sidebar;
* Topbar;
* bento card layout;
* readable tables;
* visible loading, empty and error states.

## Security and privacy rules

Proxima is local-first and privacy-first.

Do not:

* store passwords in plaintext;
* commit secrets or API keys;
* log passwords, API keys, raw broker reports, tokens, full transaction history, or plaintext encrypted payloads;
* send portfolio composition, transaction history, broker reports, tax reports, or raw database data to third-party services;
* sync the raw PostgreSQL data directory through Google Drive.

Use:

* local password gate;
* password hashing with salt;
* app-level encryption for sensitive fields/snapshots;
* encrypted snapshots for backup/sync;
* redacted structured logs;
* safe parser behavior for imported files.

Google Drive sync, if implemented, must upload/download only encrypted application snapshots.

## Iteration protocol

Each iteration implements exactly one module from:

```text
.codex/handoff/13_MODULE_US_AC_TESTS.md
```

An iteration is not complete until all of the following are done:

1. relevant User Stories are identified;
2. implementation is completed for the selected module scope;
3. every Acceptance Criterion is checked;
4. tests are added or updated;
5. tests are executed;
6. failures are fixed or explicitly documented as known limitations;
7. `.codex/iteration-log.md` is updated;
8. changes are committed using Conventional Commits.

Do not move to the next module until the current module passes:

```text
.codex/handoff/14_ITERATION_QUALITY_GATE.md
```

## Required iteration log

Maintain:

```text
.codex/iteration-log.md
```

For every iteration, record:

````md
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
- Manual:

### Commands Run
```bash
dotnet format
dotnet build
dotnet test
git status --short
````

### Result

Done / Partial / Failed

### Known Limitations

...

### Commit

<hash> <message>

````

## Testing rules

Before closing every module iteration, run:

```bash
dotnet format
dotnet build
dotnet test
git status --short
````

For every module:

* add unit tests for domain/application logic;
* add integration tests for database/import/sync behavior where feasible;
* add UI/headless tests for Avalonia behavior where feasible;
* document manual smoke tests when UI automation is impractical.

Do not mark an iteration complete if critical tests fail.

## Git rules

Use Git throughout development.

Every meaningful iteration must end with a commit.

Use Conventional Commits:

```text
<type>(<scope>): <summary>
```

Allowed types:

* `feat`
* `fix`
* `refactor`
* `test`
* `docs`
* `style`
* `build`
* `ci`
* `chore`
* `perf`
* `security`

Examples:

```bash
git commit -m "feat(shell): add sidebar and topbar navigation"
git commit -m "feat(db): add portfolio and transaction schema"
git commit -m "test(analytics): cover roi and max drawdown calculations"
git commit -m "security(auth): hash local password with salt"
git commit -m "docs(iteration): record dashboard acceptance results"
```

Do not create one giant final commit.

Commit documentation updates too.

## Documentation update rules

The files in `.codex/handoff/` are not read-only.

If implementation reveals a better decision, outdated assumption, missing acceptance criterion, or technical limitation:

* update the relevant handoff file;
* update `.codex/iteration-log.md`;
* update `.codex/known-limitations.md` if needed;
* commit the documentation change.

## Database rules

Use local PostgreSQL.

Requirements:

* EF Core migrations;
* Npgsql provider;
* local dev `docker-compose.yml`;
* seed/demo data;
* transaction-safe imports;
* decimal/numeric for money;
* timestamps with timezone-aware types;
* indexes for dashboard-critical queries.

Do not store local PostgreSQL data directory in Git.

## Feature scope

The application must support, at minimum:

* local login/unlock;
* portfolio management;
* asset management;
* transaction management;
* CSV import;
* PDF import pipeline/fallback;
* manual import fallback;
* quote provider abstraction;
* offline quote cache;
* dashboard;
* asset details;
* analytics metrics;
* goals and compound interest forecast;
* Belarus tax draft page;
* settings;
* encrypted snapshot sync abstraction;
* reporting/PDF export if feasible.

## Final response requirements

At the end of work or a major milestone, report:

* modules completed;
* modules partial/deferred;
* acceptance criteria status;
* tests added;
* commands run;
* test results;
* setup instructions;
* demo credentials if created;
* git log summary;
* known limitations.

Also include:

```bash
git status --short
git log --oneline --decorate --graph --all
```

````

И рядом создай `.codex/known-limitations.md` и `.codex/iteration-log.md`, даже пустые:

```bash
mkdir -p .codex/handoff
touch .codex/known-limitations.md
touch .codex/iteration-log.md
````

Потом закоммить:

```bash
git add AGENTS.md .codex
git commit -m "docs(codex): add project agent instructions"
```
