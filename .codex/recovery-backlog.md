# Recovery Backlog

## Sources
- Primary: `.codex/known-limitations.md` (lines 3-54), `.codex/iteration-log.md`, `.codex/handoff/13_MODULE_US_AC_TESTS.md`, `.codex/handoff/14_ITERATION_QUALITY_GATE.md`, `AGENTS.md`.
- Additional expected source: `backend-integration-audit.md` is missing in repository (`source missing`, `needs investigation`).

## Triage Notes
- Deduplication rule: semantically equivalent mock/stub/deferred items are merged into one REC with multiple source bullets.
- Status flags:
  - `needs investigation`: source is ambiguous or missing objective artifact.
  - `probably resolved, needs verification`: later iterations partially address earlier limitation.

## Recovery order

### Phase 1 - Global blockers
1. `REC-001` Build pipeline baseline
2. `REC-002` Test-suite baseline
3. `REC-003` DI/composition root and AppShell routing stability
4. `REC-004` PostgreSQL/EF runtime foundation switch (from JSON)
5. `REC-005` Runtime routes/screens open and execute end-to-end
6. `REC-006` Replace runtime mocks/placeholders with real application services

### Phase 2 - Runtime integration blockers
7. `REC-007` Auth runtime integration and recovery commands
8. `REC-008` Import runtime persistence and drag-and-drop wiring
9. `REC-009` Quote/OHLC real provider integration and history persistence
10. `REC-010` Settings/Auth/Sync command integration and owner identity binding

### Phase 3 - Module-specific recovery
11. `REC-011` Tax/legal coverage hardening and verified artifacts
12. `REC-012` Analytics accuracy hardening (lot matching, FX, realized P&L)
13. `REC-013` Goals progress baseline and forecasting controls
14. `REC-014` Reporting UX completion (preview/picker/overwrite flow)
15. `REC-015` Localization expansion beyond shell strings
16. `REC-016` UI mockup parity verification closure

### Phase 4 - Quality/test hardening
17. `REC-017` Style-guard false positives and rule alignment
18. `REC-018` Structured observability completion

## REC-001 - Build pipeline baseline
- Source:
  - Added by recovery policy as first global gate (`13_MODULE_US_AC_TESTS`, `14_ITERATION_QUALITY_GATE`).
  - Related iteration/module: global precondition across all modules.
- Type: Quality gate
- Area: Build
- Severity: Critical
- Dependency: Must be fixed before all other REC items.
- Acceptance criteria:
  - [ ] `dotnet build` exits with code 0 in clean workspace.
  - [ ] No critical warnings-as-errors bypass introduced.
- Verification: `dotnet build` -> `Build succeeded`.
- Recommended commit: `fix(build): restore full solution build stability`

## REC-002 - Test-suite baseline
- Source:
  - Added by recovery policy as second global gate (`13_MODULE_US_AC_TESTS`, `14_ITERATION_QUALITY_GATE`).
  - Related iteration/module: global precondition across all modules.
- Type: Quality gate
- Area: Testing
- Severity: Critical
- Dependency: Depends on `REC-001`; must be fixed before feature recovery REC items.
- Acceptance criteria:
  - [ ] `dotnet test` exits with code 0 for solution.
  - [ ] No critical module tests are skipped without documented limitation.
- Verification: `dotnet test` -> all test runners pass.
- Recommended commit: `test(regression): restore full test-suite baseline`

## REC-003 - Composition root + AppShell route integrity
- Source:
  - `.codex/known-limitations.md:48-49,50` (temporary owner identity, placeholder command wiring, AppShell as mandatory host).
  - Related iteration/module: Iteration 28+, shell/app runtime integration.
- Type: Integration gap
- Area: App composition/runtime
- Severity: Critical
- Dependency: Depends on `REC-001`,`REC-002`; blocks `REC-005`,`REC-010`.
- Acceptance criteria:
  - [ ] Runtime owner/user context comes from authenticated profile.
  - [ ] AppShell routes resolve dependencies without placeholder command paths.
  - [ ] Shell launch/login/unlock flow has automated smoke coverage.
