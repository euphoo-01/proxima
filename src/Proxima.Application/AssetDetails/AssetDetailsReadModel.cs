namespace Proxima.Application.AssetDetails;

public interface IAssetDetailsService
{
    Task<AssetDetailsReadModel?> GetAsync(
        Guid assetId,
        string timeframe,
        CancellationToken cancellationToken = default);
}

public sealed record AssetDetailsReadModel(
    Guid AssetId,
    string AssetName,
    string AssetTicker,
    string LogoText,
    string PriceText,
    string DeltaText,
    bool IsDeltaPositive,
    string CurrencyCode,
    string MarketCapText,
    string FdvText,
    string PeText,
    string Volume24hText,
    string SupplyText,
    string MarketDataSource,
    IReadOnlyList<AssetDetailsCandle> Candles,
    IReadOnlyList<AssetDetailsMetric> BasicMetrics,
    IReadOnlyList<AssetDetailsMetric> RiskMetrics,
    IReadOnlyList<AssetDetailsTransaction> Transactions);

public sealed record AssetDetailsCandle(
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public sealed record AssetDetailsMetric(
    string Label,
    string Value,
    string Hint,
    AssetDetailsMetricSeverity Severity);

public enum AssetDetailsMetricSeverity
{
    Neutral = 0,
    Good = 1,
    Warning = 2,
    Danger = 3,
}

public sealed record AssetDetailsTransaction(
    Guid TransactionId,
    DateTimeOffset Date,
    string TypeLabel,
    decimal Price,
    decimal Quantity,
    decimal Amount,
    decimal FeeAmount,
    string Currency,
    string Status);
