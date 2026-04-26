# Design System — Bento UI for Avalonia

## Visual Direction

Style: **light bento financial dashboard**.

Keywords:

- bright;
- trustworthy;
- modern;
- calm;
- high contrast text;
- soft gradients;
- rounded cards;
- clean typography;
- dashboard-like composition;
- no visual noise;
- no “spreadsheet chaos”.

The UI must look close to the Figma file. The Figma design is the source of truth. This file is fallback and implementation guide for Avalonia.

## Layout Structure

### Login Screen

- Full-window light background.
- Centered rounded card.
- Two input fields: login, password.
- Primary button: “Войти”.
- Tertiary action: “Забыли пароль?”
- Soft shadow.
- Radius: 24–32 px.
- Card width target: 420–520 px.

### Authenticated Shell

Every page except login uses:

```text
Window
├── Sidebar
└── MainArea
    ├── Topbar
    └── PageContent
```

### Sidebar

- Fixed width: 260–300 px.
- App name: Proxima.
- Navigation:
  - Дешборд
  - Все активы
  - Налоги
  - Цели
  - Настройки
- Bottom user card:
  - avatar;
  - full name;
  - role: “Частный инвестор” or “Финансовый аналитик”.

### Topbar

- Breadcrumbs.
- Current portfolio selector.
- “Создать портфель” button.
- Optional sync/quote status indicator.

## Bento Grid Rules

Use a 12-column conceptual grid for desktop.

Recommended spacing:

- Window padding: 24–32 px.
- Grid gap: 16–24 px.
- Card padding: 20–28 px.
- Small card radius: 18–22 px.
- Large card radius: 24–32 px.

Bento card sizes:

- 1x1: small metric.
- 1x2: vertical card.
- 2x1: wide metric.
- 2x2: chart/summary.
- 3x2: main chart.
- 4x2: table.

## Color Tokens

Final values must be extracted from Figma MCP. Fallback palette:

```text
Background/Page:        #F6F7FB
Surface/Card:           #FFFFFF
Surface/Subtle:         #F1F4F8
Text/Primary:           #111827
Text/Secondary:         #6B7280
Text/Muted:             #9CA3AF
Border/Subtle:          #E5E7EB

Accent/Primary:         #4F46E5
Accent/PrimarySoft:     #EEF2FF
Accent/Secondary:       #06B6D4
Accent/SecondarySoft:   #ECFEFF
Accent/Success:         #16A34A
Accent/SuccessSoft:     #DCFCE7
Accent/Warning:         #F59E0B
Accent/WarningSoft:     #FEF3C7
Accent/Danger:          #DC2626
Accent/DangerSoft:      #FEE2E2
Accent/Violet:          #7C3AED
Accent/Emerald:         #10B981
```

## Typography

Use the closest available font from Figma. Fallback:

- Font family: Inter / Segoe UI / system.
- Display number: 40–56 px, semibold/bold.
- Page title: 28–36 px, semibold.
- Section title: 18–22 px, semibold.
- Body: 14–16 px.
- Caption: 12–13 px.

## Avalonia Implementation

Create reusable controls/styles:

```text
src/Proxima.App/
  Controls/
    BentoCard.axaml
    MetricCard.axaml
    StatusPill.axaml
    EmptyState.axaml
    PageHeader.axaml
    SearchBox.axaml
    TimeframeSelector.axaml
    DataTableHeaderCell.axaml
  Styles/
    Tokens.axaml
    Typography.axaml
    Buttons.axaml
    Inputs.axaml
    Cards.axaml
    Tables.axaml
    Charts.axaml
```

### BentoCard API

Implement as `UserControl` or `TemplatedControl`.

Properties:

- `Title`
- `Subtitle`
- `Icon`
- `Accent`
- `SizeVariant`
- `IsInteractive`
- `Command`
- `Content`

### MetricCard API

Properties:

- `Label`
- `Value`
- `Delta`
- `DeltaKind`: Positive/Negative/Neutral
- `Footer`
- `SparklineData`

## Dashboard Layout

Suggested bento placement:

```text
[ Total Value 2x1 ][ 24h P&L 1x1 ][ Risk Snapshot 1x1 ]
[ Portfolio Chart 3x2             ][ Allocation Donut 1x2 ]
[ Latest Transactions 4x2                         ]
```

## Asset Details Layout

```text
[ Asset Header 2x1 ][ Price 1x1 ][ 24h Volume 1x1 ]
[ Candlestick Chart 3x2             ][ Key Signals 1x2 ]
[ Valuation 2x1 ][ Risk 2x1 ]
[ Advanced Metrics 4x2 ]
[ Asset Transactions 4x2 ]
```

## Taxes Layout

```text
[ Total Tax Due 2x2 ][ Surtax Gauge 1x2 ][ Taxable Base 1x2 ]
[ Crypto Exemption 1x1 ][ Currency Effect 1x1 ][ Dividends 2x1 ]
[ Deductions 1x1 ][ Loss Carryforward 1x1 ][ Deadlines 2x1 ]
```

## Motion

Use subtle transitions only:

- navigation fade/slide;
- hover elevation on cards;
- button press scale 0.98;
- no excessive animation in financial data.

## Accessibility

- Minimum body contrast WCAG AA.
- Keyboard focus visible.
- Tables navigable.
- Tooltips for complex metrics.
- Do not encode state by color only.
