namespace Proxima.Application.Goals;

public sealed record CreateGoalRequest(
    Guid PortfolioId,
    string Title,
    decimal TargetAmount,
    string Currency,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    DateTimeOffset? TargetDate);

public sealed record UpdateGoalRequest(
    Guid PortfolioId,
    Guid GoalId,
    string Title,
    decimal TargetAmount,
    string Currency,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    DateTimeOffset? TargetDate);
