namespace Proxima.Core.Application.Goals;

public sealed record GoalForecastChartPoint(int MonthIndex, decimal Amount);

public sealed record GoalForecastMilestone(
    Guid GoalId,
    string Title,
    string Currency,
    decimal TargetAmount,
    int MonthIndex,
    int EstimatedYear);
