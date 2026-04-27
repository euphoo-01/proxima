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

Pending