- Verification: `dotnet test tests/Proxima.App.Tests/Proxima.App.Tests.csproj`.
- Recommended commit: `fix(shell): complete appshell composition with authenticated owner context`

## REC-004 - PostgreSQL/EF runtime repository switch
- Source:
  - `.codex/known-limitations.md:5,7,10,13,29,31-32`.
  - Related iteration/module: Modules 02/04/06/07/15/17.
- Type: Persistence gap
- Area: Infrastructure/Database
- Severity: Critical
- Dependency: Depends on `REC-001`,`REC-002`; blocks `REC-006`,`REC-008`,`REC-009`,`REC-012`.
- Acceptance criteria:
  - [ ] Runtime repositories use PostgreSQL-backed implementations by default.
  - [ ] Migration pipeline is executable and reproducible.
  - [ ] Import/restore rollback guarantees are transactional.
- Verification: `dotnet test tests/Proxima.Infrastructure.Tests/Proxima.Infrastructure.Tests.csproj`.
- Recommended commit: `feat(db): switch runtime repositories from json to postgresql`

## REC-005 - Runtime route/screen readiness end-to-end
- Source:
  - `.codex/known-limitations.md:6,41-42,37`.
  - Related iteration/module: shell/import/ui recovery chain.
- Type: Runtime flow gap
- Area: UI/App runtime
- Severity: Critical
- Dependency: Depends on `REC-003`,`REC-004`.
- Acceptance criteria:
  - [ ] Portfolio switch triggers cross-screen reload/invalidation.
  - [ ] Import flows persist committed rows to real transaction store.
  - [ ] Route-level loading/empty/error states are functional on all authenticated pages.
- Verification: `dotnet test tests/Proxima.App.Tests/Proxima.App.Tests.csproj`.
- Recommended commit: `fix(runtime): complete authenticated route flow wiring`

## REC-006 - Replace runtime mocks/placeholders with real services
- Source:
  - `.codex/known-limitations.md:14,19,25,39,43-44,54`.
  - Related iteration/module: Modules 08/10/13 + runtime recovery.
- Type: Data integrity gap
- Area: Application integration
- Severity: Critical
- Dependency: Depends on `REC-004`; blocks production-grade analytics/tax/reporting claims.
- Acceptance criteria:
  - [ ] Auth gate is durable and profile-backed in runtime.
  - [ ] Asset details and goals consume persisted application read models.
  - [ ] Quote/OHLC and tax FX providers have real-provider path and fallback policy.
- Verification: `dotnet test tests/Proxima.Application.Tests/Proxima.Application.Tests.csproj`.
- Recommended commit: `fix(integration): replace runtime mock providers with application services`

## REC-007 - Auth runtime hardening
- Source:
  - `.codex/known-limitations.md:39-40,5`.
  - Related iteration/module: Module 02 + runtime recovery.
- Type: Security gap
- Area: Authentication
- Severity: High
- Dependency: Depends on `REC-004`,`REC-006`.
- Acceptance criteria:
  - [ ] Runtime default does not rely on fixed seeded credentials.
  - [ ] Recovery/reset command has secure workflow or explicit disable rationale.
  - [ ] Auth persistence is bound to durable user profile context.
- Verification: `dotnet test tests/Proxima.Application.Tests/Proxima.Application.Tests.csproj`.
- Recommended commit: `security(auth): harden runtime auth recovery and credential flow`

## REC-008 - Import persistence and UX completion
- Source:
  - `.codex/known-limitations.md:12-13,41-42`.
  - Related iteration/module: Module 07 + AppShell import recovery.
- Type: Runtime integration gap
- Area: Importing
- Severity: High
- Dependency: Depends on `REC-004`,`REC-005`.
- Acceptance criteria:
  - [ ] Native file picker/drag-drop is wired.
  - [ ] Manual import commit persists to transaction repository.
  - [ ] Multi-row import is atomic with rollback on failure.
- Verification: `dotnet test tests/Proxima.Importing.Tests/Proxima.Importing.Tests.csproj`.
- Recommended commit: `fix(import): wire runtime import persistence and atomic commit`

