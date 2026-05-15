namespace Proxima.Core.Application.Observability;

public sealed class NoOpAuditService : IAuditService
{
    public Task RecordAsync(Guid ownerUserId, string eventType, string outcome, string metadata, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
