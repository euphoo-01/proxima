namespace Proxima.Core.Domain.Transactions;

public sealed record PortfolioTransaction(
    Guid Id,
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
    string? Notes,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
