namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record DashboardAssetSnapshot(
    Guid AssetId,
    string AssetName,
    string Ticker,
    decimal Quantity,
    decimal Price,
    decimal Value,
    IReadOnlyList<string> Tags);
