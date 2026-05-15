namespace Proxima.Core.Application.Goals;

public sealed record GoalForecast(
    bool Reachable,
    int MonthsToGoal,
    DateTimeOffset? EstimatedDate,
    decimal ProjectedValue,
    string Message);
