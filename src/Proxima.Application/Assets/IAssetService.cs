using Proxima.Domain.Assets;

namespace Proxima.Application.Assets;

public interface IAssetService
{
    Task<IReadOnlyList<Asset>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default);

    Task<AssetOperationResult> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default);

    Task<AssetOperationResult> UpdateAsync(UpdateAssetRequest request, CancellationToken cancellationToken = default);

    Task<AssetOperationResult> ArchiveAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken = default);
}
