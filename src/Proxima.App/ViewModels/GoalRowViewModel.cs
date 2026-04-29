namespace Proxima.App.ViewModels;

public sealed record GoalRowViewModel(
    Guid Id,
    string Title,
    decimal TargetAmount,
    string Currency,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    string ForecastSummary);
