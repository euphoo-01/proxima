# Goals Figma Mapping

## 1) Figma node id
- Root: `62:962`
- Main content: `62:2707`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Goals overview cards
- Goal rows/progress area
- Forecast/projection area

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:962 Goals` | `Goals/GoalsView` |
| `62:2708 Sidebar` | `Shell/SidebarView` |
| `62:2340 Header` | `Shell/TopbarView` + `PageHeader` |
| `62:963 Main Canvas` | goal cards + `GoalRow` list container |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Card bg `#FFFFFF` | `ProximaBrush.Surface` |
| Progress accent | `ProximaBrush.Brand` |
| Success/warn/danger chips | `ProximaBrush.SuccessSurface` / `WarningSurface` / `DangerSurface` |
| Radius `12/16` | `ProximaRadius.Card` / `ProximaRadius.Large` |
| Spacing `16/24/32` | `ProximaSpace.16` / `.24` / `.32` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов.

## 6) Список недостающих компонентов
- `GoalProgressRow`
- `ForecastScenarioCard`

## 7) Список допустимых визуальных отклонений
- Допускается линейный индикатор прогресса вместо сложного кастомного, если цвет/толщина/радиус соответствуют DS.

## 8) Visual comparison notes (2026-05-03)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Composition | Pass | AppShell layout preserved: Sidebar + Topbar + content area, goals content rebuilt in dedicated `GoalsView`. |
| Spacing rhythm | Pass | Uses Proxima spacing scale (`16/24`) and bento card separation consistent with mapping node `62:962`. |
| Typography | Pass | Uses shared DS text classes (`proxima-page-title`, `proxima-card-title`, `proxima-body`, `proxima-caption`). |
| Colors | Pass | No raw hex in `Views/Goals`; surfaces/borders/brand accents come from Proxima tokens. |
| Goal CRUD affordance | Pass | Each goal row has `⋯` action with edit/archive menu and add-goal modal flow. |
| Projection area | Pass | Monthly contribution + expected return inputs feed projection line chart and summary card. |
| States | Pass | Loading, empty, and error states are explicitly represented in view. |
| No clipping / overflow | Partial | Primary desktop layout is covered; additional small-window stress test remains pending manual QA pass. |
| No default Avalonia leakage | Partial | Button/TextBox use Proxima classes; context menu and progress visuals still rely on default templates with Proxima tokenized wrappers. |

## 9) Known deviations

| Deviation | Reason | Severity | Follow-up |
| --- | --- | --- | --- |
| Goal progress is derived from shell-level current portfolio value, not per-goal linked balance | Current AppShell state exposes aggregated demo value only | Medium | Add per-goal allocation linkage in application read model and shell state provider. |
| Projection summary horizon fixed to 10 years for chart endpoint | UI recovery scope prioritized Figma parity over scenario engine breadth | Low | Add horizon selector and scenario presets in dedicated analytics iteration. |
