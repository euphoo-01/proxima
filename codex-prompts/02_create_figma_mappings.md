# Codex Prompt 02 — Create Figma Mappings Only

Используй `$figma-to-avalonia-mapping`.

Цель: создать mapping для всех core screens. Никакой реализации XAML в этой задаче.

Перед работой прочитай:
- AGENTS.md
- .codex/handoff/04_FIGMA_MCP_PROTOCOL.md
- .codex/handoff/16_FIGMA_TO_AVALONIA_MAPPING.md
- docs/figma-inspection.md

Используй custom Figma MCP server. Если MCP недоступен, остановись, запиши блокер в `.codex/known-limitations.md`, не выдумывай layout.

Создай:
- `.codex/figma-mapping/login.md` from node `62:2149`
- `.codex/figma-mapping/register.md` from node `62:2417`
- `.codex/figma-mapping/dashboard.md` from node `62:1882`
- `.codex/figma-mapping/assets.md` from node `62:1195`
- `.codex/figma-mapping/asset-details.md` from node `62:1521`
- `.codex/figma-mapping/goals.md` from node `62:962`
- `.codex/figma-mapping/taxes.md` from node `62:498`
- `.codex/figma-mapping/settings.md` from node `62:763`

Для каждого файла заполни:
- frame metrics;
- layout mapping;
- component mapping;
- token mapping;
- states;
- known deviations.

Не меняй production code.

В конце:
```bash
git status --short
git add .codex/figma-mapping .codex/design-token-gaps.md .codex/known-limitations.md
git commit -m "docs(figma): map mockup screens to avalonia components"
```
