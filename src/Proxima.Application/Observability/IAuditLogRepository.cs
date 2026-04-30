namespace Proxima.Application.Observability;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
