# Requirements Specification

## Functional Requirements

### FR-001 Authentication / Unlock

Система должна блокировать доступ к финансовым данным до ввода корректного локального пароля.

Acceptance criteria:

- Первый запуск предлагает создать локальный профиль и пароль.
- Повторный запуск показывает unlock/login screen.
- Пароль не хранится в plaintext.
- После N неудачных попыток показывается задержка/защитное сообщение.

### FR-002 Portfolio Management

Система должна поддерживать несколько изолированных портфелей.

Acceptance criteria:

- Создание портфеля.
- Переименование портфеля.
- Удаление портфеля с подтверждением.
- Переключение текущего портфеля через Topbar selector.
- Все dashboard/calculations scoped by selected portfolio.

### FR-003 Asset Management

Система должна поддерживать активы типов:

- Stock
- Crypto
- Currency
- Cash
- Bond
- ETF
- Other

Acceptance criteria:

- CRUD активов.
- Asset details page.
- Tags/categories.
- Link between assets and transactions.

### FR-004 Transaction Management

Система должна поддерживать операции:

- Buy
- Sell
- Dividend
- Deposit
- Withdrawal
- Fee
- Tax
- Transfer
- Split
- Airdrop
- StakingReward

Acceptance criteria:

- Manual transaction table entry.
- Sorting by column headers.
- Search/filter.
- Validation before save.
- Recalculation after save.

### FR-005 Import

Система должна поддерживать импорт CSV и extensible pipeline для PDF.

Acceptance criteria:

- Drag&drop modal on Assets page.
- CSV parser works for demo format.
- PDF parser has interface and safe placeholder.
- Suspicious rows are marked for manual confirmation.
- Failed import opens manual import screen.

### FR-006 Quotes

Система должна обновлять цены активов через external providers when online.

Acceptance criteria:

- Quote provider abstraction.
- Cached latest quotes.
- Offline mode uses cached quotes.
- Errors shown in UI without app crash.
- No portfolio transaction data sent to quote APIs.

### FR-007 Dashboard

Dashboard должен отображать:

- total portfolio value;
- 24h change and percentage;
- portfolio value chart with 1D/7D/1M toggle;
- latest transactions table;
- allocation pie/donut by user tags/categories.

### FR-008 Asset Details

Asset details page должна отображать:

- candlestick/OHLC chart with 1h/1d/7d/30d timeframe;
- base metrics;
- advanced metrics;
- transaction table for selected asset;
- clear bento layout.

### FR-009 Analytics

Система должна рассчитывать:

- P&L;
- ROI;
- average cost;
- volatility;
- maximum drawdown;
- Sharpe ratio;
- Sortino ratio;
- Calmar ratio;
- VaR/CVaR;
- SMA 50/200;
- RSI;
- ATR;
- Z-Score;
- correlation when benchmark data exists.

MVP допускает deterministic demo calculations where market data is incomplete, but architecture must support real implementations.

### FR-010 Goals

Goals page должна поддерживать:

- список целей;
- CRUD goals;
- monthly contribution input;
- compound-interest forecast;
- chart with current capital and goal markers.

### FR-011 Taxes

Taxes page должна формировать расчетный overview для РБ.

Acceptance criteria:

- Cards: total tax due, taxable base, tax saved, crypto status, dividends/double taxation, currency effect, deadlines, deductions, loss carryforward.
- NBRB exchange rate provider abstraction.
- Tax rules are versioned/configurable.
- UI shows “draft calculation / verify with official sources”.
- Export PDF if reporting module available.

### FR-012 Settings

Settings page должна поддерживать:

- user profile view/edit;
- avatar change;
- base currency USD/BYN;
- portfolio management;
- legal/tax profile type;
- quote update intervals;
- Google Drive encrypted snapshot sync settings;
- UI scale/language.

### FR-013 Google Drive Sync

Google Drive sync must be implemented as encrypted snapshot sync, not raw database folder sync.

Acceptance criteria:

- Export encrypted snapshot.
- Import encrypted snapshot.
- Optional Drive folder target.
- Conflict detection metadata.
- No plaintext database file uploaded.

## Non-Functional Requirements

### NFR-001 Usability

- Russian and English UI.
- Language switching without reinstall.
- UI scaling support.
- Charts understandable without long documentation.

### NFR-002 Performance

- Navigation response target: <300 ms under normal local dataset.
- Chart rendering target: <1 sec for several years of history.
- Long imports run async with progress.

### NFR-003 Security

- Local access requires password.
- No financial data is sent to uncontrolled third-party servers.
- Secrets are not stored in repo.
- Logs do not contain transaction details, full account names, passwords, tokens, or raw imported files.

### NFR-004 Reliability

- PostgreSQL transactions protect data integrity.
- Import either commits valid batch atomically or clearly reports partial mode.
- App survives failed quote update.
- Offline mode uses cached quotes.

### NFR-005 Maintainability

- Clean Architecture.
- MVVM.
- Testable services.
- No business logic in Views.
- No god classes.
- Clear module boundaries.

### NFR-006 Portability

- Desktop-first: Windows/Linux/macOS where Avalonia and PostgreSQL setup allow.
- UI should adapt to common desktop resolutions.
