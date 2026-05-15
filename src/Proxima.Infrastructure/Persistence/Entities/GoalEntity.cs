namespace Proxima.Infrastructure.Persistence;

public sealed class GoalEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MonthlyContribution { get; set; }
    public decimal? ExpectedAnnualReturnPercent { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
