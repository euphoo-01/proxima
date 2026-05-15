namespace Proxima.Infrastructure.Persistence;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
