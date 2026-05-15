namespace Proxima.Core.Application.Quotes;

public sealed record QuoteCacheEntry(
    Guid AssetId,
    string Ticker,
    decimal Price,
    string Currency,
    DateTimeOffset Timestamp,
    string Source);
