# Codex Prompt 04 — Split Shell From Legacy MainWindow

Используй `$avalonia-ui-refactor` and `$avalonia-design-system-guardian`.

Цель: создать новый shell рядом с legacy MainWindow, без полной миграции всех страниц.

Перед работой прочитай:
- .codex/handoff/17_UI_RECOVERY_PLAN.md
- .codex/figma-mapping/dashboard.md
- .codex/figma-mapping/taxes.md
- .codex/figma-mapping/settings.md

Сделай:
1. Создай `src/Proxima.App/Shell/AppShellView.axaml`.
2. Создай `SidebarView.axaml`, `TopbarView.axaml` и соответствующие ViewModels.
3. Не добавляй новый контент в `MainWindow.axaml`.
4. Перенеси только shell composition: Sidebar + Topbar + content region.
5. Все кнопки должны использовать commands, не click handlers.
6. Используй только Proxima themes/components.
7. Оставь legacy `MainWindow` как временный startup host, если полная замена startup рискованна.

В конце:
```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Shell
git status --short
git add src/Proxima.App/Shell .codex/iteration-log.md .codex/known-limitations.md
git commit -m "refactor(ui): introduce figma-aligned app shell"
```
