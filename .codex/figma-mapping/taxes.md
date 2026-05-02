# Taxes Figma Mapping

## 1) Figma node id
- Root: `62:498`
- Main content: `62:2750`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Tax year selector/filter area
- Tax summary cards
- Draft declaration/details section

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:498 Tax` | `Taxes/TaxesView` |
| `62:2751 Sidebar` | `Shell/SidebarView` |
| `62:2384 Header` | `Shell/TopbarView` + `PageHeader` |
| `62:519 Section` | tax cards + draft detail panels |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Surface card `#FFFFFF` | `ProximaBrush.Surface` |
| Border subtle `#EDEEF0` | `ProximaBrush.BorderSubtle` |
| Accent brand `#0F4C81` | `ProximaBrush.Brand` |
| Status colors | `ProximaBrush.Success/Warning/Danger` + surfaces |
| Radius `12/16` | `ProximaRadius.Card` / `ProximaRadius.Large` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов.

## 6) Список недостающих компонентов
- `TaxSummaryCard`
- `TaxDraftSection`
- `TaxYearSelector`

## 7) Список допустимых визуальных отклонений
- Допустима таблица/листинг вместо fully custom declaration grid при совпадении визуального тона и иерархии.

## 8) Visual comparison notes (2026-05-03)
- Source validation done via custom `mcp__figma__`:
  - `62:498` (Tax root), `62:2750` (Main Content), `62:2384` (Header), `62:519` (Section).
- Confirmed from MCP:
  - shell layout includes sidebar + main content;
  - section uses 32px paddings and 32px vertical rhythm;
  - top content area contains filters/action row and card/table blocks.

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Composition | Pass | Taxes screen migrated to `Views/Taxes/TaxesView`, routed from `AppShell`. |
| Spacing rhythm | Pass | Main stack margin/padding/gaps aligned to mapping (32/24/16 rhythm). |
| Typography | Pass | Uses Proxima classes (`proxima-page-title`, `proxima-card-title`, `proxima-caption`, `proxima-body`). |
| Colors | Pass | No raw colors in view; only Proxima tokens/resources via component styles. |
| States | Pass | Loading, empty, error, and offline-rate failure states implemented. |
| No clipping | Pass | Content wrapped in `ScrollViewer`; long strings use `TextWrapping`. |
| No default Avalonia leakage | Pass | Buttons/ComboBox use Proxima classes/themes; screen-level visuals come from DS styles. |

## 9) Known deviations
| Deviation | Reason | Severity | Follow-up |
| --- | --- | --- | --- |
| Exact card internals for all tax KPIs are simplified | Existing runtime exposes draft-calculation boundary, not full legal engine/read model yet | Medium | Expand with full tax read-model when tax module hardening iteration starts |
| Breakdown uses compact 3-column table | Keeps parity with current DS table primitives and avoids page-local style duplication | Low | Replace with richer declaration grid if added to DesignSystem |
