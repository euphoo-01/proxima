using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Portfolios;
using Proxima.Core.Domain.Portfolios;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresPortfolioRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IPortfolioRepository
{
    public async Task<IReadOnlyList<Portfolio>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        IQueryable<PortfolioEntity> query = ctx.Portfolios.AsNoTracking().Where(x => x.OwnerUserId == ownerUserId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new Portfolio(
                x.Id,
                x.OwnerUserId,
                x.Name,
                x.BaseCurrency,
                x.Description,
                x.ClientLabel,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Portfolio?> FindByIdAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        PortfolioEntity? entity = await ctx.Portfolios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId && x.Id == portfolioId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new Portfolio(entity.Id, entity.OwnerUserId, entity.Name, entity.BaseCurrency, entity.Description, entity.ClientLabel, entity.IsArchived, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        ctx.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolio.Id,
            OwnerUserId = portfolio.OwnerUserId,
            Name = portfolio.Name,
            BaseCurrency = portfolio.BaseCurrency,
            Description = portfolio.Description,
            ClientLabel = portfolio.ClientLabel,
            IsArchived = portfolio.IsArchived,
            CreatedAt = portfolio.CreatedAt,
            UpdatedAt = portfolio.UpdatedAt,
        });
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        PortfolioEntity entity = await ctx.Portfolios.FirstAsync(
            x => x.OwnerUserId == portfolio.OwnerUserId && x.Id == portfolio.Id,
            cancellationToken).ConfigureAwait(false);

        entity.Name = portfolio.Name;
        entity.BaseCurrency = portfolio.BaseCurrency;
        entity.Description = portfolio.Description;
        entity.ClientLabel = portfolio.ClientLabel;
        entity.IsArchived = portfolio.IsArchived;
        entity.UpdatedAt = portfolio.UpdatedAt;

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
