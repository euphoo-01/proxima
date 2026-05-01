# Figma MCP Protocol

## Objective

The Avalonia UI must be visually close to the provided Figma design. The agent must use Figma MCP before implementing UI.
For Proxima, the authoritative visual baseline for production screens is the **Mockup** page in the custom `figma_mcp_server`.

Figma URL:

`https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0`

File key: `Drxcen3JN69XP0fnYxkgOi`  
Starting node id: `62:497`

## Required MCP Steps

### Step 1 — Access validation

Use available Figma MCP tools to confirm access to the file.

Record in `docs/figma-inspection.md`:

- file name;
- inspected pages;
- inspected node ids;
- screenshots exported;
- timestamp;
- unavailable/missing assets.

If access fails, continue from spec but write the limitation in README.

### Step 2 — Extract structure

Inspect:

- page list;
- frame names;
- component names;
- screen nodes;
- navigation structure;
- design tokens/variables if available.

Mandatory page priority:

1. Mockup (desktop first).
2. Mockup (mobile/tablet references for spacing/fallback behavior).
3. Wireframe (only if Mockup is missing a required state).

### Step 3 — Extract visual tokens

Capture:

- colors;
- typography;
- radii;
- spacing;
- card dimensions;
- grid rules;
- shadows/elevation;
- icon style;
- button/input states.

Translate them into Avalonia resources:

```text
src/Proxima.App/Styles/Tokens.axaml
src/Proxima.App/Styles/Typography.axaml
src/Proxima.App/Styles/Buttons.axaml
src/Proxima.App/Styles/Inputs.axaml
src/Proxima.App/Styles/Cards.axaml
```

### Step 4 — Export references

Export screenshots for each core screen into:

```text
docs/figma/screens/
```

Required screenshots:

- Login
- Dashboard
- All Assets
- Asset Details
- Goals
- Taxes
- Settings
- Import modal/manual import if present

### Step 5 — Implement screen-by-screen

For each screen:

1. Inspect Figma node.
2. Note layout dimensions.
3. Implement Avalonia View + ViewModel.
4. Compare screenshot visually.
5. Adjust spacing/typography.
6. Commit.

Release-gate note:

- If text clipping, overflow, broken layout rhythm, missing accents/colors, or missing iconography are observed relative to Mockup, the screen is **not accepted**.
- If interactive chart placeholders are shown where Mockup expects charts, the screen is **not accepted**.

### Step 6 — Do not overfit pixels at the cost of maintainability

Figma is visual source of truth, but implementation must remain:

- componentized;
- themeable;
- responsive to desktop sizes;
- accessible;
- maintainable.

## Design-to-Avalonia Mapping

Figma frame → Avalonia View  
Figma component → Avalonia UserControl/TemplatedControl  
Figma variable → Avalonia Resource  
Figma auto-layout → StackPanel/Grid/DockPanel  
Figma bento layout → Grid with row/column spans  
Figma text style → TextBlock style  
Figma variant → style class / pseudo-class / property enum

## Mandatory Output

Create `docs/figma-inspection.md` with this template:

```md
# Figma Inspection

## Source
- URL:
- File key:
- Starting node:
- Access status:

## Pages / Frames Inspected

| Page | Frame | Node ID | Purpose |
| --- | --- | --- | --- |

## Extracted Tokens

### Colors
...

### Typography
...

### Radii
...

### Spacing
...

### Shadows
...

## Screenshots

| Screen | File |
| --- | --- |

## Implementation Notes

...

## Differences from Figma

...
```

## Fail-safe

If Figma MCP provides no data:

- implement fallback design from `03_DESIGN_SYSTEM_BENTO_AVALONIA.md`;
- do not claim pixel-perfect match;
- create issue in `docs/known-limitations.md`.

If custom `figma_mcp_server` is available, fallback mode must not be used.
