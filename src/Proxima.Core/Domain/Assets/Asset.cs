namespace Proxima.Core.Domain.Assets;

public sealed record Asset(
    Guid Id,
    Guid PortfolioId,
    string Ticker,
    string Name,
    AssetType Type,
    string Currency,
    string? Exchange,
    string? Isin,
    IReadOnlyList<string> Tags,
    decimal Quantity,
    decimal AverageBuyPrice,
    decimal CurrentPrice,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
