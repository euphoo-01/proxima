# UI Recovery Plan — Proxima

## Diagnosis

Current project contains useful domain/application/analytics/import/reporting code, but the UI implementation is not maintainable enough for Figma parity:

- `MainWindow.axaml` is a monolithic shell and all pages are embedded in one file.
- `MainWindow.axaml.cs` contains many click handlers and service composition.
- `ShellViewModel.cs` is too large and mixes many page states.
- The app uses FluentTheme plus partial Proxima styles, which allows default Avalonia/Fluent visuals to leak.
- There is no per-screen Figma mapping gate.
- There is no XAML style guard.

## Strategy

Do not restart the repository. Use a strangler refactor:

1. Freeze legacy UI.
2. Add guardrails.
3. Extract a new design system and shell next to the legacy shell.
4. Rebuild screens one by one from Figma mappings.
5. Reuse existing services/ViewModel logic where correct.
6. Delete legacy shell only after all screens pass visual and functional gates.

## Files to keep

Keep and improve:

- `src/Proxima.Domain`
- `src/Proxima.Application`
- `src/Proxima.Analytics`
- `src/Proxima.Importing`
- `src/Proxima.Reporting`
- `docker-compose.yml`
- tests, but upgrade them to real xUnit/NUnit later

## Files to stop expanding

Do not add new UI to:

- `src/Proxima.App/MainWindow.axaml`
- `src/Proxima.App/MainWindow.axaml.cs`
- `src/Proxima.App/ViewModels/ShellViewModel.cs`

These are legacy recovery targets.

## New target structure

```text
src/Proxima.App/
  DesignSystem/
    Tokens/
    Themes/
    Controls/
    Components/
  Shell/
    AppShellView.axaml
    AppShellViewModel.cs
    SidebarView.axaml
    SidebarViewModel.cs
    TopbarView.axaml
    TopbarViewModel.cs
  Views/
    Auth/LoginView.axaml
    Auth/RegisterView.axaml
    Dashboard/DashboardView.axaml
    Assets/AssetsView.axaml
    Assets/AssetDetailsView.axaml
    Goals/GoalsView.axaml
    Taxes/TaxesView.axaml
    Settings/SettingsView.axaml
  ViewModels/
    Auth/LoginViewModel.cs
    Auth/RegisterViewModel.cs
    Dashboard/DashboardViewModel.cs
    Assets/AssetsViewModel.cs
    Assets/AssetDetailsViewModel.cs
    Goals/GoalsViewModel.cs
    Taxes/TaxesViewModel.cs
    Settings/SettingsViewModel.cs
```

## UI recovery phases

### Phase A — Guardrails only

- Add style guard docs.
- Add mapping docs.
- Add skills.
- Add XAML scan script.
- Do not change UI behavior.

### Phase B — Design system hardening

- Convert global selector styles to explicit Proxima `ControlTheme` resources.
- Replace heavy shadows with Figma subtle shadows.
- Add tokens from Figma Mockup.
- Add missing components: navigation item, topbar, bento grid, table row, modal, chart shell.

### Phase C — Shell split

- Create `AppShellView` using Sidebar + Topbar + content presenter.
- Move navigation logic from code-behind into commands.
- Keep old screen logic available through adapters if needed.

### Phase D — Screen rebuild

Order:

1. Login/Register
2. Shell/Sidebar/Topbar
3. Dashboard
4. Assets
5. Asset Details
6. Goals
7. Taxes
8. Settings

### Phase E — Feature hardening

After visual parity:

- replace JSON repositories with PostgreSQL EF repositories;
- add real migrations;
- convert script-style tests to xUnit/NUnit;
- finish PDF import pipeline;
- finish real quote/NBRB provider behavior;
- add UI/headless tests.

## Definition of Done for each rebuilt screen

- Figma mapping file exists.
- No style guard violations.
- Uses Proxima components/themes only.
- No page-local reusable styling.
- All actions are commands or routed through view models.
- Build passes.
- Tests pass.
- Manual screenshot comparison recorded.
- Legacy `MainWindow.axaml` did not grow.
