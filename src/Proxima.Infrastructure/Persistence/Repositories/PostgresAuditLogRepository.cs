using Proxima.Application.Observability;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresAuditLogRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IAuditLogRepository
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        ctx.AuditLog.Add(new AuditLogEntity
        {
            Id = auditEvent.Id,
            UserId = auditEvent.OwnerUserId,
            Action = auditEvent.EventType,
            Timestamp = auditEvent.OccurredAtUtc,
            MetadataJson = auditEvent.Metadata,
        });
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
