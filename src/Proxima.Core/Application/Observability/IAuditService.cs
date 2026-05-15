namespace Proxima.Core.Application.Observability;

public interface IAuditService
{
    Task RecordAsync(Guid ownerUserId, string eventType, string outcome, string metadata, CancellationToken cancellationToken = default);
}
