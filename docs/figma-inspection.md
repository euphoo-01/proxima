# Figma Inspection

## Source

- URL: https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0
- File key: `Drxcen3JN69XP0fnYxkgOi`
- Starting node: `62:497`
- Access status: Available through two MCP providers. The custom `mcp__figma__` server was used for the final inspection because it exposes full node metadata without the Starter-plan quota that limited the first pass.
- Inspection timestamp: 2026-04-27

## Pages / Frames Inspected

| Page | Frame | Node ID | Purpose |
| --- | --- | --- | --- |
| Wireframe | Components | `2:2` | Component inventory: Nav Item, Bento Card, Button, Top Bar, Sidebar, Badge, Input, Table. |
| Wireframe | Dashboard | `2:3` | Early desktop wireframe with 1920x1080 shell and 6-column grid. |
| Mockup | Canvas | `62:497` | Starting canvas containing final desktop and mobile screens. |
| Mockup | Tax | `62:498` | Desktop authenticated shell, header, sidebar and bento tax dashboard. |
| Mockup | Tax / Sidebar | `62:2751` | Sidebar instance: 256px width, 24px padding and `#F8FAFC` surface. |
| Mockup | Tax / Main Content | `62:2750` | Main content frame: 1024px width, header + section layout. |
| Mockup | Tax / Header | `62:2384` | Header: 1024x88, padding 32x24, background blur 24. |
| Mockup | Tax / Section | `62:519` | Content section: 1024x930, padding 32, vertical gap 32. |
| Mockup | Tax / Metric Row | `62:520` | Three-column metric grid: 960x180, 24px column gap. |
| Mockup | Tax / Total Tax Due | `62:521` | Metric card: 304x180, radius 12, padding 28, shadow 0/1/2 at 5%. |
| Mockup | Settings | `62:763` | Desktop account/settings bento layout. |
| Mockup | Goals | `62:962` | Desktop goals layout. |
| Mockup | All assets | `62:1195` | Desktop assets/table layout. |
| Mockup | Asset | `62:1521` | Desktop asset detail layout. |
| Mockup | Dashboard | `62:1882` | Desktop dashboard bento layout. |
| Mockup | Login | `62:2149` | Desktop unlock/login layout. |
| Mockup | Register | `62:2417` | Desktop registration layout. |
| Mockup | Mobile Login | `94:731` | Mobile unlock/login layout. |
| Mockup | Mobile Dashboard | `94:767` | Mobile dashboard layout. |
| Mockup | Mobile Settings | `94:1953` | Mobile account/settings layout. |
| Mockup | Mobile Register | `94:2133` | Mobile registration/auth form. |

## Extracted Tokens

### Colors

- Page background: `#F8FAFB`
- Sidebar background: `#F8FAFC`
- Surface/card: `#FFFFFF`
- Surface subtle: `#F2F4F5`
- Text primary: `#001629`
- Brand/nav text: `#002B49`
- Text secondary: `#42474D`
- Text muted: `#64748B`
- Text faint: `#94A3B8`
- Border subtle: `#E2E8F0`, `#EDEEF0`
- Success card: `#A3F69C`
- Success text: `#1B6D24`
- Danger text: `#93000A`
- Danger surface: `rgba(255,218,214,0.30)`
- Primary button: `#001629`

### Typography

- Figma uses Manrope with Regular, Medium, Bold, Extra Bold weights.
- Page title: 36px, Extra Bold, line height 40, letter spacing around -0.9.
- Large numeric values: 36px, Extra Bold, line height 40.
- Secondary metric values: 30px, Bold, line height 36.
- Card title: 16px, Bold/Medium, line height 24.
- Navigation/body: 14px, Medium/Bold, line height 20.
- Captions/status: 12px, Regular/Bold, line height 16.

### Radii

- Button/nav item/input: 8px.
- Metric cards in inspected Tax frame: 12px.
- Reusable Bento Card component: 16px.
- User card/avatar container: 16px and fully rounded avatar.
- Pills: 9999px.

### Spacing

- Sidebar width: 256px.
- Sidebar padding: 24px.
- Main content width: 1024px.
- Header height: 88px.
- Header padding: 32px horizontal, 24px vertical.
- Content padding: 32px.
- Bento grid gap: 24px.
- Section vertical gap: 32px.
- Card padding: 24px or 28px depending on card density.
- Metric card height: 180px.
- Wide grid row heights: 272px and 288px.

### Shadows

- Cards and active nav use subtle shadow: `0px 1px 1px rgba(0,0,0,0.05)`.
- Some cards use `0px 1px 2px rgba(0,0,0,0.05)`.
- The visual language avoids heavy elevation.

## Screenshots

| Screen | File |
| --- | --- |
| Tax | Structured node data captured from custom MCP for `62:498`, `62:2751`, `62:2750`, `62:2384`, `62:519`, `62:520`, `62:521`. |
| Login/Register | Structured node data captured from custom MCP for `62:2149`, `62:2417`, `94:731`, `94:2133`. |
| Settings | Structured node data captured from custom MCP for `62:763` and `94:1953`. |
| Dashboard / Assets / Goals / Asset | Structured node data captured from custom MCP for `62:1882`, `62:1195`, `62:962`, `62:1521` and mobile counterparts. |

See `docs/figma/screens/README.md` for export status.

## Implementation Notes

- Avalonia tokens intentionally map Figma values to reusable resources instead of hardcoding colors and spacing in views.
- Controls are implemented as binding-friendly Avalonia custom controls with styled properties.
- The design system uses final Mockup nodes for shell/grid dimensions and Wireframe component nodes for reusable primitive behavior.
- Later screen modules should inspect their own nodes before implementing final page layouts.

## Differences from Figma

- Pixel-perfect matching is not claimed for Module 01 because this iteration establishes reusable primitives, not final screen-by-screen implementations.
- Fonts use Manrope as the design intent with Segoe UI fallback; exact packaged font embedding is deferred.
- Icons from Figma are not embedded yet; controls expose icon/content slots for later module-specific usage.
