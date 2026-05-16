using System.Globalization;
using Proxima.Core.Application.Analytics.AssetDetails;
using Proxima.Core.Application.AssetDetails;
using Proxima.Core.Application.Assets;
using Proxima.Core.Application.Portfolios;
using Proxima.Core.Application.Quotes;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Assets;
using Proxima.Core.Domain.Transactions;

namespace Proxima.Infrastructure.AssetDetails;

public sealed class AssetDetailsService(
    ICurrentPortfolioContext portfolioContext,
    IAssetRepository assetRepository,
    ITransactionRepository transactionRepository,
    IQuoteCacheRepository quoteCacheRepository,
    IQuoteProvider quoteProvider,
    TwelveDataAssetMarketDataProvider marketDataProvider) : IAssetDetailsService
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

        string quoteSymbol = ResolveQuoteSymbol(asset);
        string quotePair = ResolveQuotePair(asset);
        QuoteProviderResult latestQuote = ResolveBaseCurrencyQuote(asset, quoteSymbol)
            ?? await quoteProvider
                .GetLatestQuoteAsync(quoteSymbol, "USD", cancellationToken)
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
                    ? "Не удалось загрузить котировку Twelve Data и локального кэша для актива нет."
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
                Ticker = asset.Ticker,
                CurrentPrice = latestQuote.Quote.Price,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await assetRepository
                .UpdateAsync(asset, cancellationToken)
                .ConfigureAwait(false);
        }

        TwelveDataAssetMarketData? marketData = IsBaseCurrencyPair(quoteSymbol)
            ? null
            : await marketDataProvider
                .TryLoadAsync(quoteSymbol, NormalizeTimeframe(timeframe), cancellationToken)
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

        AssetDetailsAnalyticsResult risk = AssetDetailsCalculator.Calculate(
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

        string sourcePrefix = asset.Type is AssetType.Cash or AssetType.Currency
            ? $"Пара курса: {quotePair}. "
            : string.Empty;

        string source = sourcePrefix + (marketData is not null
            ? "Источник: Twelve Data + PostgreSQL"
            : cachedQuote is not null
                ? $"Источник: {cachedQuote.Source} + PostgreSQL"
                : "Источник: PostgreSQL + локальная оценка");

        return new AssetDetailsReadModel(
            asset.Id,
            assetName,
            asset.Ticker.ToUpperInvariant(),
            asset.Type,
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

    private static QuoteProviderResult? ResolveBaseCurrencyQuote(Asset asset, string quoteSymbol)
    {
        if (asset.Type is not (AssetType.Cash or AssetType.Currency))
        {
            return null;
        }

        string cashCode = NormalizeCashTicker(quoteSymbol, asset.Currency);
        if (!cashCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return QuoteProviderResult.Success(new QuoteData(
            "USD/USD",
            1m,
            "USD",
            DateTimeOffset.UtcNow,
            "base-currency",
            Ohlc: null,
            Volume: null));
    }

    private static bool IsBaseCurrencyPair(string quoteSymbol)
    {
        return string.Equals(quoteSymbol, "USD/USD", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveQuoteSymbol(Asset asset)
    {
        if (asset.Type is not (AssetType.Cash or AssetType.Currency))
        {
            return asset.Ticker;
        }

        string cashCode = NormalizeCashTicker(asset.Ticker, asset.Currency);
        return cashCode.Equals("USD", StringComparison.OrdinalIgnoreCase)
            ? "USD/USD"
            : $"{cashCode}/USD";
    }

    private static string ResolveQuotePair(Asset asset)
    {
        if (asset.Type is not (AssetType.Cash or AssetType.Currency))
        {
            return asset.Ticker.ToUpperInvariant();
        }

        string cashCode = NormalizeCashTicker(asset.Ticker, asset.Currency);
        return cashCode.Equals("USD", StringComparison.OrdinalIgnoreCase)
            ? "USD/USD"
            : $"{cashCode}/USD";
    }

    private static string NormalizeCashTicker(string ticker, string currency)
    {
        string value = string.IsNullOrWhiteSpace(ticker) ? currency : ticker;
        value = value.Trim().ToUpperInvariant();

        int slashIndex = value.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex > 0)
        {
            value = value[..slashIndex];
        }

        return value;
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

}
