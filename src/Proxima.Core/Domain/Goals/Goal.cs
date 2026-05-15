namespace Proxima.Core.Domain.Goals;

public sealed record Goal(
    Guid Id,
    Guid PortfolioId,
    string Title,
    decimal TargetAmount,
    string Currency,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    DateTimeOffset? TargetDate,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
