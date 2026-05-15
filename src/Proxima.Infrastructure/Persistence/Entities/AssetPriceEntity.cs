namespace Proxima.Infrastructure.Persistence;

public sealed class AssetPriceEntity
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset Timestamp { get; set; }
}
