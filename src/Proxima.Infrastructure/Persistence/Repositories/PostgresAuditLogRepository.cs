using Proxima.Application.Observability;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresAuditLogRepository(DatabaseBootstrapService db) : IAuditLogRepository
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await using ProximaDbContext ctx = db.CreateDbContext();
        ctx.AuditLog.Add(new AuditLogEntity
        {
            Id = auditEvent.Id,
            UserId = auditEvent.OwnerUserId,
            Action = auditEvent.EventType,
            Timestamp = auditEvent.OccurredAtUtc,
            MetadataJson = auditEvent.Metadata,
        });
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