## REC-009 - Real quote/OHLC provider and history storage
- Source:
  - `.codex/known-limitations.md:14-15,17-18,54`.
  - Related iteration/module: Modules 08/09/10.
- Type: Provider integration gap
- Area: Market data
- Severity: High
- Dependency: Depends on `REC-004`,`REC-006`.
- Acceptance criteria:
  - [ ] Historical OHLC snapshots are persisted and queryable.
  - [ ] Real provider integration path is available and configurable.
  - [ ] Dashboard delta and asset chart states derive from historical data.
- Verification: `dotnet test tests/Proxima.Analytics.Tests/Proxima.Analytics.Tests.csproj`.
- Recommended commit: `feat(quotes): add real ohlc provider and persisted history`

## REC-010 - Settings/Auth/Sync command wiring and identity binding
- Source:
  - `.codex/known-limitations.md:48-49,27-29`.
  - Related iteration/module: Modules 14/15 + runtime recovery.
- Type: Runtime command gap
- Area: Settings/Sync
- Severity: High
- Dependency: Depends on `REC-003`,`REC-004`,`REC-007`.
- Acceptance criteria:
  - [ ] Settings commands invoke real auth/sync backends.
  - [ ] Owner identity is derived from authenticated principal.
  - [ ] Snapshot restore supports DB-aware workflow.
- Verification: `dotnet test tests/Proxima.Infrastructure.Tests/Proxima.Infrastructure.Tests.csproj`.
- Recommended commit: `fix(settings): bind runtime settings commands to auth and sync services`

## REC-011 - Tax/legal rule coverage hardening
- Source:
  - `.codex/known-limitations.md:24-25,46-47`.
  - Related iteration/module: Module 13 + runtime taxes recovery.
- Type: Compliance gap
- Area: Taxes
- Severity: High
- Dependency: Depends on `REC-006`,`REC-009`.
- Acceptance criteria:
  - [ ] Tax rule set scope has verified legal references.
  - [ ] Draft/export labeling matches legal confidence level.
  - [ ] FX source path is explicit and test-covered.
- Verification: `dotnet test tests/Proxima.Application.Tests/Proxima.Application.Tests.csproj`.
- Recommended commit: `fix(taxes): harden legal rule coverage and fx source handling`

## REC-012 - Analytics accounting accuracy hardening
- Source:
  - `.codex/known-limitations.md:20-21`.
  - Related iteration/module: Module 11.
- Type: Domain accuracy gap
- Area: Analytics
- Severity: Medium
- Dependency: Depends on `REC-004`,`REC-006`,`REC-009`.
- Acceptance criteria:
  - [ ] Lot-matching strategy is implemented and configurable.
  - [ ] Realized P&L and tax-sensitive branches are deterministic and test-covered.
  - [ ] FX conversion abstraction has production-ready implementation path.
- Verification: `dotnet test tests/Proxima.Analytics.Tests/Proxima.Analytics.Tests.csproj`.
- Recommended commit: `feat(analytics): implement lot matching and fx-backed realized pnl`

## REC-013 - Goals baseline and forecasting controls
- Source:
  - `.codex/known-limitations.md:22-23,44-45`.
  - Status: `probably resolved, needs verification` for chart part from line 22.
  - Related iteration/module: Module 12 + chart rollout.
- Type: Product behavior gap
- Area: Goals
- Severity: Medium
- Dependency: Depends on `REC-006`,`REC-009`.
- Acceptance criteria:
  - [ ] Goal progress baseline comes from per-goal allocated portfolio values.
  - [ ] Forecast supports configurable scenarios/horizon.
  - [ ] Legacy chart limitation is explicitly resolved or reopened with evidence.
- Verification: `dotnet test tests/Proxima.Application.Tests/Proxima.Application.Tests.csproj`.
- Recommended commit: `fix(goals): bind progress baseline and scenario-based forecasting`

## REC-014 - Reporting UX completion
- Source:
  - `.codex/known-limitations.md:30,47`.
  - Related iteration/module: Module 16.
