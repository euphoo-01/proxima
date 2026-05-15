using Proxima.Core.Domain.Portfolios;

namespace Proxima.Core.Application.Portfolios;

public interface IPortfolioRepository
{
    Task<IReadOnlyList<Portfolio>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken);

    Task<Portfolio?> FindByIdAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken);

    Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken);

    Task UpdateAsync(Portfolio portfolio, CancellationToken cancellationToken);
}
