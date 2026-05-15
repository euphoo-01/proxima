using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Quotes;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresQuoteCacheRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IQuoteCacheRepository
{
    public async Task<QuoteCacheEntry?> FindLatestByAssetIdAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        QuoteCacheEntity? x = await ctx.QuoteCache.AsNoTracking()
            .FirstOrDefaultAsync(i => i.AssetId == assetId, cancellationToken)
            .ConfigureAwait(false);

        return x is null ? null : new QuoteCacheEntry(x.AssetId, x.Ticker, x.Price, x.Currency, x.Timestamp, x.Source);
    }

    public async Task UpsertLatestAsync(QuoteCacheEntry entry, CancellationToken cancellationToken = default)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        QuoteCacheEntity? existing = await ctx.QuoteCache.FirstOrDefaultAsync(i => i.AssetId == entry.AssetId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            ctx.QuoteCache.Add(new QuoteCacheEntity
            {
                Id = Guid.NewGuid(),
                AssetId = entry.AssetId,
                Ticker = entry.Ticker,
                Price = entry.Price,
                Currency = entry.Currency,
                Timestamp = entry.Timestamp,
                Source = entry.Source,
            });
        }
        else
        {
            existing.Price = entry.Price;
            existing.Ticker = entry.Ticker;
            existing.Currency = entry.Currency;
            existing.Timestamp = entry.Timestamp;
            existing.Source = entry.Source;
        }

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
