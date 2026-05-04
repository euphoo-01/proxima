using Microsoft.EntityFrameworkCore;
using Proxima.Application.Goals;
using Proxima.Domain.Goals;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresGoalRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IGoalRepository
{
    public async Task<IReadOnlyList<Goal>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        IQueryable<GoalEntity> query = ctx.Goals.AsNoTracking().Where(x => x.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query
            .OrderBy(x => x.Title)
            .Select(x => new Goal(
                x.Id,
                x.PortfolioId,
                x.Title,
                x.TargetAmount,
                x.Currency,
                x.MonthlyContribution,
                x.ExpectedAnnualReturnPercent,
                x.TargetDate,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Goal?> FindByIdAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        GoalEntity? x = await ctx.Goals.AsNoTracking()
            .FirstOrDefaultAsync(i => i.PortfolioId == portfolioId && i.Id == goalId, cancellationToken)
            .ConfigureAwait(false);

        return x is null
            ? null
            : new Goal(
                x.Id,
                x.PortfolioId,
                x.Title,
                x.TargetAmount,
                x.Currency,
                x.MonthlyContribution,
                x.ExpectedAnnualReturnPercent,
                x.TargetDate,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt);
    }

    public async Task AddAsync(Goal goal, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        ctx.Goals.Add(new GoalEntity
        {
            Id = goal.Id,
            PortfolioId = goal.PortfolioId,
            Title = goal.Title,
            TargetAmount = goal.TargetAmount,
            Currency = goal.Currency,
            MonthlyContribution = goal.MonthlyContribution,
            ExpectedAnnualReturnPercent = goal.ExpectedAnnualReturnPercent,
            TargetDate = goal.TargetDate,
            IsArchived = goal.IsArchived,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt,
        });
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Goal goal, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        GoalEntity entity = await ctx.Goals.FirstAsync(x => x.PortfolioId == goal.PortfolioId && x.Id == goal.Id, cancellationToken).ConfigureAwait(false);

        entity.Title = goal.Title;
        entity.TargetAmount = goal.TargetAmount;
        entity.Currency = goal.Currency;
        entity.MonthlyContribution = goal.MonthlyContribution;
        entity.ExpectedAnnualReturnPercent = goal.ExpectedAnnualReturnPercent;
        entity.TargetDate = goal.TargetDate;
        entity.IsArchived = goal.IsArchived;
        entity.UpdatedAt = goal.UpdatedAt;

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
