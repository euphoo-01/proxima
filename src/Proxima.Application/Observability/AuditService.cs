namespace Proxima.Application.Observability;

public sealed class AuditService(IAuditLogRepository repository) : IAuditService
{
    public Task RecordAsync(Guid ownerUserId, string eventType, string outcome, string metadata, CancellationToken cancellationToken = default)
    {
        AuditEvent auditEvent = new(
            Guid.NewGuid(),
            ownerUserId,
            DateTimeOffset.UtcNow,
            eventType,
            outcome,
            RedactionHelper.Redact(metadata));
        return repository.AppendAsync(auditEvent, cancellationToken);
    }
}
