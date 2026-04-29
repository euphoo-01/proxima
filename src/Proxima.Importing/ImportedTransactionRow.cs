using Proxima.Domain.Transactions;

namespace Proxima.Importing;

public sealed record ImportedTransactionRow(
    int RowNumber,
    DateTimeOffset TradeDate,
    string AssetTicker,
    string AssetName,
    TransactionType TransactionType,
    decimal Quantity,
    decimal Price,
    decimal GrossAmount,
    decimal FeeAmount,
    string Currency,
    string? Broker,
    string? Tag,
    ImportRowStatus Status,
    string? StatusReason);
