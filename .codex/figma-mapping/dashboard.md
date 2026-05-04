# Dashboard Figma Mapping

## 1) Figma node id
- Root: `62:1882`
- Main content: `62:1935`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Bento KPI cards area
- Charts area
- Tables/lists area

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:1882 Dashboard` | `Dashboard/DashboardView` root shell content |
| `62:2581 Sidebar` | `Shell/SidebarView` |
| `62:1936 Header` | `Shell/TopbarView` + `PageHeader` |
| `62:1957 Bento Content` | `UniformGrid/Grid` of `BentoCard` / `MetricCard` |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Sidebar bg `#EEF3F9` | `ProximaBrush.Sidebar` |
| Card bg `#FFFFFF` | `ProximaBrush.Surface` |
| Card radius `16/12` | `ProximaRadius.Large` / `ProximaRadius.Card` |
| Border subtle `#EDEEF0` | `ProximaBrush.BorderSubtle` |
| Gap baseline `16/24/32` | `ProximaSpace.16` / `.24` / `.32` |
| Card shadow (soft) | `ProximaShadow.Card` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов (для Desktop Dashboard).

## 6) Список недостающих компонентов
- `SidebarNavItem` (stateful selected/hover variant)
- `TopbarActionGroup`
- `DashboardBentoGrid`

## 7) Список допустимых визуальных отклонений
- Допустимо вертикальное переполнение основной колонки со скроллом.
- Для графиков допускается placeholder-рендер до подключения данных, но с соблюдением размеров/отступов Figma.

## 8) Visual comparison notes (2026-05-03)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Composition | Pass | В runtime shell отображается bento-компоновка: KPI сверху, chart+allocation в средней зоне, transactions card снизу. |
| Spacing rhythm | Pass | Использованы Proxima spacing tokens (`16/24/32`) и карточные отступы из DesignSystem. |
| Typography | Pass | Заголовки/подписи/табличный текст через Proxima text classes и токены шрифтов. |
| Colors | Pass | Используются только токены (`ProximaBrush.*`), без raw hex в production view. |
| Topbar breadcrumbs | Pass | При route `dashboard` topbar показывает `Dashboard` в breadcrumb/title. |
| Sidebar active item | Pass | Клик по `Дешборд` открывает `DashboardView`, item получает active state через `RouteChanged`. |
| Transactions states | Pass | Loading/Empty/Data состояния видимы в таблице; сортировка по header-кнопкам и фильтр по поиску реализованы. |
| No default Avalonia leakage | Pass | Экран использует Proxima classes/components (`BentoCard`, `MetricCard`, `TimeframeSelector`, `proxima-datagrid`). |
| Known deviation | Partial | Donut визуализирован как токенизированный статичный ring shell + legend, без точной сегментации дуг по процентам в текущей итерации. |

## 9) Visual comparison notes (2026-05-04, Proxima chart components)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Portfolio chart component | Pass | `DashboardView` использует только `ProximaCartesianChart` с обязательными X/Y axis, grid/separators, hover tooltip и форматированными значениями. |
| Allocation chart component | Pass | `DashboardView` использует `ProximaDonutChart`; локальные chart colors/axes/tooltips в screen view не настраиваются. |
| Chart states | Pass | Для обоих графиков подключены `IsLoading`, `EmptyStateText`, `ErrorStateText` через VM-состояние. |
| Local chart styling leakage | Pass | Локальные chart style-настройки в экране не добавлялись; визуальное поведение централизовано в Proxima chart controls. |
