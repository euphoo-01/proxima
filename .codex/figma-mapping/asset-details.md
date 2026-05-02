# Asset Details Figma Mapping

## 1) Figma node id
- Root: `62:1521`
- Main content: `62:1522`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Asset headline + key metrics
- Chart/history area
- Positions/transactions area

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:1521 Asset` | `Assets/AssetDetailsView` |
| `66:1353 Sidebar` | `Shell/SidebarView` |
| `62:2230 Header` | `Shell/TopbarView` + `PageHeader` |
| `62:1549 Bento Content` | bento sections (`BentoCard`, chart/table wrappers) |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Card bg `#FFFFFF` | `ProximaBrush.Surface` |
| Subtle bg `#F2F4F5` | `ProximaBrush.SurfaceSubtle` |
| Radius `12/16` | `ProximaRadius.Card` / `ProximaRadius.Large` |
| Border subtle `#EDEEF0` | `ProximaBrush.BorderSubtle` |
| Space `16/24/32` | `ProximaSpace.16` / `.24` / `.32` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов.

## 6) Список недостающих компонентов
- `AssetHeadlineCard`
- `PerformanceDeltaBadge`
- `TransactionsMiniTable`

## 7) Список допустимых визуальных отклонений
- Допустим упрощенный чартерный рендер без потери контейнерной геометрии.
- Допустимо схлопывание части вторичных метрик в 2 колонки на узких окнах.
