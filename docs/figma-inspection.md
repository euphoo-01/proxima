# Figma Inspection

## Source

- URL: https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0
- File key: `Drxcen3JN69XP0fnYxkgOi`
- Starting node: `62:497`
- Access status: Partial access. Metadata and frame-level design context were retrieved through Figma MCP; screenshot export and variable extraction hit the Starter plan MCP call limit after the Tax frame context was extracted.
- Inspection timestamp: 2026-04-27

## Pages / Frames Inspected

| Page | Frame | Node ID | Purpose |
| --- | --- | --- | --- |
| Mockup | Canvas | `62:497` | Starting canvas containing desktop and mobile screens. |
| Mockup | Tax | `62:498` | Desktop authenticated shell, header, sidebar and bento tax dashboard. |
| Mockup | Settings | `62:763` | Desktop account/settings bento layout, inspected through metadata. |
| Mockup | Register | `94:2133` | Mobile registration/auth form, inspected through metadata. |
| Mockup | Settings | `94:1953` | Mobile account/settings layout, inspected through metadata. |

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

- Button/nav item: 8px.
- Bento card: 12px in inspected Tax frame.
- User card/avatar container: 16px and fully rounded avatar.
- Pills: 9999px.

### Spacing

- Sidebar width: 256px.
- Sidebar padding: 24px.
- Main content width: 1024px.
- Header height: 88px.
- Content padding: 32px.
- Bento grid gap: 24px.
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
| Tax | Inline render returned by `get_design_context` for node `62:498`; file export blocked by Figma Starter MCP call limit. |
| Login/Register | Metadata inspected at `94:2133`; screenshot export blocked by Figma Starter MCP call limit. |
| Settings | Metadata inspected at `62:763` and `94:1953`; screenshot export blocked by Figma Starter MCP call limit. |

See `docs/figma/screens/README.md` for export status.

## Implementation Notes

- Avalonia tokens intentionally map Figma values to reusable resources instead of hardcoding colors and spacing in views.
- Controls are implemented as binding-friendly Avalonia custom controls with styled properties.
- The design system starts with Tax-frame-derived shell/bento primitives because it provided the strongest available MCP context.
- Later screen modules should inspect their own nodes before implementing final page layouts.

## Differences from Figma

- Pixel-perfect matching is not claimed for Module 01 because screenshot file export and variable extraction were blocked by the Figma MCP Starter plan call limit.
- Fonts use Manrope as the design intent with Segoe UI fallback; exact packaged font embedding is deferred.
- Icons from Figma are not embedded yet; controls expose icon/content slots for later module-specific usage.
