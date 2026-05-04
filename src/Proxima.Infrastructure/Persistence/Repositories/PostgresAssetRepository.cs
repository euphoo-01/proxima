using Microsoft.EntityFrameworkCore;
using Proxima.Application.Assets;
using Proxima.Domain.Assets;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresAssetRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IAssetRepository
{
    public async Task<IReadOnlyList<Asset>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        IQueryable<AssetEntity> query = ctx.Assets.AsNoTracking().Where(x => x.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        List<AssetEntity> assets = await query.OrderBy(x => x.Ticker).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, IReadOnlyList<string>> tags = await LoadTagsAsync(ctx, assets.Select(x => x.Id).ToArray(), cancellationToken).ConfigureAwait(false);

        return assets
            .Select(x => ToDomain(x, tags.GetValueOrDefault(x.Id) ?? []))
            .ToArray();
    }

    public async Task<Asset?> FindByIdAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        AssetEntity? entity = await ctx.Assets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PortfolioId == portfolioId && x.Id == assetId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        Dictionary<Guid, IReadOnlyList<string>> tags = await LoadTagsAsync(ctx, [assetId], cancellationToken).ConfigureAwait(false);
        return ToDomain(entity, tags.GetValueOrDefault(assetId) ?? []);
    }

    public async Task AddAsync(Asset asset, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        await using var tx = await ctx.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        ctx.Assets.Add(ToEntity(asset));
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAssetTagsAsync(ctx, asset.Id, asset.Tags, cancellationToken).ConfigureAwait(false);
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Asset asset, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        await using var tx = await ctx.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        AssetEntity entity = await ctx.Assets.FirstAsync(x => x.PortfolioId == asset.PortfolioId && x.Id == asset.Id, cancellationToken).ConfigureAwait(false);
        entity.Ticker = asset.Ticker;
        entity.Name = asset.Name;
        entity.Type = asset.Type.ToString();
        entity.Currency = asset.Currency;
        entity.Exchange = asset.Exchange;
        entity.Isin = asset.Isin;
        entity.EncryptedNotes = asset.EncryptedNotes;
        entity.Quantity = asset.Quantity;
        entity.AverageBuyPrice = asset.AverageBuyPrice;
        entity.CurrentPrice = asset.CurrentPrice;
        entity.IsArchived = asset.IsArchived;
        entity.UpdatedAt = asset.UpdatedAt;

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        List<AssetTagEntity> existing = await ctx.AssetTags.Where(x => x.AssetId == asset.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        ctx.AssetTags.RemoveRange(existing);
        await UpsertAssetTagsAsync(ctx, asset.Id, asset.Tags, cancellationToken).ConfigureAwait(false);

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Asset ToDomain(AssetEntity x, IReadOnlyList<string> tags)
    {
        return new Asset(
            x.Id,
            x.PortfolioId,
            x.Ticker,
            x.Name,
            Enum.Parse<AssetType>(x.Type, ignoreCase: true),
            x.Currency,
            x.Exchange,
            x.Isin,
            tags,
            x.EncryptedNotes,
            x.Quantity,
            x.AverageBuyPrice,
            x.CurrentPrice,
            x.IsArchived,
            x.CreatedAt,
            x.UpdatedAt);
    }

    private static AssetEntity ToEntity(Asset asset)
    {
        return new AssetEntity
        {
            Id = asset.Id,
            PortfolioId = asset.PortfolioId,
            Ticker = asset.Ticker,
            Name = asset.Name,
            Type = asset.Type.ToString(),
            Currency = asset.Currency,
            Exchange = asset.Exchange,
            Isin = asset.Isin,
            EncryptedNotes = asset.EncryptedNotes,
            Quantity = asset.Quantity,
            AverageBuyPrice = asset.AverageBuyPrice,
            CurrentPrice = asset.CurrentPrice,
            IsArchived = asset.IsArchived,
            CreatedAt = asset.CreatedAt,
            UpdatedAt = asset.UpdatedAt,
        };
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTagsAsync(ProximaDbContext ctx, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return [];
        }

        var rows = await (
                from at in ctx.AssetTags.AsNoTracking()
                join t in ctx.Tags.AsNoTracking() on at.TagId equals t.Id
                where assetIds.Contains(at.AssetId)
                select new { at.AssetId, t.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(x => x.AssetId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static async Task UpsertAssetTagsAsync(ProximaDbContext ctx, Guid assetId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        foreach (string tagName in tags.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TagEntity? tag = await ctx.Tags.FirstOrDefaultAsync(x => x.Name == tagName, cancellationToken).ConfigureAwait(false);
            if (tag is null)
            {
                tag = new TagEntity { Id = Guid.NewGuid(), Name = tagName };
                ctx.Tags.Add(tag);
            }

            ctx.AssetTags.Add(new AssetTagEntity { AssetId = assetId, TagId = tag.Id });
        }
    }
}
