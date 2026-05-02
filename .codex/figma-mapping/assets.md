# Assets Figma Mapping

## 1) Figma node id
- Root: `62:1195`
- Main content: `62:2665`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Filters/search area
- Asset summary cards
- Assets table area

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:1195 All assets` | `Assets/AssetsView` |
| `62:2622 Sidebar` | `Shell/SidebarView` |
| `62:2318 Header - TopNavBar Shell` | `Shell/TopbarView` |
| `62:1196 Main Content` | `Grid` with cards + table |
| `66:1322 Heading 2` | section header `TextBlock` style |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Sidebar bg `#EEF3F9` | `ProximaBrush.Sidebar` |
| Surface `#FFFFFF` | `ProximaBrush.Surface` |
| Border `#E2E8F0/#EDEEF0` | `ProximaBrush.Border` / `ProximaBrush.BorderSubtle` |
| Radius `12/16` | `ProximaRadius.Card` / `ProximaRadius.Large` |
| Spacing `16/24/32` | `ProximaSpace.16` / `.24` / `.32` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов.

## 6) Список недостающих компонентов
- `AssetsTable` (composed DS table wrapper)
- `AssetFilterBar`
- `TablePaginationFooter`

## 7) Список допустимых визуальных отклонений
- Допустима уменьшенная высота строк таблицы на 1-2 px при сохранении вертикального ритма.
- Допустим sticky header таблицы при скролле (если не ломает композицию).

## 8) Import flow visual comparison notes (2026-05-03)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Trigger from Assets | Pass | Кнопка `Импортировать активы` добавлена в `AssetsView`, открывает модальный import dialog. |
| Drag-and-drop modal composition | Partial | Визуальный dropzone/state modal восстановлен; drag-over состояние сейчас управляется UI-командой (без нативного OS drop handler). |
| Import states visibility | Pass | Реализованы `idle`, `drag_over`, `file_selected`, `parsing`, `validation_warnings`, `parse_failed`. |
| Manual fallback path | Pass | Из modal доступен переход в `ManualImportView`, также при parse fail данные передаются в manual table. |
| Manual transaction table | Pass | Редактируемые колонки: дата, тикер/имя, тип операции, количество, цена, комиссия, валюта, тег/категория; add/remove/save есть. |
| Suspicious highlight | Pass | Подозрительные строки выделяются отдельным warning row style. |
| Token/style compliance | Pass | Используются Proxima tokens/DesignSystem classes, без raw hex в `Views/Import` и `Views/Assets`. |
