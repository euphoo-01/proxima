using Proxima.Core.Domain.Assets;

namespace Proxima.Core.Application.AssetDetails;

public interface IAssetDetailsService
{
    Task<AssetDetailsOverview?> GetOverviewAsync(
        Guid portfolioId,
        Guid assetId,
        string timeframe,
        CancellationToken cancellationToken = default);
}

public interface IAssetMarketDataProvider
{
    Task<AssetMarketData?> GetAsync(
        string symbol,
        string timeframe,
        CancellationToken cancellationToken = default);
}

public sealed record AssetDetailsOverview(
    Guid AssetId,
    string AssetName,
    string AssetTicker,
    AssetType AssetType,
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

public sealed record AssetMarketData(
    string? Name,
    string? Currency,
    decimal? MarketCapUsd,
    decimal? FdvUsd,
    decimal? PeRatio,
    decimal? Beta,
    decimal? VolumeUnits,
    decimal? VolumeUsd,
    decimal? ShareOutstanding,
    IReadOnlyList<AssetDetailsCandle> Candles)
{
    public bool HasAnyData =>
        !string.IsNullOrWhiteSpace(Name)
        || MarketCapUsd.HasValue
        || FdvUsd.HasValue
        || PeRatio.HasValue
        || Beta.HasValue
        || VolumeUnits.HasValue
        || Candles.Count > 0;
}

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
