# Codex Prompt 03 — Rebuild Proxima Design System

Используй `$avalonia-design-system-guardian`.

Цель: подготовить Proxima DesignSystem так, чтобы будущие Views могли использовать только Proxima tokens/components/themes.

Перед работой прочитай:
- AGENTS.md
- .codex/handoff/15_AVALONIA_UI_STYLE_GUARDRAILS.md
- .codex/handoff/16_FIGMA_TO_AVALONIA_MAPPING.md
- docs/figma-inspection.md
- все `.codex/figma-mapping/*.md`

Сделай:
1. Создай `src/Proxima.App/DesignSystem/`.
2. Перенеси/нормализуй tokens из `src/Proxima.App/Styles/*.axaml` в DesignSystem.
3. Создай явные ControlTheme resources для:
   - Button primary/secondary/tertiary/toolbar/nav item
   - TextBox default/search/password
   - ComboBox
   - Card/BentoCard
   - Table header/row/cell
   - Modal overlay/dialog
   - Status pill
   - Timeframe selector
4. Сохрани старые styles временно, если они нужны для legacy `MainWindow`, но не используй их в новых Views.
5. Не меняй `MainWindow.axaml` в этой итерации.

В конце:
```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
git status --short
git add src/Proxima.App/DesignSystem src/Proxima.App/App.axaml .codex/iteration-log.md .codex/known-limitations.md
git commit -m "feat(ui): add proxima avalonia design system themes"
```
