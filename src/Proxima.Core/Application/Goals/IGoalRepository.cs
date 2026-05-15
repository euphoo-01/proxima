using Proxima.Core.Domain.Goals;

namespace Proxima.Core.Application.Goals;

public interface IGoalRepository
{
    Task<IReadOnlyList<Goal>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken);

    Task<Goal?> FindByIdAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken);

    Task AddAsync(Goal goal, CancellationToken cancellationToken);

    Task UpdateAsync(Goal goal, CancellationToken cancellationToken);
}
