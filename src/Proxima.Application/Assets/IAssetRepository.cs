using Proxima.Domain.Assets;

namespace Proxima.Application.Assets;

public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken);

    Task<Asset?> FindByIdAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken);

    Task AddAsync(Asset asset, CancellationToken cancellationToken);

    Task UpdateAsync(Asset asset, CancellationToken cancellationToken);
}
