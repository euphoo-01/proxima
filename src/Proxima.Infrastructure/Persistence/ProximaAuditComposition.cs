using Proxima.Application.Observability;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Persistence;

public static class ProximaAuditComposition
{
    public static IAuditService CreateAuditService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new AuditService(new PostgresAuditLogRepository(uowFactory, uowAccessor));
    }
}
