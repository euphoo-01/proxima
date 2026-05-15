using Proxima.Core.Domain.Transactions;

namespace Proxima.Core.Application.Transactions;

public sealed record CreateTransactionRequest(
    Guid PortfolioId,
    Guid? AssetId,
    TransactionType Type,
    DateTimeOffset TradeDate,
    decimal Quantity,
    decimal Price,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal TaxAmount,
    string Currency,
    string? Broker,
    string? ExternalId,
    string? Notes);
