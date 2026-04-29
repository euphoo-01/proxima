using Proxima.Domain.Goals;

namespace Proxima.Application.Goals;

public interface IGoalService
{
    Task<IReadOnlyList<Goal>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default);
    Task<GoalOperationResult> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalOperationResult> UpdateAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalOperationResult> ArchiveAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken = default);
    GoalForecast Forecast(Goal goal, decimal currentPortfolioValue);
}
