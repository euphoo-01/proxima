namespace Proxima.Core.Application.Goals;

public interface IGoalProjectionService
{
    GoalProjection BuildProjection(GoalProjectionRequest request);

    GoalMilestoneEstimate EstimateGoalReach(GoalReachEstimateRequest request);
}

public sealed class GoalProjectionService : IGoalProjectionService
{
    private const decimal DefaultExpectedAnnualReturnPercent = HistoricalPortfolioReturnService.DefaultFallbackAnnualReturnPercent;

    public GoalProjection BuildProjection(GoalProjectionRequest request)
    {
        decimal monthlyContribution = Math.Max(0m, request.MonthlyContribution);
        decimal monthlyRate = NormalizeMonthlyRate(request.ExpectedAnnualReturnPercent);
        decimal value = Math.Max(0m, request.CurrentAmount);
        int totalMonths = Math.Max(1, request.HorizonYears * 12);

        List<GoalForecastChartPoint> points = new(totalMonths + 1);
        List<GoalForecastMilestone> milestones = [];
        HashSet<Guid> reachedGoalIds = [];

        for (int month = 0; month <= totalMonths; month++)
        {
            if (month > 0)
            {
                value = value * (1m + monthlyRate) + monthlyContribution;
            }

            points.Add(new GoalForecastChartPoint(month, value));

            foreach (GoalProjectionTarget target in request.Targets.OrderBy(item => item.TargetAmount))
            {
                if (reachedGoalIds.Contains(target.GoalId) || value < target.TargetAmount)
                {
                    continue;
                }

                reachedGoalIds.Add(target.GoalId);
                milestones.Add(new GoalForecastMilestone(
                    target.GoalId,
                    target.Title,
                    target.Currency,
                    target.TargetAmount,
                    month,
                    DateTimeOffset.Now.AddMonths(month).Year));
            }
        }

        return new GoalProjection(points, milestones.OrderBy(item => item.MonthIndex).ToArray(), value);
    }

    public GoalMilestoneEstimate EstimateGoalReach(GoalReachEstimateRequest request)
    {
        decimal monthlyContribution = Math.Max(0m, request.MonthlyContribution);
        decimal monthlyRate = NormalizeMonthlyRate(request.ExpectedAnnualReturnPercent);
        decimal value = Math.Max(0m, request.CurrentAmount);
        int totalMonths = Math.Max(1, request.HorizonYears * 12);

        if (value >= request.TargetAmount)
        {
            return GoalMilestoneEstimate.Reachable(0, request.StartYear);
        }

        for (int month = 1; month <= totalMonths; month++)
        {
            value = value * (1m + monthlyRate) + monthlyContribution;
            if (value >= request.TargetAmount)
            {
                return GoalMilestoneEstimate.Reachable(month, DateTimeOffset.Now.AddMonths(month).Year);
            }
        }

        return GoalMilestoneEstimate.Unreachable(request.HorizonYears);
    }

    private static decimal NormalizeMonthlyRate(decimal? expectedAnnualReturnPercent)
    {
        decimal annualPercent = Math.Clamp(expectedAnnualReturnPercent ?? DefaultExpectedAnnualReturnPercent, -50m, 100m);
        double annualRate = (double)annualPercent / 100d;
        double monthlyRate = Math.Pow(1d + annualRate, 1d / 12d) - 1d;
        return (decimal)monthlyRate;
    }
}

public sealed record GoalProjectionRequest(
    decimal CurrentAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    int HorizonYears,
    int StartYear,
    IReadOnlyList<GoalProjectionTarget> Targets);

public sealed record GoalProjectionTarget(Guid GoalId, string Title, string Currency, decimal TargetAmount);

public sealed record GoalProjection(
    IReadOnlyList<GoalForecastChartPoint> Points,
    IReadOnlyList<GoalForecastMilestone> Milestones,
    decimal ProjectedAmount);

public sealed record GoalReachEstimateRequest(
    decimal CurrentAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    decimal TargetAmount,
    int HorizonYears,
    int StartYear);

public sealed record GoalMilestoneEstimate(bool IsReachable, int MonthsToGoal, int? EstimatedYear, int HorizonYears)
{
    public static GoalMilestoneEstimate Reachable(int monthsToGoal, int estimatedYear)
    {
        return new GoalMilestoneEstimate(true, monthsToGoal, estimatedYear, 0);
    }

    public static GoalMilestoneEstimate Unreachable(int horizonYears)
    {
        return new GoalMilestoneEstimate(false, 0, null, horizonYears);
    }
}
