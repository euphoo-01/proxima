using Proxima.Core.Application.Assets;
using Proxima.Core.Domain.Goals;
using Proxima.Core.Domain.Assets;

namespace Proxima.App.Views.Goals;

public interface IGoalProgressBaselineService
{
    IReadOnlyDictionary<Guid, decimal> CalculateCurrentAmounts(Guid portfolioId, IReadOnlyList<Goal> goals);
}

public sealed class GoalProgressBaselineService(IAssetService assets) : IGoalProgressBaselineService
{
    public IReadOnlyDictionary<Guid, decimal> CalculateCurrentAmounts(Guid portfolioId, IReadOnlyList<Goal> goals)
    {
        if (goals.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        decimal portfolioValue = ResolvePortfolioValue(portfolioId);
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

    private decimal ResolvePortfolioValue(Guid portfolioId)
    {
        IReadOnlyList<Asset> portfolioAssets = assets
            .ListActiveAsync(portfolioId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return portfolioAssets
            .Where(item => !item.IsArchived)
            .Sum(item => item.Quantity * item.CurrentPrice);
    }
}
