namespace Proxima.Core.Application.Portfolios;

public sealed record UpdatePortfolioRequest(
    Guid OwnerUserId,
    Guid PortfolioId,
    string Name,
    string? Description,
    string? ClientLabel);
