using System.Globalization;
using Proxima.Application.AssetDetails;
using Proxima.Application.Assets;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Transactions;

namespace Proxima.Infrastructure.AssetDetails;

public sealed class AssetDetailsService(
    ICurrentPortfolioContext portfolioContext,
    IAssetRepository assetRepository,
    ITransactionRepository transactionRepository,
    IQuoteCacheRepository quoteCacheRepository,
    IQuoteProvider quoteProvider,
    FinnhubAssetMarketDataProvider marketDataProvider) : IAssetDetailsService
{
    public async Task<AssetDetailsReadModel?> GetAsync(
        Guid assetId,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        Guid portfolioId = portfolioContext.CurrentPortfolioId;

        Asset? asset = await assetRepository
            .FindByIdAsync(portfolioId, assetId, cancellationToken)
            .ConfigureAwait(false);

        if (asset is null || asset.IsArchived)
        {
            return null;
        }

        IReadOnlyList<PortfolioTransaction> portfolioTransactions = await transactionRepository
            .ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken)
            .ConfigureAwait(false);

        PortfolioTransaction[] assetTransactions = portfolioTransactions
            .Where(item => item.AssetId == asset.Id)
            .OrderByDescending(item => item.TradeDate)
            .ToArray();

        QuoteCacheEntry? cachedQuote = await quoteCacheRepository
            .FindLatestByAssetIdAsync(asset.Id, cancellationToken)
            .ConfigureAwait(false);

        QuoteProviderResult latestQuote = await quoteProvider
            .GetLatestQuoteAsync(asset.Ticker, asset.Currency, cancellationToken)
            .ConfigureAwait(false);

        if (!latestQuote.Succeeded)
        {
            if (latestQuote.ErrorKind == QuoteProviderErrorKind.Unauthorized)
            {
                throw new InvalidOperationException(latestQuote.Message);
            }

            if (cachedQuote is null)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(latestQuote.Message)
                    ? "Не удалось загрузить котировку Finnhub и локального кэша для актива нет."
                    : latestQuote.Message);
            }
        }

        if (latestQuote.Succeeded && latestQuote.Quote is not null)
        {
            cachedQuote = new QuoteCacheEntry(
                asset.Id,
                asset.Ticker,
                latestQuote.Quote.Price,
                latestQuote.Quote.Currency,
                latestQuote.Quote.Timestamp,
                latestQuote.Quote.Source);

            await quoteCacheRepository
                .UpsertLatestAsync(cachedQuote, cancellationToken)
                .ConfigureAwait(false);

            asset = asset with
            {
                CurrentPrice = latestQuote.Quote.Price,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await assetRepository
                .UpdateAsync(asset, cancellationToken)
                .ConfigureAwait(false);
        }

        FinnhubAssetMarketData? marketData = await marketDataProvider
            .TryLoadAsync(asset.Ticker, NormalizeTimeframe(timeframe), cancellationToken)
            .ConfigureAwait(false);

        string currency = cachedQuote?.Currency
            ?? marketData?.Currency
            ?? asset.Currency;

        decimal currentPrice = cachedQuote?.Price
            ?? asset.CurrentPrice;

        IReadOnlyList<AssetDetailsCandle> candles = marketData?.Candles.Count > 0
            ? marketData.Candles
            : [];

        if (candles.Count > 0)
        {
            currentPrice = candles[^1].Close;
        }

        decimal previousClose = candles.Count > 1
            ? candles[^2].Close
            : ResolvePreviousClose(assetTransactions, currentPrice);

        decimal deltaPct = previousClose > 0m
            ? (currentPrice - previousClose) / previousClose * 100m
            : 0m;

        decimal positionValue = asset.Quantity * currentPrice;

        decimal marketCap = marketData?.MarketCapUsd
            ?? EstimateMarketCap(asset, currentPrice, positionValue);

        decimal fdv = marketData?.FdvUsd
            ?? marketCap;

        decimal volumeUnits = marketData?.VolumeUnits
            ?? EstimateVolume(assetTransactions, asset.Quantity);

        decimal volumeUsd = marketData?.VolumeUsd
            ?? volumeUnits * currentPrice;

        decimal? pe = marketData?.PeRatio;
        decimal? beta = marketData?.Beta;

        AssetRiskMetrics risk = CalculateRisk(
            candles.Select(item => item.Close).ToArray(),
            candles.Select(item => item.High).ToArray(),
            candles.Select(item => item.Low).ToArray(),
            currentPrice,
            beta);

        IReadOnlyList<AssetDetailsMetric> basicMetrics =
        [
            new("SMA 50/200", risk.SmaStatus, "Тренд по скользящим средним", risk.IsSmaPositive ? AssetDetailsMetricSeverity.Good : AssetDetailsMetricSeverity.Warning),
            new("RSI", FormatNumber(risk.Rsi), risk.RsiHint, ResolveRsiSeverity(risk.Rsi)),
            new("ATR", FormatNumber(risk.Atr), risk.AtrHint, AssetDetailsMetricSeverity.Neutral),
            new("Turnover", marketCap > 0m && volumeUsd > 0m ? FormatPercent(volumeUsd / marketCap * 100m) : "—", "Объем торгов / капитализация", AssetDetailsMetricSeverity.Neutral),
        ];

        IReadOnlyList<AssetDetailsMetric> riskMetrics =
        [
            new("Max Drawdown", FormatPercent(risk.MaxDrawdownPct), "Максимальная историческая просадка", ResolveDrawdownSeverity(risk.MaxDrawdownPct)),
            new("VaR 95%", FormatPercent(Math.Abs(risk.VarPct)), "Ожидаемый дневной убыток в нормальных условиях", AssetDetailsMetricSeverity.Warning),
            new("CVaR", FormatPercent(Math.Abs(risk.CvarPct)), "Ожидаемый убыток в хвостовых сценариях", AssetDetailsMetricSeverity.Warning),
            new("Sharpe", FormatNumber(risk.Sharpe), "Доходность на единицу риска", risk.Sharpe >= 1m ? AssetDetailsMetricSeverity.Good : AssetDetailsMetricSeverity.Neutral),
            new("Sortino", FormatNumber(risk.Sortino), "Доходность на негативную волатильность", risk.Sortino >= 1m ? AssetDetailsMetricSeverity.Good : AssetDetailsMetricSeverity.Neutral),
            new("Calmar", FormatNumber(risk.Calmar), "Доходность относительно максимальной просадки", risk.Calmar >= 1m ? AssetDetailsMetricSeverity.Good : AssetDetailsMetricSeverity.Neutral),
            new("Hurst", FormatNumber(risk.Hurst), "Оценка трендовости актива", risk.Hurst >= 0.55m ? AssetDetailsMetricSeverity.Good : AssetDetailsMetricSeverity.Neutral),
            new("Z-Score", FormatSignedNumber(risk.ZScore), "Отклонение цены от средней", Math.Abs(risk.ZScore) > 2m ? AssetDetailsMetricSeverity.Warning : AssetDetailsMetricSeverity.Neutral),
            new("Beta", FormatNumber(risk.Beta), "Чувствительность к рынку", risk.Beta > 1.5m ? AssetDetailsMetricSeverity.Warning : AssetDetailsMetricSeverity.Neutral),
            new("Spread", risk.SpreadPct > 0m ? FormatPercent(risk.SpreadPct) : "—", "Оценка торгового спреда", AssetDetailsMetricSeverity.Neutral),
        ];

        IReadOnlyList<AssetDetailsTransaction> transactionRows = assetTransactions
            .Select(ToTransactionReadModel)
            .ToArray();

        string assetName = !string.IsNullOrWhiteSpace(marketData?.Name)
            ? marketData.Name!
            : !string.IsNullOrWhiteSpace(asset.Name)
                ? asset.Name
                : asset.Ticker.ToUpperInvariant();

        string source = marketData is not null
            ? "Источник: Finnhub + PostgreSQL"
            : cachedQuote is not null
                ? $"Источник: {cachedQuote.Source} + PostgreSQL"
                : "Источник: PostgreSQL + локальная оценка";

        return new AssetDetailsReadModel(
            asset.Id,
            assetName,
            asset.Ticker.ToUpperInvariant(),
            BuildLogoText(asset.Ticker),
            FormatCurrency(currentPrice, currency),
            FormatDelta(deltaPct),
            deltaPct >= 0m,
            currency.ToUpperInvariant(),
            FormatCompactUsd(marketCap),
            FormatCompactUsd(fdv),
            pe.HasValue && pe.Value > 0m ? pe.Value.ToString("0.##", CultureInfo.InvariantCulture) : "—",
            FormatCompactUsd(volumeUsd),
            volumeUnits > 0m ? $"{FormatCompactNumber(volumeUnits)} {asset.Ticker.ToUpperInvariant()}" : "—",
            source,
            candles,
            basicMetrics,
            riskMetrics,
            transactionRows);
    }

    private static AssetDetailsTransaction ToTransactionReadModel(PortfolioTransaction transaction)
    {
        string typeLabel = transaction.Type switch
        {
            TransactionType.Buy => "Покупка",
            TransactionType.Sell => "Продажа",
            TransactionType.Dividend => "Дивиденд",
            TransactionType.Fee => "Комиссия",
            TransactionType.Tax => "Налог",
            _ => transaction.Type.ToString()
        };

        return new AssetDetailsTransaction(
            transaction.Id,
            transaction.TradeDate,
            typeLabel,
            transaction.Price,
            transaction.Quantity,
            transaction.GrossAmount,
            transaction.FeeAmount,
            transaction.Currency,
            transaction.IsArchived ? "Archived" : "Completed");
    }

    private static string NormalizeTimeframe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1д";
        }

        string normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "1h" => "1ч",
            "1ч" => "1ч",
            "hour" => "1ч",

            "1d" => "1д",
            "1д" => "1д",
            "day" => "1д",

            "7d" => "7д",
            "7д" => "7д",
            "week" => "7д",

            "30d" => "30д",
            "30д" => "30д",
            "month" => "30д",

            "1y" => "1г",
            "1г" => "1г",
            "year" => "1г",

            _ => normalized
        };
    }

    private static decimal ResolvePreviousClose(
        IReadOnlyList<PortfolioTransaction> transactions,
        decimal fallback)
    {
        PortfolioTransaction? previous = transactions
            .Where(item => item.Type is TransactionType.Buy or TransactionType.Sell && item.Price > 0m)
            .Skip(1)
            .FirstOrDefault();

        return previous?.Price > 0m
            ? previous.Price
            : fallback;
    }

    private static AssetRiskMetrics CalculateRisk(
        decimal[] closes,
        decimal[] highs,
        decimal[] lows,
        decimal currentPrice,
        decimal? beta)
    {
        closes = closes.Where(item => item > 0m).ToArray();
        highs = highs.Where(item => item > 0m).ToArray();
        lows = lows.Where(item => item > 0m).ToArray();

        if (closes.Length < 3)
        {
            return AssetRiskMetrics.Empty(beta ?? 1m);
        }

        decimal[] returns = closes
            .Zip(closes.Skip(1), (previous, current) => previous <= 0m ? 0m : (current - previous) / previous)
            .ToArray();

        decimal mean = returns.Average();
        decimal std = StandardDeviation(returns);
        decimal downside = StandardDeviation(returns.Where(item => item < 0m).DefaultIfEmpty(0m).ToArray());

        decimal maxDrawdown = MaxDrawdown(closes) * 100m;
        decimal var = Percentile(returns, 0.05m) * 100m;
        decimal cvar = returns
            .Where(item => item <= Percentile(returns, 0.05m))
            .DefaultIfEmpty(0m)
            .Average() * 100m;

        decimal sharpe = std > 0m ? mean / std * Sqrt(252m) : 0m;
        decimal sortino = downside > 0m ? mean / downside * Sqrt(252m) : 0m;
        decimal annualReturn = mean * 252m;
        decimal calmar = maxDrawdown < 0m ? annualReturn / Math.Abs(maxDrawdown / 100m) : 0m;
        decimal atr = AverageTrueRange(highs, lows, closes);
        decimal rsi = Rsi(closes);

        decimal smaShort = closes.TakeLast(Math.Min(50, closes.Length)).Average();
        decimal smaLong = closes.TakeLast(Math.Min(200, closes.Length)).Average();

        decimal zScore = std > 0m && closes.Average() > 0m
            ? (currentPrice - closes.Average()) / (std * closes.Average())
            : 0m;

        decimal trend = Math.Clamp(Math.Abs((smaShort - smaLong) / Math.Max(1m, smaLong)), 0m, 0.4m);
        decimal hurst = Math.Clamp(0.5m + trend, 0.35m, 0.9m);
        decimal correlation = Autocorrelation(returns);

        decimal spread = currentPrice > 0m
            ? atr / currentPrice * 0.18m * 100m
            : 0m;

        string smaStatus = smaShort >= smaLong ? "Золотой крест" : "Слабый тренд";
        string rsiHint = rsi >= 70m ? "Перекупленность" : rsi <= 30m ? "Перепроданность" : "Нейтрально";
        string atrHint = currentPrice > 0m && atr / currentPrice > 0.025m
            ? "Высокая волатильность"
            : "Умеренная волатильность";

        return new AssetRiskMetrics(
            sharpe,
            sortino,
            calmar,
            maxDrawdown,
            var,
            cvar,
            rsi,
            atr,
            hurst,
            zScore,
            correlation,
            beta ?? 1m,
            spread,
            smaStatus,
            smaShort >= smaLong,
            rsiHint,
            atrHint);
    }

    private static decimal StandardDeviation(decimal[] values)
    {
        if (values.Length < 2)
        {
            return 0m;
        }

        decimal mean = values.Average();
        double variance = values.Select(item => Math.Pow((double)(item - mean), 2d)).Average();
        return (decimal)Math.Sqrt(variance);
    }

    private static decimal MaxDrawdown(decimal[] closes)
    {
        decimal peak = closes[0];
        decimal maxDrawdown = 0m;

        foreach (decimal close in closes)
        {
            if (close > peak)
            {
                peak = close;
            }

            if (peak > 0m)
            {
                decimal drawdown = (close - peak) / peak;
                if (drawdown < maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    private static decimal Percentile(decimal[] values, decimal p)
    {
        if (values.Length == 0)
        {
            return 0m;
        }

        decimal[] sorted = values.Order().ToArray();
        int index = Math.Clamp(
            (int)Math.Floor((double)(p * (sorted.Length - 1))),
            0,
            sorted.Length - 1);

        return sorted[index];
    }

    private static decimal AverageTrueRange(decimal[] highs, decimal[] lows, decimal[] closes)
    {
        int count = Math.Min(highs.Length, Math.Min(lows.Length, closes.Length));
        if (count == 0)
        {
            return 0m;
        }

        List<decimal> ranges = new(count);

        for (int i = 0; i < count; i++)
        {
            decimal previousClose = i == 0 ? closes[i] : closes[i - 1];
            decimal trueRange = Math.Max(
                highs[i] - lows[i],
                Math.Max(Math.Abs(highs[i] - previousClose), Math.Abs(lows[i] - previousClose)));

            ranges.Add(trueRange);
        }

        return ranges.TakeLast(Math.Min(14, ranges.Count)).Average();
    }

    private static decimal Rsi(decimal[] closes)
    {
        if (closes.Length < 3)
        {
            return 50m;
        }

        decimal[] deltas = closes
            .Zip(closes.Skip(1), (previous, current) => current - previous)
            .TakeLast(14)
            .ToArray();

        decimal gains = deltas.Where(item => item > 0m).DefaultIfEmpty(0m).Average();
        decimal losses = Math.Abs(deltas.Where(item => item < 0m).DefaultIfEmpty(0m).Average());

        if (losses == 0m)
        {
            return 100m;
        }

        decimal rs = gains / losses;
        return 100m - 100m / (1m + rs);
    }

    private static decimal Autocorrelation(decimal[] returns)
    {
        if (returns.Length < 3)
        {
            return 0m;
        }

        decimal[] a = returns.Take(returns.Length - 1).ToArray();
        decimal[] b = returns.Skip(1).ToArray();

        decimal ma = a.Average();
        decimal mb = b.Average();

        decimal numerator = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        decimal da = a.Sum(x => (x - ma) * (x - ma));
        decimal db = b.Sum(y => (y - mb) * (y - mb));

        decimal denominator = Sqrt(da * db);

        return denominator > 0m ? numerator / denominator : 0m;
    }

    private static decimal Sqrt(decimal value) => value <= 0m ? 0m : (decimal)Math.Sqrt((double)value);

    private static decimal EstimateMarketCap(Asset asset, decimal currentPrice, decimal positionValue)
    {
        return asset.Type switch
        {
            AssetType.Cash => positionValue,
            AssetType.Crypto => Math.Max(positionValue, currentPrice * Math.Max(asset.Quantity, 1m) * 1_000m),
            _ => Math.Max(positionValue, currentPrice * Math.Max(asset.Quantity, 1m) * 10_000m)
        };
    }

    private static decimal EstimateVolume(IReadOnlyList<PortfolioTransaction> transactions, decimal currentQuantity)
    {
        decimal traded = transactions
            .Where(item => item.Type is TransactionType.Buy or TransactionType.Sell)
            .Take(20)
            .Sum(item => Math.Abs(item.Quantity));

        return traded > 0m ? traded : Math.Max(currentQuantity, 1m);
    }

    private static AssetDetailsMetricSeverity ResolveRsiSeverity(decimal rsi)
    {
        if (rsi >= 80m || rsi <= 20m)
        {
            return AssetDetailsMetricSeverity.Danger;
        }

        if (rsi >= 70m || rsi <= 30m)
        {
            return AssetDetailsMetricSeverity.Warning;
        }

        return AssetDetailsMetricSeverity.Good;
    }

    private static AssetDetailsMetricSeverity ResolveDrawdownSeverity(decimal maxDrawdownPct)
    {
        decimal abs = Math.Abs(maxDrawdownPct);

        if (abs >= 45m)
        {
            return AssetDetailsMetricSeverity.Danger;
        }

        if (abs >= 25m)
        {
            return AssetDetailsMetricSeverity.Warning;
        }

        return AssetDetailsMetricSeverity.Good;
    }

    private static string BuildLogoText(string ticker)
    {
        string normalized = string.IsNullOrWhiteSpace(ticker)
            ? "A"
            : ticker.Trim().ToUpperInvariant();

        return normalized.Length <= 2 ? normalized : normalized[..2];
    }

    private static string FormatCurrency(decimal value, string currency)
    {
        string normalized = string.IsNullOrWhiteSpace(currency)
            ? "USD"
            : currency.Trim().ToUpperInvariant();

        string symbol = normalized.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "$" : string.Empty;
        string suffix = symbol.Length == 0 ? $" {normalized}" : string.Empty;

        return $"{symbol}{value:N2}{suffix}";
    }

    private static string FormatDelta(decimal value)
    {
        return value >= 0m
            ? $"↗ +{value:0.##}%"
            : $"↘ {value:0.##}%";
    }

    private static string FormatCompactUsd(decimal value)
    {
        return "$" + FormatCompactNumber(value);
    }

    private static string FormatCompactNumber(decimal value)
    {
        decimal abs = Math.Abs(value);

        if (abs >= 1_000_000_000_000m)
        {
            return $"{value / 1_000_000_000_000m:0.##}T";
        }

        if (abs >= 1_000_000_000m)
        {
            return $"{value / 1_000_000_000m:0.##}B";
        }

        if (abs >= 1_000_000m)
        {
            return $"{value / 1_000_000m:0.##}M";
        }

        if (abs >= 1_000m)
        {
            return $"{value / 1_000m:0.##}K";
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(decimal value)
    {
        return $"{value:0.##}%";
    }

    private static string FormatNumber(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedNumber(decimal value)
    {
        return value >= 0m
            ? $"+{value:0.##}"
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private sealed record AssetRiskMetrics(
        decimal Sharpe,
        decimal Sortino,
        decimal Calmar,
        decimal MaxDrawdownPct,
        decimal VarPct,
        decimal CvarPct,
        decimal Rsi,
        decimal Atr,
        decimal Hurst,
        decimal ZScore,
        decimal Correlation,
        decimal Beta,
        decimal SpreadPct,
        string SmaStatus,
        bool IsSmaPositive,
        string RsiHint,
        string AtrHint)
    {
        public static AssetRiskMetrics Empty(decimal beta)
        {
            return new AssetRiskMetrics(
                0m,
                0m,
                0m,
                0m,
                0m,
                0m,
                50m,
                0m,
                0.5m,
                0m,
                0m,
                beta,
                0m,
                "Недостаточно данных",
                false,
                "Недостаточно данных",
                "Недостаточно данных");
        }
    }
}
