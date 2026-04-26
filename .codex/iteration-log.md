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
| AC-01.1 | Done | `docs/figma-inspection.md` records source URL, file key, starting node, inspected frames, extracted tokens, screenshot limitation and differences from Figma. |
| AC-01.2 | Done | `src/Proxima.App/Styles/{Tokens,Typography,Buttons,Inputs,Cards,Tables,Charts}.axaml` exist and are loaded from `App.axaml`; `Proxima.App.Tests.DesignSystem_FilesExist` verifies core tokens. |
| AC-01.3 | Done | `BentoCard`, `MetricCard`, `StatusPill`, `EmptyState`, `PageHeader`, `SearchBox`, `TimeframeSelector`, `DataTableHeaderCell`; `Proxima.App.Tests.DesignSystem_ControlsExposeBindingProperties` verifies styled properties. |
| AC-01.4 | Partial | Light shell and reusable states exist; full screen-by-screen keyboard/UI smoke awaits feature screens in later modules. |

### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit/Smoke | `tests/Proxima.App.Tests` | Passed |
| UI/XAML Regression | `dotnet build Proxima.sln` | Passed |
| Manual | Figma MCP Tax frame inspection and metadata review | Partial due Starter plan screenshot export limit |

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

Figma screenshot export and variable extraction were blocked by the Figma MCP Starter plan call limit after Tax frame context was retrieved. Pixel-perfect matching is not claimed.

### Commit

6232b1b feat(design-system): add figma-informed bento primitives
