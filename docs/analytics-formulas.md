# Analytics Formulas

## Assumptions

- Risk-free rate for MVP formulas: `0`.
- Annualization factor for daily returns: `sqrt(252)`.
- VaR/CVaR method: historical simulation, confidence `95%`.
- Money values use `decimal`; statistical calculations use `double`.

## Core formulas

- `TotalValue = sum(quantity * currentPrice) + cash`
- `UnrealizedPnL = quantity * (currentPrice - averageCost) - fees`
- `ROI = pnl / invested * 100` (unavailable when invested = 0)
- `AverageCostAfterBuy = (currentQty*currentAvg + buyQty*buyPrice + fees) / (currentQty + buyQty)`

## Risk formulas

- `Volatility = std(returns) * sqrt(252)`
- `MaxDrawdown = max((peak - price)/peak)`
- `Sharpe = mean(excessReturns) / std(returns) * sqrt(252)`
- `Sortino = mean(excessReturns) / downsideDeviation * sqrt(252)`
- `VaR(95%) = 5th percentile of return distribution`
- `CVaR(95%) = mean(returns below VaR threshold)`
- `Correlation = covariance(x,y) / (std(x) * std(y))`
