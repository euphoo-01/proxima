namespace Proxima.Infrastructure.Persistence;

public sealed class TransactionEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid? AssetId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset TradeDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Broker { get; set; }
    public string? ExternalId { get; set; }
    public string? EncryptedNotes { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
