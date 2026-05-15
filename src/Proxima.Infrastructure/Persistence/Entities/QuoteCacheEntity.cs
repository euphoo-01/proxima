namespace Proxima.Infrastructure.Persistence;

public sealed class QuoteCacheEntity
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
}
