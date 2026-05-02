---
name: avalonia-ui-refactor
description: Use when splitting Proxima's legacy monolithic MainWindow/ShellViewModel into maintainable Avalonia MVVM views and view models while preserving existing behavior.
---

# Goal

Recover the existing project without restarting. Apply strangler refactor: create new views and view models beside legacy UI, migrate screen by screen, then remove legacy UI after parity.

# Rules

- Do not delete domain/application/analytics/import/reporting/sync code.
- Do not add new features while refactoring UI structure.
- Do not add new page content to `MainWindow.axaml`.
- Keep code-behind limited to `InitializeComponent` and unavoidable view-only UI plumbing.
- Move click handlers to commands in view models.
- Extract one screen per task.
- Preserve tests or add regression tests before replacing behavior.

# Required target structure

Use `.codex/handoff/17_UI_RECOVERY_PLAN.md`.

# Done when

- New screen exists under `src/Proxima.App/Views/...`.
- View model exists under `src/Proxima.App/ViewModels/...` or screen-specific folder.
- Screen does not depend on legacy `ShellViewModel` internals except through a temporary adapter explicitly named `Legacy...Adapter`.
- Build/tests pass.
- Style guard passes for the new screen.
