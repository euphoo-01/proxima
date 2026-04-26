# Feature Specifications

## 1. Login / Unlock

### UI

- Rounded bento login card.
- Fields: login, password.
- Primary button: “Войти”.
- Tertiary action: “Забыли пароль?”
- First-run variant: create local profile + set password.

### Logic

- Validate password.
- Load user profile.
- Navigate to Dashboard.
- On failure show friendly error.

## 2. Shell

### Sidebar

Navigation:

- Dashboard
- All Assets
- Taxes
- Goals
- Settings

### Topbar

- Breadcrumbs.
- Portfolio selector.
- Create portfolio button.
- Optional quote status.

## 3. Dashboard

### Bento Cards

- Total portfolio value.
- 24h change.
- Portfolio chart.
- Latest transactions.
- Allocation donut by user tags/categories.
- Optional risk snapshot.

### Interactions

- 1D / 7D / 1M chart toggle.
- Search latest transactions.
- Sort transaction table by columns.
- Click asset opens Asset Details.

## 4. All Assets

### UI

- “Импортировать активы” primary action.
- Asset table.
- Three-dot menu per row.
- Click asset title opens details.

### Import Flow

- Drag&drop modal.
- Accepted: CSV, PDF.
- CSV parser real.
- PDF parser interface + broker-specific strategy stubs.
- Suspicious rows -> manual confirmation.
- Failure -> manual import page.

### Manual Import

- Editable transaction table.
- Tag selection.
- Validation summary.
- Save button commits batch.

## 5. Asset Details

### Header

- Asset name/ticker.
- Current price.
- 24h change.
- Asset type/category.

### Chart

- Candlestick/OHLC.
- Timeframes: 1h, 1d, 7d, 30d.
- Cached data fallback.

### Metrics

Base metrics:

- Market Cap
- FDV
- P/E or P/S
- FDV/TVL or Market Cap/Fees for crypto
- 24h Volume
- Circulating / Total Supply
- SMA 50
- SMA 200
- RSI

Advanced metrics:

- Sharpe
- Sortino
- Calmar
- MDD
- VaR
- CVaR
- Skewness
- Kurtosis
- Beta
- HV
- IV placeholder if unavailable
- ATR
- Turnover
- Spread/depth placeholder if unavailable
- Hurst
- Z-Score
- Pearson correlation

### Algorithm Checklist Card

Show as readable checklist:

1. Garbage filter.
2. Trend evaluation.
3. Stress test.
4. Entry/sizing.

## 6. Goals

### UI

- Goal list with three-dot CRUD menu.
- Add goal modal.
- Monthly contribution input.
- Compound interest forecast chart.
- Goal markers on chart.

### Formula

Use monthly compounding:

```text
future = current * (1 + monthlyRate)^months
       + contribution * (((1 + monthlyRate)^months - 1) / monthlyRate)
```

If monthlyRate is zero:

```text
future = current + contribution * months
```

## 7. Taxes

### UI Cards

- Total Tax Due
- Taxable Base
- Tax Saved / Exemptions
- Crypto Status
- Dividends / Double Taxation
- Currency Effect
- Surtax gauge
- Deadlines
- Deductions
- Loss Carryforward

### Important

Tax rules must be configurable and dated. Use official sources before final legal implementation. MVP can show demo calculations and “draft” label.

### NBRB Provider

Create abstraction:

```csharp
public interface IExchangeRateProvider
{
    Task<ExchangeRate?> GetRateAsync(CurrencyCode from, CurrencyCode to, DateOnly date, CancellationToken ct);
}
```

## 8. Settings

- Profile.
- Avatar.
- Base currency: USD/BYN.
- Language: RU/EN.
- UI scale.
- Portfolios.
- Legal entity type:
  - PhysicalPerson
  - SelfEmployed
  - IndividualEntrepreneur
  - LLC
  - JSC
  - Other
- Quote provider settings.
- Google Drive encrypted snapshot settings.
- Security settings.

## 9. Reporting

PDF export modules:

- Portfolio report.
- Tax report.
- Import validation report.

Reports must use clean financial layout, not raw screenshots.

## 10. Demo Mode

Provide demo data so the app looks complete immediately:

- realistic portfolio;
- historical prices;
- positive/negative P&L;
- several tags;
- goals;
- tax report draft.
