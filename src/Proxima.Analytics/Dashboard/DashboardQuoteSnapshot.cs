namespace Proxima.Analytics.Dashboard;

public sealed record DashboardQuoteSnapshot(Guid AssetId, decimal Price, DateTimeOffset Timestamp);
