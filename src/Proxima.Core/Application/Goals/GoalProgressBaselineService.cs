using Proxima.Core.Application.Assets;
using Proxima.Core.Domain.Assets;
using Proxima.Core.Domain.Goals;

namespace Proxima.Core.Application.Goals;

public interface IGoalProgressBaselineService
{
    Task<IReadOnlyDictionary<Guid, decimal>> CalculateCurrentAmountsAsync(
        Guid portfolioId,
        IReadOnlyList<Goal> goals,
        CancellationToken cancellationToken = default);
}

public sealed class GoalProgressBaselineService(IAssetService assets) : IGoalProgressBaselineService
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> CalculateCurrentAmountsAsync(
        Guid portfolioId,
        IReadOnlyList<Goal> goals,
        CancellationToken cancellationToken = default)
    {
        if (goals.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        decimal portfolioValue = await ResolvePortfolioValueAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        decimal totalTarget = goals.Sum(goal => Math.Max(0m, goal.TargetAmount));
        if (portfolioValue <= 0m || totalTarget <= 0m)
        {
            return goals.ToDictionary(item => item.Id, _ => 0m);
        }

        Dictionary<Guid, decimal> result = new(goals.Count);
        foreach (Goal goal in goals)
        {
            decimal weight = Math.Max(0m, goal.TargetAmount) / totalTarget;
            result[goal.Id] = Math.Round(portfolioValue * weight, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    private async Task<decimal> ResolvePortfolioValueAsync(Guid portfolioId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Asset> portfolioAssets = await assets
            .ListActiveAsync(portfolioId, cancellationToken)
            .ConfigureAwait(false);

        return portfolioAssets
            .Where(item => !item.IsArchived)
            .Sum(item => item.Quantity * item.CurrentPrice);
    }
}
