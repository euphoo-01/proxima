using Proxima.Core.Domain.Portfolios;

namespace Proxima.Core.Application.Portfolios;

public interface IPortfolioService
{
    Task<IReadOnlyList<Portfolio>> ListActiveAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<PortfolioOperationResult> CreateAsync(CreatePortfolioRequest request, CancellationToken cancellationToken = default);

    Task<PortfolioOperationResult> UpdateAsync(UpdatePortfolioRequest request, CancellationToken cancellationToken = default);

    Task<PortfolioOperationResult> ArchiveAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken = default);
}
