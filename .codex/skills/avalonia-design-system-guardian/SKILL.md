---
name: avalonia-design-system-guardian
description: Use before and after any Avalonia UI/XAML change in Proxima. Enforces Figma-derived tokens, Proxima ControlThemes, and prevents default Avalonia style leakage.
---

# Mandatory workflow

1. Read `.codex/handoff/15_AVALONIA_UI_STYLE_GUARDRAILS.md`.
2. Inspect all changed `.axaml` files.
3. Verify each production screen uses Proxima components/themes, not default Avalonia appearance.
4. Run the XAML style guard script when possible.
5. Report pass/fail evidence.

# Fail if any production view contains

- raw hex colors;
- inline `Background`, `Foreground`, `BorderBrush`;
- inline `CornerRadius`, `FontSize`, `FontWeight`, `BoxShadow`;
- default `Button`, `TextBox`, `ComboBox`, `DataGrid` without Proxima theme/component/class;
- page-local reusable styles;
- new UI added to legacy `MainWindow.axaml`.

# Required report

```md
## Avalonia UI Guardrail Result

| Check | Result | Evidence |
| --- | --- | --- |
| Figma mapping exists | Pass/Fail | |
| Raw visual values in views | Pass/Fail | |
| Default Avalonia leakage | Pass/Fail | |
| Page-local reusable styles | Pass/Fail | |
| Proxima tokens/themes used | Pass/Fail | |
| Style guard command | Pass/Fail | |
```

# If checks fail

Do not continue to new functionality. Fix UI guard violations first, unless the task explicitly asks only to record baseline violations.
