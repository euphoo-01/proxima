namespace Proxima.Analytics.AssetDetails;

public sealed record AssetDetailsSnapshot(
    string Name,
    string Ticker,
    decimal CurrentPrice,
    decimal Quantity,
    decimal AverageBuyPrice,
    decimal Value,
    IReadOnlyList<decimal> ClosePrices,
    IReadOnlyList<decimal> HighPrices,
    IReadOnlyList<decimal> LowPrices);
