namespace Proxima.Core.Application.Portfolios;

public sealed record CreatePortfolioRequest(
    Guid OwnerUserId,
    string Name,
    string BaseCurrency,
    string? Description,
    string? ClientLabel);
