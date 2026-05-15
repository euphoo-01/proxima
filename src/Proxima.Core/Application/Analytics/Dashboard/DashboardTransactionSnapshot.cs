namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record DashboardTransactionSnapshot(
    Guid TransactionId,
    string AssetName,
    string Ticker,
    string TypeLabel,
    DateTimeOffset TradeDate,
    decimal Price,
    decimal GrossAmount);
