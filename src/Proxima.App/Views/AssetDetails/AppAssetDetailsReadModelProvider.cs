using Proxima.Analytics.AssetDetails;
using Proxima.App.ViewModels;
using Proxima.Application.Assets;
using Proxima.Application.Quotes;
using Proxima.Application.Transactions;
using Proxima.Domain.Transactions;

namespace Proxima.App.Views.AssetDetails;

public sealed class AppAssetDetailsReadModelProvider(
    IAssetRepository assets,
    ITransactionRepository transactions,
    IQuoteCacheRepository quoteCache,
    Shell.IShellState shellState) : IAssetDetailsReadModelProvider
{
    public AssetDetailsReadModel? Get(Guid assetId, string timeframe)
    {
        Guid portfolioId = shellState.CurrentPortfolioId;
        Domain.Assets.Asset? asset = assets.FindByIdAsync(portfolioId, assetId, CancellationToken.None).GetAwaiter().GetResult();
        if (asset is null || asset.IsArchived)
        {
            return null;
        }

        IReadOnlyList<PortfolioTransaction> allTransactions = transactions
            .ListByPortfolioAsync(portfolioId, includeArchived: false, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        PortfolioTransaction[] scopedTransactions = allTransactions
            .Where(item => item.AssetId == assetId)
            .OrderByDescending(item => item.TradeDate)
            .ToArray();

        QuoteCacheEntry? latestQuote = quoteCache.FindLatestByAssetIdAsync(assetId, CancellationToken.None).GetAwaiter().GetResult();

        string currency = latestQuote?.Currency ?? asset.Currency;
        decimal currentPrice = latestQuote?.Price ?? asset.CurrentPrice;
        decimal previousClose = ResolvePreviousClose(scopedTransactions, currentPrice);

        IReadOnlyList<CandlestickPointViewModel> candles = BuildCandles(scopedTransactions, latestQuote, timeframe, currentPrice);
        AssetDetailsSnapshot snapshot = BuildSnapshot(asset, candles, currentPrice);
        IReadOnlyList<AssetMetric> baseMetrics = AssetDetailsCalculator.BuildBaseMetrics(snapshot);
        IReadOnlyList<AssetMetric> advancedMetrics = AssetDetailsCalculator.BuildAdvancedMetrics(snapshot);

        return new AssetDetailsReadModel(
            assetId,
            asset.Name,
            asset.Ticker,
            $"{currentPrice:N2} {currency}",
            BuildDeltaText(currentPrice, previousClose),
            currency,
            candles,
            baseMetrics.Select(ToViewMetric).ToArray(),
            advancedMetrics.Select(ToViewMetric).ToArray(),
            scopedTransactions.Select(ToRow).ToArray());
    }

    private static AssetMetricItemViewModel ToViewMetric(AssetMetric metric)
    {
        string value = string.IsNullOrWhiteSpace(metric.Value)
            ? "Недоступно"
            : string.IsNullOrWhiteSpace(metric.Unit)
                ? metric.Value
                : $"{metric.Value} {metric.Unit}";
        return new AssetMetricItemViewModel(metric.Name, value, metric.HelpText, metric.Risk.ToString().ToLowerInvariant());
    }

    private static string BuildDeltaText(decimal currentPrice, decimal previousClose)
    {
        if (previousClose <= 0m)
        {
            return "Недостаточно данных";
        }

        decimal delta = (currentPrice - previousClose) / previousClose * 100m;
        return delta >= 0m ? $"+{delta:0.##}%" : $"{delta:0.##}%";
    }

    private static decimal ResolvePreviousClose(IReadOnlyList<PortfolioTransaction> transactions, decimal fallback)
    {
        PortfolioTransaction? sellOrBuy = transactions
            .Where(item => item.Type is TransactionType.Buy or TransactionType.Sell)
            .Skip(1)
            .FirstOrDefault();

        if (sellOrBuy is not null && sellOrBuy.Price > 0m)
        {
            return sellOrBuy.Price;
        }

        return fallback;
    }

    private static AssetTransactionRowViewModel ToRow(PortfolioTransaction transaction)
    {
        decimal amount = transaction.GrossAmount > 0m
            ? transaction.GrossAmount
            : transaction.Price * transaction.Quantity;

        return new AssetTransactionRowViewModel(
            transaction.TradeDate,
            transaction.Type.ToString(),
            transaction.Price,
            transaction.Quantity,
            amount,
            transaction.Currency);
    }

    private static AssetDetailsSnapshot BuildSnapshot(Domain.Assets.Asset asset, IReadOnlyList<CandlestickPointViewModel> candles, decimal currentPrice)
    {
        IReadOnlyList<decimal> closes = candles.Count > 0 ? candles.Select(item => item.Close).ToArray() : [currentPrice];
        IReadOnlyList<decimal> highs = candles.Count > 0 ? candles.Select(item => item.High).ToArray() : [currentPrice];
        IReadOnlyList<decimal> lows = candles.Count > 0 ? candles.Select(item => item.Low).ToArray() : [currentPrice];
        decimal value = asset.Quantity * currentPrice;

        return new AssetDetailsSnapshot(
            asset.Name,
            asset.Ticker,
            currentPrice,
            asset.Quantity,
            asset.AverageBuyPrice,
            value,
            closes,
            highs,
            lows);
    }

    private static IReadOnlyList<CandlestickPointViewModel> BuildCandles(
        IReadOnlyList<PortfolioTransaction> transactions,
        QuoteCacheEntry? latestQuote,
        string timeframe,
        decimal fallbackPrice)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (int points, TimeSpan step) = timeframe switch
        {
            "1ч" => (12, TimeSpan.FromMinutes(5)),
            "7д" => (7, TimeSpan.FromDays(1)),
            "30д" => (10, TimeSpan.FromDays(3)),
            _ => (8, TimeSpan.FromHours(3))
        };

        List<decimal> anchors = transactions
            .Where(item => item.Type is TransactionType.Buy or TransactionType.Sell)
            .Select(item => item.Price)
            .Where(price => price > 0m)
            .Take(points - 1)
            .ToList();
        anchors.Reverse();
        while (anchors.Count < points - 1)
        {
            anchors.Insert(0, fallbackPrice);
        }

        decimal tailPrice = latestQuote?.Price ?? fallbackPrice;
        anchors.Add(tailPrice);

        DateTimeOffset start = now - TimeSpan.FromTicks(step.Ticks * (points - 1));
        List<CandlestickPointViewModel> result = new(points);
        for (int i = 0; i < points; i++)
        {
            decimal basePrice = anchors[i];
            decimal prev = i > 0 ? anchors[i - 1] : basePrice;
            decimal open = prev;
            decimal close = basePrice;
            decimal high = Math.Max(open, close) * 1.004m;
            decimal low = Math.Min(open, close) * 0.996m;
            decimal volume = 1_000m + (i * 250m);
            result.Add(new CandlestickPointViewModel(start + TimeSpan.FromTicks(step.Ticks * i), open, high, low, close, volume));
        }

        return result;
    }
}
