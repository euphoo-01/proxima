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
