using Proxima.Domain.Transactions;

namespace Proxima.Application.Taxes;

public sealed record TaxTransactionSnapshot(
    DateTimeOffset TradeDate,
    TransactionType Type,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal TaxAmount,
    string Currency);
