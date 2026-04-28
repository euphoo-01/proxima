using Proxima.Domain.Assets;

namespace Proxima.Application.Assets;

public sealed record UpdateAssetRequest(
    Guid PortfolioId,
    Guid AssetId,
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
