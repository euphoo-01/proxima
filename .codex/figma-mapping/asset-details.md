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

## 8) Visual comparison notes (2026-05-03)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Composition | Pass | Сохранена bento-структура: header, chart, key metrics, risk metrics, transactions table. |
| Spacing rhythm | Pass | Использованы `24/16` ритмы и card paddings через Proxima resources. |
| Typography | Pass | Применены `proxima-page-title`, `proxima-card-title`, `proxima-body`, `proxima-caption`. |
| Colors | Pass | Использованы только `ProximaBrush.*` через design-system classes, без raw hex в view. |
| No clipping | Pass | Длинные секции вынесены в `ScrollViewer`, таблица и метрики читаемы. |
| Default Avalonia leakage | Pass | Кнопки/инпуты/селекторы используют Proxima classes/themes. |

## 8.1) Visual comparison notes (2026-05-04, chart hardening)

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Candlestick X axis | Pass | Добавлены читаемые подписи даты/времени в зависимости от видимого диапазона (интрадей/день/неделя). |
| Candlestick Y axis | Pass | Добавлены подписи цены с валютой (`USD`) и горизонтальные grid/separators. |
| Tooltip completeness | Pass | Hover tooltip показывает `datetime`, `open`, `high`, `low`, `close`, `volume`. |
| X zoom/pan | Pass | Wheel zoom и drag pan работают по X диапазону без Y zoom. |
| Reset zoom action | Pass | Добавлена кнопка `Reset zoom`, диапазон сбрасывается на полный набор свечей через VM state. |
| Local screen chart styling leakage | Pass | Локальная стилизация чарта в `AssetDetailsView` не добавлялась; поведение вынесено в `ProximaCandlestickChart`. |

## 9) Known deviations after implementation

| Deviation | Reason | Severity | Follow-up |
| --- | --- | --- | --- |
| OHLC источник пока mock (`MockAssetDetailsReadModelProvider`) | Реальный read-model provider и quote history wiring в runtime еще не подключены | Medium | Подключить application analytics read-model + real quote cache query |
| Advanced metrics partly mock/placeholder (`IV`, `Spread/Depth`) | Нет рыночного orderbook/implied volatility источника в текущем модуле | Medium | Закрыть при интеграции Module 10/11 сервисов в runtime AppShell |
