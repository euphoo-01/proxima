namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record DashboardTransactionRow(
    Guid TransactionId,
    string AssetName,
    string Ticker,
    string TypeLabel,
    DateTimeOffset TradeDate,
    decimal Price,
    decimal GrossAmount);
