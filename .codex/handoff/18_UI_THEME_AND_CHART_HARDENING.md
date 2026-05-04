# 18 — UI Theme and Chart Hardening

## Goal

Prevent system theme leakage and enforce consistent Proxima chart behavior.

## Theme rules

Proxima is a fixed light desktop UI.

The application must not use OS/system theme colors for production UI states.

Required:

- App theme variant must be explicitly light.
- All visual controls must use Proxima ControlTheme or Proxima wrapper component.
- Button/TextBox/ComboBox/DataGrid/Menu/Flyout/ScrollBar states must define:
  - normal
  - pointerover
  - pressed
  - focused
  - disabled
  - selected where applicable
- No system accent brushes in Proxima production UI.
- No raw colors in Views.
- No default Avalonia hover/focus/pressed appearance.

Forbidden:

- `RequestedThemeVariant="Default"` for production.
- raw `#RRGGBB` colors in Views.
- page-local reusable styles.
- default Avalonia Button/TextBox/ComboBox/DataGrid appearance.
- relying on Fluent/System hover states.

## Chart rules

Every chart must be implemented through Proxima chart components or chart style factories.

Every chart must have:

- visible X axis;
- visible Y axis;
- axis labels or units;
- separators/grid;
- tooltip on hover;
- formatted values;
- loading state;
- empty state;
- error state.

Candlestick chart must also have:

- X-axis zoom;
- X-axis pan;
- reset zoom action;
- tooltip with date/time, open, high, low, close, volume;
- visible range state;
- readable axis formatting.

## Chart components

Required components/services:

- `ProximaCartesianChart`
- `ProximaCandlestickChart`
- `ProximaDonutChart`
- `ProximaChartTooltip`
- `ProximaChartAxisFactory`
- `ProximaChartTheme`

If the chart library is LiveCharts2, use a single adapter layer so screens do not configure charts directly.

## Acceptance criteria

- No system hover color appears on buttons.
- No system accent color appears in chart/controls.
- Every chart has X/Y axes.
- Every chart has tooltip on hover.
- Candlestick chart supports zoom and pan.
- Screens do not configure chart visual styles locally.
- Build and tests pass.
