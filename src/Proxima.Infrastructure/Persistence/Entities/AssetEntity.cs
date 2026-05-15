namespace Proxima.Infrastructure.Persistence;

public sealed class AssetEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string? Exchange { get; set; }
    public string? Isin { get; set; }
    public string? EncryptedNotes { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
