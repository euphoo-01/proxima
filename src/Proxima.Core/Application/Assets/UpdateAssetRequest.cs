using Proxima.Core.Domain.Assets;

namespace Proxima.Core.Application.Assets;

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
    decimal Quantity,
    decimal AverageBuyPrice,
    decimal CurrentPrice);
