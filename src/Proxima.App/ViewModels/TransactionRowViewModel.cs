using Proxima.Core.Domain.Transactions;

namespace Proxima.App.ViewModels;

public sealed record TransactionRowViewModel(
    Guid Id,
    Guid? AssetId,
    string AssetName,
    string Ticker,
    TransactionType Type,
    DateTimeOffset TradeDate,
    decimal Quantity,
    decimal Price,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal TaxAmount,
    string Currency,
    string? Broker)
{
    public string TypeLabel => Type.ToString();
}