- Type: UX gap
- Area: Reporting
- Severity: Medium
- Dependency: Depends on `REC-011`.
- Acceptance criteria:
  - [ ] Export flow has destination picker and overwrite confirmation.
  - [ ] Preview flow is available or explicitly documented as omitted.
  - [ ] Draft/legal status is visible in exported tax artifacts.
- Verification: `dotnet test tests/Proxima.Reporting.Tests/Proxima.Reporting.Tests.csproj`.
- Recommended commit: `feat(reporting): complete export ux and draft labeling`

## REC-015 - Localization expansion
- Source:
  - `.codex/known-limitations.md:33`.
  - Related iteration/module: Module 18/19.
- Type: Internationalization gap
- Area: UI localization
- Severity: Medium
- Dependency: Depends on `REC-005`.
- Acceptance criteria:
  - [ ] Feature-specific validation/error strings use localization resources.
  - [ ] Language switch applies consistently across all implemented screens.
- Verification: `dotnet test tests/Proxima.App.Tests/Proxima.App.Tests.csproj`.
- Recommended commit: `fix(i18n): expand localization coverage across feature screens`

## REC-016 - UI parity closure with Mockup
- Source:
  - `.codex/known-limitations.md:35-37`.
  - `.codex/known-limitations.md:36` marked `probably resolved, needs verification`.
  - Related iteration/module: UI recovery track.
- Type: Visual parity gap
- Area: UI design compliance
- Severity: Medium
- Dependency: Depends on `REC-005`,`REC-017`.
- Acceptance criteria:
  - [ ] Screen-by-screen comparison notes exist for mockup nodes.
  - [ ] Remaining visual defects are explicit REC/limitations.
  - [ ] Release readiness status has objective evidence.
- Verification: `bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views`.
- Recommended commit: `docs(ui): close mockup parity verification with evidence`

## REC-017 - Style-guard false positives and scope alignment
- Source:
  - `.codex/known-limitations.md:51-53`.
  - `.codex/known-limitations.md:52` marked `needs investigation` (DataGrid theme expectation vs wrapper-based table architecture).
  - Related iteration/module: Iterations 29-32 quality hardening.
- Type: Tooling gap
- Area: UI quality tooling
- Severity: Medium
- Dependency: Depends on `REC-001`,`REC-002`.
- Acceptance criteria:
  - [ ] Scanner rules align with `15_AVALONIA_UI_STYLE_GUARDRAILS.md` allowances.
  - [ ] False positives are eliminated or allowlisted with rationale.
  - [ ] DataGrid expectation is documented for wrapper-based table architecture.
- Verification: `bash scripts/scan-xaml-style-violations.sh src/Proxima.App/DesignSystem src/Proxima.App/Shell src/Proxima.App/Views`.
- Recommended commit: `fix(ui-tooling): align style-guard rules with design-system guardrails`

## REC-018 - Structured observability completion
- Source:
  - `.codex/known-limitations.md:34`.
  - Related iteration/module: Module 19.
- Type: Observability gap
- Area: Logging/Diagnostics
- Severity: Medium
- Dependency: Depends on `REC-003`,`REC-005`.
- Acceptance criteria:
  - [ ] Structured sink is configured for key runtime operations.
  - [ ] Correlation IDs propagate through major flows.
  - [ ] Exception boundaries emit redacted actionable events.
- Verification: `dotnet test tests/Proxima.App.Tests/Proxima.App.Tests.csproj`.
- Recommended commit: `feat(observability): complete structured sink and correlation flow`

## Source-to-REC coverage map
- Covered known-limitations lines: `3-54` (all items mapped).
- Deduplicated groups:
  - Mock/provider limitations (`14,17-19,25,39,43-44,54`) -> `REC-006`/`REC-009`.
  - Style-guard/tooling limitations (`51-53`) -> `REC-017`.
  - UI parity thread (`35-37`) -> `REC-016`.

## Verification snapshot (triage iteration)
- `dotnet build`: Passed (`Build succeeded`, 0 errors).
- `dotnet test`: Passed (Domain/Application/Analytics/Infrastructure/Importing/App test runners passed).
- `git status --short`: captured in iteration log.
