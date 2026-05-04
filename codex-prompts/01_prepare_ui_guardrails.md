# Codex Prompt 01 — Prepare UI Guardrails

Ты работаешь в существующем проекте Proxima. Не начинай проект с нуля.

Цель: добавить инфраструктуру контроля Figma→Avalonia UI, не меняя production UI и не реализуя новые функции.

Перед работой прочитай:
- AGENTS.md
- .codex/handoff/00_MASTER_PROMPT.md
- .codex/handoff/03_DESIGN_SYSTEM_BENTO_AVALONIA.md
- .codex/handoff/04_FIGMA_MCP_PROTOCOL.md
- .codex/handoff/14_ITERATION_QUALITY_GATE.md

Сделай:
1. Удали из AGENTS.md одноразовый bootstrap tail в конце про mkdir/touch/git commit.
2. Нормализуй ссылки на iteration-log/known-limitations к `.codex/iteration-log.md` и `.codex/known-limitations.md`.
3. Добавь новые handoff-файлы из recovery pack:
   - `.codex/handoff/15_AVALONIA_UI_STYLE_GUARDRAILS.md`
   - `.codex/handoff/16_FIGMA_TO_AVALONIA_MAPPING.md`
   - `.codex/handoff/17_UI_RECOVERY_PLAN.md`
4. Добавь `.codex/design-token-gaps.md`.
5. Добавь `.codex/figma-mapping/README.md`.
6. Добавь skills из `.codex/skills`.
7. Добавь `scripts/scan-xaml-style-violations.sh` и `.ps1`.
8. Обнови quality gate: для UI-модулей style guard обязателен.
9. Добавь Module 21 — Figma Visual Parity Recovery в `.codex/handoff/13_MODULE_US_AC_TESTS.md`.

Не меняй:
- `src/Proxima.App/MainWindow.axaml`
- `src/Proxima.App/MainWindow.axaml.cs`
- `src/Proxima.App/ViewModels/ShellViewModel.cs`
- production behavior

В конце запусти:
```bash
dotnet format Proxima.sln --no-restore
dotnet build Proxima.sln
dotnet test Proxima.sln
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
git status --short
```

Если `src/Proxima.App/Views` еще не существует, style guard может вывести pass как guardrail setup. Это нормально.

Commit:
```bash
git add AGENTS.md .codex scripts
git commit -m "docs(ui): add figma avalonia recovery guardrails"
```
