namespace Proxima.Infrastructure.Persistence;

public sealed class PortfolioEntity
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "USD";
    public string? Description { get; set; }
    public string? ClientLabel { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
