namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record DashboardQuoteSnapshot(Guid AssetId, decimal Price, DateTimeOffset Timestamp);
