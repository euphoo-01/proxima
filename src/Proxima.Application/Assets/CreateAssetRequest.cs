using Proxima.Domain.Assets;

namespace Proxima.Application.Assets;

public sealed record CreateAssetRequest(
    Guid PortfolioId,
    string Ticker,
    string Name,
    AssetType Type,
    string Currency,
    string? Exchange,
    string? Isin,
    IReadOnlyList<string>? Tags,
    string? Notes,
    decimal Quantity,
    decimal AverageBuyPrice,
    decimal CurrentPrice);
