# Codex Prompt 05 — Rebuild One Screen From Figma

Use this prompt once per screen. Replace `<screen>` and `<node>`.

Используй `$figma-to-avalonia-mapping`, `$avalonia-ui-refactor`, `$avalonia-design-system-guardian`.

Screen: `<screen>`
Figma node: `<node>`
Mapping file: `.codex/figma-mapping/<screen>.md`

Цель: реализовать один экран заново по Figma, используя новый DesignSystem. Не трогать другие экраны.

Перед работой прочитай:
- AGENTS.md
- `.codex/figma-mapping/<screen>.md`
- `.codex/handoff/15_AVALONIA_UI_STYLE_GUARDRAILS.md`
- `.codex/handoff/17_UI_RECOVERY_PLAN.md`

Сделай:
1. Проверь/обнови Figma mapping для `<screen>`.
2. Создай View в `src/Proxima.App/Views/<Area>/<Screen>View.axaml`.
3. Создай ViewModel в `src/Proxima.App/ViewModels/<Area>/<Screen>ViewModel.cs` или screen folder.
4. Используй только Proxima components/themes/tokens.
5. Не добавляй reusable styles в page XAML.
6. Не добавляй новую логику в legacy `ShellViewModel` кроме временного adapter, если без него нельзя.
7. Перенеси нужные bindings/commands из legacy logic.
8. Добавь/обнови tests для screen state and command behavior.
9. Запиши visual comparison/deviations in mapping file.

В конце:
```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
git status --short
git add src/Proxima.App/Views src/Proxima.App/ViewModels .codex/figma-mapping .codex/iteration-log.md .codex/known-limitations.md
git commit -m "feat(ui): rebuild <screen> from figma mockup"
```
