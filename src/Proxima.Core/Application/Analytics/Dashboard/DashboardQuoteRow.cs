namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record DashboardQuoteRow(Guid AssetId, decimal Price, DateTimeOffset Timestamp);
