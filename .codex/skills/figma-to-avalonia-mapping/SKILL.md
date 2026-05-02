---
name: figma-to-avalonia-mapping
description: Use before implementing any Proxima screen from Figma. Creates a screen mapping from Figma Mockup node to Avalonia components, tokens, layout, states, and known deviations.
---

# Mandatory workflow

1. Read `.codex/handoff/04_FIGMA_MCP_PROTOCOL.md`.
2. Read `.codex/handoff/16_FIGMA_TO_AVALONIA_MAPPING.md`.
3. Use the custom Figma MCP server if available.
4. Inspect the Mockup node first, not Wireframe.
5. Create or update `.codex/figma-mapping/<screen>.md`.
6. Do not implement XAML in this skill unless the user explicitly asks for implementation after mapping.

# Required screen nodes

- Login: `62:2149`
- Register: `62:2417`
- Dashboard: `62:1882`
- Assets: `62:1195`
- Asset Details: `62:1521`
- Goals: `62:962`
- Taxes: `62:498`
- Settings: `62:763`

# Output

A completed mapping table with:

- frame metrics;
- layout mapping;
- component mapping;
- token mapping;
- states;
- visual comparison plan;
- deviations.

# Prohibited

- Guessing token values.
- Implementing UI before mapping.
- Claiming pixel parity without screenshot/manual comparison.
