using Proxima.Domain.Transactions;

namespace Proxima.Application.Taxes;

public sealed record TaxTransactionSnapshot(
    Guid? AssetId,
    DateTimeOffset TradeDate,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal TaxAmount,
    string Currency)
{
    public TaxTransactionSnapshot(
        DateTimeOffset tradeDate,
        TransactionType type,
        decimal quantity,
        decimal price,
        decimal grossAmount,
        string currency)
        : this(
            null,
            tradeDate,
            type,
            quantity,
            price,
            grossAmount,
            0m,
            0m,
            currency)
    {
    }
}
