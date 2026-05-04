# Proxima Codex Recovery Pack

This pack is intended to be copied into the existing Proxima repository. It does not replace business/domain code. It adds guardrails and prompts for recovering the UI from the current monolithic Avalonia shell.

## Apply

From repository root:

```bash
cp -R path/to/proxima_codex_recovery_pack/.codex .
cp -R path/to/proxima_codex_recovery_pack/scripts .
cp -R path/to/proxima_codex_recovery_pack/codex-prompts .
chmod +x scripts/scan-xaml-style-violations.sh
```

Then manually update `AGENTS.md`:

1. Remove the one-time bootstrap tail after the final code block with `git log --oneline --decorate --graph --all`.
2. Add a pointer to the new files:
   - `.codex/handoff/15_AVALONIA_UI_STYLE_GUARDRAILS.md`
   - `.codex/handoff/16_FIGMA_TO_AVALONIA_MAPPING.md`
   - `.codex/handoff/17_UI_RECOVERY_PLAN.md`
3. Add the new UI guard command to the required UI quality gate:

```bash
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
```

## First Codex task

Use `codex-prompts/01_prepare_ui_guardrails.md`.

## Important

Do not ask Codex to redesign all UI and implement features in one task. Use the prompt sequence in `codex-prompts/`.
