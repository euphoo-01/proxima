namespace Proxima.Application.Quotes;

public interface IQuoteCacheRepository
{
    Task<QuoteCacheEntry?> FindLatestByAssetIdAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task UpsertLatestAsync(QuoteCacheEntry entry, CancellationToken cancellationToken = default);
}
