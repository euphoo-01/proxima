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
